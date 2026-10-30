using Microsoft.VisualStudio.TestTools.UnitTesting;
using Flix.Core;

namespace Flix.RegressionTests;

[TestClass]
public sealed class MatchingQualityTests
{
  [TestMethod]
  public void AddressAbbreviationsAndTokenOrderHaveHighSimilarity()
  {
    Assert.IsTrue(AddressSimilarity.Evaluate("12 Main St", "Main Street 12") >= 0.90);
  }

  [TestMethod]
  public void DifferentBuildingNumbersCannotScoreAsAnExactAddress()
  {
    Assert.IsTrue(AddressSimilarity.Evaluate("12 Main St", "14 Main Street") <= 0.65);
  }

  [TestMethod]
  public void AddressDiacriticsAreNormalized()
  {
    Assert.AreEqual(1.0, AddressSimilarity.Evaluate("Müllerstrasse 12", "Mullerstraße 12"), 0.000001);
  }
}
