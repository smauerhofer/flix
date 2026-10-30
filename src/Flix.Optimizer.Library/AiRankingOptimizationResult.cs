using Flix.Core;

namespace Flix.Optimizer;

public sealed record AiRankingOptimizationResult(
  AiMatchingOptions Options, double PairwiseAccuracy, int RelevantPairs);
