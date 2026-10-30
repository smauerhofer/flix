using Flix.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

// Latin/Cyrillic look-alike letters inside one token (SECO source typos and
// homoglyph evasion) are repaired identically for reference and incoming values.
[TestClass]
public sealed class HomoglyphNormalizationTests
{
  [DataTestMethod]
  [DataRow("Алексееўнa", "алексееуна")]       // Latin a in a Cyrillic word
  [DataRow("АНАНIЧ", "ананіч")]               // Latin I
  [DataRow("БPЫCIHA", "брысіна")]             // mostly Latin letters, but Б/Ы force Cyrillic
  [DataRow("MITPAXOBIЧ", "мітраховіч")]
  [DataRow("Pаvel", "pavel")]                 // Cyrillic а in a Latin word
  [DataRow("Vlаdimir Putin", "vladimir putin")]
  public void MixedTokensAreRepaired(string value, string expected)
  {
    Assert.AreEqual(expected, NameNormalizer.Normalize(value));
  }

  [DataTestMethod]
  [DataRow("Lukashenka Dzmitry", "lukashenka dzmitry")]
  [DataRow("Лукашенко Дмитрий", "лукашенко дмитрии")]
  [DataRow("Bank Мелли Iran", "bank мелли iran")]   // separate tokens keep their script
  [DataRow("Паvлаўна", "паvлауна")]                 // letters without twins in both scripts
  public void PureOrAmbiguousTokensAreUnchanged(string value, string expected)
  {
    Assert.AreEqual(expected, NameNormalizer.Normalize(value));
  }

  [TestMethod]
  public void HomoglyphSpellingMatchesRealSpelling()
  {
    var evaluator = new NameMatchEvaluator();
    Assert.AreEqual(1.0, evaluator.Score("Vlаdimir Putin", "Vladimir Putin", "PERSON_NAME"));
    Assert.AreEqual(1.0, evaluator.Score("АНАНIЧ Алена", "Ананіч Алена", "PERSON_NAME"));
  }

  [TestMethod]
  public void NormalizationIsIdempotent()
  {
    foreach (string value in new[] { "БPЫCIHA Жанна", "Pаvel", "Паvлаўна", "OAO Белшина" })
    {
      string once = NameNormalizer.Normalize(value);
      Assert.AreEqual(once, NameNormalizer.Normalize(once), value);
    }
  }
}
