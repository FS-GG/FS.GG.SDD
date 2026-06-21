# Contract: Schema Reference Catalog

The authoritative reference describing every public SDD output (FR-004). The
published `docs/release/schema-reference.md` is a **projection** of the `catalog`
array in `release-readiness.json` (FR-005); a test asserts the doc and the json
agree (FR-015). Each entry names the contract, its `schemaVersion`
(+ `contractVersion` where applicable), determinism guarantee, stability class,
field inventory, and a back-reference to the authoritative structured artifact.

## Covered contracts (SC-002 — 100% of public outputs)

### Generated readiness views (`GeneratedViewKind`)

| Contract | `schemaVersion` | `contractVersion` | Stability | Authoritative source |
|---|---|---|---|---|
| `work-model.json` | 1 | — | AdditiveOptional | `WorkModel` serialization |
| `analysis.json` | 1 | — | AdditiveOptional | analyze command output |
| `verify.json` | 1 | — | AdditiveOptional | `verify` readiness view |
| `ship.json` | 1 | — | AdditiveOptional | `ship` readiness view |
| `governance-handoff.json` | 1 | 1.0.0 | Stable (envelope) | `GovernanceHandoff` projection |
| `summary.md` *(Markdown projection)* | gen | — | AdditiveOptional | `summary` projection |
| `agent-commands/<target>/guidance.json` | 1 | — | AdditiveOptional | `agents` generator manifest |
| `agent-commands/<target>/commands.md` *(Markdown projection)* | gen | — | AdditiveOptional | `agents` projection |
| `agent-commands/<target>/skills.md` *(Markdown projection)* | gen | — | AdditiveOptional | `agents` projection |

> **JSON vs Markdown (resolves analysis U1/U2).** JSON contracts carry a real
> `schemaVersion` and their inventory enumerates **JSON fields**. Markdown
> outputs (`summary.md`, `commands.md`, `skills.md`) are **projections**, not
> machine contracts (Constitution II); their version column (`gen`) is the
> **generator** version, and their inventory enumerates **document sections**.
> The `AgentCommands` view is documented as **one entry per sub-file** because the
> three files differ in role (the `.json` is a machine contract; the two `.md` are
> projections).
>
> `schemaVersion`/stability above are the **starting** classification to be
> confirmed against the real produced artifacts during implementation (the
> conformance test is authoritative — FR-015). The catalog in
> `release-readiness.json` is the machine copy.

### Command-output reports

| Contract | `schemaVersion` | Stability | Authoritative source |
|---|---|---|---|
| `<command> --json` (`CommandReport`) | per report | AdditiveOptional | `CommandSerialization.serializeReport` |

## Per-entry requirements (FR-004)

Each catalog entry MUST carry:
1. **Schema version** (and `contractVersion` for cross-repo contracts) — for a
   Markdown projection this is the generator version.
2. **Inventory** — for a JSON contract, every public field with its per-field
   stability; for a Markdown projection, every document section with its
   stability.
3. **Determinism guarantee** — the byte-stability statement (FR-008).
4. **Stability classification** — `Stable | AdditiveOptional | Experimental`.
5. **Source artifact** — `ArtifactRef` to the authoritative structured contract
   (FR-005), so the reference is a projection and never a second source of truth.

## Conformance (FR-015 / SC-003)

For each entry, a produced artifact from a real lifecycle run MUST conform to the
documented schema: **no undocumented public field** and **no documented field
absent**. A mismatch is a detectable failure with the produced artifact
authoritative. Drift between this reference and reality is therefore caught, not
silently tolerated (edge case "Drift between docs and reality").

## Readiness gating (FR-012)

Any public output lacking a catalog entry, a `sourceArtifact`, or a locking
baseline is reported **not-ready**. The schema reference never passes a surface by
omission.
