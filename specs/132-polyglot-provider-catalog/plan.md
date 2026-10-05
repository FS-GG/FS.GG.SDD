# SDD928-C1 plan

Use sibling `Fsgg.ProviderCatalog` in the BCL-only shared Contracts leaf. Write signature,
public API semantic tests, then body. The pure Artifacts parser reuses the hardened YAML reader
and returns catalog-specific typed diagnostics; it does not broaden global SchemaVersion.
No MVU is necessary because these functions only consume in-memory data.

The prelude/public API tests preceded implementation. Root's bounded native08 qualification passed
all nine phases and 56 tests: 25 Contracts, 30 Artifacts and one legacy scaffold refusal. Both
reflection baselines are the exact root-accepted compiled native05 outputs (95 Contracts additions
and one Artifacts parser addition, with no removals), validated with updates disabled in native08.

Contracts own declarative shape, validation and resolution. Artifacts owns strict document decoding.
Governance #423 owns supported capability meanings, semantic floors and evidence normalization.
Command execution limits (timeout, cost class, environment IDs) remain declarations; C1 does not
claim runtime enforcement. C2 must join them with the admitted executor before invoking anything.

Root selected the additive source version join: Contracts 7.5.2 → 7.6.0 and shared SDD
2.1.0 → 2.2.0. Only local Project dependency lock edges change; external package bytes remain fixed.
Native08 qualifies this source version join. Protected landing and coordinated publication remain pending.
No feed or consumer pin is changed here. Legacy published consumers remain compatible.


## SDD928-C2.1 — read-only catalog preparation

Add explicit digest-checked `catalog inspect`, canonical semantic digest projections and closed schema-2 provenance reading/ownership projection. Preserve C1 parser/resolve and every legacy constructor. No runtime Governance reference or invocation is introduced. Inspection reports prepared declarations only, reads the explicitly selected local file and never writes or probes tools. `scaffold --catalog` refuses as unavailable before effects.

Verify raw/semantic tamper, sorted declarations versus literal argv, Unicode/control byte goldens, explicit parameter identities/defaults, schema-1 parity, complete synthetic schema-2 ownership correspondence, and malformed/unsupported provenance blocking refresh/lifecycle mutation. Real executable provider qualification remains C2.2 under root admission.
