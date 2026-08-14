namespace BPRadar.Web.Data;

public static class ComplianceStatusExtensions
{
    public static string ToDisplayText(this ComplianceStatus status) =>
        status == ComplianceStatus.NonCompliant
            ? "Non-Compliant"
            : status.ToString();
}
