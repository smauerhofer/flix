namespace Flix.Core;

public sealed class ReviewComment
{
  public DateTimeOffset AtUtc { get; set; } = DateTimeOffset.UtcNow;
  public string Actor { get; set; } = "";
  public string Text { get; set; } = "";
}
