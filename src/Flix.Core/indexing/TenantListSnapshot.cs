namespace Flix.Core;

// Immutable tenant list pair; owned and disposed by its parent FilterSnapshot.
public sealed class TenantListSnapshot : IDisposable
{
  public string TenantId { get; }
  public NameIndexSnapshot Blacklist { get; }
  public NameIndexSnapshot Whitelist { get; }
  public WhitelistGroupIndex WhitelistGroups { get; }

  public TenantListSnapshot(string version, string tenantId, string directory)
  {
    TenantId = tenantId;
    NameIndexSnapshot? black = null;
    NameIndexSnapshot? white = null;
    try
    {
      black = new NameIndexSnapshot(version, Path.Combine(directory, "blacklist"));
      white = new NameIndexSnapshot(version, Path.Combine(directory, "whitelist"));
      Blacklist = black;
      Whitelist = white;
      WhitelistGroups = new WhitelistGroupIndex(white);
    }
    catch
    {
      white?.Dispose();
      black?.Dispose();
      throw;
    }
  }

  public void Dispose()
  {
    Whitelist.Dispose();
    Blacklist.Dispose();
  }
}
