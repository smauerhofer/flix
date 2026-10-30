# Matching Quality Update

## Positive benchmarking

`evaluate-positive <root> <cases.jsonl> <report-prefix> [--version VERSION] [--config filter.yaml]`

The evaluator stores a SHA-256 fingerprint of its effective configuration and the AI model version. Its CSV includes candidate retrieval status, the expected entity score, highest observed score, the active field threshold, whitelist override status, and a diagnostic category. `candidateRetrieved` indicates evidence returned by the screening engine, not unbounded Lucene recall.

For tenant-specific blacklist cases the score diagnostic uses the screened blacklist evidence; `bestTraditionalScore` currently covers normal-list name/field hits. Do not interpret this as full stage-level Lucene profiling.

## Addresses

The address comparator supports abbreviation expansion, diacritic folding, token-order independence and number-conflict penalty. The existing Lucene `normalizedValue` and `addressTokens` schema remains unchanged, so the current SECO snapshot can still be read. Address queries use both legacy and abbreviation-aware tokens. A new index with enhanced tokenizer fields could further improve retrieval, but is outside this backward-compatible update.

## Names

Fuzzy retrieval includes four-character tokens and an all-token candidate path to improve recall for transpositions and reordered tokens. It preserves the candidate-capacity review safeguards. This implementation does not claim full cross-script transliteration or a trained transliteration model.

## Performance and validation

The name matcher no longer performs an unnecessary duplicate exact-term fuzzy-path search. The existing candidate bounds remain to avoid unbounded fuzzy scoring. Benchmark exact/fuzzy positives and independently reviewed negatives with identical snapshot/config, and profile organization P95 locally. Do not lower thresholds without reviewed negatives.
