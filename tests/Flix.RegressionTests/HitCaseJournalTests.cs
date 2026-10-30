using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class HitCaseJournalTests
{
  [TestMethod]
  public void ConcurrentAppendsAreAllWrittenAndReadableWhileOpen()
  {
    string root = Path.Combine(Path.GetTempPath(), "flix-hits-" + Guid.NewGuid().ToString("N"));
    string path = Path.Combine(root, "review", "hits.jsonl");
    try
    {
      using (var journal = new HitCaseJournal(path))
      {
        Parallel.For(0, 200, new ParallelOptions { MaxDegreeOfParallelism = 16 },
          i => journal.Append(Hit($"MSG-{i}")));
        // The Hit Reviewer reads while the API still holds the journal open.
        var whileOpen = HitCaseFile.Read(path);
        Assert.AreEqual(200, whileOpen.Count);
        Assert.AreEqual(200, whileOpen.Select(x => x.CaseId).Distinct().Count());
      }
      Assert.AreEqual(200, HitCaseFile.Read(path).Count);
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
  public void ReaderIgnoresALineStillBeingWritten()
  {
    string root = Path.Combine(Path.GetTempPath(), "flix-hits-partial-" + Guid.NewGuid().ToString("N"));
    string path = Path.Combine(root, "hits.jsonl");
    try
    {
      HitCaseFile.Append(path, Hit("MSG-1"));
      File.AppendAllText(path, "{\"CaseId\":\"incomplete");
      var cases = HitCaseFile.Read(path);
      Assert.AreEqual(1, cases.Count);
      Assert.AreEqual("MSG-1", cases[0].Message!.MessageId);
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
  public void InvalidHitIsRejected()
  {
    string root = Path.Combine(Path.GetTempPath(), "flix-hits-invalid-" + Guid.NewGuid().ToString("N"));
    try
    {
      using var journal = new HitCaseJournal(Path.Combine(root, "hits.jsonl"));
      Assert.ThrowsException<InvalidDataException>(() => journal.Append(new HitCase()));
    }
    finally
    {
      if (Directory.Exists(root))
      {
        Directory.Delete(root, true);
      }
    }
  }

  private static HitCase Hit(string messageId)
  {
    var request = new ScreeningRequest(messageId,
      [new ScreeningField("sender", "PERSON_NAME", "Example Person")]);
    var decision = new ScreeningDecision(messageId, "test", "REVIEW", false, "test",
      [], [], [], DateTimeOffset.UtcNow);
    return new HitCase { Message = request, Decision = decision };
  }
}
