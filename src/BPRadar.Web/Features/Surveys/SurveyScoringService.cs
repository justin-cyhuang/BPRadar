using BPRadar.Web.Data;

namespace BPRadar.Web.Features.Surveys;

public static class SurveyScoringService
{
    public const decimal MinimumScore = 0m;
    public const decimal MaximumScore = 100m;

    public static decimal? CalculateProfileScore(
        IEnumerable<SurveyResponse> responses) =>
        CalculateWeightedScore(responses);

    public static IReadOnlyDictionary<int, decimal> CalculateDomainScores(
        IEnumerable<SurveyResponse> responses) =>
        responses
            .Where(response => response.SurveyQuestion.DomainId is not null)
            .GroupBy(response => response.SurveyQuestion.DomainId!.Value)
            .Select(group => new
            {
                DomainId = group.Key,
                Score = CalculateWeightedScore(group)
            })
            .Where(item => item.Score is not null)
            .ToDictionary(item => item.DomainId, item => item.Score!.Value);

    private static decimal? CalculateWeightedScore(
        IEnumerable<SurveyResponse> responses)
    {
        var scored = responses
            .Select(response => new
            {
                Score = EffectiveScore(response),
                response.SurveyQuestion.Weight
            })
            .Where(item => item.Score is not null && item.Weight > 0m)
            .ToArray();
        var totalWeight = scored.Sum(item => item.Weight);
        if (totalWeight == 0m)
        {
            return null;
        }

        return scored.Sum(item => item.Score!.Value * item.Weight) / totalWeight;
    }

    private static decimal? EffectiveScore(SurveyResponse response)
    {
        if (response.ResponseLevel == SurveyResponseLevel.NotApplicable)
        {
            return null;
        }

        if (response.Score is not null)
        {
            return Math.Clamp(response.Score.Value, MinimumScore, MaximumScore);
        }

        return response.ResponseLevel switch
        {
            SurveyResponseLevel.VeryLow => 0m,
            SurveyResponseLevel.Low => 25m,
            SurveyResponseLevel.Medium => 50m,
            SurveyResponseLevel.High => 75m,
            SurveyResponseLevel.VeryHigh => 100m,
            _ => null
        };
    }
}
