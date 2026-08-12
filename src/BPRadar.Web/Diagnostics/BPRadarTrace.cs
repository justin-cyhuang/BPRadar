using System.Diagnostics;

namespace BPRadar.Web.Diagnostics;

public static class BPRadarTrace
{
    private static readonly AsyncLocal<string?> CurrentCorrelationId = new();

    public static readonly TraceSource Source = new("BPRadar");

    public static string? CorrelationId
    {
        get => CurrentCorrelationId.Value;
        set => CurrentCorrelationId.Value = value;
    }

    public static void Configure(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredLevel = configuration["Tracing:Level"];
        Source.Switch.Level = Enum.TryParse<SourceLevels>(
            configuredLevel,
            ignoreCase: true,
            out var level)
            ? level
            : SourceLevels.Warning;

        Source.Listeners.Clear();
        if (environment.IsDevelopment())
        {
            Source.Listeners.Add(new ConsoleTraceListener());
        }

        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        foreach (var path in Directory.EnumerateFiles(logDirectory, "bpradar-*.log"))
        {
            if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddDays(-14))
            {
                File.Delete(path);
            }
        }

        Source.Listeners.Add(new TextWriterTraceListener(
            Path.Combine(logDirectory, $"bpradar-{DateTime.UtcNow:yyyyMMdd}.log")));
        Source.Flush();
    }

    public static void Write(
        TraceEventType severity,
        string component,
        string operation,
        string details,
        long? durationMilliseconds = null)
    {
        var correlationId =
            CorrelationId ?? Trace.CorrelationManager.ActivityId.ToString();
        Source.TraceEvent(
            severity,
            0,
            "{0:O} severity={1} component={2} operation={3} correlationId={4} durationMs={5} {6}",
            DateTime.UtcNow,
            severity,
            component,
            operation,
            correlationId,
            durationMilliseconds?.ToString() ?? "n/a",
            details);
        Source.Flush();
    }
}
