# flix: hybrid local name matching

This build adds separate exact full-name, token, fuzzy-token, character-trigram and English-oriented Soundex retrieval paths. Their Lucene hits are unioned by document ID, then ranked using Jaro-Winkler, adjacent-transposition edit distance, one-to-one token alignment, trigram Dice overlap and phonetic token overlap. Person and organization names use separate illustrative feature weights. Aliases remain separate indexed documents.

**Important limitations:** The algorithms and thresholds have not been calibrated on real sanctions examples. Soundex is English-oriented, not a multilingual transliteration engine; no automatic non-Latin script transliteration has been added. Short tokens, missing components, and language-dependent aliases need labeled regression testing. Bounded queries can truncate results within each path; measure candidate recall on representative cases before authorizing payment release. A whitelist match still has the previously requested message-wide override; this is unsafe as an automatic legal exemption and must be independently controlled.

Matching algorithm version: `hybrid-name-v2` (stored per document in `matchingAlgorithm`). v2 indexes the tokenized fields `nameTokens` and `addressTokens` with `IndexTokenAnalyzer`, a whitespace-only analyzer, so indexed tokens are exactly the `NameNormalizer` tokens the matchers query with. v1 used Lucene's `StandardAnalyzer`, which removed English stop words (of, the, an, to, …) and split CJK/Thai names into single characters, so the token, all-token and fuzzy paths could never match those tokens. Snapshots built before v2 must be rebuilt with `import-bundle`; older indexes also lack the `nameNGrams` and `namePhonetic` fields of v1. Candidate scoring uses the original stored alias. Source and matching configuration versions should be retained in the audit data for historical replay.

## Key files

- `src/Flix.Core/name/LuceneNameMatcher.cs` — independent Lucene candidate retrieval paths
- `src/Flix.Core/name/NameMatchEvaluator.cs` — deterministic feature evaluation and name-type scoring
- `src/Flix.Core/name/NameNGrams.cs` — character n-grams
- `src/Flix.Core/name/NamePhonetics.cs` — limited Soundex token keys
- `src/Flix.Core/name/NameMatchingOptions.cs` — capacity and algorithm configuration
- `src/Flix.Core/indexing/NameIndexBuilder.cs` — precomputed retrieval fields

This remains an engineering baseline, not regulatory-certified sanctions screening software.

## Exact-match and capacity safety

The name matcher first queries the indexed `normalizedName` keyword field with the
same normalization used when building the snapshot. All exact matching Lucene
documents are returned, including different entities sharing one name, without
applying fuzzy candidate or final-result limits. An exact match does not depend
on successful fuzzy retrieval. This applies to normal, global/tenant blacklists
and whitelists; grouped whitelist qualification is unchanged.

When non-exact fuzzy retrieval exceeds its configured final candidate capacity,
`DecisionService` now emits a `REVIEW` outcome with `ReleaseAllowed=false` and
a capacity diagnostic in `Reason` instead of propagating a routine exception.
This is fail-closed; investigate and rerun overflow cases when necessary.
Very large exact-result sets can also exceed the overall screening-policy
`MaxMatches`, producing the same controlled REVIEW condition.
