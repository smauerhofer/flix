using Flix.Core;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Flix.Cli;

public static class SecoConverter
{
  public static int Convert(string inputPath, string outputPath)
  {
    using FileStream file = File.OpenRead(inputPath);
    using ZipArchive? archive = Path.GetExtension(inputPath).Equals(".zip", StringComparison.OrdinalIgnoreCase)
      ? new ZipArchive(file, ZipArchiveMode.Read, true)
      : null;
    ZipArchiveEntry? entry = null;
    if (archive is not null)
    {
      var xmlEntries = archive.Entries.Where(x => x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToList();
      if (xmlEntries.Count != 1)
      {
        throw new InvalidDataException("SECO ZIP must contain exactly one XML document.");
      }
      entry = xmlEntries[0];
    }
    using Stream source = entry is null ? file : entry.Open();
    var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true };
    using XmlReader reader = XmlReader.Create(source, settings);
    reader.MoveToContent();
    if (reader.LocalName != "swiss-sanctions-list" || reader.GetAttribute("list-type") != "whole-list")
    {
      throw new InvalidDataException("Only SECO whole-list documents can replace the normal index.");
    }
    string listDate = reader.GetAttribute("date") ?? "unknown";
    string outputFull = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(outputFull)!);
    string temp = outputFull + ".tmp";
    int targetCount = 0;
    int rowCount = 0;
    int incompleteVariantSets = 0;
    try
    {
      using (var writer = new StreamWriter(temp, false, new UTF8Encoding(false)))
      {
        writer.WriteLine("sourceId,entityId,type,value");
        while (reader.Read())
        {
          if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "target")
          {
            continue;
          }
          using XmlReader subtree = reader.ReadSubtree();
          XElement target = XElement.Load(subtree);
          string ssid = (string?)target.Attribute("ssid") ?? throw new InvalidDataException("Target without SSID.");
          targetCount++;
          var modifications = target.Elements("modification").Select(x => (string?)x.Attribute("modification-type")).ToArray();
          // The entire target is omitted only when the last recorded state is de-listed.
          if (modifications.Length > 0 && modifications[^1] == "de-listed")
          {
            continue;
          }
          string sourceId = "SECO";
          var rows = new HashSet<(string Type, string Value)>();
          foreach (XElement party in target.Elements().Where(x => x.Name.LocalName is "individual" or "entity" or "object"))
          {
            string nameType = party.Name.LocalName == "individual" ? "PERSON_NAME" : "ORGANIZATION_NAME";
            foreach (XElement identity in party.Elements("identity"))
            {
              foreach (XElement name in identity.Elements("name"))
              {
                // Full names are composed per script/language variant set; variants of
                // different scripts are never combined into one name.
                var parts = name.Elements("name-part").Select(part => new SecoNamePart(
                  (int?)part.Attribute("order") ?? 0,
                  (string?)part.Element("value"),
                  part.Elements("spelling-variant").Select(variant => new SecoSpellingVariant(
                    (string?)variant ?? "",
                    (string?)variant.Attribute("lang") ??
                    (string?)variant.Attribute("language") ??
                    (string?)variant.Attribute(XNamespace.Xml + "lang"))).ToArray())).ToArray();
                SecoNameComposition composition = SecoNameComposer.Compose(parts);
                incompleteVariantSets += composition.IncompleteVariantSets;
                foreach (string fullName in composition.Names)
                {
                  Add(rows, nameType, fullName);
                }
              }
              foreach (XElement address in identity.Descendants("address"))
              {
                string value = string.Join(", ", address.Descendants().Where(x => !x.HasElements)
                  .Select(x => (string?)x).Where(x => !string.IsNullOrWhiteSpace(x)));
                Add(rows, "ADDRESS", value);
              }
              // Country codes attached to a SECO target describe that target (address,
              // nationality), not an embargo. They are deliberately NOT emitted:
              // country blocking is maintained explicitly as COUNTRY entries in the
              // global or tenant blacklist. The country remains part of ADDRESS text.
            }
          }
          foreach (var row in rows.OrderBy(x => x.Type).ThenBy(x => x.Value, StringComparer.Ordinal))
          {
            writer.WriteLine($"{Csv(sourceId)},{Csv(ssid)},{Csv(row.Type)},{Csv(row.Value)}");
            rowCount++;
          }
        }
      }
      File.Move(temp, outputFull, true);
    }
    catch
    {
      File.Delete(temp);
      throw;
    }
    Console.WriteLine($"SECO {listDate}: {targetCount} targets, {rowCount} screening fields -> {outputFull}");
    Console.WriteLine($"Variant sets skipped because a name part has no spelling in that script: {incompleteVariantSets}");
    return 0;
  }

  private static void Add(HashSet<(string Type, string Value)> rows, string type, string? value)
  {
    value = value?.Replace('\r', ' ').Replace('\n', ' ').Trim();
    if (!string.IsNullOrWhiteSpace(value))
    {
      rows.Add((type, value));
    }
  }

  private static string Csv(string value)
  {
    return value.Contains('"') || value.Contains(',') ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
  }
}
