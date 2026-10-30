using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class TenantScreeningTests
{
  [TestMethod]
  public void TenantListsDoNotLeakBetweenTenants()
  {
    WithHarness((service, _) =>
    {
      var alpha = service.Screen(Request("alpha", "AC998877"));
      var beta = service.Screen(Request("beta", "AC998877"));
      Assert.AreEqual("REVIEW", alpha.Decision);
      Assert.AreEqual("CLEAR", beta.Decision);
      Assert.AreEqual("alpha", alpha.TenantId);
      Assert.AreEqual("beta", beta.TenantId);
    });
  }

  [TestMethod]
  public void GlobalBlacklistAppliesToBothTenants()
  {
    WithHarness((service, _) =>
    {
      Assert.AreEqual("REVIEW", service.Screen(Request("alpha", "ACGLOBAL")).Decision);
      Assert.AreEqual("REVIEW", service.Screen(Request("beta", "ACGLOBAL")).Decision);
    });
  }

  [TestMethod]
  public void UnknownTenantIsNotSilentlyScreenedAsGlobalOnly()
  {
    WithHarness((service, _) =>
    {
      Assert.ThrowsException<ArgumentException>(() => service.Screen(Request("unknown", "AC998877")));
    });
  }

  [TestMethod]
  public void TenantWhitelistDoesNotClearOtherTenantsBlacklist()
  {
    WithHarness((service, _) =>
    {
      var alpha = service.Screen(Request("alpha", "AC778899"));
      var beta = service.Screen(Request("beta", "AC778899"));
      Assert.AreEqual("WHITELISTED", alpha.Decision);
      Assert.AreEqual("CLEAR", beta.Decision);
    });
  }

  [TestMethod]
  public void WhitelistCannotMixGlobalAndTenantCriteria()
  {
    WithHarness((service, _) =>
    {
      var result = service.Screen(new ScreeningRequest("mixed", [
        new ScreeningField("sender", "ACCOUNT", "AC778899"),
        new ScreeningField("sender", "COUNTRY", "CH")
      ], "alpha"));
      Assert.IsFalse(result.WhitelistOverrideApplied);
    });
  }

  private static ScreeningRequest Request(string tenant, string account)
  {
    return new ScreeningRequest("T-" + tenant, [
      new ScreeningField("sender", "ACCOUNT", account)
    ], tenant);
  }

  private static void WithHarness(Action<DecisionService, FilterSnapshot> test)
  {
    string dir = Path.Combine(Path.GetTempPath(), "flix-tenant-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    FilterSnapshot? snapshot = null;
    try
    {
      BuildIndex(Path.Combine(dir, "normal"), [
        new ReferenceFieldRecord("N", "INTERNAL", "COUNTRY", "GB")
      ]);
      BuildIndex(Path.Combine(dir, "blacklist"), [
        new ReferenceFieldRecord("GLOBAL-B", "INTERNAL", "ACCOUNT", "ACGLOBAL")
      ]);
      BuildIndex(Path.Combine(dir, "whitelist"), [
        new ReferenceFieldRecord("GLOBAL-W", "INTERNAL", "COUNTRY", "CH")
      ]);
      foreach (string tenant in new[] { "alpha", "beta" })
      {
        string basePath = Path.Combine(dir, "tenants", tenant);
        BuildIndex(Path.Combine(basePath, "blacklist"), [
          new ReferenceFieldRecord("B-" + tenant, "INTERNAL", "ACCOUNT",
            tenant == "alpha" ? "AC998877" : "AC112233")
        ]);
        BuildIndex(Path.Combine(basePath, "whitelist"), tenant == "alpha"
          ? [new ReferenceFieldRecord("W-alpha", "INTERNAL", "ACCOUNT", "AC778899")]
          : Array.Empty<ReferenceFieldRecord>());
      }
      snapshot = new FilterSnapshot("test", dir);
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
      var service = new DecisionService(new FilterManager(snapshot), strategies,
        new DecisionPolicy(), new ListOverridePolicy());
      test(service, snapshot);
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
