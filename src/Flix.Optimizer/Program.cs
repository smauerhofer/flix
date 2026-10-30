using Flix.Core;

namespace Flix.Optimizer;

internal class Program
{
  private static int Main(string[] args)
  {
    if (args.Length < 6 || args.Length > 11)
    {
      Console.Error.WriteLine("Usage: flix-optimizer <base.yaml> <lower.yaml> <upper.yaml> <positive.jsonl> <negative.jsonl> <winner.yaml> [population] [generations] [seed] [--ranking-cases <ranking.jsonl>]");
      return 2;
    }
    try
    {
      string basePath = Path.GetFullPath(args[0]);
      FilterParameters basis = FilterParametersFile.Read(basePath);
      FilterParameters lower = FilterParametersFile.Read(args[1]);
      FilterParameters upper = FilterParametersFile.Read(args[2]);
      var bounds = new ThresholdBounds(lower, upper);
      string output = Path.GetFullPath(args[5]);
      if (File.Exists(output))
      {
        throw new IOException("Output already exists. Choose a new path so an unsuccessful run cannot leave a misleading winner: " + output);
      }
      string root = Path.GetFullPath(basis.Root, Path.GetDirectoryName(basePath)!);
      SnapshotManifest manifest = SnapshotFiles.Read(root);
      using var snapshot = new FilterSnapshot(manifest.Version, SnapshotFiles.Resolve(root, manifest));
      var manager = new FilterManager(new ScreeningFilter(snapshot, basis));
      var name = new LuceneNameMatcher();
      var field = new LuceneFieldMatcher();
      var strategies = new ScreeningStrategyRegistry(new IScreeningStrategy[]
      {
        new IbanScreeningStrategy(field),
        new AccountNumberScreeningStrategy(field),
        new BicScreeningStrategy(field),
        new CountryScreeningStrategy(field),
        new AddressScreeningStrategy(field),
        new PersonNameScreeningStrategy(name),
        new OrganizationNameScreeningStrategy(name),
        new LeiScreeningStrategy(field)
      });
      var service = new DecisionService(manager, strategies);
      var positives = TrainingDataFile.Read(args[3]);
      var negatives = TrainingDataFile.Read(args[4]);
      var cases = positives.Select(x => new OptimizationCase(x, true))
        .Concat(negatives.Select(x => new OptimizationCase(x, false))).ToArray();
      int rankingArg = Array.IndexOf(args, "--ranking-cases");
      if (rankingArg >= 0 && rankingArg != args.Length - 2)
      {
        throw new ArgumentException("--ranking-cases must be followed by a JSONL filename.");
      }
      int numericEnd = rankingArg >= 0 ? rankingArg : args.Length;
      if (numericEnd > 9)
      {
        throw new ArgumentException("Too many numeric optimizer arguments.");
      }
      var settings = new OptimizationSettings(
        numericEnd > 6 ? int.Parse(args[6]) : 30,
        numericEnd > 7 ? int.Parse(args[7]) : 40,
        numericEnd > 8 ? int.Parse(args[8]) : 74381);
      if (rankingArg >= 0 && !basis.AI.Enabled)
      {
        throw new ArgumentException("AI must be enabled with a valid local ONNX model when --ranking-cases is supplied.");
      }
      if (basis.AI.Enabled && rankingArg < 0)
      {
        throw new ArgumentException("An enabled AI model requires labeled --ranking-cases to optimize RankingWeight and MinimumConfidence.");
      }
      var optimizer = new GeneticParameterOptimizer(service, cases, basis, bounds, settings);
      OptimizationResult? winner = optimizer.Optimize((generation, best) =>
      {
        Console.WriteLine($"Generation {generation}: best feasible FP={best?.FalsePositives.ToString() ?? "none"}");
      });
      if (winner is null)
      {
        Console.Error.WriteLine("No candidate with zero false negatives found. No YAML generated.");
        return 3;
      }
      FilterParameters winningParameters = winner.Parameters;
      if (rankingArg >= 0)
      {
        using var model = new OnnxPairReranker(basis.AI);
        var rankingCases = AiRankingTrainingDataFile.Read(args[rankingArg + 1], model);
        var aiOptimizer = new AiRankingGeneticOptimizer(rankingCases, basis.AI, settings,
          lower.AI.RankingWeight, upper.AI.RankingWeight,
          lower.AI.MinimumConfidence, upper.AI.MinimumConfidence);
        var aiWinner = aiOptimizer.Optimize();
        winningParameters = winningParameters with { AI = aiWinner.Options };
        Console.WriteLine($"AI ranking: pairwise accuracy={aiWinner.PairwiseAccuracy:P2}, " +
          $"pairs={aiWinner.RelevantPairs}, weight={aiWinner.Options.RankingWeight:F5}, " +
          $"minConfidence={aiWinner.Options.MinimumConfidence:F5}");
      }
      FilterParametersFile.Write(output, winningParameters);
      Console.WriteLine($"Winner: TP={winner.TruePositives} FN={winner.FalseNegatives} FP={winner.FalsePositives} TN={winner.TrueNegatives}");
      Console.WriteLine($"Saved {output}");
      return 0;
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine(ex);
      return 1;
    }
  }
}
