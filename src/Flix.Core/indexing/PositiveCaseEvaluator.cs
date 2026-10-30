using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace Flix.Core;

// Evaluates the original generated expected (sourceId, entityId) rather than only the final decision.
// Synthetic fuzzy positives are labeled assumptions; report them separately from exact positives.
public static class PositiveCaseEvaluator
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true,
    WriteIndented = true
  };

  public static PositiveEvaluationReport Evaluate(string root, string inputPath,
    FilterParameters? parameters = null, string? version = null)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(root);
    ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
    string snapshotVersion;
    string snapshotDirectory;
    if (version is null)
    {
      var manifest = SnapshotFiles.Read(root);
      snapshotVersion = manifest.Version;
      snapshotDirectory = SnapshotFiles.Resolve(root, manifest);
    }
    else
    {
      if (version.Length == 0 || version is "." or ".." ||
        version.Contains('/') || version.Contains('\\') ||
        version.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
      {
        throw new ArgumentException("Invalid snapshot version.", nameof(version));
      }
      snapshotVersion = version;
      snapshotDirectory = Path.Combine(Path.GetFullPath(root), "snapshots", version);
    }

    var cases = new List<PositiveEvaluationCase>();
    int lineNumber = 0;
    foreach (string line in File.ReadLines(inputPath))
    {
      lineNumber++;
      if (string.IsNullOrWhiteSpace(line))
      {
        continue;
      }
      PositiveEvaluationCase item;
      try
      {
        item = JsonSerializer.Deserialize<PositiveEvaluationCase>(line, JsonOptions)
          ?? throw new InvalidDataException("Null case.");
      }
      catch (JsonException exception)
      {
        throw new InvalidDataException($"Invalid JSONL line {lineNumber}.", exception);
      }
      if (item.Field is null || item.Expected is null ||
        string.IsNullOrWhiteSpace(item.CaseId) ||
        string.IsNullOrWhiteSpace(item.MessageId) ||
        string.IsNullOrWhiteSpace(item.Expected.EntityId) ||
        string.IsNullOrWhiteSpace(item.Expected.SourceId) ||
        item.SnapshotVersion != snapshotVersion ||
        item.List is not ("normal" or "blacklist") ||
        item.GenerationType is not ("EXACT" or "SYNTHETIC_FUZZY") ||
        !item.Expected.Match)
      {
        throw new InvalidDataException($"Invalid or incompatible positive case at line {lineNumber}.");
      }
      cases.Add(item);
    }
    if (cases.Count == 0)
    {
      throw new InvalidDataException("Positive case input is empty.");
    }

    var effectiveParameters = parameters ?? new FilterParameters();
    effectiveParameters.Validate();
    string configFingerprint = Convert.ToHexString(SHA256.HashData(
      Encoding.UTF8.GetBytes(JsonSerializer.Serialize(effectiveParameters, JsonOptions))));
    var results = new List<PositiveEvaluationResult>(cases.Count);
    // The evaluation owns this offline snapshot and never touches the live filter manager.
    using (var filter = new ScreeningFilter(new FilterSnapshot(snapshotVersion, snapshotDirectory),
      effectiveParameters))
    {
      var manager = new FilterManager(filter);
      var matcher = new LuceneFieldMatcher();
      var nameMatcher = new LuceneNameMatcher();
      var strategies = new ScreeningStrategyRegistry([
        new IbanScreeningStrategy(matcher),
        new AccountNumberScreeningStrategy(matcher),
        new BicScreeningStrategy(matcher),
        new CountryScreeningStrategy(matcher),
        new AddressScreeningStrategy(matcher),
        new PersonNameScreeningStrategy(nameMatcher),
        new OrganizationNameScreeningStrategy(nameMatcher),
        new LeiScreeningStrategy(matcher)
      ]);
      var decisions = new DecisionService(manager, strategies);
      using var countryIndex = new NameIndexSnapshot(snapshotVersion,
        Path.Combine(snapshotDirectory, "normal"));
      foreach (var item in cases)
      {
        // A different tenant cannot read another tenant's index.
        filter.Snapshot.GetTenant(item.TenantId);
        string strategy = FieldNormalization.CanonicalType(item.Field.Type);
        var watch = Stopwatch.StartNew();
        ScreeningDecision decision;
        try
        {
          decision = decisions.Screen(new ScreeningRequest(item.MessageId,
            [item.Field], item.TenantId));
        }
        catch (Exception exception) when (exception is ArgumentException or ScreeningLimitException)
        {
          watch.Stop();
          results.Add(new PositiveEvaluationResult(item.CaseId, item.TenantId, item.List,
            strategy, item.GenerationType, item.Expected.SourceId, item.Expected.EntityId,
            item.ExpectedMatchIsGuaranteed, false, false, true, "ERROR",
            watch.Elapsed.TotalMilliseconds, snapshotVersion, exception.Message));
          continue;
        }
        watch.Stop();
        bool found = item.List == "blacklist"
          ? (decision.BlacklistMatches ?? []).Any(x =>
              (x.List == "GLOBAL_BLACKLIST" || x.List == "TENANT_BLACKLIST") &&
              x.SourceId == item.Expected.SourceId && x.EntityId == item.Expected.EntityId)
          : decision.NameMatches.Any(x =>
              x.SourceId == item.Expected.SourceId && x.EntityId == item.Expected.EntityId) ||
            decision.FieldMatches.Any(x =>
              x.SourceId == item.Expected.SourceId && x.EntityId == item.Expected.EntityId);
        bool review = decision.Decision == "REVIEW";
        bool country = strategy == "COUNTRY" && item.List == "normal";
        bool? countryValueDetected = country ? decision.FieldMatches.Any(x =>
          x.FieldType == "COUNTRY" &&
          FieldNormalization.Normalize("COUNTRY", x.ReferenceValue) ==
          FieldNormalization.Normalize("COUNTRY", item.Field.Value)) : null;
        bool? countryEntityPresent = country ? CountryReferenceExists(countryIndex,
          item.Field.Value, item.Expected.SourceId, item.Expected.EntityId) : null;
        var normalNames = decision.NameMatches;
        var normalFields = decision.FieldMatches;
        double? expectedScore = item.List == "normal"
          ? normalNames.Where(x => x.SourceId == item.Expected.SourceId &&
              x.EntityId == item.Expected.EntityId).Select(x => (double?)x.Similarity)
              .Concat(normalFields.Where(x => x.SourceId == item.Expected.SourceId &&
                x.EntityId == item.Expected.EntityId).Select(x => (double?)x.Score))
              .Max()
          : (decision.BlacklistMatches ?? []).Where(x =>
              x.SourceId == item.Expected.SourceId && x.EntityId == item.Expected.EntityId)
              .Select(x => (double?)x.Score).DefaultIfEmpty().Max();
        double? bestScore = strategy is "PERSON_NAME" or "ORGANIZATION_NAME"
          ? normalNames.Select(x => (double?)x.Similarity).DefaultIfEmpty().Max()
          : normalFields.Where(x => x.FieldType == strategy)
              .Select(x => (double?)x.Score).DefaultIfEmpty().Max();
        double? threshold = strategy is "PERSON_NAME" or "ORGANIZATION_NAME"
          ? effectiveParameters.NameReviewThreshold
          : strategy == "ADDRESS" ? effectiveParameters.AddressReviewThreshold : null;
        string diagnostic = decision.WhitelistOverrideApplied ? "WHITELIST_OVERRIDE"
          : !found ? "EXPECTED_ENTITY_NOT_RETRIEVED"
          : expectedScore.HasValue && threshold.HasValue && expectedScore < threshold
            ? "EXPECTED_SCORE_BELOW_THRESHOLD"
          : review ? "REVIEW" : "DECISION_NOT_REVIEW";
        results.Add(new PositiveEvaluationResult(item.CaseId, item.TenantId, item.List,
          strategy, item.GenerationType, item.Expected.SourceId, item.Expected.EntityId,
          item.ExpectedMatchIsGuaranteed, found, review, !review, decision.Decision,
          watch.Elapsed.TotalMilliseconds, snapshotVersion, null,
          countryValueDetected, countryEntityPresent,
          found, bestScore, expectedScore, threshold,
          decision.WhitelistOverrideApplied, diagnostic));
      }
    }
    return new PositiveEvaluationReport(snapshotVersion, DateTimeOffset.UtcNow,
      Metrics("ALL", results),
      results.GroupBy(x => x.TenantId).OrderBy(x => x.Key)
        .Select(g => Metrics(g.Key, g)).ToArray(),
      results.GroupBy(x => x.Strategy).OrderBy(x => x.Key)
        .Select(g => Metrics(g.Key, g)).ToArray(),
      results.GroupBy(x => x.GenerationType).OrderBy(x => x.Key)
        .Select(g => Metrics(g.Key, g)).ToArray(),
      results.GroupBy(x => x.List).OrderBy(x => x.Key)
        .Select(g => Metrics(g.Key, g)).ToArray(), results,
      configFingerprint, effectiveParameters.AI.ModelVersion);
  }

  // Reference attribution is measured independently of the bounded screening hit list.
  // This does NOT make a country alone a verified sanctions identity match.
  private static bool CountryReferenceExists(NameIndexSnapshot snapshot, string country,
    string sourceId, string entityId)
  {
    string normalized = FieldNormalization.Normalize("COUNTRY", country);
    var query = new BooleanQuery();
    query.Add(new TermQuery(new Term("fieldType", "COUNTRY")), Occur.MUST);
    query.Add(new TermQuery(new Term("normalizedValue", normalized)), Occur.MUST);
    query.Add(new TermQuery(new Term("sourceId", sourceId)), Occur.MUST);
    query.Add(new TermQuery(new Term("entityId", entityId)), Occur.MUST);
    return snapshot.Searcher.Search(query, 1).TotalHits > 0;
  }

  public static void Save(PositiveEvaluationReport report, string outputPrefix)
  {
    string output = Path.GetFullPath(outputPrefix);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    string json = output + ".json";
    string csv = output + ".csv";
    string jsonTemp = json + ".tmp";
    string csvTemp = csv + ".tmp";
    try
    {
      File.WriteAllText(jsonTemp, JsonSerializer.Serialize(report, JsonOptions),
        new UTF8Encoding(false));
      using (var writer = new StreamWriter(csvTemp, false, new UTF8Encoding(false)))
      {
        writer.WriteLine("caseId,tenantId,list,strategy,generationType,sourceId,entityId,expectedMatchIsGuaranteed,expectedEntityFound,reviewTriggered,falseNegative,decision,elapsedMs,snapshotVersion,error,countryValueDetected,countryEntityPresentInIndex,candidateRetrieved,bestTraditionalScore,expectedEntityScore,reviewThreshold,whitelistOverrideApplied,diagnostic");
        foreach (var row in report.Results)
        {
          writer.WriteLine(string.Join(",", new[]
          {
            Escape(row.CaseId), Escape(row.TenantId), Escape(row.List), Escape(row.Strategy),
            Escape(row.GenerationType), Escape(row.ExpectedSourceId), Escape(row.ExpectedEntityId),
            row.ExpectedMatchIsGuaranteed.ToString(), row.ExpectedEntityFound.ToString(),
            row.ReviewTriggered.ToString(), row.FalseNegative.ToString(), Escape(row.Decision),
            row.ElapsedMilliseconds.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
            Escape(row.SnapshotVersion), Escape(row.Error ?? ""),
            row.CountryValueDetected?.ToString() ?? "",
            row.CountryEntityPresentInIndex?.ToString() ?? "",
            row.CandidateRetrieved?.ToString() ?? "",
            row.BestTraditionalScore?.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) ?? "",
            row.ExpectedEntityScore?.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) ?? "",
            row.ReviewThreshold?.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) ?? "",
            row.WhitelistOverrideApplied?.ToString() ?? "",
            Escape(row.Diagnostic ?? "")
          }));
        }
      }
      File.Move(jsonTemp, json, true);
      File.Move(csvTemp, csv, true);
    }
    finally
    {
      if (File.Exists(jsonTemp))
      {
        File.Delete(jsonTemp);
      }
      if (File.Exists(csvTemp))
      {
        File.Delete(csvTemp);
      }
    }
  }

  private static string Escape(string value) =>
    "\"" + value.Replace("\"", "\"\"") + "\"";

  private static PositiveEvaluationMetrics Metrics(string group,
    IEnumerable<PositiveEvaluationResult> source)
  {
    var rows = source.ToArray();
    double[] timings = rows.Select(x => x.ElapsedMilliseconds).OrderBy(x => x).ToArray();
    int found = rows.Count(x => x.ExpectedEntityFound);
    int review = rows.Count(x => x.ReviewTriggered);
    int errors = rows.Count(x => x.Error is not null);
    return new PositiveEvaluationMetrics(group, rows.Length, found, rows.Length - found,
      review, rows.Count(x => x.FalseNegative), errors,
      (double)found / rows.Length, (double)review / rows.Length,
      timings.Average(), Percentile(timings, 0.50), Percentile(timings, 0.95),
      Percentile(timings, 0.99));
  }

  private static double Percentile(double[] sorted, double probability)
  {
    if (sorted.Length == 1)
    {
      return sorted[0];
    }
    double position = (sorted.Length - 1) * probability;
    int low = (int)Math.Floor(position);
    int high = (int)Math.Ceiling(position);
    return sorted[low] + (sorted[high] - sorted[low]) * (position - low);
  }
}
