namespace Flix.Core;

// Validates identifiers before any of the three reference indexes are queried.
public static class ScreeningFieldValidator
{
  public static ScreeningValidationFinding? Validate(ScreeningField field, string side)
  {
    string type = FieldNormalization.CanonicalType(field.Type);
    if (type == "IBAN" && !IbanValidator.IsValid(field.Value))
    {
      return new ScreeningValidationFinding(field.PartyId, side, type,
        "INVALID_IBAN", "Invalid IBAN format or MOD 97-10 checksum.");
    }
    if (type == "LEI" && !LeiValidator.IsValid(field.Value))
    {
      return new ScreeningValidationFinding(field.PartyId, side, type,
        "INVALID_LEI", "Invalid LEI format or MOD 97-10 checksum.");
    }
    // An unrecognised country code (e.g. alpha-3 "PRK" or a country name) could never
    // match a country block, so it must not pass silently as CLEAR.
    if (type == "COUNTRY")
    {
      string code = FieldNormalization.Normalize(type, field.Value);
      if (code.Length != 2 || !code.All(char.IsAsciiLetterUpper))
      {
        return new ScreeningValidationFinding(field.PartyId, side, type,
          "INVALID_COUNTRY", "Country must be an ISO 3166-1 alpha-2 code.");
      }
    }
    return null;
  }
}
