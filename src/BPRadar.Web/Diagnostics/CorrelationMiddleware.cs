using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace BPRadar.Web.Diagnostics;

public sealed class CorrelationMiddleware(RequestDelegate next)
{
    private const string CorrelationHeader = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var incomingCorrelationId =
            context.Request.Headers[CorrelationHeader].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(incomingCorrelationId)
            ? Guid.NewGuid().ToString()
            : incomingCorrelationId;

        Trace.CorrelationManager.ActivityId = ToActivityId(correlationId);
        BPRadarTrace.CorrelationId = correlationId;
        context.Response.Headers[CorrelationHeader] = correlationId;

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            BPRadarTrace.Write(
                TraceEventType.Error,
                "Web",
                "UnhandledException",
                $"method={context.Request.Method} path={context.Request.Path} " +
                $"exceptionType={exception.GetType().Name}");

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "An unexpected error occurred.",
                status = StatusCodes.Status500InternalServerError,
                correlationId
            });
        }
        finally
        {
            BPRadarTrace.CorrelationId = null;
        }
    }

    private static Guid ToActivityId(string correlationId)
    {
        if (Guid.TryParse(correlationId, out var activityId))
        {
            return activityId;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(correlationId));
        return new Guid(hash.AsSpan(0, 16));
    }
}
