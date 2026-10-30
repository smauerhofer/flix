namespace Flix.Core;

// Fixed feature contract for the ONNX model: float32 [1, 6].
public static class AiPairFeatures
{
  public const int Dimension = 6;

  public static float[] Extract(string incoming, string reference, double traditionalScore)
  {
    string left = NameNormalizer.Normalize(incoming);
    string right = NameNormalizer.Normalize(reference);
    var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries)
      .ToHashSet(StringComparer.Ordinal);
    var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries)
      .ToHashSet(StringComparer.Ordinal);
    int union = leftTokens.Union(rightTokens).Count();
    int common = leftTokens.Intersect(rightTokens).Count();
    double tokenJaccard = union == 0 ? 0 : (double)common / union;
    double lengthRatio = Math.Max(left.Length, right.Length) == 0 ? 1 :
      (double)Math.Min(left.Length, right.Length) / Math.Max(left.Length, right.Length);
    double firstToken = leftTokens.Count > 0 && rightTokens.Count > 0 &&
      left.Split(' ')[0] == right.Split(' ')[0] ? 1 : 0;
    double prefix = left.Length > 0 && right.Length > 0 && left[0] == right[0] ? 1 : 0;
    return [
      (float)Math.Clamp(traditionalScore, 0, 1),
      (float)Math.Clamp(NameSimilarity.Evaluate(incoming, reference), 0, 1),
      (float)tokenJaccard,
      (float)lengthRatio,
      (float)firstToken,
      (float)prefix
    ];
  }
}
