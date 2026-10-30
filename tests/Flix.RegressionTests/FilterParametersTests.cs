using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class FilterParametersTests
{
  [TestMethod]
  public void YamlRoundTripPreservesAllProperties()
  {
    string directory = Path.Combine(Path.GetTempPath(), "flix-yaml-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
      string file = Path.Combine(directory, "flix.yaml");
      var expected = new FilterParameters
      {
        Root = "../../data",
        NameReviewThreshold = 0.81,
        AddressReviewThreshold = 0.88,
        WhitelistOverridesAll = false,
        ListPrecedence = ListPrecedence.BlacklistFirst,
        ListNameThreshold = 0.93,
        ListAddressThreshold = 0.96
      };
      FilterParametersFile.Write(file, expected);
      FilterParameters actual = FilterParametersFile.Read(file);
      Assert.AreEqual(expected, actual);
    }
    finally
    {
      Directory.Delete(directory, true);
    }
  }

  [TestMethod]
  public void InvalidThresholdIsRejected()
  {
    var parameters = new FilterParameters { NameReviewThreshold = 1.1 };
    Assert.ThrowsExactly<InvalidDataException>(() => parameters.Validate());
  }

  [TestMethod]
  public void InvalidYamlIsRejected()
  {
    string file = Path.GetTempFileName();
    try
    {
      File.WriteAllText(file, "NameReviewThreshold: 2.0\nRoot: ./data\n");
      Assert.ThrowsExactly<InvalidDataException>(() => FilterParametersFile.Read(file));
    }
    finally
    {
      File.Delete(file);
    }
  }
}
