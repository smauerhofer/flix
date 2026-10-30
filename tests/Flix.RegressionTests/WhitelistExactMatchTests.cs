using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

// A whitelist releases payments: it qualifies only on the exact normalized value
// and, by default, never overrides a blacklist hit.
[TestClass]
public sealed class WhitelistExactMatchTests
{
  [TestMethod]
  public void ExactWhitelistedNameIsWhitelisted()
  {
    WithHarness(new ListOverridePolicy(), service =>
    {
      var result = service.Screen(Org("W-1", "Northern Alpine Holdings"));
      Assert.AreEqual("WHITELISTED", result.Decision);
      Assert.IsTrue(result.Sides!.Single().WhitelistHit);
    });
  }

  [DataTestMethod]
  [DataRow("Northern Alpine Holding")]     // one letter missing
  [DataRow("Nothern Alpine Holdings")]     // typo
  [DataRow("Holdings Alpine Northern")]    // re-ordered tokens score 1.0, still not exact
  public void SimilarNameDoesNotQualifyForWhitelist(string value)
  {
    WithHarness(new ListOverridePolicy(), service =>
    {
      var result = service.Screen(Org("W-2", value));
      var side = result.Sides!.Single();
      Assert.IsFalse(side.WhitelistHit, value);
      Assert.AreEqual("REVIEW", result.Decision, value);
      Assert.IsFalse(result.ReleaseAllowed, value);
    });
  }

  [TestMethod]
  public void FormattingDifferencesStillQualify()
  {
    WithHarness(new ListOverridePolicy(), service =>
    {
      var result = service.Screen(Org("W-3", "  NORTHERN-ALPINE  holdings "));
      Assert.AreEqual("WHITELISTED", result.Decision);
    });
  }

  [TestMethod]
  public void ByDefaultWhitelistDoesNotOverrideBlacklist()
  {
    WithHarness(new ListOverridePolicy(), service =>
    {
      var result = service.Screen(Person("W-4", "Mohammed Al Rashid"));
      var side = result.Sides!.Single();
      Assert.IsTrue(side.BlacklistHit);
      Assert.IsTrue(side.WhitelistHit);
      Assert.IsFalse(side.WhitelistOverrideApplied);
      Assert.AreEqual("REVIEW", result.Decision);
    });
  }

  [TestMethod]
  public void ExplicitOptInCanStillOverrideBlacklist()
  {
    WithHarness(new ListOverridePolicy(WhitelistOverridesAll: true), service =>
    {
      Assert.AreEqual("WHITELISTED", service.Screen(Person("W-5", "Mohammed Al Rashid")).Decision);
    });
  }

  [TestMethod]
  public void DefaultParametersAreSafe()
  {
    var policy = new FilterParameters().OverridePolicy;
    Assert.IsFalse(policy.WhitelistOverridesAll);
    Assert.AreEqual(ListPrecedence.BlacklistFirst, policy.Precedence);
    Assert.AreEqual(policy, new ListOverridePolicy() with
    {
      NameThreshold = policy.NameThreshold,
      AddressThreshold = policy.AddressThreshold
    });
  }

  private static ScreeningRequest Org(string id, string name) =>
    new(id, [new ScreeningField("sender", "ORGANIZATION_NAME", name)]);

  private static ScreeningRequest Person(string id, string name) =>
    new(id, [new ScreeningField("sender", "PERSON_NAME", name)]);

  private static void WithHarness(ListOverridePolicy overrides, Action<DecisionService> test)
  {
    string dir = Path.Combine(Path.GetTempPath(), "flix-white-exact-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    FilterSnapshot? snapshot = null;
    try
    {
      BuildIndex(Path.Combine(dir, "normal"), [
        new ReferenceFieldRecord("N-ORG", "SECO", "ORGANIZATION_NAME", "Northern Alpine Holdings"),
        new ReferenceFieldRecord("N-PER", "SECO", "PERSON_NAME", "Mohammed Al Rashid")
      ]);
      BuildIndex(Path.Combine(dir, "blacklist"), [
        new ReferenceFieldRecord("B-PER", "INTERNAL", "PERSON_NAME", "Mohammed Al Rashid")
      ]);
      BuildIndex(Path.Combine(dir, "whitelist"), [
        new ReferenceFieldRecord("W-ORG", "INTERNAL", "ORGANIZATION_NAME", "Northern Alpine Holdings"),
        new ReferenceFieldRecord("W-PER", "INTERNAL", "PERSON_NAME", "Mohammed Al Rashid")
      ]);
      snapshot = new FilterSnapshot("white-exact", dir);
      var fields = new LuceneFieldMatcher();
      var names = new LuceneNameMatcher();
      var strategies = new ScreeningStrategyRegistry([
        new IbanScreeningStrategy(fields),
        new AccountNumberScreeningStrategy(fields),
        new BicScreeningStrategy(fields),
        new CountryScreeningStrategy(fields),
        new AddressScreeningStrategy(fields),
        new PersonNameScreeningStrategy(names),
        new OrganizationNameScreeningStrategy(names),
        new LeiScreeningStrategy(fields)
      ]);
      test(new DecisionService(new FilterManager(snapshot), strategies,
        new DecisionPolicy(), overrides));
    }
    finally
    {
      snapshot?.Dispose();
      Directory.Delete(dir, true);
    }
  }

  private static void BuildIndex(string path, IEnumerable<ReferenceFieldRecord> records)
  {
    using var builder = new NameIndexBuilder(path);
    foreach (var record in records)
    {
      builder.AddField(record);
    }
    builder.Complete();
  }
}
