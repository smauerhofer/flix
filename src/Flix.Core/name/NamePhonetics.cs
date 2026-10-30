namespace Flix.Core;

// English-oriented Soundex key. Not a substitute for language-specific transliteration.
public static class NamePhonetics
{
  public static string Key(string token)
  {
    token = NameNormalizer.Normalize(token).Replace(" ", "");
    if (token.Length == 0 || token.Any(c => c is < 'a' or > 'z'))
    {
      return string.Empty;
    }
    char first = char.ToUpperInvariant(token[0]);
    var code = new System.Text.StringBuilder().Append(first);
    char previous = Digit(token[0]);
    for (int i = 1; i < token.Length && code.Length < 4; i++)
    {
      char digit = Digit(token[i]);
      if (digit != '0' && digit != previous)
      {
        code.Append(digit);
      }
      previous = digit;
    }
    while (code.Length < 4)
    {
      code.Append('0');
    }
    return code.ToString();
  }

  private static char Digit(char value)
  {
    return value switch
    {
      'b' or 'f' or 'p' or 'v' => '1',
      'c' or 'g' or 'j' or 'k' or 'q' or 's' or 'x' or 'z' => '2',
      'd' or 't' => '3',
      'l' => '4',
      'm' or 'n' => '5',
      'r' => '6',
      _ => '0'
    };
  }
}
