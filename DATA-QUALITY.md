# Invalid IBAN / LEI handling

During screening, IBAN and LEI values are validated before querying the normal, blacklist, or whitelist index. Invalid values produce a `ScreeningValidationFinding` containing the party, side, field type, error code, and diagnostic message. Invalid fields are skipped, but every other field is still screened.

`DecisionService.Screen()` returns `REVIEW` with `ReleaseAllowed=false` if any side contains invalid identifiers, regardless of whitelist matches. Valid findings are preserved in `NameMatches`, `FieldMatches`, `BlacklistMatches`, and `WhitelistMatches`. The `ValidationFindings` response property contains the new data-quality details. A side with invalid identifier data cannot qualify for a whitelist override.

`ScreeningService.Screen()` (candidate-only endpoint) returns `DATA_QUALITY_REVIEW` and includes `ValidationFindings` while retaining valid candidates. The payment decision endpoint should be used to control payment release; do not treat a candidate-only response as clearance.

`LuceneFieldMatcher.Match()` also defensively returns no candidates for invalid IBAN/LEI instead of throwing, for direct callers. This direct API cannot itself report data quality; use the screening services for those findings.

Only checksum/format validation is implemented here. Full country-specific IBAN structural rules and reference-entity corroboration need additional validation before production authorization.
