namespace Flix.Core;

// Data-quality finding; it is not a sanctions-list match.
public sealed record ScreeningValidationFinding(
  string PartyId,
  string Side,
  string FieldType,
  string Code,
  string Message);
