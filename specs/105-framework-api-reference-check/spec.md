# Feature Specification: Plan-Time Framework-API Reference Resolution

**Feature Branch**: `item/569-framework-ref-grammar` (Phase 1; later phases branch per phase)

**Created**: 2026-07-18

**Status**: Draft

**Input**: FS.GG.SDD#569 — "no plan-time check resolves a framework API cited in a
charter/plan against the pinned package's real public surface." Design of record:
repo-local **ADR-0004**. Origin: the Rougue1 RM2 development-cycle retrospective
(2026-07-18, fsgg-sdd 0.14.0).

## Overview

A work item routinely cites a framework API — a `val`, a package symbol — that it
intends to build against or that it claims is missing. Today that citation is an
**inline backtick token in prose**: nothing parses it, and nothing resolves it
against the package the workspace actually pins. Two failure modes follow, and
both have been paid for:

1. **Dangling** — the cited API genuinely has no matching member in the pinned
   package. Discovered mid-implementation instead of at plan time.
2. **Inverse false alarm** — the API *exists* in the pinned package, but the
   author's local view says otherwise (a stale vendored `.fsi` snapshot, a
   wrong-repo grep), so the work is mis-scoped as *blocked on a framework gap*.

The RM2 incident was mode 2. RM1 deferred a persistence device host to RM2 naming
`runAppWithAudioAndPersistence`; the RM2 author concluded the API did not exist and
carried the work forward as blocked. It was a false alarm —
`runAppWithPersistence` / `runAppWithAudioAndPersistence` are real `val`s in the
pinned **FS.GG.UI.SkiaViewer 0.12.0** package. The author's two sources were both
non-authoritative: the product's *vendored* `docs/api-surface/SkiaViewer/SkiaViewer.fsi`
(frozen at an early scaffold baseline) and a grep of an *unrelated* local checkout.

### Why nothing catches this today

- **The reference is not structured.** `plan.md` Contract Impact lines parse into
  `PlanFacts.ContractReferences` (`{ ContractId; Kind; Target; SourceIds }`,
  `Plan.fs:43-48`, `:272-294`), but `Target` is free text. No field names a
  resolvable package symbol.
- **The surface read does not exist.** `surface` / `docs/api-surface/` deal only in
  byte-identical `.fsi` **text mirrored from this repo's own `src/`** — explicitly
  no reflection (spec 086). A vendored *third-party* `.fsi` has no `src/`
  counterpart, so it registers as an inert `orphanBaseline` — advisory, never
  compared, never removed. That inert orphan *is* what misled the RM2 author.
- **The registry gives version, not surface.** `registry/dependencies.yml` records
  `id` + `version` + `package-version`, but its `surface:` field is prose. It
  resolves *which version is pinned*, never *which symbols that version has*.

ADR-0004 settled the design and its two forks: the authoritative surface is a
**committed, provenance-stamped capture read from the real restored package** (a
new `dependency-surface` verb owns the `dotnet restore`; `analyze` reads only the
committed capture, staying pure/offline/deterministic; a CI drift-guard keeps it
fresh), and severity **blocks on real contradictions, advises when blind**.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A plan citing an absent framework symbol is refused at plan time (Priority: P1)

An author writes a Contract Impact line citing `framework: FS.GG.UI.SkiaViewer#doesNotExist`.
The pinned package's captured surface has no such member. `analyze` refuses with a
blocking diagnostic naming the reference and the pinned version, and writes no
`analysis.json` — the dangling reference surfaces as "blocked on a framework
change" **before** implementation begins, not during it.

### User Story 2 - A "blocked-on-framework" deferral the real package contradicts is refused (Priority: P1)

An author defers work with `blocked-on-framework: FS.GG.UI.SkiaViewer#runAppWithAudioAndPersistence`,
asserting the API is absent. The pinned package's captured surface *contains* that
member. `analyze` refuses with a blocking diagnostic: the deferral's premise is
false — the symbol resolves in the pinned package — so the mis-scoping is caught at
plan time. This is the RM2 incident, defeated by construction.

### User Story 3 - A resolvable reference passes; an unresolvable one advises, never blocks (Priority: P2)

A reference whose symbol resolves in the captured surface passes with no finding. A
reference the check *cannot* resolve — no surface was captured for that package/
version, or the run is offline — produces a **non-blocking advisory**, exit 0.
"I could not look" is never rendered as a negative verdict (ADR-0002 / #266): the
check fails open on capability and blocks only on verdicts backed by an
authoritative surface.

### User Story 4 - Capturing and drift-guarding a package's authoritative surface (Priority: P2)

An author runs `fsgg-sdd dependency-surface --update` for a pinned package. The verb
resolves the pin (from Central Package Management), restores the package, reads its
**real** public surface, and writes a committed, provenance-stamped capture under
`docs/dependency-surface/<PackageId>/<version>.json`. `--check` (default) re-reads
and diffs against the committed capture, exiting 1 on any drift — the CI drift-guard
that keeps the authoritative surface from silently going stale.

## Requirements *(mandatory)*

### Functional

- **FR-001** A Contract Impact (`PC-###`) line MAY carry a structured framework
  reference `framework: <PackageId>[@<version>]#<symbol>`. It parses into a typed
  `FrameworkApiReference { PackageId; Version option; Symbol; SourceLocation }`
  exposed on `PlanFacts`. `@<version>` is optional; when omitted the version is the
  Central Package Management pin.
- **FR-002** An Accepted Deferral MAY carry `blocked-on-framework: <ref>` using the
  same reference grammar. It parses into the deferral's framework-absence assertion,
  exposed on `PlanFacts` alongside the deferral it annotates.
- **FR-003** A malformed framework reference (missing `#symbol`, empty `PackageId`,
  unparseable `@version`) is a blocking `DiagnosticError` naming the offending token
  — a mis-typed reference must never silently resolve to "no reference".
- **FR-004** `fsgg-sdd dependency-surface --update <PackageId>` resolves the pin,
  restores the package, reads its real public surface, and writes a capture at
  `docs/dependency-surface/<PackageId>/<version>.json` (schema v1: `schemaVersion`,
  `packageId`, `version`, `capturedFrom`, content `sha256`, `symbols[]`), writing
  only when the content changed.
- **FR-005** `fsgg-sdd dependency-surface --check` (default) re-reads the real
  surface and diffs it against the committed capture; any drift is a blocking
  `DiagnosticError` (exit 1). Coherent captures exit 0.
- **FR-006** The `dotnet restore` and surface read happen ONLY in the
  `dependency-surface` verb (at the `RunProcess`/read edge). `analyze` performs no
  restore, no reflection, and no network access; it reads the committed capture as
  an input snapshot.
- **FR-007** The `analyze` check resolves each framework reference against an
  **injected** surface oracle `resolve : PackageId -> version -> SymbolSet option`.
  Verdicts:
  - use ref, symbol ∈ surface → pass;
  - use ref, symbol ∉ surface → blocking `DiagnosticError` (dangling);
  - `blocked-on-framework` deferral, symbol ∈ surface → blocking `DiagnosticError`
    (contradicted deferral, the incident);
  - `blocked-on-framework` deferral, symbol ∉ surface → pass (legitimate);
  - oracle `None` (no capture / could not look) → non-blocking advisory `Info`.
- **FR-008** The `analyze` framework-reference diagnostics have stable ids and are
  classified in `analysisFindingSeverity` so they do not fall through to the generic
  bucket. The advisory (`None`) verdict never suppresses `analysis.json`.
- **FR-009** Generic SDD embeds no provider/package literal: the check and the
  capture verb are value-agnostic. `<PackageId>` and `<version>` come only from the
  authored reference and the CPM pin — no package id, feed, or symbol is hard-coded.

### Success Criteria

- **SC-001** A plan citing an absent symbol fails `analyze` (blocking, no
  `analysis.json`); the same plan citing a present symbol passes.
- **SC-002** A `blocked-on-framework` deferral naming a symbol that exists in the
  captured surface fails `analyze`; naming a symbol that does not exists passes.
- **SC-003** With no capture present, every framework reference resolves to a
  non-blocking advisory and `analyze` still writes `analysis.json`.
- **SC-004** `dependency-surface --check` on a stale capture exits 1 and names the
  drift; on a fresh capture exits 0 and writes nothing.
- **SC-005** No package id, symbol, feed, or version literal appears in generic SDD
  source (grep-clean) — the RM2-specific `FS.GG.UI.SkiaViewer` never enters the
  product.

## Out of Scope

- **Resolving arbitrary prose references.** Only the structured `framework:` /
  `blocked-on-framework:` grammar is resolved. A framework named only in a
  backtick sentence is untouched (that is the un-tractable case ADR-0004 declined).
- **Network access inside `analyze` / the inner loop.** The restore lives only in
  `dependency-surface`.
- **`result: pass` observation** and any Governance freshness/enforcement — separate
  concerns, unchanged.
- **Wiring FS.GG.UI.* feeds into this repo's `nuget.config`** beyond what the capture
  verb needs at `--update`/`--check` time.

## Key Entities

- **`FrameworkApiReference`** — `{ PackageId; Version: string option; Symbol; SourceLocation }`,
  parsed from a `framework:` / `blocked-on-framework:` token.
- **Dependency-surface capture** — `docs/dependency-surface/<PackageId>/<version>.json`,
  schema v1, the authoritative-by-construction record of a pinned package's public
  surface, provenance-stamped and drift-guarded.
- **Surface oracle** — `resolve : PackageId -> version -> SymbolSet option`, injected
  into the pure `analyze` check (`Some` = capture present; `None` = could not look).
