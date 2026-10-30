namespace Flix.Optimizer;

public sealed record OptimizationSettings(
  int Population = 30,
  int Generations = 40,
  int Seed = 74381)
{
  public void Validate()
  {
    if (Population < 4 || Population > 1000 || Generations < 1 || Generations > 10000)
    {
      throw new ArgumentOutOfRangeException(nameof(Population));
    }
  }
}
