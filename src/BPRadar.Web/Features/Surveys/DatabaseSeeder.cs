using System.Text.Json;
using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BPRadar.Web.Features.Surveys;

public static class DatabaseSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task SeedAsync(
        BPRadarDbContext dbContext,
        string seedDataPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedDataPath);
        var timer = Stopwatch.StartNew();
        BPRadarTrace.Write(
            TraceEventType.Start,
            "Surveys",
            "SeedSurveyTemplates",
            $"seedDataPath={seedDataPath}");

        try
        {
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var frameworksByCode = await SeedFrameworksAsync(
                dbContext,
                Path.Combine(seedDataPath, "frameworks"),
                cancellationToken);
            await SeedSurveyTemplatesAsync(
                dbContext,
                Path.Combine(seedDataPath, "survey"),
                frameworksByCode,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            BPRadarTrace.Write(
                TraceEventType.Stop,
                "Surveys",
                "SeedSurveyTemplates",
                $"templates={await dbContext.SurveyTemplates.CountAsync(cancellationToken)} " +
                $"questions={await dbContext.SurveyQuestions.CountAsync(cancellationToken)}",
                timer.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            BPRadarTrace.Write(
                TraceEventType.Error,
                "Surveys",
                "SeedSurveyTemplates",
                $"exceptionType={exception.GetType().Name}",
                timer.ElapsedMilliseconds);
            throw;
        }
    }

    private static async Task<Dictionary<string, Framework>> SeedFrameworksAsync(
        BPRadarDbContext dbContext,
        string directory,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Framework>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in RequiredJsonFiles(directory))
        {
            var seed = await DeserializeAsync<FrameworkSeedFile>(path, cancellationToken);
            var framework = await dbContext.Frameworks
                .Include(item => item.Domains)
                .ThenInclude(domain => domain.Controls)
                .SingleOrDefaultAsync(
                    item => item.Name == seed.Framework.Name &&
                        item.Version == seed.Framework.Version,
                    cancellationToken);

            if (framework is null)
            {
                framework = new Framework
                {
                    Name = seed.Framework.Name,
                    Version = seed.Framework.Version,
                    Description = seed.Framework.Description,
                    SourceUrl = seed.Framework.SourceUrl
                };
                dbContext.Frameworks.Add(framework);
            }
            else
            {
                framework.Description = seed.Framework.Description;
                framework.SourceUrl = seed.Framework.SourceUrl;
            }

            foreach (var domainSeed in seed.Domains)
            {
                var domain = framework.Domains.SingleOrDefault(
                    item => string.Equals(
                        item.Code,
                        domainSeed.Code,
                        StringComparison.OrdinalIgnoreCase));
                if (domain is null)
                {
                    domain = new Domain
                    {
                        Code = domainSeed.Code,
                        Name = domainSeed.Name,
                        SortOrder = domainSeed.SortOrder
                    };
                    framework.Domains.Add(domain);
                }
                else
                {
                    domain.Name = domainSeed.Name;
                    domain.SortOrder = domainSeed.SortOrder;
                }

                foreach (var controlSeed in domainSeed.Controls)
                {
                    var control = domain.Controls.SingleOrDefault(
                        item => string.Equals(
                            item.Code,
                            controlSeed.Code,
                            StringComparison.OrdinalIgnoreCase));
                    if (control is null)
                    {
                        domain.Controls.Add(new Control
                        {
                            Code = controlSeed.Code,
                            Title = controlSeed.Title,
                            Description = controlSeed.Description,
                            GuidanceUrl = controlSeed.GuidanceUrl,
                            SortOrder = controlSeed.SortOrder
                        });
                    }
                    else
                    {
                        control.Title = controlSeed.Title;
                        control.Description = controlSeed.Description;
                        control.GuidanceUrl = controlSeed.GuidanceUrl;
                        control.SortOrder = controlSeed.SortOrder;
                    }
                }
            }

            result.Add(seed.Framework.Code, framework);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static async Task SeedSurveyTemplatesAsync(
        BPRadarDbContext dbContext,
        string directory,
        IReadOnlyDictionary<string, Framework> frameworksByCode,
        CancellationToken cancellationToken)
    {
        foreach (var path in RequiredJsonFiles(directory))
        {
            var seed = await DeserializeAsync<SurveySeedFile>(path, cancellationToken);
            if (!frameworksByCode.TryGetValue(
                seed.SurveyTemplate.FrameworkCode,
                out var framework))
            {
                throw InvalidSeedReference(
                    path,
                    $"framework '{seed.SurveyTemplate.FrameworkCode}'");
            }

            var template = await dbContext.SurveyTemplates
                .Include(item => item.Questions)
                .SingleOrDefaultAsync(
                    item => item.Name == seed.SurveyTemplate.Name,
                    cancellationToken);
            var now = DateTime.UtcNow;
            if (template is null)
            {
                template = new SurveyTemplate
                {
                    Name = seed.SurveyTemplate.Name,
                    CreatedAt = now
                };
                dbContext.SurveyTemplates.Add(template);
            }

            template.Framework = framework;
            template.Description = seed.SurveyTemplate.Description;
            template.Cadence = seed.SurveyTemplate.Cadence;
            template.IsActive = seed.SurveyTemplate.IsActive;
            template.UpdatedAt = now;

            foreach (var questionSeed in seed.Questions)
            {
                var domain = framework.Domains.SingleOrDefault(
                    item => string.Equals(
                        item.Code,
                        questionSeed.DomainCode,
                        StringComparison.OrdinalIgnoreCase))
                    ?? throw InvalidSeedReference(
                        path,
                        $"domain '{questionSeed.DomainCode}'");
                var control = domain.Controls.SingleOrDefault(
                    item => string.Equals(
                        item.Code,
                        questionSeed.ControlCode,
                        StringComparison.OrdinalIgnoreCase))
                    ?? throw InvalidSeedReference(
                        path,
                        $"control '{questionSeed.ControlCode}' in domain '{domain.Code}'");

                var question = template.Questions.SingleOrDefault(
                    item => string.Equals(
                        item.Code,
                        questionSeed.Code,
                        StringComparison.OrdinalIgnoreCase));
                if (question is null)
                {
                    question = new SurveyQuestion
                    {
                        Code = questionSeed.Code,
                        Prompt = questionSeed.Prompt
                    };
                    template.Questions.Add(question);
                }

                question.Prompt = questionSeed.Prompt;
                question.Framework = framework;
                question.Domain = domain;
                question.Control = control;
                question.Weight = questionSeed.Weight ?? 1.0m;
                question.SortOrder = questionSeed.SortOrder;
                question.IsRequired = questionSeed.IsRequired;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<string> RequiredJsonFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"Required seed data directory was not found: {directory}");
        }

        var paths = Directory.EnumerateFiles(directory, "*.json")
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (paths.Length == 0)
        {
            throw new InvalidDataException(
                $"No JSON seed files were found in required directory: {directory}");
        }

        return paths;
    }

    private static async Task<T> DeserializeAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(
            stream,
            JsonOptions,
            cancellationToken)
            ?? throw new InvalidDataException($"Seed file is empty or invalid: {path}");
    }

    private static InvalidDataException InvalidSeedReference(
        string path,
        string reference) =>
        new($"Seed file '{path}' contains an unmatched {reference}.");
}
