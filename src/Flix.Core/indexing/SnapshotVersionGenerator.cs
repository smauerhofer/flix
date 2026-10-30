using System.Globalization;
using System.Text.RegularExpressions;

namespace Flix.Core;

/// <summary>
/// Reserves the next immutable snapshot directory for the supplied local calendar day.
/// The first snapshot is yyyyMMdd, then yyyyMMdd_001 through yyyyMMdd_999.
/// </summary>
public static class SnapshotVersionGenerator
{
  public static SnapshotReservation Reserve(string root)
  {
    return Reserve(root, DateOnly.FromDateTime(DateTime.Now));
  }

  public static SnapshotReservation Reserve(string root, DateOnly date)
  {
    string snapshotRoot = Path.Combine(Path.GetFullPath(root), "snapshots");
    Directory.CreateDirectory(snapshotRoot);

    // Only one importer can allocate a version inside this root at a time.
    // The lock protects both scanning and creation of the reserved directory.
    string lockPath = Path.Combine(snapshotRoot, ".version.lock");
    using var allocationLock = new FileStream(
      lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    string day = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    var pattern = new Regex("^" + day + "(?:_([0-9]{3}))?$", RegexOptions.CultureInvariant);
    int highest = -1;

    foreach (string directory in Directory.EnumerateDirectories(snapshotRoot))
    {
      Match match = pattern.Match(Path.GetFileName(directory));
      if (!match.Success)
      {
        continue;
      }

      int sequence = match.Groups[1].Success
        ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
        : 0;
      highest = Math.Max(highest, sequence);
    }

    if (highest >= 999)
    {
      throw new InvalidOperationException(
        $"All snapshot sequences have been used for {day}.");
    }

    string version = highest < 0
      ? day
      : $"{day}_{highest + 1:000}";

    string path = Path.Combine(snapshotRoot, version);
    Directory.CreateDirectory(path);
    return new SnapshotReservation(version, path);
  }
}
