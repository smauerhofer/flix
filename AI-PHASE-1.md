# FLIX — Phase 1: local AI candidate reranking

This version retains Lucene.NET candidate retrieval and all existing, deterministic
screening decisions. An **optional local ONNX pairwise classifier** scores name and
address candidates AFTER retrieval and exposes scores in `ScreeningDecision.AiEvidence`.

## Activation

In the filter YAML configuration:

```yaml
AI:
  Enabled: true
  ModelPath: C:/models/flix-pair-reranker-v1.onnx
  ModelVersion: pair-reranker-v1
  MaxCandidatesPerField: 100
  RankingWeight: 0.30
  MinimumConfidence: 0.70
```

AI is OFF by default. The example file path is **not shipped**. Provide a trained,
validated model before enabling. No external service is contacted. Enabling AI
without a real model file fails at snapshot activation, rather than falling back
silently. Model inference errors fail screening rather than authorizing release.

## Required ONNX contract

The model is a **binary pairwise matching classifier**, trained on labeled
name/address candidate pairs. It must have:

- input: `features`, float32 tensor `[1,6]`
- output: `score`, float32 tensor `[1,1]`, a calibrated probability in `[0,1]`
- feature order:
  1. traditional candidate score (0..1)
  2. normalized edit similarity
  3. token Jaccard similarity
  4. ratio of normalized string lengths
  5. exact first-token equality indicator
  6. first-character equality indicator

These six features are defined in `AiPairFeatures` and must be identical during
training and inference. Document the model's training data, label definition,
false-negative results, version, and approved thresholds separately. The model
version is returned with each AI score.

## Behavior

`AiEvidence` gives each candidate its score, source scope (`NORMAL`,
`GLOBAL_BLACKLIST`, `TENANT_BLACKLIST`, `GLOBAL_WHITELIST`, or
`TENANT_WHITELIST`), tenant's party identifier, entity/source IDs, reference
value and **rank** within the corresponding field-and-scope candidate set.

Scores are **advisory only** in Phase 1. No score is substituted into
`NameMatch.Similarity`, `FieldMatch.Score`, or `ListFinding.Score`. Decisions,
blacklist thresholds, whitelist grouping, and tenant isolation therefore do not
change. The scorer only processes Lucene-retrieved candidates and **does not
increase retrieval recall**. No new AI candidate retrieval/index is introduced.

The ONNX inference session is constructed once per screening-filter activation,
shared for concurrent requests, and disposed with its retired filter only after
all leases are released by the management lifecycle. Model configuration and
loaded inference session are associated with the same filter version.

## Limitations

- This repository does **not** distribute a pretrained compliance model.
- The six-feature classifier is a practical local reranking integration; it is
  not a multilingual embedding model. A future model and tokenizer can replace
  this scorer once independently evaluated.
- AI ranking is generated per incoming field and list scope, not as one global
  cross-list priority. Treat the score as evidence, not a legal determination.
- When disabled, `AiEvidence` is empty.
- Benchmark latency and test throughput with a real model before deployment.

## Configurable ranking influence

For candidates retrieved by Lucene, the presentation score is:

`CombinedScore = (1 - RankingWeight) * TraditionalScore + RankingWeight * AiScore`

Only if `AiScore >= MinimumConfidence`; otherwise `CombinedScore = TraditionalScore`.
Both settings must be finite and between 0 and 1. The defaults (0.30 and 0.70)
are illustrative and **not calibrated thresholds**. `AiEvidence` retains all three
scores and a rank ordered by `CombinedScore`, with traditional score as the
stable secondary comparator. Rank is calculated per field and list scope.

These values affect presentation only; no hit is suppressed or created by AI,
and whitelist/blacklist rules do not change. The existing genetic optimizer
optimizes screening decisions, not these presentation-only weights; ranking
weight tuning requires a separately labeled ranking evaluation dataset.

### Genetic tuning of the two AI ranking settings

See `OPTIMIZER.md` for the labeled `--ranking-cases` JSONL contract. When a
local ONNX model is enabled, the optimizer evaluates its scores for labeled
candidates and tunes `RankingWeight` and `MinimumConfidence` separately from
hold/release decision thresholds. No AI model or labeled ranking data is
bundled with the project.
