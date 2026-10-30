using System.Text.RegularExpressions;

namespace Flix.Core;

// Compatibility scorer: reads original address strings from existing Lucene snapshots.
// House-number disagreements must not receive a high confidence score.
public static class AddressSimilarity
{
  private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.Ordinal)
  {
    ["st"] = "street", ["str"] = "street", ["strasse"] = "street",
    ["straße"] = "street", ["rd"] = "road", ["ave"] = "avenue",
    ["av"] = "avenue", ["blvd"] = "boulevard", ["pl"] = "place",
    ["ste"] = "suite", ["apt"] = "apartment"
  };

  public static string[] Tokens(string value)
  {
    string normalized = NameNormalizer.Normalize(value.Replace("ß", "ss", StringComparison.OrdinalIgnoreCase));
    return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
      .Select(x => Abbreviations.TryGetValue(x, out string? expanded) ? expanded : x)
      .ToArray();
  }

  public static double Evaluate(string incoming, string reference)
  {
    string[] left = Tokens(incoming);
    string[] right = Tokens(reference);
    if (left.Length == 0 || right.Length == 0)
    {
      return 0.0;
    }
    string[] leftNumbers = left.Where(x => x.Any(char.IsDigit)).OrderBy(x => x).ToArray();
    string[] rightNumbers = right.Where(x => x.Any(char.IsDigit)).OrderBy(x => x).ToArray();
    bool numberConflict = leftNumbers.Length > 0 && rightNumbers.Length > 0 &&
      !leftNumbers.SequenceEqual(rightNumbers);
    double bestOrdered = NameSimilarity.Evaluate(string.Join(' ', left), string.Join(' ', right));
    double bestUnordered = NameSimilarity.Evaluate(
      string.Join(' ', left.OrderBy(x => x, StringComparer.Ordinal)),
      string.Join(' ', right.OrderBy(x => x, StringComparer.Ordinal)));
    double result = Math.Max(bestOrdered, bestUnordered);
    // A different building number cannot be treated as a near-exact address.
    return numberConflict ? Math.Min(0.65, result) : result;
  }
}
