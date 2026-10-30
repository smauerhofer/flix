using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Flix.Core;

// This evaluator requires independently labeled negatives; modified sanctions entries
// are never assumed to be negatives.
public static class NegativeCaseEvaluator
{
  private static readonly JsonSerializerOptions Options = new()
  {
    PropertyNameCaseInsensitive = true,
    WriteIndented = true
  };

  public static NegativeEvaluationReport Evaluate(string root, string inputPath,
    FilterParameters? parameters = null)
  {
    var manifest = SnapshotFiles.Read(root);
    var cases = new List<NegativeEvaluationCase>();
    int lineNumber = 0;
    foreach (string line in File.ReadLines(inputPath))
    {
      lineNumber++;
      if (string.IsNullOrWhiteSpace(line))
      {
        continue;
      }
      NegativeEvaluationCase? item;
      try
      {
        item = JsonSerializer.Deserialize<NegativeEvaluationCase>(line, Options);
      }
      catch (JsonException ex)
      {
        throw new InvalidDataException($"Invalid JSONL at line {lineNumber}.", ex);
      }
      if (item is null || item.Label != "NONMATCH" ||
        string.IsNullOrWhiteSpace(item.LabelSource) ||
        string.IsNullOrWhiteSpace(item.CaseId) ||
        string.IsNullOrWhiteSpace(item.MessageId) ||
        item.SnapshotVersion != manifest.Version || item.Field is null ||
        string.IsNullOrWhiteSpace(item.Field.Value))
      {
        throw new InvalidDataException($"Invalid negative case at line {lineNumber}: " +
          "requires an independently verified NONMATCH and labelSource.");
      }
      cases.Add(item);
    }
    if (cases.Count == 0)
    {
      throw new InvalidDataException("No labeled negative cases.");
    }
    var rows = new List<NegativeEvaluationResult>();
    using (var filter = new ScreeningFilter(new FilterSnapshot(manifest.Version,
      SnapshotFiles.Resolve(root, manifest)), parameters ?? new FilterParameters()))
    {
      var fields = new LuceneFieldMatcher();
      var names = new LuceneNameMatcher();
      var registry = new ScreeningStrategyRegistry([
        new IbanScreeningStrategy(fields), new AccountNumberScreeningStrategy(fields),
        new BicScreeningStrategy(fields), new CountryScreeningStrategy(fields),
        new AddressScreeningStrategy(fields), new PersonNameScreeningStrategy(names),
        new OrganizationNameScreeningStrategy(names), new LeiScreeningStrategy(fields)
      ]);
      var decisions = new DecisionService(new FilterManager(filter), registry);
      foreach (var item in cases)
      {
        filter.Snapshot.GetTenant(item.TenantId);
        var watch = Stopwatch.StartNew();
        try
        {
          var decision = decisions.Screen(new ScreeningRequest(item.MessageId,
            [item.Field], item.TenantId));
          watch.Stop();
          rows.Add(new NegativeEvaluationResult(item.CaseId, item.TenantId,
            FieldNormalization.CanonicalType(item.Field.Type), decision.Decision,
            decision.Decision == "REVIEW", watch.Elapsed.TotalMilliseconds, null));
        }
        catch (Exception ex) when (ex is ArgumentException or ScreeningLimitException)
        {
          watch.Stop();
          rows.Add(new NegativeEvaluationResult(item.CaseId, item.TenantId,
            FieldNormalization.CanonicalType(item.Field.Type), "ERROR", false,
            watch.Elapsed.TotalMilliseconds, ex.Message));
        }
      }
    }
    var times = rows.Select(x => x.ElapsedMilliseconds).OrderBy(x => x).ToArray();
    double position = (times.Length - 1) * 0.95;
    int low = (int)Math.Floor(position);
    int high = (int)Math.Ceiling(position);
    int fp = rows.Count(x => x.FalsePositive);
    int errors = rows.Count(x => x.Error is not null);
    return new NegativeEvaluationReport(manifest.Version, rows.Count, fp, errors,
      rows.Count == errors ? 0 : (double)fp / (rows.Count - errors),
      times.Average(), times[low] + (times[high] - times[low]) * (position - low), rows);
  }

  public static void Save(NegativeEvaluationReport report, string prefix)
  {
    string output = Path.GetFullPath(prefix);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output + ".json", JsonSerializer.Serialize(report, Options),
      new UTF8Encoding(false));
    using var writer = new StreamWriter(output + ".csv", false, new UTF8Encoding(false));
    writer.WriteLine("caseId,tenantId,strategy,decision,falsePositive,elapsedMs,error");
    foreach (var row in report.Results)
    {
      static string Csv(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
      writer.WriteLine(string.Join(",", Csv(row.CaseId), Csv(row.TenantId),
        Csv(row.Strategy), Csv(row.Decision), row.FalsePositive,
        row.ElapsedMilliseconds.ToString("F4", CultureInfo.InvariantCulture), Csv(row.Error)));
    }
  }
}
