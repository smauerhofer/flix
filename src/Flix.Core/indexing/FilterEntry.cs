namespace Flix.Core;

internal sealed class FilterEntry(ScreeningFilter filter)
{
  public ScreeningFilter Filter { get; } = filter;
  public FilterSnapshot Snapshot => Filter.Snapshot;
  public int Users;
}
