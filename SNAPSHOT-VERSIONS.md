# Date-based snapshot versions

The CLI assigns snapshot versions automatically, using the **local date of the importing machine**:

- First reservation on a day: `YYYYMMDD` (example: `20261009`).
- Subsequent reservations: `YYYYMMDD_001`, `YYYYMMDD_002`, etc.
- Sequence is one greater than the highest sequence already present for that day; gaps are not reused.
- At `_999`, further reservations on that date fail rather than wrap around.
- Failed imports retain their reserved directory for investigation. Their numbers are not reused.
- Legacy `vNNN` directories are ignored when selecting the next date-based version, but old manifests remain readable.

Run `./import-bundle.ps1` to create and publish a new snapshot without supplying `-Version`.

The allocator acquires an exclusive file lock (`flix-data/snapshots/.version.lock`) while selecting a name and creating the directory. An overlapping allocation will fail with a lock error rather than produce duplicate versions. Retry the import later after the other allocation finishes.

Snapshot publication remains separate: the CLI builds and validates all indexes, then writes `active.json`. The allocation date reflects the import machine's local calendar, not a sanctions data provider's publication date.
