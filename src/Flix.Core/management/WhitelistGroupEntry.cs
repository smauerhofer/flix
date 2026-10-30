namespace Flix.Core;

public sealed class WhitelistGroupEntry
{
  public string Id { get; set; } = Guid.NewGuid().ToString("N");
  public bool Active { get; set; } = true;
  public string EntityId { get; set; } = "";
  public string SourceId { get; set; } = "INTERNAL";
  public string Description { get; set; } = "";
  public List<WhitelistCriterion> Criteria { get; set; } = [];
}
