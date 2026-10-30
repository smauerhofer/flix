# Flix: operational hardening and release gates

## Implemented in this edition

- .NET 10 traditional `Main` application; offline versioned imports and an immutable Lucene index.
- Typed name, IBAN, BIC, account, country and address matching against local records.
- Correlation restricted to the same payment party and canonical reference entity.
- Explicit `CLEAR`/`REVIEW` response and `ReleaseAllowed`, with `ERROR` on failures.
- All candidate sets above policy limit fail closed (HTTP 503).
- Local durable append-only JSON-lines decision audit, flushed to disk before returning a result.
- API key requirement for non-development use (`Flix__ApiKey` environment variable).
- Background snapshot management with usage-counted, management-only disposal.
- Simple smoke tests and configuration for match thresholds.

## BLOCKERS BEFORE ANY REAL PAYMENT RELEASE

1. **Regulatory model**: ingest official designations, legal programmes, restrictions and effective dates; implement a jurisdiction-specific rules engine, ownership/control graph, exclusions and licence handling. Current reference hits are not legal determinations.
2. **Match quality**: independent compliance validation of names, aliases, transliteration, address matching, false-negative rates, query truncation and calibrated thresholds; test against true provider distributions. A Lucene top-200 candidate search can miss important matches.
3. **Completeness and provenance**: provider-specific robust readers, signed/checksummed update packages, deletions, tombstones, reconciliation, source attribution, schema validation and freshness controls. Current CSV is demonstration-only.
4. **Audit**: integrate an append-only transactional outbox with the workflow database, durable replication and retention. The local JSON-lines journal is single-node only and can contain sensitive personal data; restrict access and encrypt volumes.
5. **Security**: replace API key with authenticated mTLS/OIDC infrastructure, authorization, rate limiting, encrypted secrets and independent security review. Protect health endpoints appropriately.
6. **High availability**: tested multi-node publication barriers, disaster recovery, consistent snapshots, operational alerting and replay. On process errors the workflow MUST hold the payment.
7. **Engineering validation**: build, integration tests, load and soak tests, mutation/fault injection, dependency scanning, code review and incident runbooks. This environment could not compile the solution.
8. **Identifier semantics**: account numbers must be matched with scheme/bank scope and validation; IBAN/BIC validity and ownership should be verified. Current matching is string normalization only.
9. **Clear decision policy**: this edition can produce `CLEAR` using incomplete context; disable release in deployment until data coverage and candidate recall are independently certified.

## Run locally

From the solution root:

```powershell
.\import-fields.ps1 -Version v001
dotnet run --project .\tests\Flix.SmokeTests
dotnet run --project .\src\Flix.Api --urls http://localhost:5080
```

For non-development deployments configure `Flix__ApiKey` via a secrets manager or environment variable and terminate TLS at a trusted ingress. Use a new snapshot version for every import.
