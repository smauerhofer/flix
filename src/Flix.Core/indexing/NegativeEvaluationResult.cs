namespace Flix.Core;

public sealed record NegativeEvaluationResult(string CaseId, string TenantId,
  string Strategy, string Decision, bool FalsePositive,
  double ElapsedMilliseconds, string? Error);
