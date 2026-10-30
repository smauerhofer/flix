namespace Flix.Core;

public sealed class HitCase
{
  public string CaseId { get; set; } = Guid.NewGuid().ToString("N");
  public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
  public ScreeningRequest? Message { get; set; }
  public ScreeningDecision? Decision { get; set; }
  public List<ReviewComment> Comments { get; set; } = [];
}
