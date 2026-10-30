# FLIX benchmark quality update

## Country attribution

`evaluate-positive` now writes two additional nullable case columns:

- `countryValueDetected`: whether the screening output contains the incoming country code.
- `countryEntityPresentInIndex`: whether a direct exact Lucene query for `(fieldType, normalizedValue, sourceId, entityId)` finds the expected entry in the selected global normal snapshot.

These are separate from `expectedEntityFound`, which retains its original meaning: the expected source/entity appeared in the *bounded screening output*. A country may be correctly matched but have hundreds or thousands of reference entities attached to it; the evaluation should not treat an arbitrary top-N list as complete entity attribution. Country-only matching is NOT proof of a sanctioned person's identity.

## Fuzzy positives

`generate-positive` supports `--variation fuzzy|mixed` and `--mutation mixed|deletion|transpose|diacritic|word-order`. Generation uses the provided seed and writes `mutationType`, `generationType`, and `expectedMatchIsGuaranteed=false` for synthetic fuzzy cases. If an operation is inapplicable, a deletion is used as the recorded fallback. Fuzzy labels represent test hypotheses, not proven true identity matches. Use the same seed, tenant and snapshot for comparisons.

```powershell
dotnet run --project .\src\Flix.Cli -- generate-positive .\flix-data .\test-data\seco-fuzzy.jsonl 10000 --tenant alpha --seed 74381 --variation fuzzy --mutation mixed
dotnet run --project .\src\Flix.Cli -- evaluate-positive .\flix-data .\test-data\seco-fuzzy.jsonl .\reports\seco-fuzzy
```

## Negative cases

`evaluate-negative` accepts externally verified NONMATCH labels. Each JSONL record has `caseId`, `tenantId`, `messageId`, `snapshotVersion`, `field`, `label: NONMATCH`, and a nonempty `labelSource` documenting independent review. Do not treat random alterations of sanctioned persons as automatically legitimate nonmatches.

```json
{"caseId":"NEG-001","tenantId":"alpha","messageId":"PAY-NEG-001","snapshotVersion":"20261011","label":"NONMATCH","labelSource":"compliance-reviewed-2026-10","field":{"partyId":"sender","type":"PERSON_NAME","value":"Reviewed Legitimate Example"}}
```

```powershell
dotnet run --project .\src\Flix.Cli -- evaluate-negative .\flix-data .\test-data\reviewed-negative.jsonl .\reports\negative
```

The negative report contains per-case decisions, the fraction of successfully screened verified nonmatches triggering REVIEW (operational false-positive rate), and latency. Errors are reported separately and excluded from the rate denominator; they must not be mistaken for CLEAR decisions.

## Limitations

The negative evaluator assumes human NONMATCH labels are accurate; it does not verify that fact. Its current rate is a single-field, single-side proxy, not a complete live-payment transaction FPR. The evaluator still uses default filter parameters unless a caller supplies custom `FilterParameters` programmatically. The fuzzy generator does not claim transliteration coverage and is not a replacement for independently labeled multilingual examples. The snapshot is not modified.
