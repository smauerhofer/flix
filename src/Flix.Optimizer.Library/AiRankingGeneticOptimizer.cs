using Flix.Core;

namespace Flix.Optimizer;

// Optimizes ranking quality, not the independent hold/release decision fitness.
public sealed class AiRankingGeneticOptimizer
{
  private readonly AiMatchingOptions basis;
  private readonly (AiRankingTrainingCase Positive, AiRankingTrainingCase Negative)[] pairs;
  private readonly OptimizationSettings settings;
  private readonly Random random;
  private readonly (double Min, double Max) weights;
  private readonly (double Min, double Max) confidences;

  public AiRankingGeneticOptimizer(IReadOnlyList<AiRankingTrainingCase> cases,
    AiMatchingOptions basis, OptimizationSettings settings,
    double minimumWeight = 0, double maximumWeight = 1,
    double minimumConfidence = 0, double maximumConfidence = 1)
  {
    ArgumentNullException.ThrowIfNull(cases);
    ArgumentNullException.ThrowIfNull(basis);
    ArgumentNullException.ThrowIfNull(settings);
    settings.Validate();
    basis.Validate();
    if (!basis.Enabled)
    {
      throw new ArgumentException("A configured AI model is required for AI ranking optimization.");
    }
    if (!ValidRange(minimumWeight, maximumWeight) ||
      !ValidRange(minimumConfidence, maximumConfidence))
    {
      throw new ArgumentOutOfRangeException(nameof(minimumWeight));
    }
    weights = (minimumWeight, maximumWeight);
    confidences = (minimumConfidence, maximumConfidence);
    this.basis = basis;
    this.settings = settings;
    random = new Random(settings.Seed);
    foreach (var sample in cases)
    {
      if (string.IsNullOrWhiteSpace(sample.QueryId) ||
        !IsScore(sample.TraditionalScore) || !IsScore(sample.AiScore))
      {
        throw new ArgumentException("Ranking cases require query IDs and scores in [0,1].");
      }
    }
    pairs = cases.GroupBy(x => x.QueryId, StringComparer.Ordinal)
      .SelectMany(group => group.Where(x => x.Relevant)
        .SelectMany(positive => group.Where(x => !x.Relevant)
          .Select(negative => (Positive: positive, Negative: negative))))
      .ToArray();
    if (pairs.Length == 0)
    {
      throw new ArgumentException("Ranking data must contain a relevant and an irrelevant candidate for at least one query.");
    }
  }

  public AiRankingOptimizationResult Optimize()
  {
    var population = new List<(double Weight, double Confidence)>
    {
      (Math.Clamp(basis.RankingWeight, weights.Min, weights.Max),
        Math.Clamp(basis.MinimumConfidence, confidences.Min, confidences.Max)),
      (weights.Min, confidences.Min),
      (weights.Max, confidences.Max)
    };
    while (population.Count < settings.Population)
    {
      population.Add(RandomGenes());
    }
    (double Weight, double Confidence) best = population[0];
    double bestScore = Score(best);
    for (int generation = 0; generation < settings.Generations; generation++)
    {
      var ranked = population.Select(x => (Genes: x, Score: Score(x)))
        .OrderByDescending(x => x.Score)
        .ThenBy(x => x.Genes.Weight)
        .ThenBy(x => x.Genes.Confidence)
        .ToArray();
      if (ranked[0].Score > bestScore ||
        (ranked[0].Score == bestScore && ranked[0].Genes.Weight < best.Weight))
      {
        best = ranked[0].Genes;
        bestScore = ranked[0].Score;
      }
      int elite = Math.Max(2, settings.Population / 5);
      var next = ranked.Take(elite).Select(x => x.Genes).ToList();
      while (next.Count < settings.Population)
      {
        var a = ranked[random.Next(Math.Max(elite, settings.Population / 2))].Genes;
        var b = ranked[random.Next(Math.Max(elite, settings.Population / 2))].Genes;
        var child = (Weight: random.Next(2) == 0 ? a.Weight : b.Weight,
          Confidence: random.Next(2) == 0 ? a.Confidence : b.Confidence);
        if (random.NextDouble() < 0.30)
        {
          child.Weight = Mutate(child.Weight, weights);
        }
        if (random.NextDouble() < 0.30)
        {
          child.Confidence = Mutate(child.Confidence, confidences);
        }
        next.Add(child);
      }
      population = next;
    }
    var options = basis with
    {
      RankingWeight = best.Weight,
      MinimumConfidence = best.Confidence
    };
    options.Validate();
    return new AiRankingOptimizationResult(options, bestScore, pairs.Length);
  }

  private double Score((double Weight, double Confidence) genes)
  {
    var options = basis with
    {
      RankingWeight = genes.Weight,
      MinimumConfidence = genes.Confidence
    };
    double correct = 0;
    foreach (var (positive, negative) in pairs)
    {
      double pos = AiRankingPolicy.Combine(positive.TraditionalScore, positive.AiScore, options);
      double neg = AiRankingPolicy.Combine(negative.TraditionalScore, negative.AiScore, options);
      correct += pos > neg ? 1 : pos == neg ? 0.5 : 0;
    }
    return correct / pairs.Length;
  }

  private (double Weight, double Confidence) RandomGenes()
  {
    return (weights.Min + random.NextDouble() * (weights.Max - weights.Min),
      confidences.Min + random.NextDouble() * (confidences.Max - confidences.Min));
  }

  private double Mutate(double value, (double Min, double Max) bounds)
  {
    return Math.Round(Math.Clamp(value + (random.NextDouble() - 0.5) *
      (bounds.Max - bounds.Min) * 0.3, bounds.Min, bounds.Max), 5);
  }

  private static bool IsScore(double value)
  {
    return double.IsFinite(value) && value >= 0 && value <= 1;
  }

  private static bool ValidRange(double min, double max)
  {
    return IsScore(min) && IsScore(max) && min <= max;
  }
}
