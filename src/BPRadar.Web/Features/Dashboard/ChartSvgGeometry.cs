using System.Globalization;

namespace BPRadar.Web.Features.Dashboard;

public static class ChartSvgGeometry
{
    private const double RadarCenterX = 300;
    private const double RadarCenterY = 220;
    private const double RadarRadius = 160;
    public static IReadOnlyList<int> TrendScaleLevels { get; } =
        [0, 25, 50, 75, 100];

    public static string RadarGridPoints(int axisCount, decimal level) =>
        RadarSeriesPoints(
            Enumerable.Repeat<decimal?>(level, axisCount).ToArray());

    public static string RadarSeriesPoints(IReadOnlyList<decimal?> values) =>
        string.Join(
            " ",
            values.Select((value, index) =>
                RadarPoint(
                    index,
                    values.Count,
                    RadarRadius * (double)value!.Value / 100d)));

    public static string RadarValueCoordinate(
        int index,
        int count,
        decimal value,
        bool xCoordinate)
    {
        var point = RadarPoint(
            index,
            count,
            RadarRadius * (double)value / 100d).Split(',');
        return xCoordinate ? point[0] : point[1];
    }

    public static string RadarCoordinate(
        int index,
        int count,
        double radius,
        bool xCoordinate)
    {
        var point = RadarPoint(index, count, radius).Split(',');
        return xCoordinate ? point[0] : point[1];
    }

    public static string RadarTextAnchor(int index, int count)
    {
        var x = double.Parse(
            RadarCoordinate(index, count, 190, xCoordinate: true),
            CultureInfo.InvariantCulture);
        return x < RadarCenterX - 5
            ? "end"
            : x > RadarCenterX + 5
                ? "start"
                : "middle";
    }

    public static string RadarGridLabelY(decimal level) =>
        (RadarCenterY - RadarRadius * (double)level / 100d)
        .ToString("0.##", CultureInfo.InvariantCulture);

    public static string TrendPoints(IReadOnlyList<SurveyTrendPoint> points) =>
        string.Join(
            " ",
            points.Select((point, index) =>
                $"{TrendX(index, points.Count)},{TrendY(point.Score)}"));

    public static string TrendX(int index, int count) =>
        (count <= 1 ? 310d : 60d + 500d * index / (count - 1d))
        .ToString("0.##", CultureInfo.InvariantCulture);

    public static double TrendY(decimal score) =>
        20d + (100d - (double)score) * 1.8d;

    public static string TrendDateLabel(SurveyTrendPoint point) =>
        point.SnapshotDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string TrendPointDescription(SurveyTrendPoint point) =>
        $"{TrendDateLabel(point)}: Self-Reported State score " +
        $"{point.Score.ToString("0.#", CultureInfo.InvariantCulture)}%";

    public static string TrendTextAnchor(int index, int count) =>
        index == 0 ? "start" : index == count - 1 ? "end" : "middle";

    private static string RadarPoint(int index, int count, double radius)
    {
        var angle = -Math.PI / 2d + 2d * Math.PI * index / Math.Max(count, 1);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{RadarCenterX + Math.Cos(angle) * radius:0.##}," +
            $"{RadarCenterY + Math.Sin(angle) * radius:0.##}");
    }
}

public sealed record SurveyTrendChartModel(
    string Id,
    IReadOnlyList<SurveyTrendPoint> Points);
