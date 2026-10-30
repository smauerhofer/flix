namespace Flix.Core;

public static class FieldNormalization
{
  public static string CanonicalType(string type) => type.Trim().ToUpperInvariant() switch
  {
    "ACCOUNT_NUMBER" => "ACCOUNT",
    "LEI_CODE" => "LEI",
    "PERSON" => "PERSON_NAME",
    "ORGANIZATION" => "ORGANIZATION_NAME",
    "COMPANY_NAME" => "ORGANIZATION_NAME",
    "BICFI" => "BIC",
    "COUNTRY_CODE" => "COUNTRY",
    "POSTAL_ADDRESS" => "ADDRESS",
    var other => other
  };

  public static string Normalize(string type, string value)
  {
    type = CanonicalType(type);
    if (type is "IBAN" or "BIC" or "ACCOUNT" or "LEI")
    {
      return new string(value.Where(char.IsLetterOrDigit)
        .Select(char.ToUpperInvariant).ToArray());
    }
    if (type == "COUNTRY")
    {
      return value.Trim().ToUpperInvariant();
    }
    return NameNormalizer.Normalize(value);
  }

  public static bool Supported(string type) => CanonicalType(type) is
    "NAME" or "PERSON_NAME" or "ORGANIZATION_NAME" or "IBAN" or "BIC" or "ACCOUNT" or "LEI" or "COUNTRY" or "ADDRESS";
}
