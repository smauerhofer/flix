using System.Text.Json;

namespace Flix.Core;

public static class SnapshotFiles
{
  public static SnapshotManifest Read(string root)
  {
    var json = File.ReadAllText(Path.Combine(root, "active.json"));
    var manifest = JsonSerializer.Deserialize<SnapshotManifest>(json)
      ?? throw new InvalidDataException("Invalid active manifest.");
    Validate(root, manifest);
    return manifest;
  }

  public static string Resolve(string root, SnapshotManifest manifest)
  {
    Validate(root, manifest);
    return Path.Combine(Path.GetFullPath(root), "snapshots", manifest.Directory);
  }

  public static void Publish(string root, SnapshotManifest manifest)
  {
    Validate(root, manifest);
    var target = Path.Combine(root, "active.json");
    var temporary = target + ".tmp";
    File.WriteAllText(temporary, JsonSerializer.Serialize(manifest));
    File.Move(temporary, target, overwrite: true);
  }

  private static void Validate(string root, SnapshotManifest manifest)
  {
    if (string.IsNullOrWhiteSpace(manifest.Version) ||
      string.IsNullOrWhiteSpace(manifest.Directory) ||
      manifest.Directory is "." or ".." ||
      manifest.Version is "." or ".." ||
      manifest.Version.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
      manifest.Version.Contains('/') || manifest.Version.Contains('\\') ||
      manifest.Directory.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
      manifest.Directory.Contains('/') || manifest.Directory.Contains('\\'))
    {
      throw new InvalidDataException("Unsafe snapshot manifest.");
    }
    var indexPath = Path.Combine(root, "snapshots", manifest.Directory);
    if (!System.IO.Directory.Exists(indexPath))
    {
      throw new DirectoryNotFoundException(indexPath);
    }
  }
}
