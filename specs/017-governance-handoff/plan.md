# Implementation Plan: Governance Readiness Handoff Contract

**Branch**: `017-governance-handoff` | **Date**: 2026-06-20 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/017-governance-handoff/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this repository
is on Git branch `main`; the active feature context is persisted in
`.specify/feature.json` as `specs/017-governance-handoff`.

## Implementation Status — ✅ Complete (validated 2026-06-20)

> Legend: ✅ done · 🟡 in progress · ⬜ not started / queued · ➖ out of scope here

**SDD producer side (this repo) — ✅ shipped, `dotnet test` 336 passing / 0 failed:**

| Area | Status | Where |
|---|---|---|
| ✅ Pure projection module (`GovernanceHandoff.fsi/.fs`) | ✅ | `src/FS.GG.SDD.Artifacts/GovernanceHandoff.*` |
| ✅ New `GovernanceHandoff` generated-view kind + output path | ✅ | `GenerationManifest.fsi/.fs` |
| ✅ `ship` emits `readiness/<id>/governance-handoff.json` | ✅ | `CommandWorkflow.fs` (additive, gated like `ship.json`) |
| ✅ `refresh` regenerates + currency-reports the handoff | ✅ | `CommandWorkflow.fs` (`govClass`, `perViewState`) |
| ✅ Declared evidence nodes/edges, no computed taint (US2) | ✅ | total `mapEvidenceState`; boundary-exclusion tests |
| ✅ Governed refs + `.fsgg` presence, no route (US3) | ✅ | `governanceConfigPresence`, `governedReferences` |
| ✅ Merge-boundary readiness as advisory facts (US4) | ✅ | `ReadinessFacts` parsed from `ship.json` |
| ✅ 18 new tests (envelope, determinism, mapping, edges, stale/refresh, boundary-exclusion) | ✅ | `tests/.../GovernanceHandoffTests.fs` |
| ✅ Advisory `GovernanceCompatibility*` placeholders repointed to the handoff (T019) | ✅ | `.fsi` pointer docs (structural removal deferred) |
| ✅ Evidence: CLI smoke, FSI transcript, quickstart validation, full suite | ✅ | `specs/017-governance-handoff/readiness/` |

**FS.GG.Governance consumer side (sibling repo) — coordination accepted, build queued:**

The sibling repo has shipped the consumer *surface* the contract targets (F005 evidence,
F014 config, F015 routing, F016 snapshot, F017 findings, F018 gates, F019 route selection,
F020 route.json, F021 gates.json) and **formally accepted this contract**:

| Cross-repo item | Status | Source |
|---|---|---|
| ✅ Contract **v1.0.0** acknowledged & accepted | ✅ | Governance `docs/decisions/0002-sdd-governance-handoff-contract.md` (2026-06-20) |
| ✅ Open mapping point `deferred → skipped` **confirmed** (no `contractVersion` bump) | ✅ | ADR 0002 — a deferral is a `[-]` skip-with-rationale, not `[ ]` pending |
| ✅ Ownership boundary confirmed (SDD declares; Governance computes taint/route/gate/freshness) | ✅ | ADR 0002 |
| ⬜ Handoff reader/parser pinned to `contractVersion` 1.x | ⬜ queued (Governance-side, non-blocking) | ADR 0002 §queued #1 |
| ⬜ SDD-native adapter → `Evidence.build`/`Evidence.effective` | ⬜ queued | ADR 0002 §queued #2 |
| ⬜ Fold `governedReferences` into `Routing.route` (optional) | ⬜ queued (MAY ignore for F016 snapshot) | ADR 0002 §queued #3 |
| ⬜ Decide SDD readiness → F018 gate entry / F010 merge fence | ⬜ queued | ADR 0002 §queued #4 |
| ➖ Governance rule eval / freshness / enforcement | ➖ Governance-owned, out of scope for SDD | CLAUDE.md boundary |

**Net**: the SDD→Governance seam is **live and accepted from both sides**. SDD emits the
declared-facts handoff today; Governance's consumer wiring (reader → kernel adapter →
gate/fence decision) is queued Governance-side work that does not block this feature.

## Summary

Deliver the first explicit, versioned, optional SDD-owned contract *consumed by*
FS.GG.Governance: a **Governance handoff** generated readiness view,
`readiness/<id>/governance-handoff.json`, that projects the already-complete
normalized work model plus verify/ship readiness into exactly the shapes
Governance's now-shipped consumer surface ingests — **declared** evidence nodes
and dependency edges in the form `FS.GG.Governance.Kernel.Evidence.build`
consumes (F005), normalized governed-path references for path→capability routing
(F015), and the merge-boundary readiness facts a gate/fence reads (F018) — while
performing **no** rule evaluation, taint closure, freshness computation, routing,
profile, or gate selection. SDD declares facts; Governance computes and enforces.

The headline design artifact is the cross-repo **integration-requirements
contract** ([contracts/integration-requirements.md](contracts/integration-requirements.md)),
authored first: it maps every handoff field to its concrete Governance consumer
type and pins the single schema version both repositories agree on. The handoff
JSON schema ([contracts/governance-handoff.md](contracts/governance-handoff.md))
is the SDD-owned machine contract; the projection is a pure value/fold over the
work model (no new MVU), emitted through the existing `ship` and `refresh` command
workflows and currency-checked like every other generated view.

This feature **supersedes** SDD's existing advisory placeholders — the
`GovernanceCompatibility` booleans (`RouteAware`/`ProfileAware`/`FreshnessAware`/
`EnforceableBySdd`) and per-command `GovernanceCompatibilityFact` — by emitting
the concrete facts they approximated. It adds a new generated-view schema and a
new public F# surface (a pure projection module + serialization); it adds **no**
new lifecycle stage and changes **no** authored-source schema. SDD remains fully
usable with no Governance runtime installed: the handoff is always produced from
declared SDD facts and is consumed only when a Governance feature adopts the
versioned contract.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0`.

**Primary Dependencies**: Existing `FS.GG.SDD.Artifacts` (the `WorkModel`,
`GenerationManifest`, `Diagnostics`, `SchemaVersion`/`GeneratorVersion`,
`ArtifactRef`, and canonical `Serialization` modules) and `FS.GG.SDD.Commands`
(the `ship` and `refresh` command workflows and their effect interpreter). No new
package dependency, and **no compile-time dependency on any FS.GG.Governance
package** — the contract is reproduced as an SDD-owned schema and validated
against Governance's published consumer shapes by the integration-requirements
contract, not by referencing Governance code (Constitution engineering
constraints; CLAUDE.md "explicit, versioned, optional").

**Storage**: Filesystem only. The handoff is written to
`readiness/<id>/governance-handoff.json` through the existing command effect
interpreter; the projection reads the normalized work model and verify/ship
readiness already present under `readiness/<id>/`.

**Schema/Migration**: New generated-view schema `governance-handoff.json`,
`schemaVersion = 1`, carrying a `contractVersion` ("1.0.0") that is the cross-repo
integration version. A new `GenerationManifest.GeneratedViewKind` case,
`GovernanceHandoff`, identifies the view. Migration posture: additive; future
field additions bump `contractVersion` minor, breaking shape changes bump
`schemaVersion` and require a migration note. Markdown stays an authoring surface;
this JSON is the machine contract (Constitution II).

**Testing**: `dotnet test` with xUnit over real disposable-project fixtures
(reusing `TestSupport`): projection unit tests over a normalized work model with
mixed declared evidence states and a dependency topology; determinism (byte-stable
across two productions); no-Governance production (no `.fsgg/policy.yml`/
`capabilities.yml`/`tooling.yml` present); boundary-exclusion assertions (no
route/profile/gate/severity/effective/`autoSynthetic` token ever appears in an
SDD-produced handoff); evidence-state mapping table coverage (every SDD evidence
`Result`/`Synthetic` combination maps to the agreed declared
`pending|real|synthetic|failed|skipped` token); edge-projection coverage; stale +
refresh currency; authored-source byte-identity; a real CLI `ship`/`refresh`
process smoke captured as readiness evidence; FSI public-surface transcript;
surface baseline update; full suite green.

**Target Platform**: Cross-platform .NET on Linux/macOS/Windows.

**Project Type**: Library projection + generated-view contract over the existing
F# command workflow. One new public module in `FS.GG.SDD.Artifacts`, additive
wiring in `FS.GG.SDD.Commands`, no new project.

**Performance Goals**: The handoff projection and emission add negligible overhead
to `ship`/`refresh`; the projection is a pure fold over the in-memory work model.
Two productions over an identical source tree are byte-identical.

**Constraints**: Declared facts only — SDD MUST NOT compute effective/auto-synthetic
evidence states, select routes/profiles/gates, or compute freshness (FR-005,
FR-009, SC-004, SC-005). Deterministic JSON excludes implicit clocks, durations,
host paths, ordering nondeterminism, and ANSI styling (FR-004). The handoff is
optional and additive: no Governance runtime required, and no existing command's
output changes when `.fsgg` Governance files are absent (FR-010, SC-002). Authored
sources stay byte-identical (FR-015, SC-007). Generated-view currency comes from
regeneration, never file presence (FR-012). No FS.GG.Governance package reference
in SDD code; the cross-repo schema is versioned in
`contracts/integration-requirements.md`.

**Scale/Scope**: One new generated view per work item; one pure projection module
+ serialization; additive `ship`/`refresh` emission; one new `GeneratedViewKind`
case; supersession of two advisory placeholder types; the cross-repo contract doc.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | The new `GovernanceHandoff` projection + serialization is a public surface; it is authored `.fsi`-first, exercised through the public surface in FSI, given semantic tests over the public API, then implemented. | PASS (planned) |
| II. Structured Artifacts Are the Machine Contract | The handoff JSON is a schema-versioned machine contract; the integration-requirements + handoff-schema contracts define authoritative data; any Markdown rendering is a projection. Prose/structured conflict follows the work model's structured-wins rule and surfaces the existing diagnostic. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | The new module ships a `.fsi`; the public surface baseline is updated; the `GeneratedViewKind` addition and any `CommandTypes` additions update signatures and baselines. | PASS |
| IV. Idiomatic Simplicity Is the Default | The projection is plain records + a total fold over `WorkModel`; reuses existing canonical serialization. No framework, reflection, or new abstraction. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | The projection is pure (no MVU needed). Emission reuses the existing `ship`/`refresh` `Model`/`Msg`/`Effect`/`update` boundary and effect interpreter — no new stateful workflow or I/O path. | PASS |
| VI. Test Evidence Is Mandatory | Real disposable-project fixtures drive `ship`/`refresh`; determinism, no-Governance, boundary-exclusion, evidence-mapping, edge, stale/refresh, and byte-identity assertions; real CLI smoke evidence; green full suite. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | The handoff is a generated view derived from the one normalized model; it is not a second source of truth; authored sources are preserved; CLI, agents, CI, and Governance consume the same artifact. | PASS |
| VIII. Observability And Safe Failure | Stale handoff, missing/partial `.fsgg` Governance config, and projection failures produce actionable diagnostics; the optional integration degrades explicitly and never fails an SDD command when Governance is absent. | PASS |

No constitution violations. Complexity tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/017-governance-handoff/
|-- plan.md
|-- research.md
|-- data-model.md
|-- contracts/
|   |-- integration-requirements.md   # FIRST artifact: handoff field -> Governance consumer + pinned schema version
|   `-- governance-handoff.md          # SDD-owned governance-handoff.json schema contract
|-- quickstart.md
`-- tasks.md                           # created by /speckit-tasks, not this command
```

### Source Code (repository root)

```text
src/
|-- FS.GG.SDD.Artifacts/
|   |-- GovernanceHandoff.fsi          # NEW: handoff record types + pure projection + JSON serialization signatures
|   |-- GovernanceHandoff.fs           # NEW: pure fold over WorkModel + verify/ship readiness -> handoff -> canonical JSON
|   |-- GenerationManifest.fsi/.fs     # UPDATED: add GovernanceHandoff GeneratedViewKind case + expected output path helper
|   |-- LifecycleRuleContracts.fsi/.fs # UPDATED: supersede advisory GovernanceCompatibility booleans with the concrete contract reference
|   `-- ArtifactRef.fsi/.fs            # UPDATED (if needed): GovernanceHandoff view artifact reference
|-- FS.GG.SDD.Commands/
|   |-- CommandWorkflow.fs             # UPDATED: ship + refresh emit/regenerate the handoff view (additive effect)
|   |-- CommandTypes.fsi/.fs           # UPDATED: handoff summary in CommandReport; supersede GovernanceCompatibilityFact
|   |-- CommandReports.fs              # UPDATED: build the CommandReport handoff summary; supersede GovernanceCompatibilityFact
|   `-- CommandSerialization.fs        # UPDATED: serialize the handoff summary deterministically
`-- FS.GG.SDD.Cli/                     # UNCHANGED command surface (ship/refresh already exist)

tests/
`-- FS.GG.SDD.Commands.Tests/
    |-- GovernanceHandoffTests.fs      # NEW: projection, mapping table, edges, determinism, no-Governance, boundary-exclusion, stale/refresh
    `-- TestSupport.fs                 # REUSED (existing run* helpers incl. runShip, runRefresh)

surface/                               # UPDATED: public surface baseline for the new module + GeneratedViewKind case
specs/017-governance-handoff/readiness/  # CLI ship/refresh smoke + full-suite + FSI + boundary-review evidence
```

**Structure Decision**: Add one pure projection module to `FS.GG.SDD.Artifacts`
(where `WorkModel` and `GenerationManifest` already live) and wire its emission
additively into the existing `ship` and `refresh` command workflows in
`FS.GG.SDD.Commands`. The handoff is a generated view exactly like
`work-model.json`/`summary.md`, so it follows their proven manifest + currency +
refresh machinery rather than introducing a new generator or command. The
projection is pure (Constitution IV/V), so only the already-MVU `ship`/`refresh`
commands touch I/O. The cross-repo schema lives in `contracts/` and is versioned,
not imported from Governance, preserving the optional, decoupled boundary.

## Complexity Tracking

No complexity exceptions are introduced.

## Phase 0 Research

Research is recorded in [research.md](research.md). It resolves: the evidence
state mapping (SDD `EvidenceEntry.Result`/`Synthetic` -> Governance declared
`EvidenceState` token), the evidence dependency-graph node identity and edge
derivation, the routing-reference vs Governance-git-sensing overlap, the
handoff-vs-existing-views decision, and the schema/version ownership split. No
clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [contracts/integration-requirements.md](contracts/integration-requirements.md) (authored first)
- [contracts/governance-handoff.md](contracts/governance-handoff.md)
- [data-model.md](data-model.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: the new projection module is `.fsi`-first with a baseline; the `GeneratedViewKind` and `CommandTypes` additions update signatures and baselines. |
| Structured machine contract | PASS: the handoff JSON is the versioned machine contract; the integration-requirements doc pins the cross-repo schema; Markdown stays authoring-only. |
| Public API baseline | PASS: surface baseline updated for the new module and enum case; no unrelated baseline churn. |
| MVU boundary | PASS: pure projection; emission reuses the existing `ship`/`refresh` MVU + interpreter. |
| Evidence | PASS: real-fixture projection/determinism/no-Governance/boundary-exclusion/mapping/stale-refresh tests, CLI smoke evidence, green full suite. |
| Agent contract | PASS: generated view from the one model; authored sources preserved; agent context marker points at this plan. |
| Safe failure | PASS: stale/missing/partial-config diagnostics; optional integration degrades explicitly; no SDD command fails when Governance is absent. |

No new complexity exceptions were introduced by Phase 1 design.
