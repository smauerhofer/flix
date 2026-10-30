using Flix.Core;
using Flix.Optimizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class ScreeningRegressionTests
{
  [TestMethod]
  public void GeneticOptimizerFindsZeroFalseNegativesOnRealIndex()
  {
    var cases = new OptimizationCase[]
    {
      new(new ScreeningRequest("POS-IBAN", [
        new ScreeningField("beneficiary", "IBAN", "CH93 0076 2011 6238 5295 7")]), true),
      new(new ScreeningRequest("NEG-IBAN", [
        new ScreeningField("beneficiary", "IBAN", "GB82WEST12345698765432")]), false)
    };
    var optimizer = new GeneticParameterOptimizer(_decisions, cases,
      new FilterParameters(), new ThresholdBounds(
        new FilterParameters { NameReviewThreshold = 0.0, AddressReviewThreshold = 0.0, ListNameThreshold = 0.0, ListAddressThreshold = 0.0 },
        new FilterParameters { NameReviewThreshold = 1.0, AddressReviewThreshold = 1.0, ListNameThreshold = 1.0, ListAddressThreshold = 1.0 }),
      new OptimizationSettings(8, 4, 13));
    OptimizationResult? winner = optimizer.Optimize();
    Assert.IsNotNull(winner);
    Assert.AreEqual(0, winner.FalseNegatives);
    Assert.AreEqual(0, winner.FalsePositives);
  }

  private static string _root = string.Empty;
  private static FilterManager _manager = null!;
  private static ScreeningStrategyRegistry _strategies = null!;
  private static DecisionService _decisions = null!;

  [ClassInitialize]
  public static void Initialize(TestContext context)
  {
    _root = Path.Combine(Path.GetTempPath(), "flix-regression-" + Guid.NewGuid().ToString("N"));
    string versionRoot = Path.Combine(_root, "regression-v1");
    Directory.CreateDirectory(versionRoot);

    using (var normal = new NameIndexBuilder(Path.Combine(versionRoot, "normal")))
    {
      // 8 field types x 200 synthetic records = 1,600 records before anchors.
      for (int i = 0; i < 200; i++)
      {
        string id = $"SYN-{i:D4}";
        normal.AddTypedName(new NameRecord(id + "-P", "TEST", $"Zorvani Person {i:D4}"), "PERSON_NAME");
        normal.AddTypedName(new NameRecord(id + "-O", "TEST", $"Tenvora Industries {i:D4}"), "ORGANIZATION_NAME");
        normal.AddField(new ReferenceFieldRecord(id + "-I", "TEST", "IBAN", CreateGermanIban(i)));
        normal.AddField(new ReferenceFieldRecord(id + "-A", "TEST", "ACCOUNT", $"XQ9{i:D7}"));
        normal.AddField(new ReferenceFieldRecord(id + "-B", "TEST", "BIC", $"XYZADE{i:D2}"));
        normal.AddField(new ReferenceFieldRecord(id + "-C", "TEST", "COUNTRY", "FR"));
        normal.AddField(new ReferenceFieldRecord(id + "-D", "TEST", "ADDRESS", $"{i + 400} Zorvani Avenue, Fictiontown"));
        normal.AddField(new ReferenceFieldRecord(id + "-L", "TEST", "LEI", CreateLei(i)));
      }

      normal.AddTypedName(new NameRecord("P-ANCHOR", "TEST", "Mohammed Al Rashid"), "PERSON_NAME");
      normal.AddTypedName(new NameRecord("O-ANCHOR", "TEST", "Northern Alpine Holdings Limited"), "ORGANIZATION_NAME");
      normal.AddField(new ReferenceFieldRecord("I-ANCHOR", "TEST", "IBAN", "CH9300762011623852957"));
      normal.AddField(new ReferenceFieldRecord("A-ANCHOR", "TEST", "ACCOUNT", "AC123456789"));
      normal.AddField(new ReferenceFieldRecord("B-ANCHOR", "TEST", "BIC", "DEUTDEFF"));
      normal.AddField(new ReferenceFieldRecord("C-ANCHOR", "TEST", "COUNTRY", "GB"));
      normal.AddField(new ReferenceFieldRecord("D-ANCHOR", "TEST", "ADDRESS", "25 King Street London"));
      normal.AddField(new ReferenceFieldRecord("L-ANCHOR", "TEST", "LEI", "5493001KJTIIGC8Y1R12"));
      normal.Complete();
    }

    using (var blacklist = new NameIndexBuilder(Path.Combine(versionRoot, "blacklist")))
    {
      blacklist.AddField(new ReferenceFieldRecord("BLACK-IBAN", "TEST", "IBAN", "CH9300762011623852957"));
      blacklist.Complete();
    }
    using (var whitelist = new NameIndexBuilder(Path.Combine(versionRoot, "whitelist")))
    {
      whitelist.AddField(new ReferenceFieldRecord("WHITE-LEI", "TEST", "LEI", "5493001KJTIIGC8Y1R12"));
      whitelist.Complete();
    }

    _manager = new FilterManager(new FilterSnapshot("regression-v1", versionRoot));
    var names = new LuceneNameMatcher();
    var fields = new LuceneFieldMatcher();
    _strategies = new ScreeningStrategyRegistry([
      new IbanScreeningStrategy(fields),
      new AccountNumberScreeningStrategy(fields),
      new BicScreeningStrategy(fields),
      new CountryScreeningStrategy(fields),
      new AddressScreeningStrategy(fields),
      new PersonNameScreeningStrategy(names),
      new OrganizationNameScreeningStrategy(names),
      new LeiScreeningStrategy(fields)
    ]);
    _decisions = new DecisionService(_manager, _strategies, new DecisionPolicy(),
      new ListOverridePolicy());
  }

  [ClassCleanup]
  public static void Cleanup()
  {
    // The active snapshot is not retired/disposable via FilterManager.
    // Release Lucene handles at the end of the test fixture only.
    if (_root.Length > 0)
    {
      // Retire the active snapshot by activating a minimal successor.
      string next = Path.Combine(_root, "cleanup");
      Directory.CreateDirectory(next);
      foreach (string type in new[] { "normal", "blacklist", "whitelist" })
      {
        using var builder = new NameIndexBuilder(Path.Combine(next, type));
        if (type == "normal")
        {
          builder.AddName(new NameRecord("CLEANUP", "TEST", "Cleanup Sentinel"));
        }
        builder.Complete();
      }
      var successor = new FilterSnapshot("cleanup", next);
      _manager.Activate(successor);
      _manager.CleanupRetired();
      // Manager owns the active snapshot until service shutdown.
      successor.Dispose();
      try
      {
        Directory.Delete(_root, true);
      }
      catch (IOException)
      {
        // Windows Lucene file handles can be released after the process exits.
      }
    }
  }

  [TestMethod]
  public void WhitelistRequiresEveryCriterionOnSameSide()
  {
    var result = _decisions.Screen(new ScreeningRequest("SIDE-ALL", [
      new ScreeningField("receiver", "LEI", "5493001KJTIIGC8Y1R12"),
      new ScreeningField("receiver", "IBAN", "CH9300762011623852957")
    ]));
    Assert.AreEqual("REVIEW", result.Decision);
    Assert.IsFalse(result.ReleaseAllowed);
    var side = result.Sides!.Single(x => x.Side == "RECEIVER");
    Assert.AreEqual(2, side.FieldCount);
    Assert.AreEqual(1, side.WhitelistMatchedFieldCount);
    Assert.IsFalse(side.WhitelistHit);
    Assert.IsTrue(side.BlacklistHit);
  }

  [TestMethod]
  public void WhitelistOnReceiverDoesNotClearSenderBlacklist()
  {
    var result = _decisions.Screen(new ScreeningRequest("SIDE-CROSS", [
      new ScreeningField("sender", "IBAN", "CH9300762011623852957"),
      new ScreeningField("receiver", "LEI", "5493001KJTIIGC8Y1R12")
    ]));
    Assert.AreEqual("REVIEW", result.Decision);
    Assert.IsFalse(result.ReleaseAllowed);
    Assert.AreEqual("REVIEW", result.Sides!.Single(x => x.Side == "SENDER").Decision);
    Assert.AreEqual("WHITELISTED", result.Sides!.Single(x => x.Side == "RECEIVER").Decision);
  }

  [TestMethod]
  public void AllFieldsWhitelistedOnReceiverClearsReceiverOnly()
  {
    var result = _decisions.Screen(new ScreeningRequest("SIDE-WHITE", [
      new ScreeningField("beneficiary", "LEI", "5493001KJTIIGC8Y1R12")
    ]));
    Assert.AreEqual("WHITELISTED", result.Decision);
    Assert.IsTrue(result.ReleaseAllowed);
    Assert.IsTrue(result.Sides!.Single().WhitelistHit);
  }

  [TestMethod]
  public void NormalListHitsWhenAnyCriterionMatchesOnSender()
  {
    // Normal-list COUNTRY is evidence only, so use a BIC as the single qualifying criterion.
    var result = _decisions.Screen(new ScreeningRequest("SIDE-NORMAL", [
      new ScreeningField("debtor", "BIC", "DEUTDEFF"),
      new ScreeningField("debtor", "ACCOUNT", "UNRELATED")
    ]));
    Assert.AreEqual("REVIEW", result.Decision);
    Assert.IsTrue(result.Sides!.Single().NormalHit);
  }

  [TestMethod]
  public void IndexContainsAtLeast1600RecordsAcrossEightTypes()
  {
    using var lease = _manager.Acquire();
    Assert.IsTrue(lease.Snapshot.Normal.DocumentCount >= 1600);
    Assert.AreEqual(1, lease.Snapshot.Blacklist.DocumentCount);
    Assert.AreEqual(1, lease.Snapshot.Whitelist.DocumentCount);
  }

  [DataTestMethod]
  [DataRow("CH9300762011623852957")]
  [DataRow("CH93 0076 2011 6238 5295 7")]
  [DataRow("ch93-0076-2011-6238-5295-7")]
  [DataRow(" ch93 - 0076 - 2011 - 6238 - 5295 - 7 ")]
  public void IbanAcceptsFormattingVariants(string value)
  {
    AssertMatch("IBAN", value, "I-ANCHOR");
    Assert.IsTrue(IbanValidator.IsValid(value), "IBAN validator should allow cosmetic grouping separators.");
  }

  [DataTestMethod]
  [DataRow("AC123456789")]
  [DataRow("ac 123 456 789")]
  [DataRow("AC-123-456-789")]
  public void AccountNumberMatchesFormattingVariants(string value)
  {
    AssertMatch("ACCOUNT", value, "A-ANCHOR");
  }

  [DataTestMethod]
  [DataRow("DEUTDEFF")]
  [DataRow("deutdeff")]
  [DataRow("DEUT-DEFF")]
  [DataRow("DEUT DEFF")]
  public void BicMatchesFormattingVariants(string value)
  {
    AssertMatch("BIC", value, "B-ANCHOR");
  }

  [DataTestMethod]
  [DataRow("GB")]
  [DataRow("gb")]
  [DataRow(" GB ")]
  public void CountryMatchesCodeVariants(string value)
  {
    AssertMatch("COUNTRY", value, "C-ANCHOR");
  }

  [DataTestMethod]
  [DataRow("5493001KJTIIGC8Y1R12")]
  [DataRow("5493 001K JTI IGC8 Y1R12")]
  [DataRow("5493-001K-JTIIGC8-Y1R12")]
  [DataRow("5493001kjtiigc8y1r12")]
  public void LeiMatchesFormattingVariants(string value)
  {
    AssertMatch("LEI", value, "L-ANCHOR");
    Assert.IsTrue(LeiValidator.IsValid(value));
  }

  [DataTestMethod]
  [DataRow("25 King Street London")]
  [DataRow("25 KING STREET LONDON")]
  [DataRow("25 King-Street, London")]
  public void AddressMatchesFormattingVariants(string value)
  {
    AssertMatch("ADDRESS", value, "D-ANCHOR");
  }

  [DataTestMethod]
  [DataRow("Mohammed Al Rashid")]
  [DataRow("Al Rashid Mohammed")]
  [DataRow("mohammed al-rashid")]
  public void PersonNameMatchesWritingStyles(string value)
  {
    AssertMatch("PERSON_NAME", value, "P-ANCHOR");
  }

  [DataTestMethod]
  [DataRow("Northern Alpine Holdings Limited")]
  [DataRow("Holdings Northern Alpine Limited")]
  [DataRow("NORTHERN ALPINE HOLDINGS LIMITED")]
  public void OrganizationNameMatchesWritingStyles(string value)
  {
    AssertMatch("ORGANIZATION_NAME", value, "O-ANCHOR");
  }

  [TestMethod]
  public void InvalidIdentifiersDoNotMatchAndLeiFailsValidation()
  {
    Assert.IsFalse(IbanValidator.IsValid("CH9300762011623852958"));
    Assert.IsFalse(LeiValidator.IsValid("5493001KJTIIGC8Y1R13"));
    Assert.IsFalse(IbanValidator.IsValid("CH93.0076.2011.6238.5295.7"));
    AssertNoMatch("IBAN", "CH9300762011623852958", "I-ANCHOR");
    AssertNoMatch("BIC", "DEUTDEFG", "B-ANCHOR");
    AssertNoMatch("ACCOUNT", "AC123456788", "A-ANCHOR");
    AssertNoMatch("LEI", "5493001KJTIIGC8Y1R13", "L-ANCHOR");
  }

  [TestMethod]
  public void InvalidLeiPreservesNormalCountryMatchAndRequiresReview()
  {
    var result = _decisions.Screen(new ScreeningRequest("INVALID-LEI", [
      new ScreeningField("sender", "LEI", "UNRELATED"),
      new ScreeningField("sender", "COUNTRY", "GB")
    ]));
    Assert.AreEqual("REVIEW", result.Decision);
    Assert.IsFalse(result.ReleaseAllowed);
    // The country is still reported as evidence but is no longer a normal-list hit;
    // the REVIEW comes from the invalid LEI.
    Assert.IsFalse(result.Sides!.Single().NormalHit);
    Assert.IsTrue(result.FieldMatches.Any(x => x.FieldType == "COUNTRY"));
    Assert.AreEqual("INVALID_LEI", result.ValidationFindings!.Single().Code);
  }

  [TestMethod]
  public void InvalidIbanPreservesBlacklistOrNormalNameEvidence()
  {
    var result = _decisions.Screen(new ScreeningRequest("INVALID-IBAN", [
      new ScreeningField("receiver", "IBAN", "CH9300762011623852958"),
      new ScreeningField("receiver", "PERSON_NAME", "Mohammed Al Rashid")
    ]));
    Assert.AreEqual("REVIEW", result.Decision);
    Assert.IsFalse(result.ReleaseAllowed);
    Assert.IsTrue(result.NameMatches.Any(x => x.EntityId == "P-ANCHOR"));
    Assert.AreEqual("INVALID_IBAN", result.ValidationFindings!.Single().Code);
    Assert.IsFalse(result.WhitelistOverrideApplied);
  }

  [TestMethod]
  public void InvalidIdentifierOnWhitelistedSidePreventsOverride()
  {
    var result = _decisions.Screen(new ScreeningRequest("INVALID-WHITE", [
      new ScreeningField("receiver", "LEI", "5493001KJTIIGC8Y1R12"),
      new ScreeningField("receiver", "IBAN", "INVALID")
    ]));
    Assert.AreEqual("REVIEW", result.Decision);
    Assert.IsFalse(result.ReleaseAllowed);
    Assert.IsFalse(result.Sides!.Single().WhitelistHit);
    Assert.AreEqual("INVALID_IBAN", result.ValidationFindings!.Single().Code);
  }

  [TestMethod]
  public void TwoInvalidIdentifiersProduceTwoFindingsWithoutThrowing()
  {
    var result = _decisions.Screen(new ScreeningRequest("TWO-INVALID", [
      new ScreeningField("sender", "LEI", "BAD-LEI"),
      new ScreeningField("sender", "IBAN", "BAD-IBAN")
    ]));
    Assert.AreEqual("REVIEW", result.Decision);
    Assert.IsFalse(result.ReleaseAllowed);
    CollectionAssert.AreEquivalent(
      new[] { "INVALID_LEI", "INVALID_IBAN" },
      result.ValidationFindings!.Select(x => x.Code).ToArray());
  }

  [TestMethod]
  public void WhitelistAndBlacklistRemainSeparateFromNormalIndex()
  {
    using var lease = _manager.Acquire();
    var iban = new ScreeningField("creditor", "IBAN", "CH9300762011623852957");
    Assert.IsTrue(_strategies.Match(lease.Snapshot.Normal, iban).Fields.Any(x => x.EntityId == "I-ANCHOR"));
    Assert.IsTrue(_strategies.Match(lease.Snapshot.Blacklist, iban).Fields.Any(x => x.EntityId == "BLACK-IBAN"));
    Assert.AreEqual(0, _strategies.Match(lease.Snapshot.Whitelist, iban).Fields.Count);

    var combined = _decisions.Screen(new ScreeningRequest("TEST-MIXED", [
      iban, new ScreeningField("creditor", "LEI", "5493001KJTIIGC8Y1R12")
    ]));
    // Individual list evidence remains visible, but a partial whitelist
    // match cannot override a blacklist hit on this receiver.
    Assert.IsTrue(combined.BlacklistMatches!.Any());
    Assert.IsTrue(combined.WhitelistMatches!.Any());
    Assert.IsFalse(combined.WhitelistOverrideApplied);
    Assert.AreEqual("REVIEW", combined.Decision);
    Assert.IsFalse(combined.ReleaseAllowed);

    var receiver = combined.Sides!.Single(x => x.Side == "RECEIVER");
    Assert.IsTrue(receiver.BlacklistHit);
    Assert.IsFalse(receiver.WhitelistHit);
    Assert.AreEqual("REVIEW", receiver.Decision);
  }

  [TestMethod]
  public void GeneratedIdentifierSamplesCanBeFound()
  {
    foreach (int number in new[] { 0, 17, 88, 199 })
    {
      AssertMatch("IBAN", CreateGermanIban(number), $"SYN-{number:D4}-I");
      AssertMatch("LEI", CreateLei(number), $"SYN-{number:D4}-L");
      AssertMatch("ACCOUNT", $"XQ9{number:D7}", $"SYN-{number:D4}-A");
    }
  }

  private static void AssertMatch(string type, string value, string expectedEntityId)
  {
    StrategyMatches matches = Match(type, value);
    bool found = matches.Names.Any(x => x.EntityId == expectedEntityId) ||
      matches.Fields.Any(x => x.EntityId == expectedEntityId);
    Assert.IsTrue(found, $"Expected {type} '{value}' to match entity {expectedEntityId}.");
  }

  private static void AssertNoMatch(string type, string value, string entityId)
  {
    StrategyMatches matches = Match(type, value);
    Assert.IsFalse(matches.Names.Any(x => x.EntityId == entityId) ||
      matches.Fields.Any(x => x.EntityId == entityId));
  }

  private static StrategyMatches Match(string type, string value)
  {
    using var lease = _manager.Acquire();
    return _strategies.Match(lease.Snapshot.Normal, new ScreeningField("creditor", type, value));
  }

  private static string CreateGermanIban(int value)
  {
    string bban = $"37040044{value:D10}";
    string converted = bban + "131400";
    int remainder = 0;
    foreach (char digit in converted)
    {
      remainder = (remainder * 10 + (digit - '0')) % 97;
    }
    return $"DE{98 - remainder:D2}{bban}";
  }

  private static string CreateLei(int value)
  {
    string prefix = $"529900{value:D12}";
    int remainder = 0;
    foreach (char digit in prefix + "00")
    {
      remainder = (remainder * 10 + (digit - '0')) % 97;
    }
    return prefix + (98 - remainder).ToString("D2");
  }
}
