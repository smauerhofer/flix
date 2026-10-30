namespace Flix.Core;

public sealed record PositiveEvaluationReport(
  string SnapshotVersion, DateTimeOffset GeneratedAtUtc,
  PositiveEvaluationMetrics Overall,
  IReadOnlyList<PositiveEvaluationMetrics> ByTenant,
  IReadOnlyList<PositiveEvaluationMetrics> ByStrategy,
  IReadOnlyList<PositiveEvaluationMetrics> ByGenerationType,
  IReadOnlyList<PositiveEvaluationMetrics> ByList,
  IReadOnlyList<PositiveEvaluationResult> Results,
  string? ConfigurationSha256 = null, string? AiModelVersion = null);
