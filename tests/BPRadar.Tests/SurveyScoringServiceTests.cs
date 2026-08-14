using BPRadar.Web.Data;
using BPRadar.Web.Features.Surveys;

namespace BPRadar.Tests;

[TestClass]
public sealed class SurveyScoringServiceTests
{
    [TestMethod]
    public void Explicit_scores_are_clamped_for_profile_and_domain_rollups()
    {
        var responses = new[]
        {
            CreateResponse(-25m),
            CreateResponse(125m)
        };

        var profileScore = SurveyScoringService.CalculateProfileScore(responses);
        var domainScores = SurveyScoringService.CalculateDomainScores(responses);

        Assert.AreEqual(50m, profileScore);
        Assert.AreEqual(50m, domainScores[1]);
        Assert.IsTrue(profileScore is >= 0m and <= 100m);
        Assert.IsTrue(domainScores.Values.All(score => score is >= 0m and <= 100m));
    }

    private static SurveyResponse CreateResponse(decimal score) =>
        new()
        {
            ResponseLevel = SurveyResponseLevel.Medium,
            Score = score,
            SurveyQuestion = new SurveyQuestion
            {
                Code = "BOUND",
                Prompt = "Bound explicit score",
                DomainId = 1,
                Weight = 1m
            }
        };
}
