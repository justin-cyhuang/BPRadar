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

        return endpoints;
    }
}
