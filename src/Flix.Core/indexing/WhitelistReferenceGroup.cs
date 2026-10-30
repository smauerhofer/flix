namespace Flix.Core;

// One approved source record, not a union of unrelated whitelist entries.
public sealed record WhitelistReferenceGroup(
  string EntityId,
  string SourceId,
  IReadOnlyList<WhitelistReferenceCriterion> Criteria);
