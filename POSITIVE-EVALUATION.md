# Snapshot positive-case evaluation

The `evaluate-positive` command replays cases produced by `generate-positive` through the FLIX `DecisionService` against a **specific immutable snapshot**. No production snapshot is changed.

```powershell
dotnet run --project .\src\Flix.Cli -- generate-positive .\flix-data .\test-data\positive.jsonl 10000 --seed 74381
dotnet run --project .\src\Flix.Cli -- evaluate-positive .\flix-data .\test-data\positive.jsonl .\reports\seco-positive
```

To select a historical snapshot, pass `--version 20261011` on **both** commands. Input case versions must equal the evaluated snapshot version. The CLI returns exit code 1 when any expected entity is missed or any positive case does not trigger review; 0 otherwise. Invalid input or missing snapshot causes an error.

The report produces `seco-positive.json` (aggregate metrics, categories and every case) and `seco-positive.csv` (row-level cases including expected IDs, detected status, disposition and elapsed milliseconds).

## Definitions

- **Expected-entity detection:** The output matches the exact expected `(sourceId, entityId)` in the requested list scope. A hit on a different entity does not count. A blacklist case matches against the global or selected tenant blacklist.
- **Review triggered:** The overall screening decision is `REVIEW`. This is a separate metric because another entity may cause review even when the expected target is missed.
- **False negative:** A generated positive that does not produce `REVIEW`, including errors. This reflects operational disposition, not reference-specific recall. The distinct `MissedExpectedEntities` metric measures reference-specific recall.
- **Strategy:** The canonical *input field type* (`PERSON_NAME`, `ADDRESS`, etc.), not a breakdown of internal Lucene/fuzzy sub-algorithms.
- **Latency:** Wall-clock timing of the `DecisionService.Screen` call per case, including name/field matching and any enabled AI ranking, excluding input parsing and report writing. P50/P95/P99 are empirical interpolated percentiles. No concurrent throughput estimate is implied.

Groupings include tenant, strategy, generation type, and source list. **Synthetic fuzzy** cases are transformations with hypothetical relevance labels, not independently validated real-world matches; review them separately from **exact** cases.

The evaluator validates tenant provisioning and snapshot identity, and never mixes results from different snapshots. The CLI currently uses default screening configuration when replaying; for parity with a production deployment, pass the production `FilterParameters` programmatically to `PositiveCaseEvaluator.Evaluate` (the CLI does not yet accept a YAML file). AI models are not loaded by the CLI unless configured through that API.

Only positive-case data is evaluated. Specificity, false-positive rate and precision require negative and manually labeled evaluation datasets. Results should not be interpreted as model quality certification.

## Multi-tenant snapshots

If the published snapshot contains tenant indexes, specify a provisioned tenant explicitly.
The global `normal` list is still used for `--list normal`, but the generated
request must carry a valid tenant ID for replay through the screening engine.

```powershell
Get-ChildItem .\flix-data\snapshots\20261011\tenants -Directory | Select-Object -ExpandProperty Name
dotnet run --project .\src\Flix.Cli -- generate-positive .\flix-data .\test-data\positive-seco.jsonl 10000 --tenant alpha --seed 74381
```

Replace `alpha` with an ID actually provisioned in your snapshot.
A legacy snapshot without tenant indexes continues to accept `default`.
