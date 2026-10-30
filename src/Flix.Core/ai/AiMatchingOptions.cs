namespace Flix.Core;

// A locally trained pairwise reranking model. Disabled unless explicitly configured.
public sealed record AiMatchingOptions
{
  public bool Enabled { get; init; } = false;
  public string ModelPath { get; init; } = "";
  public string ModelVersion { get; init; } = "";
  public int MaxCandidatesPerField { get; init; } = 100;
  public double RankingWeight { get; init; } = 0.30;
  public double MinimumConfidence { get; init; } = 0.70;

  public void Validate()
  {
    if (!double.IsFinite(RankingWeight) || RankingWeight < 0 || RankingWeight > 1 ||
      !double.IsFinite(MinimumConfidence) || MinimumConfidence < 0 || MinimumConfidence > 1)
    {
      throw new InvalidDataException("AI RankingWeight and MinimumConfidence must be finite values between 0 and 1.");
    }
    if (MaxCandidatesPerField is < 1 or > 2000)
    {
      throw new InvalidDataException("AI MaxCandidatesPerField must be between 1 and 2000.");
    }
    if (Enabled && (string.IsNullOrWhiteSpace(ModelPath) || string.IsNullOrWhiteSpace(ModelVersion)))
    {
      throw new InvalidDataException("AI ModelPath and ModelVersion are required when AI is enabled.");
    }
  }
}
