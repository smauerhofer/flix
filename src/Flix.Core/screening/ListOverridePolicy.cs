namespace Flix.Core;

public sealed record ListOverridePolicy(
  bool WhitelistOverridesAll = true,
  ListPrecedence Precedence = ListPrecedence.WhitelistFirst,
  double NameThreshold = 0.92,
  double AddressThreshold = 0.95);
