namespace Flix.Core;

// ISO 13616 IBAN with ISO 7064 MOD 97-10 checksum.
public static class IbanValidator
{
  public static bool IsValid(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return false;
    }

    string iban = new string(value
      .Where(c => c != ' ' && c != '-')
      .Select(char.ToUpperInvariant)
      .ToArray());

    if (iban.Length < 15 || iban.Length > 34)
    {
      return false;
    }

    if (!char.IsAsciiLetterUpper(iban[0]) ||
        !char.IsAsciiLetterUpper(iban[1]))
    {
      return false;
    }

    if (!char.IsAsciiDigit(iban[2]) ||
        !char.IsAsciiDigit(iban[3]))
    {
      return false;
    }

    foreach (char c in iban)
    {
      if (!char.IsAsciiLetterUpper(c) &&
          !char.IsAsciiDigit(c))
      {
        return false;
      }
    }

    string rearranged = iban[4..] + iban[..4];
    int remainder = 0;

    foreach (char c in rearranged)
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
