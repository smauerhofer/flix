namespace Flix.Core;

public sealed record NameMatch(string PartyId, string EntityId, string SourceId,
  string ReferenceName, double Similarity, float RetrievalScore);
