using BPRadar.Web.Data;
using BPRadar.Web.Diagnostics;
using BPRadar.Web.Features.IssueMatching;
using BPRadar.Web.Features.Issues;
using BPRadar.Web.Features.Import;
using BPRadar.Web.Features.ManualEntry;
using BPRadar.Web.Features.Surveys;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=bpradar.db";
builder.Services.AddDbContext<BPRadarDbContext>(
    options => options.UseSqlite(connectionString));
builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<ImportSessionStore>();
builder.Services.AddScoped<AssessmentImportService>();
builder.Services.AddIssueMatching(
    builder.Configuration,
    builder.Environment.ContentRootPath);

var app = builder.Build();
BPRadarTrace.Configure(builder.Configuration, builder.Environment);

app.UseMiddleware<CorrelationMiddleware>();

await using (var scope = app.Services.CreateAsyncScope())
{
    var startupCorrelationId = Guid.NewGuid();
    Trace.CorrelationManager.ActivityId = startupCorrelationId;
    BPRadarTrace.CorrelationId = startupCorrelationId.ToString();
    var dbContext = scope.ServiceProvider.GetRequiredService<BPRadarDbContext>();
    await dbContext.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(
        dbContext,
        Path.Combine(AppContext.BaseDirectory, "seed-data"));
}

app.MapRazorPages();
app.MapSurveyEndpoints();
app.MapIssueEndpoints();
app.MapManualEntryEndpoints();
app.MapPost(
    "/api/issue-matching/candidates",
    IssueMatchingEndpoint.MatchCandidatesAsync);
app.MapGet("/", () => Results.Redirect("/Assessments"));
app.Run();

public partial class Program;
