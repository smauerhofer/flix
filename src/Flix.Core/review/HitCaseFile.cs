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
    foreach (string line in ReadCompleteLines(path))
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

  // Single, occasional writes only (tools, tests). The API uses HitCaseJournal,
  // which serializes concurrent writers on one open stream.
  public static void Append(string path, HitCase hit)
  {
    Validate(hit);
    JsonLineJournal.Append(path, hit);
  }

  public static void Validate(HitCase hit)
  {
    if (hit.Message is null || hit.Decision is null ||
        hit.Message.MessageId != hit.Decision.MessageId ||
        string.IsNullOrWhiteSpace(hit.CaseId))
    {
      throw new InvalidDataException("Invalid hit.");
    }
  }

  // Opens with FileShare.ReadWrite so the file can be read while the API keeps its
  // HitCaseJournal stream open. A last line without '\n' is still being written
  // and is skipped; it is read on the next refresh.
  private static IEnumerable<string> ReadCompleteLines(string path)
  {
    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
      FileShare.ReadWrite | FileShare.Delete);
    using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
    string content = reader.ReadToEnd();
    int end = content.LastIndexOf('\n');
    if (end < 0)
    {
      yield break;
    }
    foreach (string line in content[..end].Split('\n'))
    {
      yield return line.TrimEnd('\r');
    }
  }
}
