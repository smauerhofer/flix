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
    return buffer.ToString().Trim();
  }
}
