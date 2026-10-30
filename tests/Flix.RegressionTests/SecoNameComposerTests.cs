using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

[TestClass]
public sealed class SecoNameComposerTests
{
  [TestMethod]
  public void VariantsAreCombinedPerScriptAndLanguageOnly()
  {
    var result = SecoNameComposer.Compose([
      Part(1, "Lukashenka", ("Lukashenko", "rus"), ("Лукашенко", "rus"), ("Лукашэнка", "bel")),
      Part(2, "Dzmitry", ("Dmitri", "rus"), ("Дмитрий", "rus"), ("Дзмітрый", "bel")),
      Part(3, "Aliaksandravich", ("Aleksandrovich", "rus"), ("Александрович", "rus"), ("Аляксандравіч", "bel"))
    ]);
    CollectionAssert.AreEquivalent(new[]
    {
      "Lukashenka Dzmitry Aliaksandravich",
      "Lukashenko Dmitri Aleksandrovich",
      "Лукашенко Дмитрий Александрович",
      "Лукашэнка Дзмітрый Аляксандравіч"
    }, result.Names.ToArray());
    Assert.AreEqual(0, result.IncompleteVariantSets);
    AssertNoMixedScript(result);
  }

  [TestMethod]
  public void VariantsWithoutLanguageArePairedByPositionNotMultiplied()
  {
    var result = SecoNameComposer.Compose([
      Part(1, "Ivanov", ("Иванов", null), ("Іваноў", null)),
      Part(2, "Ivan", ("Иван", null))
    ]);
    CollectionAssert.AreEquivalent(new[] { "Ivanov Ivan", "Иванов Иван", "Іваноў Иван" },
      result.Names.ToArray());
    AssertNoMixedScript(result);
  }

  [TestMethod]
  public void IncompleteScriptSetIsSkippedInsteadOfMixed()
  {
    var result = SecoNameComposer.Compose([
      Part(1, "Petrov", ("Петров", "rus")),
      Part(2, "Ivan")
    ]);
    CollectionAssert.AreEqual(new[] { "Petrov Ivan" }, result.Names.ToArray());
    Assert.AreEqual(1, result.IncompleteVariantSets);
    AssertNoMixedScript(result);
  }

  [TestMethod]
  public void SameScriptVariantKeepsOtherPrimaryParts()
  {
    var result = SecoNameComposer.Compose([
      Part(1, "Abu", ("Abou", null)),
      Part(2, "Bakr")
    ]);
    CollectionAssert.AreEquivalent(new[] { "Abu Bakr", "Abou Bakr" }, result.Names.ToArray());
  }

  [TestMethod]
  public void PartsAreOrderedByOrderAttribute()
  {
    var result = SecoNameComposer.Compose([
      Part(2, "Ivan", ("Иван", "rus")),
      Part(1, "Petrov", ("Петров", "rus"))
    ]);
    CollectionAssert.AreEqual(new[] { "Petrov Ivan", "Петров Иван" }, result.Names.ToArray());
  }

  [TestMethod]
  public void ScriptNeutralPartsFitAnyScript()
  {
    var result = SecoNameComposer.Compose([
      Part(1, "Bank", ("Банк", "rus")),
      Part(2, "7")
    ]);
    CollectionAssert.AreEquivalent(new[] { "Bank 7", "Банк 7" }, result.Names.ToArray());
  }

  [TestMethod]
  public void LookAlikeLettersDoNotSplitAVariantSet()
  {
    // SECO data: Belarusian Cyrillic family name typed with Latin P, C, I, H, A.
    var result = SecoNameComposer.Compose([
      Part(1, "Brysina", ("БPЫCIHA", "bel")),
      Part(2, "Zhanna", ("Жанна", "bel")),
      Part(3, "Leanidauna", ("Леанiдаўна", "bel"))
    ]);
    Assert.AreEqual(0, result.IncompleteVariantSets);
    CollectionAssert.Contains(result.Names.ToArray(), "БPЫCIHA Жанна Леанiдаўна");
  }

  [TestMethod]
  public void TitleDoesNotBlockOtherScriptAndIsKeptInPublishedName()
  {
    var result = SecoNameComposer.Compose([
      new SecoNamePart(1, "Manager of Al Nasr Detention Center", [], IsOptional: true),
      Part(2, "Al Kuni", ("الكوني", "ara")),
      Part(3, "Osama", ("أسامة", "ara"))
    ]);
    CollectionAssert.AreEqual(new[]
    {
      "Manager of Al Nasr Detention Center Al Kuni Osama",
      "Al Kuni Osama",
      "الكوني أسامة"
    }, result.Names.ToArray());
    Assert.AreEqual(0, result.IncompleteVariantSets);
  }

  [DataTestMethod]
  [DataRow("Lukashenka", "LATIN")]
  [DataRow("Müller", "LATIN")]
  [DataRow("Лукашенко", "CYRILLIC")]
  [DataRow("التكريتي", "ARABIC")]
  [DataRow("习近平", "CJK")]
  [DataRow("김정은", "HANGUL")]
  [DataRow("Al-7", "LATIN")]
  [DataRow("1234", SecoNameComposer.Neutral)]
  [DataRow("Lukashenka Александрович", SecoNameComposer.Mixed)]
  public void DetectScript(string value, string expected)
  {
    Assert.AreEqual(expected, SecoNameComposer.DetectScript(value));
  }

  private static void AssertNoMixedScript(SecoNameComposition result)
  {
    foreach (string name in result.Names)
    {
      Assert.AreNotEqual(SecoNameComposer.Mixed, SecoNameComposer.DetectScript(name), name);
    }
  }

  private static SecoNamePart Part(int order, string value, params (string Value, string? Language)[] variants)
  {
    return new SecoNamePart(order, value,
      variants.Select(x => new SecoSpellingVariant(x.Value, x.Language)).ToArray());
  }
}
