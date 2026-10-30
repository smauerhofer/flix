namespace Flix.Core;

public sealed record PositiveEvaluationResult(
  string CaseId, string TenantId, string List, string Strategy,
  string GenerationType, string ExpectedSourceId, string ExpectedEntityId,
  bool ExpectedMatchIsGuaranteed, bool ExpectedEntityFound, bool ReviewTriggered,
  bool FalseNegative, string Decision, double ElapsedMilliseconds,
  string SnapshotVersion, string? Error = null,
  bool? CountryValueDetected = null, bool? CountryEntityPresentInIndex = null,
  bool? CandidateRetrieved = null, double? BestTraditionalScore = null,
  double? ExpectedEntityScore = null, double? ReviewThreshold = null,
  bool? WhitelistOverrideApplied = null, string? Diagnostic = null);
