# Snapshot-based positive test generation

Generate deterministic positive test fixtures directly from a published FLIX Lucene snapshot. The generator is read-only: it does not mutate indexes, snapshots, or release decisions.

```powershell
dotnet run --project .\src\Flix.Cli -- generate-positive .\flix-data .\test-data\positive-seco.jsonl 10000 --seed 74381
```

Optional flags:

- `--version YYYYMMDD[_nnn]`: read an existing snapshot version instead of `active.json` (the version must equal the snapshot directory name).
- `--tenant alpha`: identify the requesting tenant. Mandatory when the snapshot contains tenant indexes. Global normal data remains global; `--list blacklist` with a tenant reads that tenant's own blacklist, while the default tenant reads the global blacklist in legacy snapshots.
- `--list normal|blacklist`: choose the source index. Whitelists are excluded: a whitelist criterion alone is not a positive hit under the grouped-whitelist rules.
- `--variation exact|fuzzy|mixed`: `exact` is the default. `fuzzy` produces deterministic single-character deletion variants of name/address records; `mixed` alternates where possible. Fuzzy cases are labeled synthetic and are **not proof** the current engine matches them.
- `--seed 74381`: makes sampling reproducible.

The generator samples unique Lucene documents without replacement. It refuses requests for more cases than eligible documents. It preserves the original indexed `sourceId`, `entityId`, field type and snapshot version. Every line is a JSON object with `caseId`, `tenantId`, `messageId`, `snapshotVersion`, `list`, `seed`, `field`, `expected`, `generationType`, and `expectedMatchIsGuaranteed`.

For model and threshold validation, run generated requests through the actual screening pipeline and report **observed** matches separately from generated **expected** matches. Synthetic fuzzy labels do not establish identity and do not guarantee successful detection. Exact matches on names/addresses are still subject to screening configuration, so the guarantee flag only describes equality to the stored index value, not a guaranteed end-to-end screening decision.

The generator intentionally does not create negative cases or aggregate multi-criterion party identities. Outputs should be stored securely because indexed reference values may be sensitive or restricted.

## Tenant provisioning validation

A legacy snapshot without a `tenants/` directory permits only `--tenant default`.
A multi-tenant snapshot with a `tenants/` directory requires a provisioned
tenant subdirectory; `default` is not implicitly provisioned. This applies
even when generating from the shared global normal index, so requests cannot
claim an arbitrary tenant while reading global reference data.

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
