using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Util;

namespace Flix.Core;

// Analyzer for the tokenized fields (nameTokens, addressTokens).
//
// Those fields are always fed with text that has ALREADY been normalized by
// NameNormalizer (lower-case, diacritics removed, single spaces between tokens),
// and the matchers query them with TermQuery/FuzzyQuery built from the same
// NameNormalizer tokens. The analyzer must therefore only split on spaces:
//  - no stop-word removal (StandardAnalyzer dropped "of", "the", "an", "to", ...),
//  - no Unicode word segmentation (StandardAnalyzer split CJK/Thai names into
//    single characters that a query token could never match),
//  - no further case or character changes.
// Indexed tokens are then byte-identical to NameNormalizer.Normalize(x).Split(' ').
public static class IndexTokenAnalyzer
{
  public static Analyzer Create() => new WhitespaceAnalyzer(LuceneVersion.LUCENE_48);
}
