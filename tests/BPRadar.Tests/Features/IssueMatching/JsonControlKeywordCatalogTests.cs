using BPRadar.Web.Features.IssueMatching;

namespace BPRadar.Tests.Features.IssueMatching;

[TestClass]
public sealed class JsonControlKeywordCatalogTests
{
    [TestMethod]
    public async Task GetAllAsync_LoadsEverySeededControl()
    {
        var seedPath = Path.Combine(
            AppContext.BaseDirectory,
            "seed-data",
            "control-keywords.json");
        var catalog = new JsonControlKeywordCatalog(seedPath);

        var controls = await catalog.GetAllAsync();

        Assert.HasCount(184, controls);
        var backupControl = controls.Single(
            control =>
                control.FrameworkCode == "ISO27001_2022" &&
                control.ControlCode == "A.8.13");
        CollectionAssert.Contains(
            backupControl.Keywords.ToArray(),
            "backup failure");
    }
}
