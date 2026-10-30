namespace Flix.Core;

public sealed record NegativeEvaluationReport(string SnapshotVersion, int Cases,
  int FalsePositives, int Errors, double FalsePositiveRate,
  double MeanMilliseconds, double P95Milliseconds,
  IReadOnlyList<NegativeEvaluationResult> Results);
