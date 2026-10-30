using Flix.Core;

namespace Flix.Optimizer;

// Positive = MUST HOLD, negative = MUST PASS.
// False negative = positive allowed through; false positive = negative held.
public sealed class GeneticParameterOptimizer
{
  private readonly DecisionService _service;
  private readonly IReadOnlyList<OptimizationCase> _cases;
  private readonly FilterParameters _basis;
  private readonly ThresholdBounds _bounds;
  private readonly OptimizationSettings _settings;
  private readonly Random _random;
  private readonly Dictionary<string, OptimizationResult> _cache = new();

  public GeneticParameterOptimizer(DecisionService service,
    IReadOnlyList<OptimizationCase> cases, FilterParameters basis,
    ThresholdBounds bounds, OptimizationSettings settings)
  {
    _service = service;
    _cases = cases;
    _basis = basis;
    _bounds = bounds;
    _settings = settings;
    settings.Validate();
    basis.Validate();
    if (!_cases.Any(x => x.MustHold) || !_cases.Any(x => !x.MustHold))
    {
      throw new ArgumentException("Both positive and negative cases are required.");
    }
    _random = new Random(settings.Seed);
  }

  public OptimizationResult? Optimize(Action<int, OptimizationResult?>? progress = null)
  {
    var population = new List<ParameterChromosome>
    {
      Clamp(ParameterChromosome.From(_basis)),
      ParameterChromosome.From(_bounds.Lower),
      ParameterChromosome.From(_bounds.Upper)
    };
    while (population.Count < _settings.Population)
    {
      population.Add(RandomChromosome());
    }
    OptimizationResult? best = null;
    for (int generation = 0; generation < _settings.Generations; generation++)
    {
      var ranked = population.Select(x => (Genes: x, Result: Evaluate(x)))
        .OrderBy(x => x.Result.FalseNegatives)
        .ThenBy(x => x.Result.FalsePositives)
        .ToArray();
      foreach (var candidate in ranked)
      {
        if (candidate.Result.FalseNegatives == 0 &&
          (best is null || candidate.Result.FalsePositives < best.FalsePositives))
        {
          best = candidate.Result;
        }
      }
      progress?.Invoke(generation + 1, best);
      int elite = Math.Max(2, _settings.Population / 5);
      var next = ranked.Take(elite).Select(x => x.Genes).ToList();
      while (next.Count < _settings.Population)
      {
        var a = Tournament(ranked);
        var b = Tournament(ranked);
        next.Add(Mutate(Cross(a, b)));
      }
      population = next;
    }
    return best;
  }

  private OptimizationResult Evaluate(ParameterChromosome genes)
  {
    if (_cache.TryGetValue(genes.Key, out var found))
    {
      return found;
    }
    FilterParameters parameters = genes.Apply(_basis);
    int fp = 0, fn = 0, tp = 0, tn = 0;
    foreach (var sample in _cases)
    {
      // An invalid screening request or screening error must not count as a valid CLEAR.
      ScreeningDecision decision = _service.ScreenWithPolicies(sample.Request,
        parameters.DecisionPolicy, parameters.OverridePolicy);
      bool hold = !decision.ReleaseAllowed;
      if (sample.MustHold)
      {
        if (hold)
        {
          tp++;
        }
        else
        {
          fn++;
        }
      }
      else
      {
        if (hold)
        {
          fp++;
        }
        else
        {
          tn++;
        }
      }
    }
    var result = new OptimizationResult(parameters, fp, fn, tp, tn);
    _cache.Add(genes.Key, result);
    return result;
  }

  private ParameterChromosome Clamp(ParameterChromosome source)
  {
    return new ParameterChromosome(source.Values.Select((value, i) =>
      Math.Clamp(value, Lower(i), Upper(i))));
  }

  private double Lower(int i)
  {
    return (double)ThresholdProperties.All[i].GetValue(_bounds.Lower)!;
  }

  private double Upper(int i)
  {
    return (double)ThresholdProperties.All[i].GetValue(_bounds.Upper)!;
  }

  private ParameterChromosome RandomChromosome()
  {
    return new ParameterChromosome(Enumerable.Range(0, ThresholdProperties.All.Count)
      .Select(i => Lower(i) + _random.NextDouble() * (Upper(i) - Lower(i))));
  }

  private ParameterChromosome Tournament(
    (ParameterChromosome Genes, OptimizationResult Result)[] ranked)
  {
    var a = ranked[_random.Next(ranked.Length)];
    var b = ranked[_random.Next(ranked.Length)];
    return a.Result.FalseNegatives < b.Result.FalseNegatives ||
      (a.Result.FalseNegatives == b.Result.FalseNegatives &&
        a.Result.FalsePositives <= b.Result.FalsePositives) ? a.Genes : b.Genes;
  }

  private ParameterChromosome Cross(ParameterChromosome a, ParameterChromosome b)
  {
    return new ParameterChromosome(a.Values.Select((x, i) =>
      _random.Next(2) == 0 ? x : b.Values[i]));
  }

  private ParameterChromosome Mutate(ParameterChromosome chromosome)
  {
    return new ParameterChromosome(chromosome.Values.Select((value, i) =>
    {
      if (_random.NextDouble() >= 0.25)
      {
        return value;
      }
      double width = Upper(i) - Lower(i);
      double result = value + (_random.NextDouble() - 0.5) * width * 0.25;
      return Math.Round(Math.Clamp(result, Lower(i), Upper(i)), 5);
    }));
  }
}
