using System.Globalization;
using System.Text.Json;
using Flix.Core;
using Microsoft.VisualBasic.FileIO;

namespace Flix.EvaluationInspector;

public static class EvaluationReportLoader
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true
  };

  public static List<PositiveEvaluationResult> LoadReport(string path)
  {
    if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
    {
      var report = JsonSerializer.Deserialize<PositiveEvaluationReport>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException("Empty evaluation report.");
      return report.Results.ToList();
    }

    using var parser = new TextFieldParser(path);
    parser.TextFieldType = FieldType.Delimited;
    parser.SetDelimiters(",");
    parser.HasFieldsEnclosedInQuotes = true;
    string[] header = parser.ReadFields() ?? throw new InvalidDataException("CSV header is missing.");
    var columns = header.Select((name, index) => (name, index))
      .ToDictionary(x => x.name.Trim().TrimStart('\uFEFF'), x => x.index, StringComparer.OrdinalIgnoreCase);
    foreach (string name in new[] { "caseId", "decision", "sourceId", "entityId" })
    {
      if (!columns.ContainsKey(name))
      {
        throw new InvalidDataException($"Required evaluation CSV column '{name}' not found.");
      }
    }

    var results = new List<PositiveEvaluationResult>();
    while (!parser.EndOfData)
    {
      string[] fields = parser.ReadFields() ?? [];
      string Get(string name)
      {
        return columns.TryGetValue(name, out int index) && index < fields.Length ? fields[index] : string.Empty;
      }
      bool Flag(string name) => bool.TryParse(Get(name), out bool value) && value;
      bool? OptionalFlag(string name) => bool.TryParse(Get(name), out bool value) ? value : null;
      double Number(string name) => double.TryParse(Get(name), NumberStyles.Float,
        CultureInfo.InvariantCulture, out double value) ? value : 0;
      double? OptionalNumber(string name) => double.TryParse(Get(name), NumberStyles.Float,
        CultureInfo.InvariantCulture, out double value) ? value : null;
      results.Add(new PositiveEvaluationResult(
        Get("caseId"), Get("tenantId"), Get("list"), Get("strategy"), Get("generationType"),
        Get("sourceId"), Get("entityId"), Flag("expectedMatchIsGuaranteed"),
        Flag("expectedEntityFound"), Flag("reviewTriggered"), Flag("falseNegative"),
        Get("decision"), Number("elapsedMs"), Get("snapshotVersion"),
        Get("error"), OptionalFlag("countryValueDetected"), OptionalFlag("countryEntityPresentInIndex"),
        OptionalFlag("candidateRetrieved"), OptionalNumber("bestTraditionalScore"),
        OptionalNumber("expectedEntityScore"), OptionalNumber("reviewThreshold"),
        OptionalFlag("whitelistOverrideApplied"), Get("diagnostic")));
    }
    return results;
  }

  public static Dictionary<string, PositiveEvaluationCase> LoadCases(string path)
  {
    var cases = new Dictionary<string, PositiveEvaluationCase>(StringComparer.Ordinal);
    int lineNumber = 0;
    foreach (string line in File.ReadLines(path))
    {
      lineNumber++;
      if (string.IsNullOrWhiteSpace(line))
      {
        continue;
      }
      try
      {
        var item = JsonSerializer.Deserialize<PositiveEvaluationCase>(line, JsonOptions)
          ?? throw new InvalidDataException($"Null case on line {lineNumber}.");
        if (!cases.TryAdd(item.CaseId, item))
        {
          throw new InvalidDataException($"Duplicate case ID '{item.CaseId}' on line {lineNumber}.");
        }
      }
      catch (JsonException e)
      {
        throw new InvalidDataException($"Invalid JSONL case at line {lineNumber}.", e);
      }
    }
    return cases;
  }
}
