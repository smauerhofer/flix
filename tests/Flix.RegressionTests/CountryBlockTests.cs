using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

// Business requirement: a blacklisted COUNTRY shuts down the whole country and
// ALWAYS wins over any whitelist, regardless of WhitelistOverridesAll/precedence.
[TestClass]
public sealed class CountryBlockTests
{
  [TestMethod]
  public void GlobalCountryBlockBeatsCompleteWhitelistGroup()
  {
    WithHarness(service =>
    {
      // ACC-WHITE + KP is a complete whitelist group, so without the block this
      // side would be WHITELISTED.
      var result = service.Screen(new ScreeningRequest("CB-1", [
        new ScreeningField("sender", "ACCOUNT", "ACCWHITE"),
        new ScreeningField("sender", "COUNTRY", "kp")
      ], "alpha"));
      var side = result.Sides!.Single();
      Assert.AreEqual("REVIEW", result.Decision);
      Assert.IsFalse(result.ReleaseAllowed);
      Assert.IsTrue(side.WhitelistHit);
      Assert.IsFalse(side.WhitelistOverrideApplied);
      Assert.IsTrue(side.CountryBlockHit);
      Assert.IsFalse(result.WhitelistOverrideApplied);
      StringAssert.Contains(result.Reason, "Blocked country");
    });
  }

  [TestMethod]
  public void TenantCountryBlockBeatsWhitelist()
  {
    WithHarness(service =>
    {
      var result = service.Screen(new ScreeningRequest("CB-2", [
        new ScreeningField("receiver", "ACCOUNT", "ACCTENANT"),
        new ScreeningField("receiver", "COUNTRY", "IR")
      ], "alpha"));
      Assert.AreEqual("REVIEW", result.Decision);
      Assert.IsTrue(result.Sides!.Single().CountryBlockHit);
      Assert.IsFalse(result.Sides!.Single().WhitelistOverrideApplied);
    });
  }

  [TestMethod]
  public void TenantCountryBlockDoesNotLeakToOtherTenant()
  {
    WithHarness(service =>
    {
      var result = service.Screen(new ScreeningRequest("CB-3", [
        new ScreeningField("receiver", "COUNTRY", "IR")
      ], "beta"));
      Assert.AreEqual("CLEAR", result.Decision);
      Assert.IsFalse(result.Sides!.Single().CountryBlockHit);
    });
  }

  [TestMethod]
  public void CountryBlockOnOneSideHoldsMessageButDoesNotMarkOtherSide()
  {
    WithHarness(service =>
    {
      var result = service.Screen(new ScreeningRequest("CB-4", [
        new ScreeningField("sender", "COUNTRY", "CH"),
        new ScreeningField("receiver", "COUNTRY", "KP")
      ], "alpha"));
      Assert.AreEqual("REVIEW", result.Decision);
      Assert.IsFalse(result.Sides!.Single(x => x.Side == "SENDER").CountryBlockHit);
      Assert.IsTrue(result.Sides!.Single(x => x.Side == "RECEIVER").CountryBlockHit);
    });
  }

  [TestMethod]
  public void NonCountryBlacklistHitCanStillBeWhitelisted()
  {
    // Existing behaviour for non-country blacklist entries is unchanged.
    WithHarness(service =>
    {
      var result = service.Screen(new ScreeningRequest("CB-5", [
        new ScreeningField("sender", "ACCOUNT", "ACCBOTH")
      ], "alpha"));
      Assert.AreEqual("WHITELISTED", result.Decision);
      Assert.IsFalse(result.Sides!.Single().CountryBlockHit);
    });
  }

  [TestMethod]
  public void NormalListCountryIsEvidenceOnly()
  {
    WithHarness(service =>
    {
      var result = service.Screen(new ScreeningRequest("CB-6", [
        new ScreeningField("sender", "COUNTRY", "GB")
      ], "alpha"));
      Assert.AreEqual("CLEAR", result.Decision);
      Assert.IsFalse(result.Sides!.Single().NormalHit);
      Assert.IsTrue(result.FieldMatches.Any(x => x.FieldType == "COUNTRY"));
    });
  }

  [DataTestMethod]
  [DataRow("PRK")]
  [DataRow("North Korea")]
  [DataRow("K1")]
  public void UnrecognisedIncomingCountryRequiresReview(string value)
  {
    WithHarness(service =>
    {
      var result = service.Screen(new ScreeningRequest("CB-7", [
        new ScreeningField("sender", "COUNTRY", value)
      ], "alpha"));
      Assert.AreEqual("REVIEW", result.Decision);
      Assert.AreEqual("INVALID_COUNTRY", result.ValidationFindings!.Single().Code);
    });
  }

  [DataTestMethod]
  [DataRow("PRK")]
  [DataRow("Korea")]
  [DataRow("")]
  public void BlacklistImportRejectsInvalidCountryCode(string value)
  {
    var entries = new List<BlacklistEntry>
    {
      new() { Id = "E1", EntityId = "EMB-1", SourceId = "EMBARGO", Type = "COUNTRY", Value = value }
    };
    Assert.ThrowsException<InvalidDataException>(() =>
      ReferenceListValidation.ValidateBlacklist(entries));
  }

  private static void WithHarness(Action<DecisionService> test)
  {
    string dir = Path.Combine(Path.GetTempPath(), "flix-country-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    FilterSnapshot? snapshot = null;
    try
    {
      BuildIndex(Path.Combine(dir, "normal"), [
        new ReferenceFieldRecord("N-1", "SECO", "PERSON_NAME", "Example Listed Person"),
        new ReferenceFieldRecord("N-1", "SECO", "COUNTRY", "GB")
      ]);
      BuildIndex(Path.Combine(dir, "blacklist"), [
        new ReferenceFieldRecord("EMB-KP", "EMBARGO", "COUNTRY", "KP"),
        new ReferenceFieldRecord("B-ACC", "INTERNAL", "ACCOUNT", "ACCBOTH")
      ]);
      BuildIndex(Path.Combine(dir, "whitelist"), [
        new ReferenceFieldRecord("W-1", "INTERNAL", "ACCOUNT", "ACCWHITE"),
        new ReferenceFieldRecord("W-1", "INTERNAL", "COUNTRY", "KP"),
        new ReferenceFieldRecord("W-2", "INTERNAL", "ACCOUNT", "ACCBOTH")
      ]);
      foreach (string tenant in new[] { "alpha", "beta" })
      {
        string basePath = Path.Combine(dir, "tenants", tenant);
        BuildIndex(Path.Combine(basePath, "blacklist"), tenant == "alpha"
          ? [new ReferenceFieldRecord("EMB-IR", "TENANT-EMBARGO", "COUNTRY", "IR")]
          : Array.Empty<ReferenceFieldRecord>());
        BuildIndex(Path.Combine(basePath, "whitelist"), tenant == "alpha"
          ? [
              new ReferenceFieldRecord("TW-1", "INTERNAL", "ACCOUNT", "ACCTENANT"),
              new ReferenceFieldRecord("TW-1", "INTERNAL", "COUNTRY", "IR")
            ]
          : Array.Empty<ReferenceFieldRecord>());
      }
      snapshot = new FilterSnapshot("country-test", dir);
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
      // Most permissive whitelist settings: the country block must still win.
      var service = new DecisionService(new FilterManager(snapshot), strategies,
        new DecisionPolicy(), new ListOverridePolicy(
          WhitelistOverridesAll: true, Precedence: ListPrecedence.WhitelistFirst));
      test(service);
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
