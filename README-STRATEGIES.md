# Typed strategies and LEI

`IScreeningStrategy` routes structured metadata to separate implementation classes:
`IbanScreeningStrategy`, `AccountNumberScreeningStrategy`, `BicScreeningStrategy`,
`CountryScreeningStrategy`, `AddressScreeningStrategy`, `PersonNameScreeningStrategy`,
`OrganizationNameScreeningStrategy`, and `LeiScreeningStrategy`.

Accepted CSV `type` values now include `PERSON_NAME`, `ORGANIZATION_NAME`, `LEI`, plus
legacy `NAME`. The `NAME` type fans out to both person/organization strategies.
New snapshots are built with `import-bundle`, and all three independent Lucene indexes
(normal, blacklist, whitelist) receive the same typed normalization.

Example row (illustrative publicly documented LEI checksum-valid example):
`ENT-LEI,TEST,LEI,5493001KJTIIGC8Y1R12`

LEI inputs must pass 20-character ASCII alphanumeric and ISO 7064 MOD 97-10 validation.
LEI comparison is exact after removing separators and uppercasing.
Do not accept a country restriction or legal designation solely from a successful match.

**Operational warning:** This keeps the existing user-requested message-wide whitelist
behavior, which can override a normal sanctions hit. This behavior is unsafe as a legal
sanctions clearance mechanism; a compliance-approved restriction layer is required
before authorizing real payments. Name retrieval/thresholds must also be independently
validated for recall and false negatives.
