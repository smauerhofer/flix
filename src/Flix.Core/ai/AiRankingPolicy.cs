namespace Flix.Core;

// Applies AI only to presentation order. Never modifies screening hit decisions.
public static class AiRankingPolicy
{
  public static double Combine(double traditionalScore, double aiScore,
    AiMatchingOptions options)
  {
    options.Validate();
    if (!double.IsFinite(traditionalScore) || traditionalScore < 0 || traditionalScore > 1 ||
      !double.IsFinite(aiScore) || aiScore < 0 || aiScore > 1)
    {
      throw new ArgumentOutOfRangeException(nameof(aiScore),
        "Candidate scores must be finite values between 0 and 1.");
    }
    if (aiScore < options.MinimumConfidence)
    {
      return traditionalScore;
    }
    return (1 - options.RankingWeight) * traditionalScore + options.RankingWeight * aiScore;
  }
}
