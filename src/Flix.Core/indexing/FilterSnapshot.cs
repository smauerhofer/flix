namespace Flix.Core;

// All three independently indexed lists are acquired and retired as one immutable version.
public sealed class FilterSnapshot : IDisposable
{
  public string Version { get; }
  public NameIndexSnapshot Normal { get; }
  public NameIndexSnapshot Blacklist { get; }
  public NameIndexSnapshot Whitelist { get; }
  public WhitelistGroupIndex WhitelistGroups { get; }
  public IReadOnlyDictionary<string, TenantListSnapshot> Tenants { get; }
  private int _disposed;

  public FilterSnapshot(string version, string snapshotDirectory)
  {
    Version = version;
    NameIndexSnapshot? normal = null, blacklist = null, whitelist = null;
    var tenantSnapshots = new Dictionary<string, TenantListSnapshot>(StringComparer.Ordinal);
    try
    {
      normal = new NameIndexSnapshot(version, Path.Combine(snapshotDirectory, "normal"));
      blacklist = new NameIndexSnapshot(version, Path.Combine(snapshotDirectory, "blacklist"));
      whitelist = new NameIndexSnapshot(version, Path.Combine(snapshotDirectory, "whitelist"));
      Normal = normal;
      Blacklist = blacklist;
      Whitelist = whitelist;
      WhitelistGroups = new WhitelistGroupIndex(whitelist);
      string tenantRoot = Path.Combine(snapshotDirectory, "tenants");
      if (Directory.Exists(tenantRoot))
      {
        foreach (string tenantDirectory in Directory.GetDirectories(tenantRoot))
        {
          string tenantId = Path.GetFileName(tenantDirectory);
          if (!TenantIdValidator.IsValid(tenantId))
          {
            throw new InvalidDataException("Invalid tenant directory name.");
          }
          tenantSnapshots.Add(tenantId,
            new TenantListSnapshot(version, tenantId, tenantDirectory));
        }
      }
      Tenants = tenantSnapshots;
      if (normal.DocumentCount == 0)
      {
        throw new InvalidDataException("Normal reference index cannot be empty.");
      }
    }
    catch
    {
      foreach (var tenant in tenantSnapshots.Values)
      {
        tenant.Dispose();
      }
      whitelist?.Dispose(); blacklist?.Dispose(); normal?.Dispose();
      throw;
    }
  }

  public TenantListSnapshot? GetTenant(string tenantId)
  {
    if (!TenantIdValidator.IsValid(tenantId))
    {
      throw new ArgumentException("Invalid tenant identifier.");
    }
    if (Tenants.Count == 0 && tenantId == "default")
    {
      return null; // Legacy snapshot compatibility only.
    }
    if (!Tenants.TryGetValue(tenantId, out var tenant))
    {
      throw new ArgumentException("Tenant is not provisioned in the active snapshot.");
    }
    return tenant;
  }

  public void Dispose()
  {
    if (Interlocked.Exchange(ref _disposed, 1) != 0)
    {
      return;
    }
    foreach (var tenant in Tenants.Values)
    {
      tenant.Dispose();
    }
    Whitelist.Dispose();
    Blacklist.Dispose();
    Normal.Dispose();
  }
}
