using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class PositiveCaseEvaluatorTests
{
  [TestMethod]
  public void FindsExpectedSourceAndEntityAndReportsMetrics()
  {
    string root = CreateFixture();
    try
    {
      string input = Path.Combine(root, "cases.jsonl");
      PositiveCaseGenerator.Generate(root, input, 1, 74381);
      var report = PositiveCaseEvaluator.Evaluate(root, input);
      Assert.AreEqual(1, report.Overall.Cases);
      Assert.AreEqual(1, report.Overall.ExpectedMatchesFound);
      Assert.AreEqual(0, report.Overall.MissedExpectedEntities);
      Assert.IsTrue(report.Results[0].ExpectedEntityFound);
      Assert.IsTrue(report.Results[0].ReviewTriggered);
      Assert.AreEqual("SECO", report.Results[0].ExpectedSourceId);
      Assert.AreEqual("101", report.Results[0].ExpectedEntityId);
      Assert.AreEqual(1, report.ByTenant.Count);
      Assert.AreEqual("default", report.ByTenant[0].Group);
      string output = Path.Combine(root, "evaluation");
      PositiveCaseEvaluator.Save(report, output);
      Assert.IsTrue(File.Exists(output + ".json"));
      Assert.IsTrue(File.Exists(output + ".csv"));
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  [TestMethod]
  public void RejectsMismatchedSnapshotVersion()
  {
    string root = CreateFixture();
    try
    {
      string input = Path.Combine(root, "cases.jsonl");
      PositiveCaseGenerator.Generate(root, input, 1, 1);
      Assert.ThrowsExactly<InvalidDataException>(() =>
        PositiveCaseEvaluator.Evaluate(root, input, version: "different"));
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  [TestMethod]
  public void DoesNotCountDifferentEntityAsExpectedMatch()
  {
    string root = CreateFixture();
    try
    {
      string input = Path.Combine(root, "cases.jsonl");
      PositiveCaseGenerator.Generate(root, input, 1, 1);
      string changed = File.ReadAllText(input).Replace("\"entityId\":\"101\"",
        "\"entityId\":\"NONEXISTENT\"");
      File.WriteAllText(input, changed);
      var report = PositiveCaseEvaluator.Evaluate(root, input);
      Assert.AreEqual(0, report.Overall.ExpectedMatchesFound);
      Assert.AreEqual(1, report.Overall.MissedExpectedEntities);
      Assert.AreEqual(1, report.Overall.ReviewsTriggered);
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  private static string CreateFixture()
  {
    string root = Path.Combine(Path.GetTempPath(), "flix-eval-" + Guid.NewGuid().ToString("N"));
    string folder = Path.Combine(root, "snapshots", "v1");
    Directory.CreateDirectory(folder);
    using (var writer = new NameIndexBuilder(Path.Combine(folder, "normal")))
    {
      writer.AddTypedName(new NameRecord("101", "SECO", "Muhammad Ali"), "PERSON_NAME");
      writer.Complete();
    }
    using (var writer = new NameIndexBuilder(Path.Combine(folder, "blacklist")))
    {
      writer.Complete();
    }
    using (var writer = new NameIndexBuilder(Path.Combine(folder, "whitelist")))
    {
      writer.Complete();
    }
    SnapshotFiles.Publish(root, new SnapshotManifest("v1", "v1"));
    return root;
  }
}
