# flix typed reference-field extension

Import sample data: `./import-fields.ps1 -Version v003` from PowerShell. Each version MUST be new.
CLI: `dotnet run --project src/Flix.Cli -- import-fields ./flix-data v003 ./sample-data/fields.csv`
CSV fields: `sourceId,entityId,type,value`; quoted commas and escaped double quotes are supported.
Supported `type` values: `NAME`, `IBAN`, `ACCOUNT`, `BIC`, `COUNTRY`, `ADDRESS`.

Example POST `/screen` payload:
```json
{"messageId":"PAY-001","fields":[{"partyId":"beneficiary","type":"IBAN","value":"CH9300762011623852957"},{"partyId":"beneficiary","type":"COUNTRY","value":"CH"},{"partyId":"beneficiary","type":"ADDRESS","value":"25 King St London GB"},{"partyId":"beneficiary","type":"NAME","value":"Mohammed Al Rashid"}]}
```

This is a **candidate matcher**, not a sanctions policy engine. Country matches are **not** payment prohibitions. Account identifiers must be scoped with bank/scheme information in `Value`, consistently at import and query time. Address lookup is approximate candidate retrieval and its similarity is basic; it must not auto-clear sanctions matches. The `FieldMatches` response property contains uncorrelated evidence; correlate `PartyId` and `EntityId` in an actual policy evaluator. Example data are fictional test associations and do not imply any real sanctions designation. For source-wide updates, build a new version and publish it atomically. During screening, the snapshot remains read-only and management exclusively disposes old versions. Do not use this starter for real payment-release decisions without validation, full reference coverage and legal policy evaluation.
