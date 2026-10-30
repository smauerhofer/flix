# YAML filter configuration

`src/Flix.Api/flix.yaml` contains the parameters for screening. The ASP.NET Core
`appsettings.json` sets only `Flix:ParametersFile` (the YAML file location).
The YAML `Root` path is resolved relative to the API content root.

```yaml
Root: ../../flix-data
NameReviewThreshold: 0.82
AddressReviewThreshold: 0.90
WhitelistOverridesAll: false
ListPrecedence: BlacklistFirst
ListNameThreshold: 0.92
ListAddressThreshold: 0.95
```

In code, `FilterParametersFile.Read(path)` and `FilterParametersFile.Write(path, parameters)`
read and write typed `FilterParameters`. `new ScreeningFilter(snapshot, parameters)` binds
an immutable parameter set to its snapshot. A `FilterLease` exposes both in `Filter`.

The management worker polls YAML and the active snapshot manifest; it swaps the entire
filter when either changes. It alone retires and disposes old snapshots. Root changes
require an application restart. Invalid YAML preserves the previous active filter.

With these defaults a qualified whitelist overrides normal (sanctions) list hits on its side, but never a blacklist hit. Whitelist criteria qualify only on the exact normalized value (names: same `NameNormalizer` output; identifiers: same normalized code); `ListNameThreshold` and `ListAddressThreshold` apply to blacklists only. A blacklisted country is never overridable (see `SIDE-SCREENING.md`).

**Important:** `WhitelistOverridesAll` is a user-requested, dangerous whole-message
policy that can clear genuine sanctions hits. It is not a legally valid sanctions
exemption and must not be used to authorize payments without separately approved controls.
