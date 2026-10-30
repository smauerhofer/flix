namespace Flix.Core;

public sealed class ReviewDisposition
{
  public string CaseId { get; set; } = "";
  public string MessageId { get; set; } = "";
  public string Action { get; set; } = "";
  public string Actor { get; set; } = "";
  public DateTimeOffset AtUtc { get; set; } = DateTimeOffset.UtcNow;
  public bool WhitelistAdded { get; set; }
  public HitCase? Hit { get; set; }
}
