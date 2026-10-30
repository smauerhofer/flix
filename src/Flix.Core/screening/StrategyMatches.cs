namespace Flix.Core;

public sealed record StrategyMatches(
  IReadOnlyList<NameMatch> Names,
  IReadOnlyList<FieldMatch> Fields)
{
  public static StrategyMatches Empty { get; } = new([], []);
}
