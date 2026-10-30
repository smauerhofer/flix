namespace Flix.Core;

public sealed record PositiveEvaluationCase(
  string CaseId,
  string TenantId,
  string MessageId,
  string SnapshotVersion,
  string List,
  ScreeningField Field,
  PositiveEvaluationExpected Expected,
  string GenerationType,
  bool ExpectedMatchIsGuaranteed);
