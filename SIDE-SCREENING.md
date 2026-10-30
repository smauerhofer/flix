# Side-based screening

Each `ScreeningField.PartyId` identifies the payment side. Supported values (case-insensitive):

- Sender: `sender`, `debtor`, `originator`, `payer`
- Receiver: `receiver`, `creditor`, `beneficiary`, `payee`

Unknown values fail validation; do not silently guess a side. Data for intermediary banks or ultimate parties requires an explicit policy extension rather than mapping it onto a side without authorization.

The normal and blacklist indexes match a side when **at least one** criterion on that side qualifies. The whitelist matches a side only when **every submitted criterion** on that side qualifies for the whitelist. Multiple matching reference aliases for one field count as only one matched criterion. An empty side cannot whitelist. The sender and receiver decisions are combined: if either side requires review, the message requires review; otherwise the message is `WHITELISTED` only if all present sides are whitelisted, and `CLEAR` otherwise. `ScreeningDecision.Sides` exposes per-side counters and decisions. `WhitelistOverrideApplied` means one or more sides were overridden, not necessarily that the message was cleared.

Existing `WhitelistOverridesAll` and `ListPrecedence` options apply **only inside each side**, never across sides. They should not be interpreted as legally valid sanctions exemptions. To prevent incomplete input from clearing a payment, enforce mandatory sender and receiver field schemas in the workflow before allowing `ReleaseAllowed=true`. The implementation currently accepts one-sided requests for backward compatibility and does not confirm that source records used for different criteria belong to the same legal entity. Production whitelisting should bind criteria to a vetted identity and exception, and never be treated as a legal sanctions authorization by itself.

## Country blocks

A whole country is shut down by an active `COUNTRY` entry (ISO 3166-1 alpha-2, e.g. `KP`) in the global blacklist, or in a tenant blacklist for that tenant only. A side whose COUNTRY field matches such an entry is always `REVIEW` with `CountryBlockHit=true`. **A country block always wins**: no whitelist group can override it, whatever `WhitelistOverridesAll` or `ListPrecedence` says. Other blacklist entries keep the existing override behaviour.

COUNTRY matches against the normal (sanctions) list are reported as evidence in `FieldMatches` only and never make a side `REVIEW` on their own. An incoming COUNTRY value that is not a two-letter code (e.g. `PRK`, `North Korea`) produces the `INVALID_COUNTRY` validation finding and `REVIEW`, because it could never match a block. Blacklist imports reject COUNTRY entries that are not two-letter codes.

Run `dotnet test flix.slnx` to run smoke and regression checks in MSTest.
