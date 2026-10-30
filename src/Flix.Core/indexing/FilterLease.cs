namespace Flix.Core;

public sealed class FilterLease : IDisposable
{
  private FilterManager? _owner;
  private readonly FilterEntry _entry;

  internal FilterLease(FilterManager owner, FilterEntry entry)
  {
    _owner = owner;
    _entry = entry;
  }

  public FilterSnapshot Snapshot => _entry.Snapshot;
  public ScreeningFilter Filter => _entry.Filter;

  public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release(_entry);
}
