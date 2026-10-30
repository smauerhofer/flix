using System.Text;

namespace Flix.Core;

public static class ReferenceListStorage
{
  public static void Save(string path, string contents)
  {
    string full = Path.GetFullPath(path);
    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
    string temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try
    {
      File.WriteAllText(temporary, contents, new UTF8Encoding(false));
      File.Move(temporary, full, true);
    }
    finally
    {
      if (File.Exists(temporary))
      {
        File.Delete(temporary);
      }
    }
  }
}
