# flix: hybrid local name matching

This build adds separate exact full-name, token, fuzzy-token, character-trigram and English-oriented Soundex retrieval paths. Their Lucene hits are unioned by document ID, then ranked using Jaro-Winkler, adjacent-transposition edit distance, one-to-one token alignment, trigram Dice overlap and phonetic token overlap. Person and organization names use separate illustrative feature weights. Aliases remain separate indexed documents.

**Important limitations:** The algorithms and thresholds have not been calibrated on real sanctions examples. Soundex is English-oriented, not a multilingual transliteration engine; no automatic non-Latin script transliteration has been added. Short tokens, missing components, and language-dependent aliases need labeled regression testing. Bounded queries can truncate results within each path; measure candidate recall on representative cases before authorizing payment release. A whitelist match still has the previously requested message-wide override; this is unsafe as an automatic legal exemption and must be independently controlled.

Matching algorithm version: `hybrid-name-v3` (stored per document in `matchingAlgorithm`). v3 adds homoglyph repair to `NameNormalizer`: a token mixing Latin and Cyrillic look-alike letters (e.g. `АНАНIЧ` with a Latin I, `Pаvel` with a Cyrillic а) is converted to its unambiguous script (`ScriptHomoglyphs`), identically for reference and incoming names. v2 indexes the tokenized fields `nameTokens` and `addressTokens` with `IndexTokenAnalyzer`, a whitespace-only analyzer, so indexed tokens are exactly the `NameNormalizer` tokens the matchers query with. v1 used Lucene's `StandardAnalyzer`, which removed English stop words (of, the, an, to, …) and split CJK/Thai names into single characters, so the token, all-token and fuzzy paths could never match those tokens. Snapshots built before v3 must be rebuilt with `import-bundle`; older indexes also lack the `nameNGrams` and `namePhonetic` fields of v1. Candidate scoring uses the original stored alias. Source and matching configuration versions should be retained in the audit data for historical replay.

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
applying fuzzy candidate or final-result limits. The fuzzy paths always run as well,
so similar spellings belonging to other entities are returned alongside the exact
hits (exact documents are not duplicated). This applies to normal, global/tenant
blacklists and whitelists; grouped whitelist qualification is unchanged.

The fuzzy paths (tokens, all-tokens, fuzzy, n-grams, phonetic) each return at most
`CandidateLimitPerPath` documents (200). The default `FinalCandidateLimit` is
`MaxRetrievalPaths * CandidateLimitPerPath` (5 x 200 = 1000), so their union can
never exceed it and common names do not produce capacity REVIEWs. Each path still
keeps only its best-ranked documents; completeness comes from the unlimited exact
path, not from the fuzzy union.

With a smaller, explicitly configured `FinalCandidateLimit` (`CanOverflow` is true):
- if there is an exact hit, the fuzzy candidates are cut to the best
  `FinalCandidateLimit` by Lucene score, because the exact hit already decides the
  field (review-level hit, or the only qualifying whitelist evidence);
- otherwise `DecisionService` emits `REVIEW` with `ReleaseAllowed=false` and a
  capacity diagnostic in `Reason` (fail-closed).

Very large result sets across a message can exceed the screening-policy
`MaxMatches` (2000), producing the same controlled capacity REVIEW.
