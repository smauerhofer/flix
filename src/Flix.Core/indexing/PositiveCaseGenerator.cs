using System.Text;
using System.Text.Json;
using Lucene.Net.Index;
using Lucene.Net.Util;

namespace Flix.Core;

// Reads published Lucene documents. Never modifies a snapshot or screening decision.
public static class PositiveCaseGenerator
{
  public static int Generate(string root, string outputPath, int count, int seed,
    string? version = null, string tenantId = "default", string list = "normal",
    string variation = "exact", string mutation = "mixed")
  {
    if (count <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(count));
    }
    if (variation is not ("exact" or "fuzzy" or "mixed"))
    {
      throw new ArgumentException("Variation must be exact, fuzzy, or mixed.");
    }
    if (mutation is not ("mixed" or "deletion" or "transpose" or "diacritic" or "word-order"))
    {
      throw new ArgumentException("Mutation must be mixed, deletion, transpose, diacritic, or word-order.");
    }
    if (list is not ("normal" or "blacklist"))
    {
      throw new ArgumentException("Only normal and blacklist indexes support positive hit generation.");
    }
    if (!TenantIdValidator.IsValid(tenantId))
    {
      throw new ArgumentException("Invalid tenant ID.");
    }

    string snapshotDirectory;
    string snapshotVersion;
    if (version is null)
    {
      var manifest = SnapshotFiles.Read(root);
      snapshotDirectory = SnapshotFiles.Resolve(root, manifest);
      snapshotVersion = manifest.Version;
    }
    else
    {
      if (version.Length == 0 || version is "." or ".." ||
        version.Contains('/') || version.Contains('\\') ||
        version.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
      {
        throw new ArgumentException("Invalid snapshot version.");
      }
      snapshotDirectory = Path.Combine(Path.GetFullPath(root), "snapshots", version);
      if (!Directory.Exists(snapshotDirectory))
      {
        throw new DirectoryNotFoundException(snapshotDirectory);
      }
      snapshotVersion = version;
    }

    // Tenant authorization is independent of the selected (possibly global) index.
    // Legacy snapshots have no tenants directory and provision only "default".
    string tenantsDirectory = Path.Combine(snapshotDirectory, "tenants");
    if (!Directory.Exists(tenantsDirectory))
    {
      if (tenantId != "default")
      {
        throw new ArgumentException("Tenant is not provisioned in this snapshot.", nameof(tenantId));
      }
    }
    else
    {
      if (tenantId == "default" ||
        !Directory.Exists(Path.Combine(tenantsDirectory, tenantId)))
      {
        string[] provisioned = Directory.GetDirectories(tenantsDirectory)
          .Select(Path.GetFileName)
          .Where(x => !string.IsNullOrWhiteSpace(x))
          .OrderBy(x => x, StringComparer.Ordinal)
          .Select(x => x!)
          .ToArray();
        string available = provisioned.Length == 0
          ? "none"
          : string.Join(", ", provisioned);
        throw new ArgumentException(
          $"Tenant '{tenantId}' is not provisioned in snapshot '{snapshotVersion}'. " +
          $"Available tenants: {available}. Specify --tenant <tenant-id> " +
          "when generating cases for a multi-tenant snapshot.", nameof(tenantId));
      }
    }

    string indexDirectory = list == "normal"
      ? Path.Combine(snapshotDirectory, "normal")
      : tenantId == "default"
        ? Path.Combine(snapshotDirectory, "blacklist")
        : Path.Combine(tenantsDirectory, tenantId, "blacklist");
    if (!Directory.Exists(indexDirectory))
    {
      throw new DirectoryNotFoundException(indexDirectory);
    }

    var candidates = new List<ReferenceFieldRecord>();
    using (var snapshot = new NameIndexSnapshot(snapshotVersion, indexDirectory))
    {
      IndexReader reader = snapshot.Searcher.IndexReader;
      var liveDocs = MultiFields.GetLiveDocs(reader);
      for (int documentId = 0; documentId < reader.MaxDoc; documentId++)
      {
        if (liveDocs is not null && !liveDocs.Get(documentId))
        {
          continue;
        }
        var document = reader.Document(documentId);
        string? source = document.Get("sourceId");
        string? entity = document.Get("entityId");
        string? type = document.Get("fieldType") ?? document.Get("nameType");
        string? value = document.Get("originalValue") ?? document.Get("originalName");
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(entity) ||
          string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(value))
        {
          throw new InvalidDataException($"Incomplete reference document {documentId}.");
        }
        type = FieldNormalization.CanonicalType(type);
        if (variation == "fuzzy" && !CanVary(type, value))
        {
          continue;
        }
        candidates.Add(new ReferenceFieldRecord(entity, source, type, value));
      }
    }
    if (candidates.Count < count)
    {
      throw new InvalidOperationException($"Requested {count} cases but only {candidates.Count} eligible reference fields exist.");
    }
    // Stable ordering before seeded sampling, independent of Lucene insertion/segment order.
    candidates.Sort((a, b) => StringComparer.Ordinal.Compare(
      Key(a), Key(b)));
    var random = new Random(seed);
    for (int index = 0; index < count; index++)
    {
      int selected = random.Next(index, candidates.Count);
      (candidates[index], candidates[selected]) = (candidates[selected], candidates[index]);
    }

    string fullOutputPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
    string temporary = fullOutputPath + ".tmp";
    try
    {
      using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(false)))
      {
        for (int i = 0; i < count; i++)
        {
          var record = candidates[i];
          bool fuzzy = variation == "fuzzy" ||
            (variation == "mixed" && i % 2 == 1 && CanVary(record.Type, record.Value));
          string method = fuzzy ? mutation == "mixed"
            ? new[] { "deletion", "transpose", "diacritic", "word-order" }[i % 4]
            : mutation : "none";
          string value = fuzzy ? Vary(record.Value, method) : record.Value;
          if (fuzzy && value == record.Value)
          {
            method = "deletion";
            value = Vary(record.Value, method);
          }
          var output = new
          {
            caseId = $"POS-{i + 1:D6}",
            tenantId,
            messageId = $"TEST-{i + 1:D6}",
            snapshotVersion,
            list,
            seed,
            field = new { partyId = "sender", type = record.Type, value },
            expected = new { sourceId = record.SourceId, entityId = record.EntityId, match = true },
            generationType = fuzzy ? "SYNTHETIC_FUZZY" : "EXACT",
            mutationType = method,
            expectedMatchIsGuaranteed = !fuzzy
          };
          writer.WriteLine(JsonSerializer.Serialize(output));
        }
      }
      File.Move(temporary, fullOutputPath, true);
    }
    finally
    {
      if (File.Exists(temporary))
      {
        File.Delete(temporary);
      }
    }
    return count;
  }

  private static string Key(ReferenceFieldRecord record) =>
    string.Join("\u001f", record.SourceId, record.EntityId, record.Type, record.Value);

  private static bool CanVary(string type, string value) =>
    (type is "NAME" or "PERSON_NAME" or "ORGANIZATION_NAME" or "ADDRESS") &&
    value.Count(char.IsLetter) >= 4;

  private static string Vary(string value, string method)
  {
    int[] letters = Enumerable.Range(0, value.Length)
      .Where(i => char.IsLetter(value[i])).ToArray();
    if (letters.Length < 4)
    {
      return value;
    }
    if (method == "word-order")
    {
      string[] words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (words.Length > 1)
      {
        (words[0], words[1]) = (words[1], words[0]);
        return string.Join(" ", words);
      }
    }
    if (method == "diacritic")
    {
      string normalized = value.Normalize(System.Text.NormalizationForm.FormD);
      string withoutMarks = new string(normalized.Where(x =>
        System.Globalization.CharUnicodeInfo.GetUnicodeCategory(x) !=
        System.Globalization.UnicodeCategory.NonSpacingMark).ToArray())
        .Normalize(System.Text.NormalizationForm.FormC);
      if (withoutMarks != value)
      {
        return withoutMarks;
      }
    }
    if (method == "transpose")
    {
      int first = letters[letters.Length / 2 - 1];
      int second = letters[letters.Length / 2];
      if (second == first + 1 && value[first] != value[second])
      {
        char[] characters = value.ToCharArray();
        (characters[first], characters[second]) = (characters[second], characters[first]);
        return new string(characters);
      }
    }
    return value.Remove(letters[letters.Length / 2], 1);
  }
}
