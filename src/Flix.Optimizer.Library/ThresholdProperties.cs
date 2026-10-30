using System.Reflection;
using Flix.Core;

namespace Flix.Optimizer;

public static class ThresholdProperties
{
  public static IReadOnlyList<PropertyInfo> All { get; } =
    typeof(FilterParameters).GetProperties(BindingFlags.Instance | BindingFlags.Public)
      .Where(p => p.Name.EndsWith("Threshold", StringComparison.Ordinal) &&
        p.PropertyType == typeof(double) && p.GetMethod is not null)
      .OrderBy(p => p.Name, StringComparer.Ordinal)
      .ToArray();
}
