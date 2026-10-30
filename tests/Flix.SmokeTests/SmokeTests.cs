using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.SmokeTests
{
  [TestClass]
  public sealed class SmokeTests
  {
    [TestMethod]
    public void ExistingSmokeScenario()
    {
      string root = Path.Combine(Path.GetTempPath(), "flix-test-" + Guid.NewGuid().ToString("N"));
      try
      {
        string a = Path.Combine(root, "A");
        Directory.CreateDirectory(a);
        string normalA = Path.Combine(a, "normal");
        using (var builder = new NameIndexBuilder(normalA))
        {
          builder.AddName(new NameRecord("E1", "DEMO", "Mohammed Al Rashid"));
          builder.AddField(new ReferenceFieldRecord("E1", "DEMO", "IBAN", "CH9300762011623852957"));
          builder.AddField(new ReferenceFieldRecord("E1", "DEMO", "COUNTRY", "CH"));
          builder.AddField(new ReferenceFieldRecord("E1", "DEMO", "LEI", "5493001KJTIIGC8Y1R12"));
          builder.AddField(new ReferenceFieldRecord("E3", "DEMO", "PERSON_NAME", "Jane Example"));
          builder.AddField(new ReferenceFieldRecord("E4", "DEMO", "ORGANIZATION_NAME", "Sample Holdings Ltd"));
          builder.Complete();
        }
        using (var black = new NameIndexBuilder(Path.Combine(a, "blacklist")))
        {
          black.AddName(new NameRecord("BLACK-1", "INTERNAL", "Mohammed Al Rashid"));
          black.Complete();
        }
        using (var white = new NameIndexBuilder(Path.Combine(a, "whitelist")))
        {
          white.AddName(new NameRecord("WHITE-1", "INTERNAL", "Mohammed Al Rashid"));
          white.Complete();
        }
        var manager = new FilterManager(new FilterSnapshot("A", a));
        var registry = CreateStrategies();
        var decision = new DecisionService(manager, registry,
          new DecisionPolicy(), new ListOverridePolicy());
        var request = new ScreeningRequest("P1", [
          new ScreeningField("creditor", "NAME", "Mohammed Al Rashid"),
          new ScreeningField("creditor", "IBAN", "CH93 0076 2011 6238 5295 7")]);
        using var held = manager.Acquire();
        var result = decision.Screen(request);
        Assert(result.Decision == "REVIEW" && !result.ReleaseAllowed &&
          result.BlacklistMatches!.Count > 0 && !result.WhitelistOverrideApplied,
          "whitelist requires every criterion on receiver side");
        var blacklistFirst = new DecisionService(manager, registry, new DecisionPolicy(),
          new ListOverridePolicy(false, ListPrecedence.BlacklistFirst));
        var blocked = blacklistFirst.Screen(request);
        Assert(blocked.Decision == "REVIEW" && !blocked.ReleaseAllowed,
          "blacklist precedence when global override disabled");
        var crossParty = decision.Screen(new ScreeningRequest("P-CROSS", [
          new ScreeningField("debtor", "NAME", "Mohammed Al Rashid"),
          new ScreeningField("creditor", "IBAN", "CH9300762011623852957") ]));
        Assert(crossParty.Decision == "REVIEW" && !crossParty.ReleaseAllowed,
          "sender blacklist cannot be overridden by receiver whitelist");
        Assert(result.Entities.Count == 1 && result.Entities[0].IdentifierMatched,
          "entity correlation");
        Assert(LeiValidator.IsValid("5493001KJTIIGC8Y1R12"), "valid LEI");
        Assert(!LeiValidator.IsValid("5493001KJTIIGC8Y1R13"), "invalid LEI checksum");
        var leiResult = decision.Screen(new ScreeningRequest("P-LEI", [
          new ScreeningField("creditor", "LEI", "5493001KJTIIGC8Y1R12") ]));
        Assert(leiResult.FieldMatches.Any(x => x.FieldType == "LEI"), "LEI exact retrieval");
        var personResult = decision.Screen(new ScreeningRequest("P-PERSON", [
          new ScreeningField("creditor", "PERSON_NAME", "Jane Example") ]));
        Assert(personResult.NameMatches.Any(x => x.EntityId == "E3"), "person strategy");
        var hybrid = new NameMatchEvaluator();
        Assert(hybrid.Score("Al Rashid Mohammed", "Mohammed Al Rashid", "PERSON_NAME") > .75,
          "reordered person-name tokens");
        Assert(hybrid.Score("Al Rashid Mohammed", "Mohammed Al Rashid", "PERSON_NAME") == 1,
          "exact token permutation");
        Assert(hybrid.Score("John John Smith", "John Smith Smith", "PERSON_NAME") < 1,
          "token multiplicity preserved");
        Assert(hybrid.Features("Stephen Smith", "Steven Smith").PhoneticSimilarity > 0,
          "phonetic comparison");
        Assert(NameNGrams.Generate("mohammed").Contains("moh"), "trigram generation");
        Assert(hybrid.Features("Mohammed", "Mohamed").NGramSimilarity > 0,
          "n-gram comparison");
        var retrieval = new LuceneNameMatcher();
        using (var active = manager.Acquire())
        {
          Assert(retrieval.Match(active.Snapshot.Normal, "creditor", "Al Rashid Mohammed")
            .Any(x => x.EntityId == "E1"), "hybrid candidate retrieval");
        }
        var orgResult = decision.Screen(new ScreeningRequest("P-ORG", [
          new ScreeningField("creditor", "ORGANIZATION_NAME", "Sample Holdings Ltd") ]));
        Assert(orgResult.NameMatches.Any(x => x.EntityId == "E4"), "organization strategy");
        var clear = decision.Screen(new ScreeningRequest("P2", [
          new ScreeningField("debtor", "NAME", "Entirely Unrelated Name Zzz") ]));
        Assert(clear.Decision == "CLEAR", "baseline non-hit");
        string b = Path.Combine(root, "B");
        Directory.CreateDirectory(b);
        using (var builder = new NameIndexBuilder(Path.Combine(b, "normal")))
        {
          builder.AddName(new NameRecord("E2", "DEMO", "Jane Doe"));
          builder.Complete();
        }
        using (var black = new NameIndexBuilder(Path.Combine(b, "blacklist")))
        {
          black.Complete();
        }
        using (var white = new NameIndexBuilder(Path.Combine(b, "whitelist")))
        {
          white.Complete();
        }
        manager.Activate(new FilterSnapshot("B", b));
        Assert(manager.CleanupRetired() == 0, "leased snapshot retained");
        held.Dispose();
        Assert(manager.CleanupRetired() == 1, "management cleanup");
        Console.WriteLine("PASS: name, identifier, entity correlation, release gate and retirement");
      }
      finally { try { Directory.Delete(root, true); } catch { } }
    }
    private static ScreeningStrategyRegistry CreateStrategies()
    {
      var names = new LuceneNameMatcher();
      var fields = new LuceneFieldMatcher();
      IScreeningStrategy[] strategies = [
        new IbanScreeningStrategy(fields),
        new AccountNumberScreeningStrategy(fields),
        new BicScreeningStrategy(fields),
        new CountryScreeningStrategy(fields),
        new AddressScreeningStrategy(fields),
        new PersonNameScreeningStrategy(names),
        new OrganizationNameScreeningStrategy(names),
        new LeiScreeningStrategy(fields)
      ];
      return new ScreeningStrategyRegistry(strategies);
    }

    private static void Assert(bool success, string name)
    {
      if (!success)
      {
        throw new Exception("FAIL: " + name);
      }
    }
  }
}
