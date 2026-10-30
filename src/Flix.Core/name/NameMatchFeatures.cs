namespace Flix.Core;

public sealed record NameMatchFeatures(
  double JaroWinkler,
  double DamerauLevenshtein,
  double TokenSimilarity,
  double NGramSimilarity,
  double PhoneticSimilarity,
  bool ExactNormalizedMatch);
