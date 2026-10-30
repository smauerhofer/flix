namespace Flix.Core;

public sealed record NegativeEvaluationCase(string CaseId, string TenantId,
  string MessageId, string SnapshotVersion, ScreeningField Field,
  string Label, string? LabelSource = null);
