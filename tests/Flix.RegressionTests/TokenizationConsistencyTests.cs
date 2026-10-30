using Flix.Core;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flix.RegressionTests;

// The matchers query nameTokens/addressTokens with TermQuery/FuzzyQuery built from
// NameNormalizer tokens. The index must contain exactly those tokens; any analyzer
// behaviour beyond splitting on spaces (stop words, Unicode segmentation) silently
// breaks the token-based retrieval paths.
[TestClass]
public sealed class TokenizationConsistencyTests
{
  public static IEnumerable<object[]> Names =>
  [
    ["Bank of China"],
    ["The Islamic Republic of Iran Shipping Lines"],
    ["To Lam"],
    ["Nguyen Van An"],
    ["Such and Such Trading Is Not A Company"],
    ["习近平"],
    ["金正恩 Kim Jong Un"],
    ["김정은"],
    ["สมชาย วงศ์สวัสดิ์"],
    ["عبد الله بن محمد"],
    ["Лукашенко Александр Григорьевич"],
    ["Al-Qa'ida in the Arabian Peninsula"],
    ["Müller-Lüdenscheid GmbH & Co. KG"],
    ["Group 7 AB 2024"],
    ["25 King Street, London"]
  ];

  [DataTestMethod]
  [DynamicData(nameof(Names))]
  public void AnalyzerTokensEqualNameNormalizerTokens(string value)
  {
    string normalized = NameNormalizer.Normalize(value);
    string[] expected = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    foreach (string field in new[] { "nameTokens", "addressTokens" })
    {
      CollectionAssert.AreEqual(expected, Analyze(field, normalized),
        $"Analyzer output differs from NameNormalizer for '{value}' in {field}.");
    }
  }

  [TestMethod]
  public void StopWordAndCjkTokensAreSearchableInTheIndex()
  {
    string root = Path.Combine(Path.GetTempPath(), "flix-tokens-" + Guid.NewGuid().ToString("N"));
    try
    {
      using (var builder = new NameIndexBuilder(root))
      {
        builder.AddTypedName(new NameRecord("O-1", "TEST", "Bank of China"), "ORGANIZATION_NAME");
        builder.AddTypedName(new NameRecord("P-1", "TEST", "To Lam"), "PERSON_NAME");
        builder.AddTypedName(new NameRecord("P-2", "TEST", "习近平"), "PERSON_NAME");
        builder.AddField(new ReferenceFieldRecord("A-1", "TEST", "ADDRESS", "1 Avenue of the Americas"));
        builder.Complete();
      }
      using var snapshot = new NameIndexSnapshot("test", root);
      Assert.AreEqual("O-1", Single(snapshot, "nameTokens", "of"));
      Assert.AreEqual("P-1", Single(snapshot, "nameTokens", "to"));
      Assert.AreEqual("P-2", Single(snapshot, "nameTokens", "习近平"));
      Assert.AreEqual("A-1", Single(snapshot, "addressTokens", "the"));
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
  public void NewIndexesRecordTheCurrentAlgorithmVersion()
  {
    Assert.AreEqual(NameIndexBuilder.AlgorithmVersion, new NameMatchingOptions().AlgorithmVersion);
  }

  private static string Single(NameIndexSnapshot snapshot, string field, string token)
  {
    var hits = snapshot.Searcher.Search(new TermQuery(new Term(field, token)), 10).ScoreDocs;
    Assert.AreEqual(1, hits.Length, $"Expected exactly one document for {field}:{token}.");
    return snapshot.Searcher.Doc(hits[0].Doc).Get("entityId");
  }

  private static List<string> Analyze(string field, string text)
  {
    using var analyzer = IndexTokenAnalyzer.Create();
    using var stream = analyzer.GetTokenStream(field, text);
    var term = stream.AddAttribute<ICharTermAttribute>();
    var tokens = new List<string>();
    stream.Reset();
    while (stream.IncrementToken())
    {
      tokens.Add(term.ToString());
    }
    stream.End();
    return tokens;
  }
}
