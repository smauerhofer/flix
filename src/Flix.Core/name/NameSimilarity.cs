namespace Flix.Core;

public static class NameSimilarity
{
  // Baseline only; sanctions-grade scoring requires calibrated aliases,
  // multilingual tests, token-order handling and independently measured recall.
  public static double Evaluate(string left, string right)
  {
    left = NameNormalizer.Normalize(left);
    right = NameNormalizer.Normalize(right);
    if (left == right)
    {
      return 1.0;
    }
    if (left.Length == 0 || right.Length == 0)
    {
      return 0.0;
    }
    var previous = new int[right.Length + 1];
    var current = new int[right.Length + 1];
    for (int j = 0; j <= right.Length; j++)
    {
      previous[j] = j;
    }
    for (int i = 1; i <= left.Length; i++)
    {
      current[0] = i;
      for (int j = 1; j <= right.Length; j++)
      {
        var cost = left[i - 1] == right[j - 1] ? 0 : 1;
        current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
      }
      (previous, current) = (current, previous);
    }
    return 1.0 - (double)previous[right.Length] / Math.Max(left.Length, right.Length);
  }
}
