namespace Flix.Core;

public static class ReferenceListValidation
{
  public static void ValidateBlacklist(IReadOnlyList<BlacklistEntry> entries)
  {
    var ids = new HashSet<string>(StringComparer.Ordinal);
    foreach (BlacklistEntry entry in entries)
    {
      if (entry is null || string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id) ||
        string.IsNullOrWhiteSpace(entry.EntityId) || string.IsNullOrWhiteSpace(entry.SourceId) ||
        !FieldNormalization.Supported(entry.Type) || string.IsNullOrWhiteSpace(entry.Value))
      {
        throw new InvalidDataException("Invalid or duplicate blacklist entry.");
      }
      ValidateIdentifier(entry.Type, entry.Value);
    }
  }

  public static void ValidateWhitelist(IReadOnlyList<WhitelistGroupEntry> groups)
  {
    var ids = new HashSet<string>(StringComparer.Ordinal);
    var identities = new HashSet<(string, string)>();
    foreach (WhitelistGroupEntry group in groups)
    {
      if (group is null || string.IsNullOrWhiteSpace(group.Id) || !ids.Add(group.Id) ||
        string.IsNullOrWhiteSpace(group.EntityId) || string.IsNullOrWhiteSpace(group.SourceId) ||
        !identities.Add((group.EntityId, group.SourceId)) || group.Criteria is null || group.Criteria.Count == 0)
      {
        throw new InvalidDataException("Invalid, empty or duplicate whitelist group.");
      }
      var criterionIds = new HashSet<string>(StringComparer.Ordinal);
      foreach (WhitelistCriterion criterion in group.Criteria)
      {
        if (criterion is null || string.IsNullOrWhiteSpace(criterion.Id) || !criterionIds.Add(criterion.Id) ||
          !FieldNormalization.Supported(criterion.Type) || string.IsNullOrWhiteSpace(criterion.Value))
        {
          throw new InvalidDataException("Invalid or duplicate whitelist criterion.");
        }
        ValidateIdentifier(criterion.Type, criterion.Value);
      }
    }
  }

  private static void ValidateIdentifier(string type, string value)
  {
    string normalizedType = FieldNormalization.CanonicalType(type);
    if (normalizedType == "LEI" && !LeiValidator.IsValid(value))
    {
      throw new InvalidDataException("Invalid LEI.");
    }
    if (normalizedType == "IBAN" && !IbanValidator.IsValid(value))
    {
      throw new InvalidDataException("Invalid IBAN.");
    }
    // Country blocks must be exact ISO 3166-1 alpha-2 codes; a typo would silently
    // fail to block, so reject anything else at import time.
    if (normalizedType == "COUNTRY")
    {
      string code = FieldNormalization.Normalize("COUNTRY", value);
      if (code.Length != 2 || !code.All(char.IsAsciiLetterUpper))
      {
        throw new InvalidDataException("Invalid country code; expected ISO 3166-1 alpha-2.");
      }
    }
  }
}
