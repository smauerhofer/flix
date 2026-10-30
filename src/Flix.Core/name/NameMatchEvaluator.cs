namespace Flix.Core;

// Scores are ranking features, NOT calibrated probabilities of sanctions identity.
public sealed class NameMatchEvaluator
{
  public NameMatchFeatures Features(string incoming, string reference)
  {
    string left = NameNormalizer.Normalize(incoming);
    string right = NameNormalizer.Normalize(reference);
    var lt = left.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var rt = right.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var leftKeys = lt.Select(NamePhonetics.Key).Where(x => x.Length > 0).ToHashSet();
    var rightKeys = rt.Select(NamePhonetics.Key).Where(x => x.Length > 0).ToHashSet();
    double phonetic = leftKeys.Count == 0 || rightKeys.Count == 0
      ? 0 : (double)leftKeys.Intersect(rightKeys).Count() / Math.Max(leftKeys.Count, rightKeys.Count);
    return new NameMatchFeatures(
      JaroWinkler(left, right),
      Damerau(left, right),
      TokenScore(lt, rt),
      Dice(NameNGrams.Generate(left), NameNGrams.Generate(right)),
      phonetic,
      left.Length != 0 && left == right);
  }

  public double Score(string incoming, string reference, string type)
  {
    NameMatchFeatures f = Features(incoming, reference);
    if (f.ExactNormalizedMatch)
    {
      return 1;
    }
    // A pure token permutation identifies the same full name, regardless of order.
    // Preserve token multiplicity; a set comparison would ignore duplicate tokens.
    string[] incomingTokens = NameNormalizer.Normalize(incoming)
      .Split(' ', StringSplitOptions.RemoveEmptyEntries);
    string[] referenceTokens = NameNormalizer.Normalize(reference)
      .Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (incomingTokens.Length > 0 && incomingTokens.Length == referenceTokens.Length &&
        incomingTokens.OrderBy(x => x, StringComparer.Ordinal)
          .SequenceEqual(referenceTokens.OrderBy(x => x, StringComparer.Ordinal),
            StringComparer.Ordinal))
    {
      return 1;
    }
    // Person names emphasize token alignment and spelling; companies emphasize
    // tokens and character overlap. Adjust only after validation on labeled cases.
    double score = type == "ORGANIZATION_NAME"
      ? .20 * f.JaroWinkler + .15 * f.DamerauLevenshtein + .40 * f.TokenSimilarity +
        .20 * f.NGramSimilarity + .05 * f.PhoneticSimilarity
      : .30 * f.JaroWinkler + .20 * f.DamerauLevenshtein + .30 * f.TokenSimilarity +
        .10 * f.NGramSimilarity + .10 * f.PhoneticSimilarity;
    return Math.Clamp(score, 0, 1);
  }

  private static double Dice(IReadOnlyList<string> a, IReadOnlyList<string> b)
  {
    if (a.Count == 0 || b.Count == 0)
    {
      return 0;
    }
    int shared = a.Intersect(b, StringComparer.Ordinal).Count();
    return 2.0 * shared / (a.Count + b.Count);
  }

  private static double TokenScore(string[] a, string[] b)
  {
    if (a.Length == 0 || b.Length == 0)
    {
      return 0;
    }
    var used = new bool[b.Length];
    double sum = 0;
    foreach (string token in a.OrderByDescending(x => x.Length))
    {
      double best = 0;
      int index = -1;
      for (int i = 0; i < b.Length; i++)
      {
        if (used[i])
        {
          continue;
        }
        double candidate = JaroWinkler(token, b[i]);
        if (candidate > best)
        {
          best = candidate;
          index = i;
        }
      }
      if (index >= 0)
      {
        used[index] = true;
        sum += best;
      }
    }
    return sum / Math.Max(a.Length, b.Length);
  }

  private static double Damerau(string a, string b)
  {
    if (a == b)
    {
      return 1;
    }
    if (a.Length == 0 || b.Length == 0)
    {
      return 0;
    }
    int[,] d = new int[a.Length + 1, b.Length + 1];
    for (int i = 0; i <= a.Length; i++)
    {
      d[i, 0] = i;
    }
    for (int j = 0; j <= b.Length; j++)
    {
      d[0, j] = j;
    }
    for (int i = 1; i <= a.Length; i++)
    {
      for (int j = 1; j <= b.Length; j++)
      {
        int cost = a[i - 1] == b[j - 1] ? 0 : 1;
        d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
        if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
        {
          d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
        }
      }
    }
    return 1.0 - (double)d[a.Length, b.Length] / Math.Max(a.Length, b.Length);
  }

  private static double JaroWinkler(string a, string b)
  {
    if (a == b)
    {
      return a.Length == 0 ? 0 : 1;
    }
    int range = Math.Max(0, Math.Max(a.Length, b.Length) / 2 - 1);
    bool[] am = new bool[a.Length];
    bool[] bm = new bool[b.Length];
    int matches = 0;
    for (int i = 0; i < a.Length; i++)
    {
      for (int j = Math.Max(0, i - range); j < Math.Min(b.Length, i + range + 1); j++)
      {
        if (!bm[j] && a[i] == b[j])
        {
          am[i] = bm[j] = true;
          matches++;
          break;
        }
      }
    }
    if (matches == 0)
    {
      return 0;
    }
    int transpositions = 0;
    for (int i = 0, j = 0; i < a.Length; i++)
    {
      if (!am[i])
      {
        continue;
      }
      while (!bm[j])
      {
        j++;
      }
      if (a[i] != b[j++])
      {
        transpositions++;
      }
    }
    double m = matches;
    double jaro = (m / a.Length + m / b.Length + (m - transpositions / 2.0) / m) / 3;
    int prefix = 0;
    while (prefix < Math.Min(4, Math.Min(a.Length, b.Length)) && a[prefix] == b[prefix])
    {
      prefix++;
    }
    return jaro > .7 ? jaro + prefix * .1 * (1 - jaro) : jaro;
  }
}
