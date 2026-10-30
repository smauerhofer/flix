using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class ExactNameCapacityTests
{
  [TestMethod]
  public void ExactLookupReturnsAllEntitiesDespiteSmallCandidateLimits()
  {
    string root = Path.Combine(Path.GetTempPath(), "flix-exact-" + Guid.NewGuid().ToString("N"));
    try
    {
      using (var builder = new NameIndexBuilder(root))
      {
        for (int i = 0; i < 24; i++)
        {
          builder.AddTypedName(new NameRecord($"E-{i}", "SECO", "John Example"), "PERSON_NAME");
        }
        builder.AddTypedName(new NameRecord("OTHER", "SECO", "John Examples"), "PERSON_NAME");
        builder.Complete();
      }
      using var snapshot = new NameIndexSnapshot("v1", root);
      var matcher = new LuceneNameMatcher(new NameMatchingOptions(
        CandidateLimitPerPath: 1, FinalCandidateLimit: 1), new NameMatchEvaluator());
      var results = matcher.Match(snapshot, "sender", "JOHN EXAMPLE", 1, "PERSON_NAME");
      // All 24 exact entities despite limits of 1; the similar "John Examples"
      // entity may additionally be returned by the fuzzy paths.
      var exact = results.Where(x => x.ReferenceName == "John Example").ToArray();
      Assert.AreEqual(24, exact.Length);
      Assert.AreEqual(24, exact.Select(x => x.EntityId).Distinct().Count());
      Assert.IsTrue(results.All(x => x.SourceId == "SECO"));
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
  public void ExactHitDoesNotSuppressSimilarSpellingsOfOtherEntities()
  {
    string root = Path.Combine(Path.GetTempPath(), "flix-exact-fuzzy-" + Guid.NewGuid().ToString("N"));
    try
    {
      using (var builder = new NameIndexBuilder(root))
      {
        builder.AddTypedName(new NameRecord("EXACT", "SECO", "Mohammed Al Rashid"), "PERSON_NAME");
        builder.AddTypedName(new NameRecord("SIMILAR", "SECO", "Mohamed Al Rashid"), "PERSON_NAME");
        builder.AddTypedName(new NameRecord("OTHER", "SECO", "Jane Example"), "PERSON_NAME");
        builder.Complete();
      }
      using var snapshot = new NameIndexSnapshot("v1", root);
      var results = new LuceneNameMatcher().Match(snapshot, "sender", "Mohammed Al Rashid",
        type: "PERSON_NAME");
      Assert.AreEqual("EXACT", results[0].EntityId);
      Assert.AreEqual(1.0, results[0].Similarity);
      Assert.IsTrue(results.Any(x => x.EntityId == "SIMILAR"),
        "A similar spelling of another entity must be retrieved alongside the exact hit.");
      Assert.AreEqual(1, results.Count(x => x.EntityId == "EXACT"), "Exact hit must not be duplicated.");
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
  public void DefaultOptionsCannotOverflow()
  {
    var options = new NameMatchingOptions();
    Assert.IsFalse(options.CanOverflow);
    Assert.IsTrue(options.FinalCandidateLimit >=
      NameMatchingOptions.MaxRetrievalPaths * options.CandidateLimitPerPath);
  }

  [TestMethod]
  public void CommonNameWithDefaultOptionsDoesNotHitCapacity()
  {
    string root = Path.Combine(Path.GetTempPath(), "flix-common-" + Guid.NewGuid().ToString("N"));
    try
    {
      using (var builder = new NameIndexBuilder(root))
      {
        builder.AddTypedName(new NameRecord("EXACT", "SECO", "Mohammad Ali"), "PERSON_NAME");
        for (int i = 0; i < 1500; i++)
        {
          // Many distinct documents per path: the old 600 final limit overflowed.
          builder.AddTypedName(new NameRecord($"E-{i}", "SECO",
            $"Mohammad Ali {(char)('a' + i % 26)}{(char)('a' + i / 26 % 26)}{i}"), "PERSON_NAME");
          builder.AddTypedName(new NameRecord($"M-{i}", "SECO",
            $"Muhammad Aly Hassan{i}"), "PERSON_NAME");
        }
        builder.Complete();
      }
      using var snapshot = new NameIndexSnapshot("v1", root);
      var matcher = new LuceneNameMatcher();
      var withExact = matcher.Match(snapshot, "sender", "Mohammad Ali", type: "PERSON_NAME");
      Assert.AreEqual("EXACT", withExact[0].EntityId);
      Assert.IsTrue(withExact.Count > 1, "Fuzzy candidates are returned alongside the exact hit.");
      var withoutExact = matcher.Match(snapshot, "sender", "Mohamad Aly", type: "PERSON_NAME");
      Assert.IsTrue(withoutExact.Count > 0, "Common name without exact hit is screened, not rejected.");
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
  public void ExactHitWithSmallFinalLimitTruncatesInsteadOfFailing()
  {
    string root = Path.Combine(Path.GetTempPath(), "flix-exact-trunc-" + Guid.NewGuid().ToString("N"));
    try
    {
      using (var builder = new NameIndexBuilder(root))
      {
        builder.AddTypedName(new NameRecord("EXACT", "SECO", "John Example"), "PERSON_NAME");
        for (int i = 0; i < 12; i++)
        {
          builder.AddTypedName(new NameRecord($"E-{i}", "SECO", $"John Example {i}"), "PERSON_NAME");
        }
        builder.Complete();
      }
      using var snapshot = new NameIndexSnapshot("v1", root);
      var options = new NameMatchingOptions(CandidateLimitPerPath: 100, FinalCandidateLimit: 1);
      Assert.IsTrue(options.CanOverflow);
      var results = new LuceneNameMatcher(options, new NameMatchEvaluator())
        .Match(snapshot, "sender", "John Example", type: "PERSON_NAME");
      Assert.AreEqual("EXACT", results[0].EntityId);
      Assert.AreEqual(2, results.Count, "Exact hit plus fuzzy candidates capped at FinalCandidateLimit.");
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
  public void FuzzyCapacityOverflowReturnsReviewRatherThanErrorOrClear()
  {
    string root = Path.Combine(Path.GetTempPath(), "flix-capacity-" + Guid.NewGuid().ToString("N"));
    try
    {
      foreach (string list in new[] { "normal", "blacklist", "whitelist" })
      {
        using var builder = new NameIndexBuilder(Path.Combine(root, list));
        if (list == "normal")
        {
          for (int i = 0; i < 12; i++)
          {
            builder.AddTypedName(new NameRecord($"E-{i}", "SECO", $"John Example {i}"), "PERSON_NAME");
          }
        }
        builder.Complete();
      }
      using var snapshot = new FilterSnapshot("v1", root);
      var manager = new FilterManager(snapshot);
      var matcher = new LuceneNameMatcher(new NameMatchingOptions(
        CandidateLimitPerPath: 100, FinalCandidateLimit: 1), new NameMatchEvaluator());
      var fields = new LuceneFieldMatcher();
      var registry = new ScreeningStrategyRegistry([
        new IbanScreeningStrategy(fields),
        new AccountNumberScreeningStrategy(fields),
        new BicScreeningStrategy(fields),
        new CountryScreeningStrategy(fields),
        new AddressScreeningStrategy(fields),
        new PersonNameScreeningStrategy(matcher),
        new OrganizationNameScreeningStrategy(matcher),
        new LeiScreeningStrategy(fields)
      ]);
      var service = new DecisionService(manager, registry);
      var result = service.Screen(new ScreeningRequest("CAPACITY", [
        new ScreeningField("sender", "PERSON_NAME", "John Example")
      ]));
      Assert.AreEqual("REVIEW", result.Decision);
      Assert.IsFalse(result.ReleaseAllowed);
      StringAssert.Contains(result.Reason, "capacity");
      Assert.AreEqual("REVIEW", result.Sides!.Single().Decision);
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
