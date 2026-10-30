namespace Flix.Core;

public sealed class BlacklistEntry
{
  public string Id { get; set; } = Guid.NewGuid().ToString("N");
  public bool Active { get; set; } = true;
  public string EntityId { get; set; } = "";
  public string SourceId { get; set; } = "INTERNAL";
  public string Type { get; set; } = "NAME";
  public string Value { get; set; } = "";
}
