namespace Flix.Core;

public sealed record ListFinding(
  string List, string PartyId, string EntityId, string SourceId,
  string FieldType, string ReferenceValue, double Score);
