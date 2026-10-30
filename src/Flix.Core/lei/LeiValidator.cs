namespace Flix.Core;

// ISO 17442 LEI: 20 alphanumeric characters with ISO 7064 MOD 97-10 checksum.
public static class LeiValidator
{
  public static bool IsValid(string? value)
  {
    if (value is null)
    {
      return false;
    }
    string lei = FieldNormalization.Normalize("LEI", value);
    if (lei.Length != 20 || lei.Any(c => !char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c)))
    {
      return false;
    }

    int remainder = 0;
    foreach (char c in lei)
    {
      if (c >= '0' && c <= '9')
      {
        remainder = (remainder * 10 + (c - '0')) % 97;
      }
      else
      {
        int digits = c - 'A' + 10;
        remainder = (remainder * 100 + digits) % 97;
      }
    }
    return remainder == 1;
  }
}
