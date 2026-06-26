---
title: Schema Reference
category: SDD
categoryindex: 6
index: 18
description: The versioned, stability-classified reference for every public FS.GG.SDD generated view and command-output contract, projected from release-readiness.json.
---

# Schema Reference

This document is a **projection** of the `catalog[]` array in
[`release-readiness.json`](release-readiness.json), which is the authoritative
machine contract. A conformance test asserts that this document and the JSON
agree, and that each produced artifact matches its documented schema — no
undocumented public field, no documented field absent. On any disagreement, the
produced/structured artifact is authoritative and the discrepancy is a detectable
failure. This reference is never a second source of truth. (FR-004 / FR-005 /
FR-015)

Every public output has exactly one catalog entry. An output with no entry, no
`sourceArtifact`, or no locking baseline is reported **not-ready**; the reference
never passes a surface by omission. (FR-012)

## Declared exception: the `validation-report`

The `fsgg-sdd validate` harness emits a public `validation-report` JSON contract
(`schemaVersion = 1`). It is **intentionally not catalogued** here, and this
exclusion is declared rather than silent (the same anti-omission principle this
reference enforces). The report is **harness output, not a produced lifecycle
artifact**: it carries an explicitly-fenced `sensed` block (wall-clock / duration /
host facts) excluded from its deterministic comparison, and it is written to stdout
on demand / on a schedule rather than to `readiness/<id>/`. Because the exclusion is
recorded here, the harness's own coverage-reconciliation does not flag the
`validation-report` as a coverage gap. (feature 020 / validation-report C-4)

## Declared exception: scaffold provenance + provider registry

`fsgg-sdd scaffold` produces two project-level artifacts that are **intentionally
not catalogued** in the machine catalog's lifecycle equality set (which enumerates
exactly the `readiness/<id>/` views plus the command report). Both are real,
schema-versioned, byte-deterministic artifacts; the exclusion is declared here
rather than silent.

- **`.fsgg/scaffold-provenance.json`** (schema v1, `owner: sdd`,
  `requiredBySdd: false`, `stability: additiveOptional`) — SDD writes it whenever a
  provider actually runs. It records the provider name, the provider
  `contractVersion`, the `templateRef`, the `outcome`
  (`providerSucceeded` / `providerSucceededEmpty` / `providerFailed`), and the
  `producedPaths` (sorted, each marked `owner: generatedProduct`). It is the
  **authority for refresh exclusion** (FR-007 / SC-007): `fsgg-sdd refresh` reads it
  and never regenerates or flags provider-produced runtime files as stale SDD views;
  malformed provenance surfaces `scaffold.provenanceMalformed` and is treated as
  absent (fail-safe). Unlike the `validation-report` it represents
  *externally-owned output references* rather than sensed metadata.
- **`.fsgg/providers.yml`** (schema v1, `owner: author/provider`) — the
  author-/provider-supplied selection registry mapping a `--provider <name>` to its
  `contractVersion`, `templateId`, `source`, and declared `parameters`. SDD ships no
  default entries and embeds none of these values in code (FR-002 / SC-005).

These are produced by the cross-cutting scaffold command (whose real exercise spawns
an external `dotnet new` template engine), not by the standard lifecycle the catalog
conformance reconciles — the same boundary that excludes the `validation-report`.
Their schema/round-trip contracts are enforced by the scaffold semantic suite and
the `ScaffoldProvenance` round-trip tests.

## JSON contracts vs Markdown projections

- **JSON contracts** carry a real `schemaVersion` (currently `1`) and their
  inventory enumerates **JSON fields**.
- **Markdown projections** (`summary.md`, `commands.md`, `skills.md`) are
  projections, not machine contracts. Their version **tracks the generator
  version** (`0.2.0`, from `generatorVersion.version` in the envelope) and their
  inventory enumerates **document sections**, not fields.
- The `AgentCommands` view is documented as **one entry per sub-file** because the
  three files differ in role: `guidance.json` is a machine contract; `commands.md`
  and `skills.md` are projections.

Every entry shares the same **determinism guarantee**:
`byte-stable; canonical key order; no clock/path/ANSI`. Producing any artifact
twice over identical inputs yields byte-identical output. (FR-008)

## Catalog

| Contract | Kind / format | `schemaVersion` | `contractVersion` | Stability | Determinism | Source artifact |
|---|---|---|---|---|---|---|
| `work-model.json` | generated view (`workModel`) / JSON | 1 | — | AdditiveOptional | byte-stable | `readiness/<id>/work-model.json` |
| `analysis.json` | generated view (`analysis`) / JSON | 1 | — | AdditiveOptional | byte-stable | `readiness/<id>/analysis.json` |
| `verify.json` | generated view (`verify`) / JSON | 1 | — | AdditiveOptional | byte-stable | `readiness/<id>/verify.json` |
| `ship.json` | generated view (`ship`) / JSON | 1 | — | AdditiveOptional | byte-stable | `readiness/<id>/ship.json` |
| `governance-handoff.json` | generated view (`governance-handoff`) / JSON | 1 | **1.0.0** | **Stable** | byte-stable | `readiness/<id>/governance-handoff.json` |
| `summary.md` | generated view (`summary`) / Markdown projection | gen (`0.2.0`) | — | AdditiveOptional | byte-stable | `readiness/<id>/summary.md` |
| `agent-commands/<target>/guidance.json` | generated view (`agentCommands`) / JSON | 1 | — | AdditiveOptional | byte-stable | `readiness/<id>/agent-commands/<target>/guidance.json` |
| `agent-commands/<target>/commands.md` | generated view (`agentCommands`) / Markdown projection | gen (`0.2.0`) | — | AdditiveOptional | byte-stable | `readiness/<id>/agent-commands/<target>/commands.md` |
| `agent-commands/<target>/skills.md` | generated view (`agentCommands`) / Markdown projection | gen (`0.2.0`) | — | AdditiveOptional | byte-stable | `readiness/<id>/agent-commands/<target>/skills.md` |
| `command-report (--json)` | command output / JSON | 1 | — | AdditiveOptional | byte-stable | `src/FS.GG.SDD.Commands/CommandSerialization.fs` |

> `governance-handoff.json` is the only contract carrying a cross-repo
> `contractVersion` (`1.0.0`), which moves independently of its `schemaVersion`.
> It is classed **Stable** (the envelope is frozen). All other contracts are
> **AdditiveOptional** and may gain optional fields under a minor bump; consumers
> MUST tolerate unknown fields.

## Field and section inventories

For each contract the catalog enumerates an inventory: every JSON field (with
per-field stability) for JSON contracts, or every document section for Markdown
projections. In every JSON contract the `schemaVersion` field is classed
**Stable**; remaining fields are **AdditiveOptional**. The full inventories live
in the authoritative `catalog[].inventory` of
[`release-readiness.json`](release-readiness.json); summaries follow.

### JSON contracts (fields)

- **`work-model.json`** — `decisions`, `diagnostics`, `evidence`,
  `generatedViews`, `governanceBoundaries`, `modelVersion`, `project`,
  `requirements`, `schemaVersion` *(Stable)*, `sources`, `tasks`, `workId`,
  `workItem`.
- **`analysis.json`** — `diagnostics`, `findings`, `generatedViews`, `generator`,
  `nextAction`, `optionalBoundaryFacts`, `readiness`, `schemaVersion` *(Stable)*,
  `sourceRelationships`, `sources`, `stage`, `status`, `viewVersion`, `workId`.
- **`verify.json`** — `diagnostics`, `evidenceDispositions`, `findings`,
  `generatedViews`, `generator`, `governanceCompatibility`, `lifecycleReadiness`,
  `nextAction`, `readiness`, `schemaVersion` *(Stable)*, `skillVisibility`,
  `sources`, `stage`, `status`, `taskGraph`, `testDispositions`, `viewVersion`,
  `workId`.
- **`ship.json`** — `diagnostics`, `disposition`, `evidenceDispositions`,
  `findings`, `generatedViews`, `generator`, `governanceCompatibility`,
  `lifecycleReadiness`, `nextAction`, `readiness`, `schemaVersion` *(Stable)*,
  `sources`, `stage`, `status`, `verificationReadiness`, `viewVersion`, `workId`.
- **`governance-handoff.json`** — `contractVersion` *(Stable)*, `diagnostics`,
  `evidence`, `generatorVersion`, `governanceConfig`, `governedReferences`,
  `readiness`, `schemaVersion` *(Stable)*, `sources`, `workId`.
- **`agent-commands/<target>/guidance.json`** — `behaviorModelDigest`,
  `commands`, `diagnostics`, `generated`, `generator`, `renderedFiles`,
  `schemaVersion` *(Stable)*, `skills`, `sources`, `targetId`, `viewVersion`,
  `workId`.
- **`command-report (--json)`** — `agentGuidance`, `analysis`, `changedArtifacts`,
  `checklist`, `clarification`, `command`, `context`, `diagnostics`, `evidence`,
  `generatedViews`, `governanceCompatibility`, `invocation`, `nextAction`,
  `outcome`, `plan`, `refresh`, `reportVersion`, `scaffold`, `schemaVersion`
  *(Stable)*, `ship`, `specification`, `tasks`, `verification`.

### Markdown projections (sections)

- **`summary.md`** — `Diagnostics`, `Generated-view currency`, `Next action`.
- **`agent-commands/<target>/commands.md`** — `Agent commands`.
- **`agent-commands/<target>/skills.md`** — `Agent skills`.
