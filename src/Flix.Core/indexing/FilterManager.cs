namespace Flix.Core;

// Readers may only Acquire/Release. The management thread alone calls CleanupRetired.
// A single gate atomically couples active-snapshot selection with usage increment.
public sealed class FilterManager
{
  private readonly object _gate = new();
  private FilterEntry _active;
  private readonly List<FilterEntry> _retired = [];

  public FilterManager(FilterSnapshot initial)
    : this(new ScreeningFilter(initial, new FilterParameters()))
  {
  }

  public FilterManager(ScreeningFilter initial)
  {
    _active = new FilterEntry(initial);
  }

  public FilterLease Acquire()
  {
    lock (_gate)
    {
      _active.Users++;
      return new FilterLease(this, _active);
    }
  }

  internal void Release(FilterEntry entry)
  {
    lock (_gate)
    {
      if (entry.Users <= 0)
      {
        throw new InvalidOperationException("Unbalanced release.");
      }
      entry.Users--;
    }
  }

  // Invoke only with a fully built, independently validated snapshot.
  public void Activate(FilterSnapshot next)
  {
    Activate(new ScreeningFilter(next, new FilterParameters()));
  }

  public void Activate(ScreeningFilter next)
  {
    lock (_gate)
    {
      if (ReferenceEquals(next.Snapshot, _active.Snapshot) ||
        _retired.Any(x => ReferenceEquals(x.Snapshot, next.Snapshot)))
      {
        throw new InvalidOperationException("Snapshot already managed.");
      }
      _retired.Add(_active);
      _active = new FilterEntry(next);
    }
  }

  // Sole disposal path. Entries are claimed under lock; disposal is outside lock.
  // Only one cleanup thread should invoke this method at a time.
  public int CleanupRetired()
  {
    var claimed = new List<FilterEntry>();
    lock (_gate)
    {
      for (int i = _retired.Count - 1; i >= 0; i--)
      {
        if (_retired[i].Users != 0)
        {
          continue;
        }
        claimed.Add(_retired[i]);
        _retired.RemoveAt(i);
      }
    }
    foreach (var entry in claimed)
    {
      entry.Filter.Dispose();
    }
    return claimed.Count;
  }

  public (string ActiveVersion, int ActiveUsers, int RetiredCount) GetStatus()
  {
    lock (_gate) return (_active.Snapshot.Version, _active.Users, _retired.Count);
  }

}
