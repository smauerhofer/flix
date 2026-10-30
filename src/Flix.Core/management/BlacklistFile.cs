using System.Text.Json;

namespace Flix.Core;

public static class BlacklistFile
{
  private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

  public static List<BlacklistEntry> Read(string path)
  {
    return JsonSerializer.Deserialize<List<BlacklistEntry>>(File.ReadAllText(path), Options)
      ?? throw new InvalidDataException("Invalid blacklist JSON.");
  }

  public static void Write(string path, IReadOnlyList<BlacklistEntry> entries)
  {
    ReferenceListValidation.ValidateBlacklist(entries);
    ReferenceListStorage.Save(path, JsonSerializer.Serialize(entries, Options));
  }
}
