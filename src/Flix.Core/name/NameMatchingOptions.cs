namespace Flix.Core;

// Snapshot-building and screening must use the same normalization and algorithm versions.
public sealed record NameMatchingOptions(
  int CandidateLimitPerPath = 200,
  int FinalCandidateLimit = 600,
  int MinNGramLength = 3,
  int MaxFuzzyExpansions = 50,
  string AlgorithmVersion = NameIndexBuilder.AlgorithmVersion)
{
  public void Validate()
  {
    if (CandidateLimitPerPath is < 1 or > 5000 ||
      FinalCandidateLimit is < 1 or > 10000 ||
      MinNGramLength is < 2 or > 4 ||
      MaxFuzzyExpansions is < 1 or > 500 ||
      AlgorithmVersion.Length == 0)
    {
      throw new ArgumentOutOfRangeException(nameof(NameMatchingOptions));
    }
  }
}
