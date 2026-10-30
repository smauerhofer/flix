using Flix.Core;
using Flix.Optimizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class AiRankingOptimizerTests
{
  private static AiMatchingOptions Model() => new()
  {
    Enabled = true,
    ModelPath = "configured.onnx",
    ModelVersion = "test-model"
  };

  [TestMethod]
  public void OptimizerCanImproveRankingUsingAiScores()
  {
    var cases = new[]
    {
      new AiRankingTrainingCase("query-1", 0.40, 0.95, true),
      new AiRankingTrainingCase("query-1", 0.85, 0.10, false),
      new AiRankingTrainingCase("query-2", 0.50, 0.90, true),
      new AiRankingTrainingCase("query-2", 0.80, 0.20, false)
    };
    var optimizer = new AiRankingGeneticOptimizer(cases, Model(),
      new OptimizationSettings(20, 12, 123));
    var winner = optimizer.Optimize();
    Assert.AreEqual(1, winner.PairwiseAccuracy, 0.00001);
    Assert.AreEqual(2, winner.RelevantPairs);
    Assert.IsTrue(winner.Options.RankingWeight >= 0);
    Assert.IsTrue(winner.Options.RankingWeight <= 1);
    Assert.IsTrue(winner.Options.MinimumConfidence >= 0);
    Assert.IsTrue(winner.Options.MinimumConfidence <= 1);
  }

  [TestMethod]
  public void ModelIsRequiredForAiOptimization()
  {
    var cases = new[]
    {
      new AiRankingTrainingCase("query", 0.3, 0.9, true),
      new AiRankingTrainingCase("query", 0.8, 0.2, false)
    };
    Assert.ThrowsException<ArgumentException>(() =>
      new AiRankingGeneticOptimizer(cases, new AiMatchingOptions(),
        new OptimizationSettings()));
  }

  [TestMethod]
  public void RankingLabelsRequirePositiveAndNegativeForSameQuery()
  {
    var cases = new[]
    {
      new AiRankingTrainingCase("one", 0.3, 0.9, true),
      new AiRankingTrainingCase("two", 0.8, 0.2, false)
    };
    Assert.ThrowsException<ArgumentException>(() =>
      new AiRankingGeneticOptimizer(cases, Model(),
        new OptimizationSettings()));
  }

  [TestMethod]
  public void FixedBoundsAreHonored()
  {
    var cases = new[]
    {
      new AiRankingTrainingCase("query", 0.3, 0.9, true),
      new AiRankingTrainingCase("query", 0.8, 0.2, false)
    };
    var result = new AiRankingGeneticOptimizer(cases, Model(),
      new OptimizationSettings(6, 2, 7), 0.5, 0.5, 0.25, 0.25).Optimize();
    Assert.AreEqual(0.5, result.Options.RankingWeight);
    Assert.AreEqual(0.25, result.Options.MinimumConfidence);
  }
}
