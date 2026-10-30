namespace Flix.Core;

public sealed record ScreeningResponse(string MessageId, string SnapshotVersion,
  string Status, IReadOnlyList<NameMatch> PotentialMatches,
  IReadOnlyList<FieldMatch>? FieldMatches = null,
  IReadOnlyList<ScreeningValidationFinding>? ValidationFindings = null);
