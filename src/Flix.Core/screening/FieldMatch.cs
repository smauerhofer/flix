namespace Flix.Core;

public sealed record FieldMatch(string PartyId, string FieldType, string EntityId,
  string SourceId, string ReferenceValue, string MatchKind, double Score);
