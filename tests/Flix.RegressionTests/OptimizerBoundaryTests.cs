using Flix.Core;
using Flix.Optimizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class OptimizerBoundaryTests
{
  [TestMethod]
  public void ThresholdPropertiesContainExactlyExpectedNumbers()
  {
    CollectionAssert.AreEquivalent(
      new[] { "NameReviewThreshold", "AddressReviewThreshold",
        "ListNameThreshold", "ListAddressThreshold" },
      ThresholdProperties.All.Select(x => x.Name).ToArray());
  }

  [TestMethod]
  public void RejectsReversedBounds()
  {
    var low = new FilterParameters { NameReviewThreshold = 0.9 };
    var high = new FilterParameters { NameReviewThreshold = 0.2 };
    Assert.ThrowsExactly<ArgumentException>(() => new ThresholdBounds(low, high));
  }

  [TestMethod]
  public void ChromosomeDoesNotChangeNonThresholdSettings()
  {
    var baseline = new FilterParameters
    {
      Root = "test-root",
      WhitelistOverridesAll = false,
      ListPrecedence = ListPrecedence.BlacklistFirst
    };
    var candidate = new ParameterChromosome(
      ThresholdProperties.All.Select(_ => 0.6)).Apply(baseline);
    Assert.AreEqual(0.6, candidate.NameReviewThreshold);
    Assert.AreEqual(0.6, candidate.AddressReviewThreshold);
    Assert.AreEqual(0.6, candidate.ListNameThreshold);
    Assert.AreEqual(0.6, candidate.ListAddressThreshold);
    Assert.AreEqual(baseline.Root, candidate.Root);
    Assert.AreEqual(baseline.WhitelistOverridesAll, candidate.WhitelistOverridesAll);
    Assert.AreEqual(baseline.ListPrecedence, candidate.ListPrecedence);
  }
}
