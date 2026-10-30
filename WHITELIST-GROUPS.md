# Grouped whitelist semantics

A whitelist entry is identified by `(EntityId, SourceId)` within the dedicated whitelist index. Each entry contains all its indexed criteria, such as person name, IBAN, LEI, and address. Do not give different approved entries the same pair of identifiers.

For each sender/receiver side, a whitelist entry qualifies only if:

1. **Every supplied field on that side** has a qualifying match to that exact whitelist entry, and
2. **Every distinct indexed criterion of that entry** is matched by at least one field on that side.

This is a two-way coverage requirement. Hits from different whitelist entries cannot be combined to qualify a side. Repeated aliases that are intended to be alternatives need a more explicit group-with-alternatives schema; this implementation treats each indexed alias as a required criterion. Missing fields do not satisfy required whitelist-entry criteria.

Blacklists and normal reference lists remain any-hit per side; either side requiring review means the message requires review. All indexes remain part of the immutable versioned filter snapshot. `WhitelistGroupIndex` builds its metadata once on snapshot loading, not per request.

**Security/compliance:** A grouped match remains an engine decision rather than proof of a legal exemption. Avoid using fuzzy names alone to grant whitelist release; require governed approvals and sufficient identifying evidence.
