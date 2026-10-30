namespace Flix.Core;

public sealed record ScreeningDecision(string MessageId, string SnapshotVersion,
  string Decision, bool ReleaseAllowed, string Reason,
  IReadOnlyList<EntityEvidence> Entities,
  IReadOnlyList<NameMatch> NameMatches,
  IReadOnlyList<FieldMatch> FieldMatches,
  DateTimeOffset ScreenedAtUtc,
  IReadOnlyList<ListFinding>? BlacklistMatches = null,
  IReadOnlyList<ListFinding>? WhitelistMatches = null,
  bool WhitelistOverrideApplied = false,
  IReadOnlyList<SideScreeningDecision>? Sides = null,
  IReadOnlyList<ScreeningValidationFinding>? ValidationFindings = null,
  string TenantId = "default",
  IReadOnlyList<AiMatchEvidence>? AiEvidence = null);
