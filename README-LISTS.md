# Separate blacklist and whitelist indexes

Build from the solution root with `./import-bundle.ps1 -Version v005` or use:

```powershell
dotnet run --project ./src/Flix.Cli -- import-bundle ./flix-data v005 ./sample-data/fields.csv ./sample-data/blacklist.csv ./sample-data/whitelist.csv
```

This creates independently stored Lucene indexes `snapshots/v005/normal`, `blacklist`, and `whitelist`. The three indexes are activated and retired **as one version**; readers only decrement usage references, and management does the disposal. The CSV format is `sourceId,entityId,type,value`, including RFC-style quoted commas and escaped quotation marks. Both override indexes may be empty.

## Override behavior

Default `Flix:WhitelistOverridesAll=true`: **any sufficiently strong whitelist hit anywhere in the payment** overrides **all blacklist and normal reference hits**, even for other parties. The output is `WHITELISTED`, `ReleaseAllowed=true`, `WhitelistOverrideApplied=true`; ALL evidence is still returned and audited. If set false, `Flix:ListPrecedence` controls whether blacklist or whitelist wins on mixed hits. `BlacklistFirst` holds mixed hits; `WhitelistFirst` clears them.

Matching strength: exact IBAN/ACCOUNT/BIC/COUNTRY hits qualify; NAME and ADDRESS require configured `ListNameThreshold` and `ListAddressThreshold`. The default whole-message override is intentionally broad, requested by the application owner, and **is not legally safe as a sanctions exemption**. A whitelist name collision or a match to a different party can clear a genuine designated-party hit. Do not use this setting as an unconditional payment-release authorization in a regulated environment. Require compliance-approved scope, expiry, rationale, four-eyes approval, identity verification and validation before production use.

In particular, `ReleaseAllowed=true` here is an engine decision, not evidence that applicable sanctions law permits release.

Existing single-index snapshot directories are incompatible with this three-index format. Rebuild them with `import-bundle` and choose a fresh version. The old `import-fields` CLI remains for compatibility but should not be used for publishing snapshots consumed by this upgraded API.
