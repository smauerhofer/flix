namespace Flix.Core;

// CountryBlockHit: a COUNTRY entry of the global or tenant blacklist matched this side.
// A country block always results in REVIEW and can never be overridden by a whitelist.
public sealed record SideScreeningDecision(
  string Side,
  int FieldCount,
  int WhitelistMatchedFieldCount,
  bool NormalHit,
  bool BlacklistHit,
  bool WhitelistHit,
  bool WhitelistOverrideApplied,
  string Decision,
  bool CountryBlockHit = false);
