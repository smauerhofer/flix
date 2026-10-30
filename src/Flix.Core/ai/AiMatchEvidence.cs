namespace Flix.Core;

// Auxiliary ranking evidence. Never replaces deterministic threshold decisions.
public sealed record AiMatchEvidence(
  string ListScope, string PartyId, string FieldType,
  string EntityId, string SourceId, string ReferenceValue,
  double TraditionalScore, double AiScore, string ModelVersion,
  int Rank, double CombinedScore = 0);
