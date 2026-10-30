namespace Flix.Core;

public static class NameNGrams
{
  public static IReadOnlyList<string> Generate(string normalized, int size = 3)
  {
    string value = normalized.Replace(" ", "");
    if (value.Length < size)
    {
      return [];
    }
    return Enumerable.Range(0, value.Length - size + 1)
      .Select(i => value.Substring(i, size))
      .Distinct(StringComparer.Ordinal)
      .ToArray();
  }
}
