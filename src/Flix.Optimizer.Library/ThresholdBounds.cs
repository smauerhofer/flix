using Flix.Core;

namespace Flix.Optimizer;

public sealed class ThresholdBounds
{
  public FilterParameters Lower { get; }
  public FilterParameters Upper { get; }

  public ThresholdBounds(FilterParameters lower, FilterParameters upper)
  {
    Lower = lower;
    Upper = upper;
    foreach (var property in ThresholdProperties.All)
    {
      double lo = (double)property.GetValue(lower)!;
      double hi = (double)property.GetValue(upper)!;
      if (lo > hi)
      {
        throw new ArgumentException($"Lower bound exceeds upper bound for {property.Name}.");
      }
    }
  }
}
