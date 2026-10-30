using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class ListManagementTests
{
  [TestMethod]
  public void BlacklistImporterIncludesOnlyActiveEntries()
  {
    string root = TemporaryDirectory();
    try
    {
      string file = Path.Combine(root, "blacklist.json");
      BlacklistFile.Write(file, [
        new BlacklistEntry { EntityId = "B-1", Type = "NAME", Value = "Example Active", Active = true },
        new BlacklistEntry { EntityId = "B-2", Type = "NAME", Value = "Example Disabled", Active = false }
      ]);
      using (var writer = new NameIndexBuilder(Path.Combine(root, "index")))
      {
        Assert.AreEqual(1, ReferenceListImporter.ImportBlacklist(file, writer));
        writer.Complete();
      }
      using var snapshot = new NameIndexSnapshot("test", Path.Combine(root, "index"));
      Assert.AreEqual(1, snapshot.DocumentCount);
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  [TestMethod]
  public void WhitelistImporterRejectsPartiallyDisabledGroup()
  {
    string root = TemporaryDirectory();
    try
    {
      string file = Path.Combine(root, "whitelist.json");
      WhitelistFile.Write(file, [
        new WhitelistGroupEntry
        {
          EntityId = "W-1",
          Criteria = [
            new WhitelistCriterion { Type = "NAME", Value = "Active Name", Active = true },
            new WhitelistCriterion { Type = "COUNTRY", Value = "CH", Active = false }
          ]
        },
        new WhitelistGroupEntry
        {
          EntityId = "W-2",
          Criteria = [
            new WhitelistCriterion { Type = "NAME", Value = "Approved Company", Active = true },
            new WhitelistCriterion { Type = "COUNTRY", Value = "GB", Active = true }
          ]
        }
      ]);
      using (var writer = new NameIndexBuilder(Path.Combine(root, "index")))
      {
        Assert.AreEqual(2, ReferenceListImporter.ImportWhitelist(file, writer));
        writer.Complete();
      }
      using var snapshot = new NameIndexSnapshot("test", Path.Combine(root, "index"));
      Assert.AreEqual(2, snapshot.DocumentCount);
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  [TestMethod]
  public void DisabledWhitelistGroupDoesNotGetIndexed()
  {
    string root = TemporaryDirectory();
    try
    {
      string file = Path.Combine(root, "whitelist.json");
      WhitelistFile.Write(file, [
        new WhitelistGroupEntry
        {
          EntityId = "W-1",
          Active = false,
          Criteria = [new WhitelistCriterion { Type = "NAME", Value = "Disabled Group" }]
        }
      ]);
      using (var writer = new NameIndexBuilder(Path.Combine(root, "index")))
      {
        Assert.AreEqual(0, ReferenceListImporter.ImportWhitelist(file, writer));
        writer.Complete();
      }
      using var snapshot = new NameIndexSnapshot("test", Path.Combine(root, "index"));
      Assert.AreEqual(0, snapshot.DocumentCount);
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  private static string TemporaryDirectory()
  {
    string directory = Path.Combine(Path.GetTempPath(), "flix-list-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    return directory;
  }
}
