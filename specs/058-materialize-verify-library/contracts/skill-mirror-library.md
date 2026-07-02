# Contract: `Fsgg.SkillMirror` — the one materialize-and-verify library

**Package**: `FS.GG.Contracts` (contract `1.4.0`, additive minor over 057's `1.3.0`).
**Consumers**: `fsgg-sdd` orchestrated lane (this repo); the standalone lane (Rendering P2) vendors a
byte-copy of this module. **Decision**: ADR-0014 §Decision 2/3/5.

## Guarantees

- **Pure & BCL-only.** No I/O, no serialization, no non-BCL dependency. `sha256` uses BCL
  `System.Security.Cryptography.SHA256` over UTF-8 bytes. Safe to vendor into the standalone lane.
- **Root-set-parameterized.** Every destination derives from the passed `roots` (the callers pass
  `Fsgg.Schemas.agentSkillRoots`); the module hardcodes no root except `providerSourceRoot` (the
  provider-owned source root, itself the ADR §Decision 6 invariant).
- **Deterministic.** `mirror` sorts skills by id; `verify` returns drift sorted by id. Same inputs →
  same outputs, byte-for-byte.

## Materialize — `mirror roots skills`

Given a union of `(id, body)` skills and the root set, returns one `{ Path; Body }` per (skill × root)
at `<root>/skills/<id>/SKILL.md`. The caller turns each into a `WriteFile(..., AgentGuidanceTarget)`
(no-clobber) effect. Provider copies use `retargetSkillPath` over `mirrorTargetRoots roots` so the
source `.agents` body is fanned into `.claude`/`.codex` verbatim.

## Verify — `verify roots expected actual`

For every expected skill, asserts the three ADR-0014 §Decision 3 invariants and returns the drift:

| Check | Field | Meaning |
|---|---|---|
| present in each root | `MissingRoots` | roots with no copy of the skill |
| byte-identical across roots | `Divergent` | present copies are not all equal |
| matches the manifest hash | `HashMismatchRoots` | present roots whose `sha256 body ≠ expected.Sha256` |

A skill with all three clean is omitted (coherent). An empty `expected.Sha256` skips only the
hash-match (a product skill whose digest predates P1); presence and cross-root identity still hold.

## Backward / forward compatibility

- Additive to `FS.GG.Contracts`: a new public module; no change to existing types. Consumers compiled
  against `1.3.0` keep working.
- No persisted-schema change: `scaffoldProvenanceVersion`/`skillManifestVersion` stay `1`. The
  provenance `sha256` field (added in 057, contract `scaffold-provenance` `1.1.0`) is now populated;
  a digest-free provenance still parses (absent ⇒ `None`).
- The standalone-lane vendored copy MUST stay byte-identical to this module (P2/P3 assert it); any
  change here is a `FS.GG.Contracts` minor bump and a re-vendor.
