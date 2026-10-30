using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class WhitelistGroupTests
{
  private readonly WhitelistGroupMatcher _matcher = new();

  [TestMethod]
  public void CompleteSingleEntryMatchesWithFormattedIdentifiers()
  {
    var fields = new ScreeningField[]
    {
      new("sender", "IBAN", "CH93 0076 2011 6238 5295 7"),
      new("sender", "LEI", "5493001KJTIIGC8Y1R12")
    };
    var matched = new IReadOnlyList<ListFinding>[]
    {
      [Hit("WHITE-A", "RECORD-A", "IBAN", "CH9300762011623852957")],
      [Hit("WHITE-A", "RECORD-A", "LEI", "5493001KJTIIGC8Y1R12")]
    };
    Assert.IsTrue(_matcher.Matches(fields, matched,
      [Group("WHITE-A", "RECORD-A", ("IBAN", "CH9300762011623852957"),
        ("LEI", "5493001KJTIIGC8Y1R12"))]));
  }

  [TestMethod]
  public void MatchesAcrossDifferentWhitelistEntriesDoNotCombine()
  {
    var fields = new ScreeningField[]
    {
      new("receiver", "IBAN", "CH9300762011623852957"),
      new("receiver", "LEI", "5493001KJTIIGC8Y1R12")
    };
    var matched = new IReadOnlyList<ListFinding>[]
    {
      [Hit("WHITE-A", "RECORD-A", "IBAN", "CH9300762011623852957")],
      [Hit("WHITE-B", "RECORD-B", "LEI", "5493001KJTIIGC8Y1R12")]
    };
    Assert.IsFalse(_matcher.Matches(fields, matched,
      [Group("WHITE-A", "RECORD-A", ("IBAN", "CH9300762011623852957")),
       Group("WHITE-B", "RECORD-B", ("LEI", "5493001KJTIIGC8Y1R12"))]));
  }

  [TestMethod]
  public void ReferenceEntryWithUnmatchedExtraFieldDoesNotQualify()
  {
    var fields = new ScreeningField[]
    {
      new("sender", "IBAN", "CH9300762011623852957")
    };
    var matched = new IReadOnlyList<ListFinding>[]
    {
      [Hit("WHITE-A", "RECORD-A", "IBAN", "CH9300762011623852957")]
    };
    Assert.IsFalse(_matcher.Matches(fields, matched,
      [Group("WHITE-A", "RECORD-A", ("IBAN", "CH9300762011623852957"),
        ("LEI", "5493001KJTIIGC8Y1R12"))]));
  }

  [TestMethod]
  public void UnmatchedInputCriterionDoesNotQualify()
  {
    var fields = new ScreeningField[]
    {
      new("sender", "IBAN", "CH9300762011623852957"),
      new("sender", "ACCOUNT", "123456")
    };
    var matched = new IReadOnlyList<ListFinding>[]
    {
      [Hit("WHITE-A", "RECORD-A", "IBAN", "CH9300762011623852957")],
      []
    };
    Assert.IsFalse(_matcher.Matches(fields, matched,
      [Group("WHITE-A", "RECORD-A", ("IBAN", "CH9300762011623852957"))]));
  }

  private static ListFinding Hit(string entity, string source, string type, string value)
  {
    return new ListFinding("WHITELIST", "sender", entity, source, type, value, 1);
  }

  private static WhitelistReferenceGroup Group(string entity, string source,
    params (string Type, string Value)[] criteria)
  {
    return new WhitelistReferenceGroup(entity, source,
      criteria.Select(x => new WhitelistReferenceCriterion(x.Type, x.Value)).ToArray());
  }
}
