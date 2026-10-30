# Flix multi-tenant screening

A screening request carries a `TenantId`. The normal sanctions reference index is global.
Each snapshot includes global blacklist/whitelist indexes, plus optional separate tenant
blacklist/whitelist indexes. The screening engine combines evidence only from the global
and the specified tenant's indexes. Other tenants' indexes are never searched.

## Directory structure

The offline importer accepts an optional directory of tenant-managed JSON files:

```
reference-data/
  blacklist.json                 # global blacklist
  whitelist.json                 # global whitelist
  tenants/
    alpha/
      blacklist.json
      whitelist.json
    beta/
      blacklist.json
      whitelist.json
```

`import-bundle.ps1` imports both global files and tenant directories. The CLI can also be
invoked explicitly:

```
dotnet run --project src/Flix.Cli -- import-bundle flix-data sample-data/fields.csv reference-data/blacklist.json reference-data/whitelist.json reference-data/tenants
```

The resulting immutable snapshot is:

```
snapshots/YYYYMMDD[_nnn]/
  normal/
  blacklist/
  whitelist/
  tenants/
    alpha/
      blacklist/
      whitelist/
    beta/
      blacklist/
      whitelist/
```

Each snapshot has one version and is swapped atomically, including all tenant lists.
Only active blacklist entries and fully active whitelist groups are imported.

## Request

```json
{
  "tenantId": "alpha",
  "messageId": "PAY-001",
  "fields": [
    { "partyId": "sender", "type": "ACCOUNT", "value": "AC998877" },
    { "partyId": "receiver", "type": "COUNTRY", "value": "CH" }
  ]
}
```

For each side:
- The normal list hits if **any** screening criterion matches.
- Either the global blacklist or the tenant blacklist hits if **any** criterion matches.
- The global whitelist qualifies only if the **entire side** matches one complete global whitelist group.
- The tenant whitelist qualifies only if the **entire side** matches one complete group from that tenant's whitelist.
- Global and tenant whitelist criteria **cannot** combine to complete a group.
- The existing list-override policy decides whether a qualified whitelist clears that side.
- The message is reviewed if either side remains in `REVIEW`.

`ScreeningDecision.TenantId` identifies the screened tenant. List findings carry `GLOBAL_BLACKLIST`,
`TENANT_BLACKLIST`, `GLOBAL_WHITELIST`, or `TENANT_WHITELIST` source labels.

## Security and deployment

**IMPORTANT:** A tenant ID supplied by a client must not be trusted by itself. The API currently
uses one shared API-key mechanism. A production deployment must bind the tenant ID to an
authenticated principal or trusted gateway-issued claim and reject mismatches. Otherwise a
client with access could submit another tenant's ID. Provision and authorize tenant access
before enabling a production release decision.

Unknown/unprovisioned tenant IDs are rejected; in the legacy snapshot layout (without a
`tenants` directory), `default` remains available for older tests and integrations.

All required tenant files must exist at import time. Add/modify tenant lists offline, then build
and publish a **new** immutable snapshot. Do not edit active index files.
