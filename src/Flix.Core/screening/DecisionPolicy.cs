namespace Flix.Core;

public sealed record DecisionPolicy(double NameReviewThreshold = 0.82,
  double AddressReviewThreshold = 0.90, int MaxFields = 100,
  int MaxMatches = 2000);
