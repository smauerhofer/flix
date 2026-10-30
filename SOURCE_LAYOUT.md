# flix source layout

The `Flix.Core` source tree is grouped by component responsibility:

- `name/` — name matching, name records, person and organization screening
- `account/` — account number strategy
- `address/` — address strategy
- `iban/` — IBAN strategy
- `lei/` — LEI strategy and validation
- `country/` — country strategy
- `bic/` — BIC strategy
- `screening/` — screening API contracts, strategy dispatch, evidence and decision policies
- `indexing/` — Lucene index building, immutable snapshots, lifecycle management and publication

Other source files remain where they were. Each C# type stays in its own `.cs` file.
Namespaces remain `Flix.Core` deliberately, to preserve compatibility with existing application and test code. Folder structure in SDK-style .NET projects does not require a namespace change.

This change is a file-layout refactor; no matching or policy behavior was modified.
