namespace Flix.Core;

public sealed record PositiveEvaluationMetrics(
  string Group, int Cases, int ExpectedMatchesFound, int MissedExpectedEntities,
  int ReviewsTriggered, int FalseNegatives, int Errors,
  double DetectionRate, double ReviewRate, double MeanMilliseconds,
  double P50Milliseconds, double P95Milliseconds, double P99Milliseconds);
