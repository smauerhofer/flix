using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class SecoCsvImportTests
{
  [TestMethod]
  public void ImportCsvRejectsLegacyEntityFirstHeader()
  {
    string root = Path.Combine(Path.GetTempPath(), "flix-csv-order-" + Guid.NewGuid().ToString("N"));
    try
    {
      Directory.CreateDirectory(root);
      string csv = Path.Combine(root, "legacy.csv");
      File.WriteAllText(csv, "entityId,sourceId,type,value\n123,SECO,PERSON_NAME,Example\n");
      using var writer = new NameIndexBuilder(Path.Combine(root, "index"));
      Assert.ThrowsException<InvalidDataException>(() => ReferenceListImporter.ImportCsv(csv, writer));
    }
    finally
    {
      if (Directory.Exists(root))
      {
        Directory.Delete(root, true);
      }
    }
  }

  [TestMethod]
  public void ImportCsvPreservesQuotedCommasAndEscapedQuotes()
  {
    string root = Path.Combine(Path.GetTempPath(), "flix-seco-" + Guid.NewGuid().ToString("N"));
    try
    {
      Directory.CreateDirectory(root);
      string csv = Path.Combine(root, "reference.csv");
      File.WriteAllText(csv,
        "sourceId,entityId,type,value\n" +
        "SECO,123,ORGANIZATION_NAME,\"Alpha, Beta Holdings\"\n" +
        "SECO,124,ORGANIZATION_NAME,\"A \"\"Quoted\"\" Name\"\n");
      using (var writer = new NameIndexBuilder(Path.Combine(root, "index")))
      {
        Assert.AreEqual(2, ReferenceListImporter.ImportCsv(csv, writer));
        writer.Complete();
      }
      using var index = new NameIndexSnapshot("test", Path.Combine(root, "index"));
      Assert.AreEqual(2, index.DocumentCount);
    }
    finally
    {
      if (Directory.Exists(root))
      {
        Directory.Delete(root, true);
      }
    }
  }
}
