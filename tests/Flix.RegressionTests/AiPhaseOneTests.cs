using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class AiPhaseOneTests
{
  [TestMethod]
  public void AiIsOptInAndDisabledByDefault()
  {
    var parameters = new FilterParameters();
    Assert.IsFalse(parameters.AI.Enabled);
    parameters.Validate();
  }

  [TestMethod]
  public void EnabledModelRequiresVersionAndPath()
  {
    var config = new AiMatchingOptions { Enabled = true };
    Assert.ThrowsException<InvalidDataException>(() => config.Validate());
  }

  [TestMethod]
  public void AiFeatureVectorIsFiniteAndBounded()
  {
    float[] features = AiPairFeatures.Extract(
      "Muhammad Ali", "Mohammed Ali", 0.75);
    Assert.AreEqual(AiPairFeatures.Dimension, features.Length);
    Assert.IsTrue(features.All(x => float.IsFinite(x) && x >= 0 && x <= 1));
  }

  [TestMethod]
  public void EquivalentNamesHavePerfectFeatures()
  {
    float[] features = AiPairFeatures.Extract("Alice Smith", "Alice Smith", 1);
    Assert.IsTrue(features.All(x => x == 1));
  }

  [TestMethod]
  public void ModelFileMustExistBeforeScreeningStarts()
  {
    var config = new AiMatchingOptions
    {
      Enabled = true,
      ModelPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".onnx"),
      ModelVersion = "test-v1"
    };
    Assert.ThrowsException<FileNotFoundException>(() => new OnnxPairReranker(config));
  }
  [TestMethod]
  public void RankingWeightEndpointsAndBlendAreCorrect()
  {
    var traditional = new AiMatchingOptions { RankingWeight = 0, MinimumConfidence = 0 };
    var ai = new AiMatchingOptions { RankingWeight = 1, MinimumConfidence = 0 };
    var blended = new AiMatchingOptions { RankingWeight = 0.30, MinimumConfidence = 0.70 };
    Assert.AreEqual(0.8, AiRankingPolicy.Combine(0.8, 0.95, traditional), 1e-10);
    Assert.AreEqual(0.95, AiRankingPolicy.Combine(0.8, 0.95, ai), 1e-10);
    Assert.AreEqual(0.845, AiRankingPolicy.Combine(0.8, 0.95, blended), 1e-10);
  }

  [TestMethod]
  public void BelowMinimumConfidenceKeepsTraditionalScore()
  {
    var options = new AiMatchingOptions { RankingWeight = 1, MinimumConfidence = 0.70 };
    Assert.AreEqual(0.82, AiRankingPolicy.Combine(0.82, 0.69, options), 1e-10);
    Assert.AreEqual(0.70, AiRankingPolicy.Combine(0.82, 0.70, options), 1e-10);
  }

  [TestMethod]
  public void InvalidAiRankingParametersAreRejected()
  {
    Assert.ThrowsException<InvalidDataException>(() =>
      new AiMatchingOptions { RankingWeight = double.NaN }.Validate());
    Assert.ThrowsException<InvalidDataException>(() =>
      new AiMatchingOptions { RankingWeight = 1.01 }.Validate());
    Assert.ThrowsException<InvalidDataException>(() =>
      new AiMatchingOptions { MinimumConfidence = -0.1 }.Validate());
  }

}
