using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class SnapshotVersionTests
{
  private string _root = string.Empty;

  [TestInitialize]
  public void Setup()
  {
    _root = Path.Combine(Path.GetTempPath(), "flix-versions-" + Guid.NewGuid().ToString("N"));
  }

  [TestCleanup]
  public void Cleanup()
  {
    if (Directory.Exists(_root))
    {
      Directory.Delete(_root, true);
    }
  }

  [TestMethod]
  public void FirstVersionHasDateWithoutSuffix()
  {
    var result = SnapshotVersionGenerator.Reserve(_root, new DateOnly(2026, 10, 9));
    Assert.AreEqual("20261009", result.Version);
    Assert.IsTrue(Directory.Exists(result.DirectoryPath));
  }

  [TestMethod]
  public void LaterVersionsUseThreeDigitSequence()
  {
    var date = new DateOnly(2026, 10, 9);
    Assert.AreEqual("20261009", SnapshotVersionGenerator.Reserve(_root, date).Version);
    Assert.AreEqual("20261009_001", SnapshotVersionGenerator.Reserve(_root, date).Version);
    Assert.AreEqual("20261009_002", SnapshotVersionGenerator.Reserve(_root, date).Version);
  }

  [TestMethod]
  public void UsesHighestExistingSequenceNotNumberOfDirectories()
  {
    string root = Path.Combine(_root, "snapshots");
    Directory.CreateDirectory(Path.Combine(root, "20261009"));
    Directory.CreateDirectory(Path.Combine(root, "20261009_002"));
    Directory.CreateDirectory(Path.Combine(root, "20261009_017"));
    Directory.CreateDirectory(Path.Combine(root, "20261008_999"));
    Directory.CreateDirectory(Path.Combine(root, "v005"));
    Assert.AreEqual("20261009_018",
      SnapshotVersionGenerator.Reserve(_root, new DateOnly(2026, 10, 9)).Version);
  }

  [TestMethod]
  public void NewDayStartsWithoutSuffix()
  {
    SnapshotVersionGenerator.Reserve(_root, new DateOnly(2026, 10, 9));
    Assert.AreEqual("20261010",
      SnapshotVersionGenerator.Reserve(_root, new DateOnly(2026, 10, 10)).Version);
  }

  [TestMethod]
  public void RejectsSequenceExhaustion()
  {
    Directory.CreateDirectory(Path.Combine(_root, "snapshots", "20261009_999"));
    Assert.ThrowsException<InvalidOperationException>(() =>
      SnapshotVersionGenerator.Reserve(_root, new DateOnly(2026, 10, 9)));
  }
}
