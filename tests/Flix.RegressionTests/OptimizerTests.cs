using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Flix.RegressionTests;

[TestClass]
public sealed class OptimizerTests
{
  [TestMethod]
  public void OptimizerYamlExcludesComputedPolicies()
  {
    string file = Path.Combine(Path.GetTempPath(), $"flix-optimizer-{Guid.NewGuid():N}.yaml");
    try
    {
      var parameters = new FilterParameters { NameReviewThreshold = 0.71 };
      FilterParametersFile.Write(file, parameters);
      string yaml = File.ReadAllText(file);
      Assert.IsFalse(yaml.Contains("DecisionPolicy:"));
      Assert.IsFalse(yaml.Contains("OverridePolicy:"));
      Assert.AreEqual(0.71, FilterParametersFile.Read(file).NameReviewThreshold);
    }
    finally
    {
      if (File.Exists(file))
      {
        File.Delete(file);
      }
    }
  }
}
