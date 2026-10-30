using System.Text.Json;

namespace Flix.Core;

public static class HitCaseFile
{
  private static readonly JsonSerializerOptions Options = new()
  {
    PropertyNameCaseInsensitive = true,
    WriteIndented = false
  };

  public static IReadOnlyList<HitCase> Read(string path)
  {
    if (!File.Exists(path))
    {
      return [];
    }

    var cases = new List<HitCase>();
    var ids = new HashSet<string>(StringComparer.Ordinal);
    foreach (string line in File.ReadLines(path))
    {
      if (string.IsNullOrWhiteSpace(line))
      {
        continue;
      }
      using JsonDocument document = JsonDocument.Parse(line);
      HitCase hit;
      if (document.RootElement.TryGetProperty("Hit", out JsonElement nested))
      {
        hit = nested.Deserialize<HitCase>(Options)
          ?? throw new InvalidDataException("Null investigated case.");
      }
      else
      {
        hit = JsonSerializer.Deserialize<HitCase>(line, Options)
          ?? throw new InvalidDataException("Null hit entry.");
      }
      if (string.IsNullOrWhiteSpace(hit.CaseId) || hit.Message is null ||
          hit.Decision is null || !ids.Add(hit.CaseId) ||
          hit.Message.MessageId != hit.Decision.MessageId)
      {
        throw new InvalidDataException("Invalid, duplicate, or inconsistent hit entry.");
      }
      cases.Add(hit);
    }
    return cases;
  }

  public static void Append(string path, HitCase hit)
  {
    if (hit.Message is null || hit.Decision is null ||
        hit.Message.MessageId != hit.Decision.MessageId ||
        string.IsNullOrWhiteSpace(hit.CaseId))
    {
      throw new InvalidDataException("Invalid hit.");
    }
    JsonLineJournal.Append(path, hit);
  }
}
