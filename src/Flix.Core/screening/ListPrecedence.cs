namespace Flix.Core;

public enum ListPrecedence
{
  WhitelistFirst,
  BlacklistFirst
}

// WhitelistOverridesAll is an explicit high-risk business override; it is NOT
// evidence that a transaction is legally permitted under sanctions law.
