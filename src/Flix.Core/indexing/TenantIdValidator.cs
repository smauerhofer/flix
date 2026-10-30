namespace Flix.Core;

public static class TenantIdValidator
{
  public static bool IsValid(string? tenantId)
  {
    if (string.IsNullOrWhiteSpace(tenantId) || tenantId.Length > 64)
    {
      return false;
    }
    return tenantId.All(c => (c >= 'a' && c <= 'z') ||
      (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c is '-' or '_');
  }
}
