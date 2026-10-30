namespace Flix.Core;

public static class ReferenceListImporter
{
  public static int ImportBlacklist(string path, NameIndexBuilder writer)
  {
    if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
    {
      return ImportCsv(path, writer);
    }
    List<BlacklistEntry> entries = BlacklistFile.Read(path);
    ReferenceListValidation.ValidateBlacklist(entries);
    int count = 0;
    foreach (BlacklistEntry entry in entries)
    {
      if (entry.Active)
      {
        writer.AddField(new ReferenceFieldRecord(entry.EntityId, entry.SourceId, entry.Type, entry.Value));
        count++;
      }
    }
    return count;
  }

  public static int ImportWhitelist(string path, NameIndexBuilder writer)
  {
    if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
    {
      return ImportCsv(path, writer);
    }
    List<WhitelistGroupEntry> groups = WhitelistFile.Read(path);
    ReferenceListValidation.ValidateWhitelist(groups);
    int count = 0;
    foreach (WhitelistGroupEntry group in groups)
    {
      if (!group.Active)
      {
        continue;
      }
      // A partially disabled group must never be indexed as a smaller approval scope.
      if (group.Criteria.Any(x => !x.Active))
      {
        continue;
      }
      foreach (WhitelistCriterion criterion in group.Criteria)
      {
        writer.AddField(new ReferenceFieldRecord(group.EntityId, group.SourceId, criterion.Type, criterion.Value));
        count++;
      }
    }
    return count;
  }

  private static string[] ParseCsvLine(string line)
  {
    var parts = new List<string>();
    var value = new System.Text.StringBuilder();
    bool quoted = false;
    for (int index = 0; index < line.Length; index++)
    {
      char character = line[index];
      if (character == '"')
      {
        if (quoted && index + 1 < line.Length && line[index + 1] == '"')
        {
          value.Append('"');
          index++;
        }
        else
        {
          quoted = !quoted;
        }
      }
      else if (character == ',' && !quoted)
      {
        parts.Add(value.ToString());
        value.Clear();
      }
      else
      {
        value.Append(character);
      }
    }
    if (quoted)
    {
      throw new InvalidDataException("Unterminated quoted CSV field.");
    }
    parts.Add(value.ToString());
    return parts.ToArray();
  }

  public static int ImportCsv(string path, NameIndexBuilder writer)
  {
    using var lines = File.ReadLines(path).GetEnumerator();
    if (!lines.MoveNext())
    {
      throw new InvalidDataException("CSV file is empty: " + path);
    }
    string[] header = ParseCsvLine(lines.Current.TrimStart('\uFEFF'));
    if (header.Length != 4 ||
        header[0].Trim() != "sourceId" ||
        header[1].Trim() != "entityId" ||
        header[2].Trim() != "type" ||
        header[3].Trim() != "value")
    {
      throw new InvalidDataException("Expected CSV header sourceId,entityId,type,value: " + path);
    }
    int count = 0;
    while (lines.MoveNext())
    {
      string line = lines.Current;
      if (string.IsNullOrWhiteSpace(line))
      {
        continue;
      }
      string[] parts = ParseCsvLine(line);
      if (parts.Length != 4)
      {
        throw new InvalidDataException("Expected sourceId,entityId,type,value: " + line);
      }
      writer.AddField(new ReferenceFieldRecord(parts[1], parts[0], parts[2], parts[3]));
      count++;
    }
    return count;
  }
}
