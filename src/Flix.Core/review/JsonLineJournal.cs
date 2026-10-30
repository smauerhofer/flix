using System.Text;
using System.Text.Json;

namespace Flix.Core;

public static class JsonLineJournal
{
  public static void Append<T>(string path, T record)
  {
    string fullPath = Path.GetFullPath(path);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    string json = JsonSerializer.Serialize(record);
    byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");
    using var stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write,
      FileShare.Read, 4096, FileOptions.WriteThrough);
    stream.Write(bytes);
    stream.Flush(true);
  }
}
