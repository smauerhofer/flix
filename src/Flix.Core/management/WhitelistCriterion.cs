namespace Flix.Core;

public sealed class WhitelistCriterion
{
  public string Id { get; set; } = Guid.NewGuid().ToString("N");
  public bool Active { get; set; } = true;
  public string Type { get; set; } = "NAME";
  public string Value { get; set; } = "";
}
