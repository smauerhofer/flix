namespace Flix.Core;

public sealed class ReviewFilePaths
{
  public required string Release { get; init; }
  public required string Reject { get; init; }
  public required string Investigate { get; init; }
  public required string Whitelist { get; init; }
  public required string Processed { get; init; }
}
