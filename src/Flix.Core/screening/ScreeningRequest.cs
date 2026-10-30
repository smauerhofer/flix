namespace Flix.Core;

// The tenant must be sourced from trusted routing/authentication context in production.
// Default preserves existing locally constructed test requests.
public sealed record ScreeningRequest(
  string MessageId,
  IReadOnlyList<ScreeningField> Fields,
  string TenantId = "default");
