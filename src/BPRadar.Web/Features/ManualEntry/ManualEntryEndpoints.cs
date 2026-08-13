using BPRadar.Web.Data;

namespace BPRadar.Web.Features.ManualEntry;

public static class ManualEntryEndpoints
{
    public static IEndpointRouteBuilder MapManualEntryEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut(
            "/api/assessments/{assessmentId:int}/results/{controlId:int}",
            async (
                int assessmentId,
                int controlId,
                SaveAssessmentResultRequest request,
                BPRadarDbContext dbContext,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                var outcome = await ManualEntryService.UpsertAsync(
                    dbContext,
                    timeProvider,
                    assessmentId,
                    controlId,
                    request,
                    cancellationToken);
                if (outcome.NotFoundMessage is not null)
                {
                    return Results.NotFound(new { message = outcome.NotFoundMessage });
                }

                return outcome.Errors is not null
                    ? Results.ValidationProblem(outcome.Errors)
                    : Results.Ok(outcome.Result);
            });

        return endpoints;
    }
}
