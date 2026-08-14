namespace BPRadar.Web.Utilities;

internal static class TextNormalization
{
    public static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
