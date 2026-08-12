using BPRadar.Web.Data;

namespace BPRadar.Web.Features.Surveys;

public static class SurveyEndpoints
{
    public static IEndpointRouteBuilder MapSurveyEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/admin/survey-templates",
            async (BPRadarDbContext dbContext, CancellationToken cancellationToken) =>
                await SurveyTemplateQueries.ListAsync(dbContext, cancellationToken));

        endpoints.MapGet(
            "/api/admin/survey-templates/{templateId:int}",
            async (
                int templateId,
                BPRadarDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var template = await SurveyTemplateQueries.GetActiveAsync(
                    dbContext,
                    templateId,
                    cancellationToken);
                return template is null ? Results.NotFound() : Results.Ok(template);
            });

        endpoints.MapPost(
            "/api/organizations/{organizationId:int}/survey-submissions",
            async (
                int organizationId,
                CreateSurveySubmissionRequest request,
                BPRadarDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var result = await SurveySubmissionService.CreateAsync(
                    dbContext,
                    organizationId,
                    request,
                    cancellationToken);
                if (result.Errors is not null)
                {
                    return Results.ValidationProblem(result.Errors);
                }

                var submission = result.Submission!;
                return Results.Created(
                    $"/api/organizations/{organizationId}/survey-submissions/{submission.Id}",
                    submission);
            });

        endpoints.MapGet(
            "/api/organizations/{organizationId:int}/survey-submissions/latest",
            async (
                int organizationId,
                BPRadarDbContext dbContext,
                CancellationToken cancellationToken) =>
                    await SurveySubmissionService.GetLatestAsync(
                        dbContext,
                        organizationId,
                        cancellationToken));

        return endpoints;
    }
}
