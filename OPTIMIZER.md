# Genetic threshold optimizer

`Flix.Optimizer` searches thresholds on a fixed, immutable local sanctions snapshot.

## Inputs

- `base.yaml`: normal filter configuration, including `Root`, whitelist policy and precedence.
- `lower.yaml`: lower bounds for every numeric property ending in `Threshold`.
- `upper.yaml`: upper bounds for those properties.
- `positive.jsonl`: payments that **must be held** (one `ScreeningRequest` JSON per line).
- `negative.jsonl`: payments that **must pass** (same JSONL format).
- `winner.yaml`: new output file, written only if a candidate has **zero false negatives**.

The lower and upper YAML use the same format as `flix.yaml`; non-threshold properties are ignored in the bounds files. Only `*Threshold` numeric properties are varied. The base file's `Root`, `WhitelistOverridesAll`, and `ListPrecedence` remain unchanged.

## Run from solution root

```powershell
dotnet run --project .\src\Flix.Optimizer -- .\src\Flix.Api\flix.yaml .\training\lower.yaml .\training\upper.yaml .\training\positive.jsonl .\training\negative.jsonl .\winner.yaml 40 80 74381
```

A positive sample is one that should be blocked/held. **False negative:** a positive sample is released. **False positive:** a negative sample is held. The optimizer first minimizes false negatives, then false positives, and exports only zero-FN candidates. If none is found it exits with code 3 and creates no YAML. It refuses to overwrite an existing output file to avoid mistaking an old winner for a new result.

`YYYYMMDD` snapshots are built using the existing importer before optimization. The optimizer never calls external APIs or activates the exported settings.

**Important:** zero FN on training samples does not imply zero real-world misses. Hold out independent test cases, exercise blacklist/whitelist interactions, and require compliance approval. Unconditional whitelist overrides are particularly risky.

## Project split

- `src/Flix.Optimizer.Library`: reusable genetic search engine and its models, with the existing `Flix.Optimizer` namespace to preserve caller compatibility. Depends only on `Flix.Core`.
- `src/Flix.Optimizer`: console executable, responsible for arguments, YAML/JSONL file access, progress logging and exporting `winner.yaml`. It references the library and core.
- `tests/Flix.RegressionTests`: references the library directly, so tests run in Visual Studio Test Explorer without launching a CLI.

The CLI invocation remains unchanged. The `TrainingDataFile` JSONL reader remains in the executable because it is an input adapter, not part of the genetic algorithm.

## AI ranking optimization (when an ONNX model is provided)

AI ranking does not affect the hold/release decision. Consequently its weight and
confidence threshold **cannot** be trained meaningfully using positive/negative
message decisions alone. FLIX now performs an additional genetic ranking stage
against labeled candidate pairs after the existing decision-threshold stage.

Enable `AI.Enabled: true` and set a valid `AI.ModelPath` and `AI.ModelVersion`
in `base.yaml`; supply bounds for `AI.RankingWeight` and `AI.MinimumConfidence`
using `lower.yaml` and `upper.yaml`. Then invoke:

```powershell
dotnet run --project src/Flix.Optimizer -- base.yaml lower.yaml upper.yaml positive.jsonl negative.jsonl winner.yaml 30 40 74381 --ranking-cases labeled-ranking.jsonl
```

One JSON object per line in `labeled-ranking.jsonl` represents an individual
candidate for a given incoming name/address query:

```json
{"queryId":"name-101","incoming":"Mohammed Ali","reference":"Muhammad Ali","traditionalScore":0.76,"relevant":true}
{"queryId":"name-101","incoming":"Mohammed Ali","reference":"Mohammed Alim","traditionalScore":0.88,"relevant":false}
```

Each query must include relevant and irrelevant candidates; at least one such
query is mandatory. The configured local ONNX model produces candidate AI
scores using the existing `OnnxPairReranker`, then the GA searches for values
maximizing the proportion of correctly ordered relevant/irrelevant candidate
pairs. Ties receive half credit. This is a ranking metric, **not** sanctions
false-negative risk. Independent holdout evaluation and latency benchmarking
remain required. `winner.yaml` includes both optimized decision thresholds and
`AI.RankingWeight` and `AI.MinimumConfidence`.

If the AI model is disabled, the existing threshold-only optimizer still works
without `--ranking-cases`; AI values remain unchanged. If AI is enabled but
ranking labels are omitted, the command fails explicitly rather than claiming
to optimize two parameters that the decision metric cannot observe.
