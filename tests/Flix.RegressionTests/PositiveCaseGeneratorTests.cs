using System.Text.Json;
using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class PositiveCaseGeneratorTests
{
  [TestMethod]
  public void ExactGenerationIsReproducibleAndKeepsSourceAndEntity()
  {
    string root = CreateFixture();
    try
    {
      string first = Path.Combine(root, "first.jsonl");
      string second = Path.Combine(root, "second.jsonl");
      Assert.AreEqual(2, PositiveCaseGenerator.Generate(root, first, 2, 42));
      Assert.AreEqual(2, PositiveCaseGenerator.Generate(root, second, 2, 42));
      Assert.AreEqual(File.ReadAllText(first), File.ReadAllText(second));
      foreach (string line in File.ReadAllLines(first))
      {
        using var json = JsonDocument.Parse(line);
        Assert.AreEqual("EXACT", json.RootElement.GetProperty("generationType").GetString());
        Assert.IsTrue(json.RootElement.GetProperty("expectedMatchIsGuaranteed").GetBoolean());
        Assert.AreEqual("SECO", json.RootElement.GetProperty("expected").GetProperty("sourceId").GetString());
        Assert.AreEqual("v1", json.RootElement.GetProperty("snapshotVersion").GetString());
      }
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  [TestMethod]
  public void FuzzyGenerationIsExplicitlySynthetic()
  {
    string root = CreateFixture();
    try
    {
      string output = Path.Combine(root, "fuzzy.jsonl");
      PositiveCaseGenerator.Generate(root, output, 1, 17, variation: "fuzzy");
      using var json = JsonDocument.Parse(File.ReadAllText(output));
      Assert.AreEqual("SYNTHETIC_FUZZY", json.RootElement.GetProperty("generationType").GetString());
      Assert.IsFalse(json.RootElement.GetProperty("expectedMatchIsGuaranteed").GetBoolean());
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  [TestMethod]
  public void RefusesUnprovisionedTenant()
  {
    string root = CreateFixture();
    try
    {
      Assert.ThrowsExactly<ArgumentException>(() =>
        PositiveCaseGenerator.Generate(root, Path.Combine(root, "invalid.jsonl"), 1, 1,
          tenantId: "other"));
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  [TestMethod]
  public void MissingTenantErrorListsProvisionedTenants()
  {
    string root = CreateFixture();
    try
    {
      string snapshot = Path.Combine(root, "snapshots", "v1");
      Directory.CreateDirectory(Path.Combine(snapshot, "tenants", "alpha"));
      Directory.CreateDirectory(Path.Combine(snapshot, "tenants", "beta"));

      var error = Assert.ThrowsExactly<ArgumentException>(() =>
        PositiveCaseGenerator.Generate(root, Path.Combine(root, "missing.jsonl"), 1, 42));

      StringAssert.Contains(error.Message, "alpha, beta");
      StringAssert.Contains(error.Message, "--tenant <tenant-id>");
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  [TestMethod]
  public void AllowsProvisionedTenantToReadGlobalNormalIndex()
  {
    string root = CreateFixture();
    try
    {
      string snapshot = Path.Combine(root, "snapshots", "v1");
      Directory.CreateDirectory(Path.Combine(snapshot, "tenants", "alpha"));
      string output = Path.Combine(root, "alpha.jsonl");

      Assert.AreEqual(1, PositiveCaseGenerator.Generate(root, output, 1, 42,
        tenantId: "alpha"));
      using var json = JsonDocument.Parse(File.ReadAllText(output));
      Assert.AreEqual("alpha", json.RootElement.GetProperty("tenantId").GetString());
      Assert.AreEqual("normal", json.RootElement.GetProperty("list").GetString());

      Assert.ThrowsExactly<ArgumentException>(() =>
        PositiveCaseGenerator.Generate(root, Path.Combine(root, "other.jsonl"), 1, 1,
          tenantId: "other"));
      Assert.ThrowsExactly<ArgumentException>(() =>
        PositiveCaseGenerator.Generate(root, Path.Combine(root, "default.jsonl"), 1, 1));
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  private static string CreateFixture()
  {
    string root = Path.Combine(Path.GetTempPath(), "flix-positive-" + Guid.NewGuid().ToString("N"));
    string folder = Path.Combine(root, "snapshots", "v1");
    Directory.CreateDirectory(folder);
    using (var writer = new NameIndexBuilder(Path.Combine(folder, "normal")))
    {
      writer.AddTypedName(new NameRecord("101", "SECO", "Muhammad Ali"), "PERSON_NAME");
      writer.AddField(new ReferenceFieldRecord("102", "SECO", "ADDRESS", "25 King Street"));
      writer.Complete();
    }
    SnapshotFiles.Publish(root, new SnapshotManifest("v1", "v1"));
    return root;
  }
}
