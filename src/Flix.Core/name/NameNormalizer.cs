using System.Globalization;
using System.Text;

namespace Flix.Core;

public static class NameNormalizer
{
  public static string Normalize(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return string.Empty;
    }
    var decomposed = value.Normalize(NormalizationForm.FormD);
    var buffer = new StringBuilder(decomposed.Length);
    var spaced = false;
    foreach (var ch in decomposed)
    {
      if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
      {
        continue;
      }
      if (char.IsLetterOrDigit(ch))
      {
        buffer.Append(char.ToLowerInvariant(ch));
        spaced = false;
      }
      else if (!spaced && buffer.Length > 0)
      {
        buffer.Append(' ');
        spaced = true;
      }
    }
    string normalized = buffer.ToString().Trim();
    // Repair Latin/Cyrillic look-alike letters inside a token (source typos and
    // homoglyph evasion). Applied identically to reference and incoming values.
    string[] tokens = normalized.Split(' ');
    bool changed = false;
    for (int i = 0; i < tokens.Length; i++)
    {
      string repaired = ScriptHomoglyphs.RepairToken(tokens[i]);
      if (!ReferenceEquals(repaired, tokens[i]))
      {
        tokens[i] = repaired;
        changed = true;
      }
    }
    return changed ? string.Join(' ', tokens) : normalized;
  }
}
