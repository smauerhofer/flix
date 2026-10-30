using Flix.Core;
namespace Flix.Optimizer;

public sealed record OptimizationResult(
  FilterParameters Parameters,
  int FalsePositives,
  int FalseNegatives,
  int TruePositives,
  int TrueNegatives);
