using Flix.Core;

namespace Flix.Cli
{
  internal static class Program
  {
    private static int Main(string[] args)
    {
      if (args.Length == 3 && args[0] == "convert-seco")
      {
        return SecoConverter.Convert(args[1], args[2]);
      }

      if (args.Length == 4 && args[0] == "evaluate-negative")
      {
        var report = NegativeCaseEvaluator.Evaluate(args[1], args[2]);
        NegativeCaseEvaluator.Save(report, args[3]);
        Console.WriteLine($"Evaluated {report.Cases} labeled negatives on {report.SnapshotVersion}: " +
          $"false-positive rate {report.FalsePositiveRate:P2}, errors {report.Errors}, " +
          $"P95 {report.P95Milliseconds:F2} ms");
        return report.Errors > 0 ? 1 : 0;
      }

      if (args.Length >= 4 && args[0] == "evaluate-positive")
      {
        return EvaluatePositive(args);
      }

      if (args.Length >= 4 && args[0] == "generate-positive")
      {
        return GeneratePositive(args);
      }

      if ((args.Length == 5 || args.Length == 6) && args[0] == "import-bundle")
      {
        return ImportBundle(args);
      }

      Console.Error.WriteLine(
        "Usage: flix convert-seco <full-list.xml-or.zip> <normal.csv>\n       flix import-bundle <data-root> <normal.csv> <blacklist.csv> <whitelist.csv> [tenant-lists-directory]\n       flix generate-positive <data-root> <output.jsonl> <count> [--seed N] [--version VERSION] [--tenant ID] [--list normal|blacklist] [--variation exact|fuzzy|mixed] [--mutation mixed|deletion|transpose|diacritic|word-order]\n       flix evaluate-positive <data-root> <positive.jsonl> <output-prefix> [--version VERSION] [--config filter.yaml]\n       flix evaluate-negative <data-root> <labeled-negative.jsonl> <output-prefix>");
      return 2;
    }

    private static int EvaluatePositive(string[] args)
    {
      if (args.Length < 4 || (args.Length - 4) % 2 != 0)
      {
        throw new ArgumentException("Usage: evaluate-positive <data-root> <positive.jsonl> <output-prefix> [--version VERSION] [--config FILE]");
      }
      string? version = null;
      string? config = null;
      for (int i = 4; i < args.Length; i += 2)
      {
        switch (args[i])
        {
          case "--version":
            version = args[i + 1];
            break;
          case "--config":
            config = args[i + 1];
            break;
          default:
            throw new ArgumentException($"Unknown option {args[i]}.");
        }
      }
      FilterParameters? parameters = config is null ? null : FilterParametersFile.Read(config);
      var report = PositiveCaseEvaluator.Evaluate(args[1], args[2], parameters, version);
      PositiveCaseEvaluator.Save(report, args[3]);
      Console.WriteLine($"Evaluated {report.Overall.Cases} cases on snapshot {report.SnapshotVersion}.");
      Console.WriteLine($"Expected entity detection: {report.Overall.DetectionRate:P2}; review rate: {report.Overall.ReviewRate:P2}; false negatives: {report.Overall.FalseNegatives}; P95: {report.Overall.P95Milliseconds:F2} ms");
      Console.WriteLine($"Configuration SHA256: {report.ConfigurationSha256}");
      Console.WriteLine($"Reports: {Path.GetFullPath(args[3])}.json and .csv");
      return report.Overall.FalseNegatives > 0 || report.Overall.MissedExpectedEntities > 0 ? 1 : 0;
    }

  private static int GeneratePositive(string[] args)
    {
      if (!int.TryParse(args[3], out int count) || count <= 0)
      {
        throw new ArgumentException("Case count must be a positive integer.");
      }
      int seed = 74381;
      string? version = null;
      string tenant = "default";
      string list = "normal";
      string variation = "exact";
      string mutation = "mixed";
      if ((args.Length - 4) % 2 != 0)
      {
        throw new ArgumentException("Options must be provided as --name value pairs.");
      }
      for (int i = 4; i < args.Length; i += 2)
      {
        switch (args[i])
        {
          case "--seed":
            if (!int.TryParse(args[i + 1], out seed))
            {
              throw new ArgumentException("Invalid seed.");
            }
            break;
          case "--version":
            version = args[i + 1];
            break;
          case "--tenant":
            tenant = args[i + 1];
            break;
          case "--list":
            list = args[i + 1];
            break;
          case "--variation":
            variation = args[i + 1];
            break;
          case "--mutation":
            mutation = args[i + 1];
            break;
          default:
            throw new ArgumentException($"Unknown option {args[i]}.");
        }
      }
      int generated = PositiveCaseGenerator.Generate(args[1], args[2], count, seed,
        version, tenant, list, variation, mutation);
      Console.WriteLine($"Generated {generated} positive cases in {Path.GetFullPath(args[2])}.");
      return 0;
    }

    // Offline build: each list has a physically separate Lucene index.
    // Publish only after all three readers can open and counts agree.
    private static int ImportBundle(string[] args)
    {
      string root = Path.GetFullPath(args[1]);
      SnapshotReservation reservation = SnapshotVersionGenerator.Reserve(root);
      string version = reservation.Version;
      string basePath = reservation.DirectoryPath;
      string[] buckets = ["normal", "blacklist", "whitelist"];
      int[] counts = new int[3];
      for (int index = 0; index < 3; index++)
      {
        string csv = Path.GetFullPath(args[2 + index]);
        using (var writer = new NameIndexBuilder(Path.Combine(basePath, buckets[index])))
        {
          counts[index] = buckets[index] switch
          {
            "normal" => ReferenceListImporter.ImportCsv(csv, writer),
            "blacklist" => ReferenceListImporter.ImportBlacklist(csv, writer),
            "whitelist" => ReferenceListImporter.ImportWhitelist(csv, writer),
            _ => throw new InvalidOperationException("Unknown list")
          };
          writer.Complete();
        }
      }
      var tenantCounts = new Dictionary<string, (int Black, int White)>();
      if (args.Length == 6)
      {
        string tenantRoot = Path.GetFullPath(args[5]);
        if (!Directory.Exists(tenantRoot))
        {
          throw new DirectoryNotFoundException(tenantRoot);
        }
        foreach (string directory in Directory.GetDirectories(tenantRoot))
        {
          string tenantId = Path.GetFileName(directory);
          if (!TenantIdValidator.IsValid(tenantId))
          {
            throw new InvalidDataException("Unsafe tenant ID.");
          }
          string blackFile = Path.Combine(directory, "blacklist.json");
          string whiteFile = Path.Combine(directory, "whitelist.json");
          if (!File.Exists(blackFile) || !File.Exists(whiteFile))
          {
            throw new InvalidDataException($"Tenant {tenantId} must provide both list files.");
          }
          string tenantOutput = Path.Combine(basePath, "tenants", tenantId);
          int blackCount;
          int whiteCount;
          using (var writer = new NameIndexBuilder(Path.Combine(tenantOutput, "blacklist")))
          {
            blackCount = ReferenceListImporter.ImportBlacklist(blackFile, writer);
            writer.Complete();
          }
          using (var writer = new NameIndexBuilder(Path.Combine(tenantOutput, "whitelist")))
          {
            whiteCount = ReferenceListImporter.ImportWhitelist(whiteFile, writer);
            writer.Complete();
          }
          tenantCounts.Add(tenantId, (blackCount, whiteCount));
        }
      }
      if (counts[0] == 0)
      {
        throw new InvalidDataException("Normal reference index must not be empty.");
      }
      using (var snapshot = new FilterSnapshot(version, basePath))
      {
        if (snapshot.Normal.DocumentCount != counts[0] ||
          snapshot.Blacklist.DocumentCount != counts[1] ||
          snapshot.Whitelist.DocumentCount != counts[2])
        {
          throw new InvalidDataException("Index counts do not match imported row counts.");
        }
        foreach (var entry in tenantCounts)
        {
          var tenant = snapshot.Tenants[entry.Key];
          if (tenant.Blacklist.DocumentCount != entry.Value.Black ||
            tenant.Whitelist.DocumentCount != entry.Value.White)
          {
            throw new InvalidDataException("Tenant index counts differ from imported counts.");
          }
        }
      }
      SnapshotFiles.Publish(root, new SnapshotManifest(version, version));
      Console.WriteLine($"Published {version}: normal={counts[0]}, blacklist={counts[1]}, whitelist={counts[2]}");
      return 0;
    }
  }
}
