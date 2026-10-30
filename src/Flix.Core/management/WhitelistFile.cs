using System.Text.Json;

namespace Flix.Core;

public static class WhitelistFile
{
  private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

  public static List<WhitelistGroupEntry> Read(string path)
  {
    return JsonSerializer.Deserialize<List<WhitelistGroupEntry>>(File.ReadAllText(path), Options)
      ?? throw new InvalidDataException("Invalid whitelist JSON.");
  }

  public static void Write(string path, IReadOnlyList<WhitelistGroupEntry> groups)
  {
    ReferenceListValidation.ValidateWhitelist(groups);
    ReferenceListStorage.Save(path, JsonSerializer.Serialize(groups, Options));
  }
}
