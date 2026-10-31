namespace Flix.Core;

// Repairs tokens that mix Latin and Cyrillic look-alike letters, e.g. "алексееўнa"
// (Latin a), "ананiч" (Latin i) or "pаvel" (Cyrillic а). Such tokens occur in the
// SECO source data and are a known way to evade sanctions screening. Applied by
// NameNormalizer to every token, on both the reference and the incoming side.
//
// Works on normalized tokens (lower-case, diacritics removed). A token is converted
// to one script only when that script is unambiguous:
//  - it contains a Cyrillic letter without a Latin twin (б, г, ы, л, ...): Cyrillic,
//  - it contains a Latin letter without a Cyrillic twin (d, f, g, l, n, ...): Latin,
//  - both kinds: genuinely mixed, left unchanged,
//  - only twins: the majority script wins; a tie is left unchanged.
public static class ScriptHomoglyphs
{
  private static readonly Dictionary<char, char> LatinToCyrillic = new()
  {
    ['a'] = 'а', ['b'] = 'в', ['c'] = 'с', ['e'] = 'е', ['h'] = 'н', ['i'] = 'і',
    ['k'] = 'к', ['ĸ'] = 'к', ['m'] = 'м', ['o'] = 'о', ['p'] = 'р', ['t'] = 'т',
    ['x'] = 'х', ['y'] = 'у'
  };

  private static readonly Dictionary<char, char> CyrillicToLatin = new()
  {
    ['а'] = 'a', ['в'] = 'b', ['с'] = 'c', ['е'] = 'e', ['н'] = 'h', ['і'] = 'i',
    ['к'] = 'k', ['м'] = 'm', ['о'] = 'o', ['р'] = 'p', ['т'] = 't', ['х'] = 'x',
    ['у'] = 'y', ['ѕ'] = 's', ['ј'] = 'j', ['ԁ'] = 'd'
  };

  public static string RepairToken(string token)
  {
    int latin = 0, cyrillic = 0;
    bool latinOnly = false, cyrillicOnly = false;
    foreach (char c in token)
    {
      if (!char.IsLetter(c))
      {
        continue;
      }
      if (IsLatin(c))
      {
        latin++;
        latinOnly |= !LatinToCyrillic.ContainsKey(c);
      }
      else if (IsCyrillic(c))
      {
        cyrillic++;
        cyrillicOnly |= !CyrillicToLatin.ContainsKey(c);
      }
    }
    if (latin == 0 || cyrillic == 0 || (latinOnly && cyrillicOnly))
    {
      return token;
    }
    bool toCyrillic;
    if (cyrillicOnly)
    {
      toCyrillic = true;
    }
    else if (latinOnly)
    {
      toCyrillic = false;
    }
    else if (cyrillic != latin)
    {
      toCyrillic = cyrillic > latin;
    }
    else
    {
      return token;
    }
    var buffer = token.ToCharArray();
    for (int i = 0; i < buffer.Length; i++)
    {
      char c = buffer[i];
      if (toCyrillic && IsLatin(c) && LatinToCyrillic.TryGetValue(c, out char cyr))
      {
        buffer[i] = cyr;
      }
      else if (!toCyrillic && IsCyrillic(c) && CyrillicToLatin.TryGetValue(c, out char lat))
      {
        buffer[i] = lat;
      }
    }
    return new string(buffer);
  }

  private static bool IsLatin(char c) => c <= 'ɏ' || (c >= 'Ḁ' && c <= 'ỿ');

  private static bool IsCyrillic(char c) => c >= 'Ѐ' && c <= 'ԯ';
}
