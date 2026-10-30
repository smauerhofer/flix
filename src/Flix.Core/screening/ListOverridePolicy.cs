namespace Flix.Core;

// Defaults: a qualified whitelist overrides normal-list hits but never a blacklist hit.
// NameThreshold/AddressThreshold apply to blacklists; whitelists require exact values.
public sealed record ListOverridePolicy(
  bool WhitelistOverridesAll = false,
  ListPrecedence Precedence = ListPrecedence.BlacklistFirst,
  double NameThreshold = 0.92,
  double AddressThreshold = 0.95);
