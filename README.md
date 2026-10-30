# flix — offline sanctions name filter (.NET 10)

A **starter project**, not a certified sanctions-screening product. Contains a local Lucene.NET name index, an HTTP API for **already parsed and typed metadata**, a streaming name importer, immutable index snapshots, reference-counted reader leases, and **management-only cleanup** of retired indexes.

## Prerequisites

- .NET 10 SDK
- NuGet package access at build/restore time (Lucene.NET 4.8.0-beta00018)
- No external API calls on the payment-screening path

## Quick start

From the repository root:

```bash
dotnet restore src/Flix.Api/Flix.Api.csproj
powershell -ExecutionPolicy Bypass -File ./import-sample.ps1 -Version v001
dotnet run --project src/Flix.Api --urls http://localhost:5080
```

Then:

```bash
curl -X POST http://localhost:5080/screen \
  -H 'Content-Type: application/json' \
  -d '{"messageId":"P1","fields":[{"partyId":"creditor","type":"NAME","value":"Mohammed Al-Rashid"}]}'
```

To switch to a new immutable index while the API is running, import a **new** version:

```bash
dotnet run --project src/Flix.Cli -- import ./flix-data v002 ./sample-data/names.csv
```

The API polls `active.json` and activates the new version. Old screening requests keep their leased snapshot. Only the management background task disposes retired readers after usage reaches zero.

Run smoke tests (console tests, no external test framework):

```bash
dotnet run --project tests/Flix.SmokeTests
```

## Projects

- `Flix.Core` — Lucene index writer/search, basic normalization and Levenshtein ranking, snapshot manager, screening service
- `Flix.Cli` — offline *demo-format* CSV import, immutable snapshot publication
- `Flix.Api` — synchronous screening endpoint, read-only matching, management polling and disposal
- `Flix.SmokeTests` — basic matching and snapshot-lifetime regression checks

## Important constraints

- **Only NAME fields are screened.** IBAN, account numbers, countries, addresses, entity ownership and legal designations are extension points and **are not implemented**. The `NO_NAME_CANDIDATES` result is *not* a sanctions CLEAR decision. Do not release payments based solely on this prototype.
- Returned Lucene top-N candidates can miss true matches. Calibrate recall using a representative, independently validated sanctions test corpus. Basic Levenshtein matching misses reordered names, many transliterations, and other variants.
- The demo CSV reader does not support CSV quoting, escaping or multiline fields. Replace with properly tested World-Check/OFAC source-specific adapters (including deleted-record handling, completeness checking, and licensing constraints).
- The example doesn't include authentication, authorization, rate limits, storage integrity/signatures, audit persistence, list-freshness policy, PEP segregation, high-availability coordination, or operational readiness checks. Add these before deployment.
- Index publication via `File.Move` assumes the manifest temp file and destination share a filesystem and support atomic replacement; verify on your actual storage platform. Do not share a mutable Lucene index directory between nodes.
- `FilterManager` holds the active snapshot until process shutdown. Production graceful shutdown should stop admitting new screening requests, drain outstanding leases, and have the management task dispose remaining snapshots. The demo does not implement this shutdown protocol.
- Snapshot versions must be unique and should be treated as monotonically published. The manager prevents reuse of its currently managed references, but a durable deployment inventory should enforce version uniqueness across restarts.
- Storage containing sensitive screening data requires access control and appropriate encryption.

## Baseline screening model

Lucene exact full-name, exact token and fuzzy token queries retrieve candidates; Levenshtein re-ranks candidates. This deliberately returns `POTENTIAL_MATCHES` rather than pretending to make legal sanctions decisions. Add domain policies and match thresholds only after validation.

## Relative data root on Windows

`Flix.Api/appsettings.json` sets `Flix:Root` to `../../flix-data`, resolved against `builder.Environment.ContentRootPath` by `Flix.Api/Program.cs`. The CLI script `import-sample.ps1` uses `$PSScriptRoot` and is safe to run from another current directory. The manifest is generated automatically by the CLI using Pascal-case JSON properties `Version` and `Directory`. Do not put name records into `active.json`.

## Optional local AI reranking

Phase 1 ONNX-based name/address candidate reranking is documented in [AI-PHASE-1.md](AI-PHASE-1.md). It is disabled by default and does not change sanctions decisions.


AI Phase 1 ranking accepts `AI.RankingWeight` (0..1, default 0.30) and `AI.MinimumConfidence` (0..1, default 0.70). Refer to `AI-PHASE-1.md`; scores affect display ordering only.

## Snapshot-derived positive cases

See [POSITIVE-CASE-GENERATOR.md](POSITIVE-CASE-GENERATOR.md) for the `generate-positive` command, reproducible cases, and synthetic fuzzy labels.

## Positive-case evaluation

The `evaluate-positive` CLI command replays generated test cases and writes detection, false-negative and latency reports. See `POSITIVE-EVALUATION.md`.

## Evaluation Inspector GUI

See [EVALUATION-INSPECTOR.md](EVALUATION-INSPECTOR.md) for the new Windows Forms evaluation-report inspector.
