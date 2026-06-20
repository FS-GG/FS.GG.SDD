---
title: SDD → Governance handoff integration requirements
category: SDD
description: Cross-repo contract mapping each governance-handoff.json field to its concrete FS.GG.Governance consumer, and pinning the schema version both repositories agree on.
---

# SDD → Governance Handoff Integration Requirements (contract v1.0.0)

This is the **shared, versioned contract** between `FS.GG.SDD` (the producer) and
`FS.GG.Governance` (the consumer). It is authored first because it pins the schema
both repositories must agree on. SDD owns this contract (CLAUDE.md: "optional
contracts consumed by FS.GG.Governance"); Governance pins the `contractVersion` it
supports and reads `readiness/<id>/governance-handoff.json` against it.

**SDD imports no FS.GG.Governance code.** The Governance types named below are the
*target* shapes this contract is validated against (by inspection and by the
mapping tests), not compile-time dependencies. This keeps the integration optional
and the two repos decoupled.

- **Producer**: `FS.GG.SDD` — emits `readiness/<id>/governance-handoff.json` as a
  generated view from the normalized work model + verify/ship readiness.
- **Consumer**: `FS.GG.Governance` — ingests the handoff into its kernel evidence
  model, routing, and gate registry.
- **Contract version**: `1.0.0` (carried in the handoff as `contractVersion`).
- **Handoff schema version**: `schemaVersion = 1` (see
  [governance-handoff.md](governance-handoff.md)).

## Ownership boundary (load-bearing)

| SDD produces (declared facts) | Governance computes / enforces |
|---|---|
| Declared evidence nodes + states (`pending`/`real`/`synthetic`/`failed`/`skipped`) | `effective` taint closure → `autoSynthetic` (`Kernel.Evidence.effective`) |
| Directed dependency edges `(dependent, dependency)` | DAG validation, cycle rejection (`Kernel.Evidence.build`) |
| Normalized governed-path references | `route` path→capability selection + glob precedence (F015) |
| Merge-boundary readiness facts + blocking diagnostic ids | Gate/fence verdict, severity, enforcement (F018/F010 merge fence) |
| `.fsgg` config presence/pointers | Profile/freshness/policy decisions (F014/Freshness) |

SDD MUST NOT emit any `autoSynthetic`/effective state, selected route, capability
verdict, profile, gate selection, severity, or pass/fail verdict (FR-005, FR-009;
verified by the boundary-exclusion test, SC-005).

## Field → consumer mapping

### 1. Evidence projection → `FS.GG.Governance.Kernel.Evidence`

Governance's evidence model (F005) is built by
`Evidence.build (nodes: ('id * EvidenceState) list) (dependencies: ('id * 'id) list)`
and closed by `Evidence.effective`. The handoff supplies exactly those two inputs.

| Handoff field | Governance consumer | Notes |
|---|---|---|
| `evidence.nodes[].id` | `'id` (node identity) in `Evidence.build` | Stable, namespaced string id (see id scheme below). `'id: comparison` is satisfied by string. |
| `evidence.nodes[].state` | `EvidenceState` declared case | One of `pending`/`real`/`synthetic`/`failed`/`skipped` — **never** `autoSynthetic` (computed-only; `build` returns `AutoSyntheticDeclared` if it sees one). Tokens match `Kernel.Json` exactly. |
| `evidence.dependencies[].dependent` / `.dependency` | edge `(a, b)` = "`a` rests on `b`" in `Evidence.build` | Direction is dependent→dependency, matching the kernel's documented edge semantics. |

**Evidence state mapping** (SDD `WorkModel.EvidenceEntry` → declared
`EvidenceState` token). Total and exhaustive over SDD's evidence result space; the
mapping table is covered by tests (SC-004):

| SDD `EvidenceEntry` | Handoff `state` | Rationale |
|---|---|---|
| `Synthetic = true` (any result) | `synthetic` | Root-cause taint; declared at source, reported verbatim (kernel FR-008). Synthetic dominates the result. |
| `Result` ∈ {supported, passed, real, verified} | `real` | Done, real evidence. |
| `Result` ∈ {deferred, accepted-deferral} | `skipped` | `[-]` Skipped with a rationale the consumer holds; SDD carries `rationale` alongside. |
| `Result` ∈ {missing, none, not-started} | `pending` | `[ ]` Not started. |
| `Result` ∈ {failed, invalid} | `failed` | `[F]` Failed. |
| `Result` = stale | base state + `staleEvidence` diagnostic | Staleness is Governance-owned freshness (Kernel.Freshness); SDD maps to the underlying declared state and reports a stale diagnostic, never inventing a state token. |

> ✅ **Resolved (2026-06-20):** Governance **confirmed `deferred → skipped`** in
> `FS.GG.Governance` ADR `docs/decisions/0002-sdd-governance-handoff-contract.md`
> — a deferral carries a recorded rationale, so it is a `[-]` skip ("done, skipped
> with rationale"), not a `[ ]` not-started (`pending`). No `contractVersion` bump.
> SDD's verify readiness still counts deferrals separately (`EvidenceDeferredCount`).

**Node id scheme.** To give the generic `'id` graph unambiguous, stable identities
across evidence and the tasks evidence rests on, ids are namespaced:

- `evidence:<EvidenceId>` for each `EvidenceEntry`.
- `task:<TaskId>` for each `TaskEntry` that participates in a dependency edge.

**Edge derivation** from the normalized work model:

- `evidence:<e>` → `task:<t>` for each `t` in `EvidenceEntry.TaskRefs`.
- `task:<t>` → `task:<d>` for each `d` in `TaskEntry.Dependencies`.
- `task:<t>` → `evidence:<e>` for each `e` in `TaskEntry.RequiredEvidence`.

Task node declared state derives from `TaskEntry.Status` (done→`real`,
blocked/failed→`failed`, todo/in-progress→`pending`); this lets Governance's taint
closure flow through the task graph end-to-end, exactly as its F10 dogfood adapter
does over `TaskDependsOn`. SDD emits the DAG as-is; if SDD's own diagnostics
already flag a cycle, the handoff carries it plus the existing diagnostic and
Governance's `build` will return `Cycle` (SDD does not pre-reject).

### 2. Governed-path references → routing (F015)

`Routing.route` matches F014-normalized `GovernedPath`s against capability globs.
The handoff supplies the work item's governed/changed artifact paths as routing
*enrichment*.

| Handoff field | Governance consumer | Notes |
|---|---|---|
| `governedReferences[].path` | `GovernedPath` input to `Routing.route` | Already-normalized, forward-slash, repo-relative (matches F014 normalization). |
| `governedReferences[].owner` / `.relationship` | advisory routing context | From `WorkModel.GovernanceBoundaries`. |
| `governedReferences[].kind` / `.operation` | advisory | From `CommandTypes.ArtifactChange`. |

> Overlap note (resolved): Governance can already sense changed paths itself via
> F016 git/CI snapshot facts. The handoff's references are therefore **optional
> enrichment** (artifact-level provenance tied to the work item), not the primary
> routing source. Governance MAY ignore them and route from its own snapshot. SDD
> emits them because they are cheap and make the work-item→path linkage explicit;
> they carry no selected route.

### 3. Merge-boundary readiness → gate/fence (F018 / F010 merge fence)

The gate registry (F018) builds `Gate` records from `.fsgg` capability checks;
gate prerequisites are `RequiresCommand` only. SDD does **not** supply gates. SDD
supplies the *readiness facts* a merge fence / future "SDD-readiness" gate reads.

| Handoff field | Governance consumer | Notes |
|---|---|---|
| `readiness.shipDisposition` | fence input (advisory) | From `CommandTypes.ShipSummary.Disposition`. |
| `readiness.verificationReadiness` | fence input (advisory) | From `ShipSummary.VerificationReadiness`. |
| `readiness.blockingDiagnosticIds` | fence input (advisory) | The ids a blocking rule (e.g. `evidenceNotSynthetic`, contracts-current) can reference. |
| `readiness.counts` (advisory/warning/blocking) | fence input (advisory) | From `ShipSummary`/`VerificationSummary` counts. |
| `readiness.perViewState` | currency input (advisory) | Per generated-view currency; relates to Governance stale-view blocking (SDD Phase 7 boundary item). |

These are **declared advisory inputs to a Governance decision**, never an
enforcement verdict (FR-008). Whether SDD readiness becomes an actual entry in the
F018 gate registry or a merge-fence condition is a **Governance-side decision**,
out of scope for this SDD feature.

### 4. `.fsgg` configuration presence → profile/policy/freshness (F014)

| Handoff field | Governance consumer | Notes |
|---|---|---|
| `governanceConfig.policyPresent` + `.policyPointer` | F014 `.fsgg/policy.yml` loader | Presence + pointer only; SDD never parses policy semantics. |
| `governanceConfig.capabilitiesPresent` + `.capabilitiesPointer` | F014 `.fsgg/capabilities.yml` loader | Drives F015 routing + F018 gates on the Governance side. |
| `governanceConfig.toolingPresent` + `.toolingPointer` | F014 `.fsgg/tooling.yml` loader | Drives gate timeouts/command index. |

When absent, each `*Present` is `false` and the pointer is omitted; the handoff is
still produced (FR-011, SC-002).

## Provenance / currency fields (SDD-owned generated-view discipline)

| Handoff field | Source | Purpose |
|---|---|---|
| `schemaVersion` | constant `1` | Machine-contract version of the document shape. |
| `contractVersion` | constant `1.0.0` | This cross-repo contract version both repos pin. |
| `generatorVersion` | `GeneratorVersion` | Stale detection by generator identity. |
| `sources[]` (path + digest + schema version) | `GenerationManifest.SourceIdentity` | Stale detection by source digest, not file presence (FR-003, FR-012). |
| `diagnostics[]` | `Diagnostics.Diagnostic` | Stale/missing/partial-config/cycle diagnostics (Constitution VIII). |

## What Governance must build to consume this (Governance-repo work — not blocking SDD)

1. A reader/parser for `readiness/<id>/governance-handoff.json` (F008-style
   `ReadArtifact` + parse, parallel to F014's `.fsgg` loader), pinned to
   `contractVersion` 1.x.
2. An SDD-native adapter mapping the handoff's `evidence.nodes` + `dependencies`
   into `Evidence.build` and running `Evidence.effective` (parallel to the F10
   SpecKit adapter over `TaskDependsOn`).
3. Optional: fold `governedReferences` into `Routing.route` inputs (or ignore in
   favour of F016 snapshot facts).
4. A decision on whether SDD merge-boundary readiness becomes a gate-registry
   entry / merge-fence condition (F018/F010).

## Versioning

- Additive field additions: bump `contractVersion` **minor** (1.0.0 → 1.1.0);
  consumers on 1.0.0 ignore unknown fields.
- Mapping or shape changes that alter meaning: bump `contractVersion` **major**
  and `schemaVersion`, and add a migration note here and in the handoff schema.
- A consumer that does not recognize the handoff's `contractVersion` major MUST
  report a version-mismatch finding rather than misread the document
  (Constitution VIII).
