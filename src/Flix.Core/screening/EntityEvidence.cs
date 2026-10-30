namespace Flix.Core;

public sealed record EntityEvidence(string PartyId, string EntityId,
  IReadOnlyList<string> Sources, double BestNameScore,
  bool IdentifierMatched, bool AddressMatched, bool CountryMatched);
