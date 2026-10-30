using System.Text.Json;
using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class BenchmarkQualityTests
{
  [TestMethod]
  public void CountryAttributionRemainsSeparateFromBoundedHits()
  {
    string root = Fixture();
    try
    {
      string input = Path.Combine(root, "country.jsonl");
      File.WriteAllText(input, JsonSerializer.Serialize(new
      {
        caseId = "COUNTRY-1", tenantId = "default", messageId = "MSG-1",
        snapshotVersion = "v1", list = "normal", generationType = "EXACT",
        expectedMatchIsGuaranteed = true,
        field = new { partyId = "sender", type = "COUNTRY", value = "CH" },
        expected = new { sourceId = "SECO", entityId = "COUNTRY-1", match = true }
      }) + "\n");
      var report = PositiveCaseEvaluator.Evaluate(root, input);
      Assert.AreEqual(true, report.Results[0].CountryValueDetected);
      Assert.AreEqual(true, report.Results[0].CountryEntityPresentInIndex);
      Assert.AreEqual("COUNTRY", report.Results[0].Strategy);
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  [TestMethod]
  public void FuzzyGeneratorRecordsMutationAndReproducesCases()
  {
    string root = Fixture();
    try
    {
      string first = Path.Combine(root, "first.jsonl");
      string second = Path.Combine(root, "second.jsonl");
      // COUNTRY is ineligible, but this fixture also has a person name.
      PositiveCaseGenerator.Generate(root, first, 1, 12, variation: "fuzzy",
        mutation: "transpose");
      PositiveCaseGenerator.Generate(root, second, 1, 12, variation: "fuzzy",
        mutation: "transpose");
      Assert.AreEqual(File.ReadAllText(first), File.ReadAllText(second));
      using var doc = JsonDocument.Parse(File.ReadAllText(first));
      Assert.AreEqual("transpose", doc.RootElement.GetProperty("mutationType").GetString());
      Assert.AreEqual("SYNTHETIC_FUZZY", doc.RootElement.GetProperty("generationType").GetString());
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  [TestMethod]
  public void NegativeEvaluatorRequiresIndependentLabelProvenance()
  {
    string root = Fixture();
    try
    {
      string input = Path.Combine(root, "negative.jsonl");
      File.WriteAllText(input, JsonSerializer.Serialize(new
      {
        caseId = "NEG-1", tenantId = "default", messageId = "PAY-1",
        snapshotVersion = "v1", label = "NONMATCH",
        field = new { partyId = "sender", type = "PERSON_NAME", value = "Unrelated Sample Person" }
      }) + "\n");
      Assert.ThrowsExactly<InvalidDataException>(() => NegativeCaseEvaluator.Evaluate(root, input));
      File.WriteAllText(input, JsonSerializer.Serialize(new
      {
        caseId = "NEG-1", tenantId = "default", messageId = "PAY-1",
        snapshotVersion = "v1", label = "NONMATCH", labelSource = "human-reviewed-fixture",
        field = new { partyId = "sender", type = "PERSON_NAME", value = "Unrelated Sample Person" }
      }) + "\n");
      var report = NegativeCaseEvaluator.Evaluate(root, input);
      Assert.AreEqual(1, report.Cases);
      Assert.AreEqual(0, report.Errors);
      NegativeCaseEvaluator.Save(report, Path.Combine(root, "negative-report"));
      Assert.IsTrue(File.Exists(Path.Combine(root, "negative-report.csv")));
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  private static string Fixture()
  {
    string root = Path.Combine(Path.GetTempPath(), "flix-benchmark-" + Guid.NewGuid().ToString("N"));
    string folder = Path.Combine(root, "snapshots", "v1");
    Directory.CreateDirectory(folder);
    using (var writer = new NameIndexBuilder(Path.Combine(folder, "normal")))
    {
      writer.AddField(new ReferenceFieldRecord("COUNTRY-1", "SECO", "COUNTRY", "CH"));
      writer.AddTypedName(new NameRecord("NAME-1", "SECO", "Mohammed Ali"), "PERSON_NAME");
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
