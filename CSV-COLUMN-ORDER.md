# FLIX CSV column ordering

All reference CSV files (normal, blacklist, whitelist and SECO conversion) use:

```csv
sourceId,entityId,type,value
SECO,5142,PERSON_NAME,Example Name
```

- `sourceId`: identifies the list/provider, e.g., SECO or INTERNAL.
- `entityId`: identifies the target or item **within that source**.
- `type` and `value`: indexed criterion.

This modifies the CSV serialization only. The internal `ReferenceFieldRecord(EntityId, SourceId, Type, Value)` contract and the external JSON list schema remain unchanged. The importer validates the exact header and fails on legacy entity-first files to avoid silently reversing identifiers. Any custom CSV files must be migrated before index generation. Rebuild/publish a fresh immutable snapshot after importing.
