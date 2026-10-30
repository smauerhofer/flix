namespace Flix.Core;

// Snapshot-building and screening must use the same normalization and algorithm versions.
//
// LuceneNameMatcher unions up to MaxRetrievalPaths independent paths (tokens,
// all-tokens, fuzzy, n-grams, phonetic), each returning at most
// CandidateLimitPerPath documents. With FinalCandidateLimit >= MaxRetrievalPaths *
// CandidateLimitPerPath (the default: 5 x 200 = 1000) the union can never exceed the
// final limit, so common names no longer produce capacity REVIEWs. Smaller final
// limits are allowed (e.g. to cap scoring cost); the matcher then fails closed.
public sealed record NameMatchingOptions(
  int CandidateLimitPerPath = 200,
  int FinalCandidateLimit = 1000, // MaxRetrievalPaths * CandidateLimitPerPath
  int MinNGramLength = 3,
  int MaxFuzzyExpansions = 50,
  string AlgorithmVersion = NameIndexBuilder.AlgorithmVersion)
{
  public const int MaxRetrievalPaths = 5;

  // True when the union of all retrieval paths can exceed FinalCandidateLimit.
  public bool CanOverflow => FinalCandidateLimit < MaxRetrievalPaths * CandidateLimitPerPath;

  public void Validate()
  {
    if (CandidateLimitPerPath is < 1 or > 5000 ||
      FinalCandidateLimit is < 1 or > MaxRetrievalPaths * 5000 ||
      MinNGramLength is < 2 or > 4 ||
      MaxFuzzyExpansions is < 1 or > 500 ||
      AlgorithmVersion.Length == 0)
    {
      throw new ArgumentOutOfRangeException(nameof(NameMatchingOptions));
    }
  }
}
