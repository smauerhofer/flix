# flix IDE tests

Both projects use MSTest and are discoverable from Visual Studio Test Explorer or Rider Unit Tests.

Run from solution directory:

```powershell
dotnet test .\flix.slnx
```

- `Flix.SmokeTests` verifies the basic end-to-end workflow, list precedence, LEI and snapshot retirement.
- `Flix.RegressionTests` creates its own temporary Lucene indexes containing **at least 1,600** normal-list records (200 per metadata type), plus blacklist and whitelist records. No production reference data or API server is required.

The regression suite covers case, whitespace, hyphenation, punctuation, token reordering, alias-like spellings, normalization, validator checks, exact-match nonmatches, index separation and retirement behavior. All test entities are fabricated; no findings establish sanctions status.

Note: the current account-number key lacks banking context, and country-code matching is intentionally contextual rather than proof of a sanctions restriction. Test results do not establish regulatory screening quality.
