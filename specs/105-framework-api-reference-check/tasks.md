# Tasks: Plan-Time Framework-API Reference Resolution

**Input**: `specs/105-framework-api-reference-check/{spec,plan}.md`; design of record ADR-0004.

**Tier**: Tier 1 (new grammar, new CLI verb, new committed schema + drift-guard, new
blocking `analyze` diagnostics, additive public surface).

**Tracks**: FS.GG.SDD#569.

**Sequencing**: dependency-ordered phases; each phase is its own reviewable PR.
Touch-set declared on #569, confirmed DISJOINT from every live claim.

## Format

`[ID] [P?] [Story] Description` — `[X]` done, `[ ]` open, `[-]` dropped. `[P]` = parallelizable.

## Phase 1: Reference grammar (this PR series' first increment)

- [X] T001 [US1] `FrameworkReferenceKind` (`FrameworkUse | FrameworkBlockedOn`) and
  `FrameworkApiReference` type (`{ PackageId; Version: string option; Symbol; Kind;
  SourceIds; SourceLocation }`), plus the internal token parse for
  `framework: <PackageId>[@<version>]#<symbol>`
  (`src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Plan.fs` + `.fsi`) — FR-001
- [X] T002 [US1] Parse `framework:` tokens on Contract Impact lines and
  `blocked-on-framework:` tokens on Accepted Deferral lines (the bare `framework:`
  guarded against matching the tail of `blocked-on-framework:`); expose both on the
  new `PlanFacts.FrameworkApiReferences` field, deterministically sorted — FR-001, FR-002
- [X] T003 [P] [US1] Malformed-token diagnostic `malformedFrameworkReference`
  (`DiagnosticError`, `Diagnostics.fs` + `.fsi`): a `framework:`/`blocked-on-framework:`
  token missing `#symbol` or with an empty `PackageId` blocks, naming the token —
  never a silent non-match — FR-003
- [X] T004 [P] Regenerated `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline`
  (`FSGG_UPDATE_BASELINE=1`) and the byte-mirror `docs/api-surface/` baselines for
  `Plan.fsi` + `Diagnostics.fsi`; `surface --check` coherent (59 checked, 0 drift) —
  Constitution III
- [X] T005 [US1] Unit tests: a well-formed `framework:` ref parses to the expected
  triple (with and without `@version`); a `blocked-on-framework:` deferral parses;
  a malformed token blocks and is dropped; a plain prose plan yields no reference
  (`tests/FS.GG.SDD.Artifacts.Tests/PlanArtifactTests.fs`) — FR-001..003
- [ ] T006 [P] Authoring docs for the grammar (`.claude/skills/fs-gg-sdd-plan/`,
  `.claude/skills/fs-gg-sdd-authoring-contracts/`, `docs/reference/authoring-contracts.md`).
  **Deferred to Phase 3**: documenting a `framework:` reference before the analyze
  check exists would teach authors a grammar nothing yet resolves; the plan skill is
  a *seeded* skill (lockstep `.codex`/`.agents` mirror + `skill-manifest` + golden
  re-pin), so it lands with the check that gives the grammar meaning — Constitution II

## Phase 2: `dependency-surface` capture verb + drift-guard

Split into two reviewable PRs: **2a** the capture artifact model + surface-read
extraction (T007 + the pure surface-read that T008's edge calls), landed with no
verb yet — same "foundation ahead of its consumer" shape as Phase 1's grammar;
**2b** the `dependency-surface` verb, CLI, projections, drift-guard, and CI (T008–T012).

- [X] T007 Capture artifact model: schema v1 record (`schemaVersion`, `packageId`,
  `version`, `capturedFrom`, `sha256`, `symbols[]`) + serialize/parse in `Artifacts`
  (`DependencySurface.fs(i)`), content-addressed by a canonical symbol digest, plus the
  reflection-tolerant `symbolsFromAssembly` surface-read extraction the verb's edge calls
  (kept in `Artifacts`, single-sourced and unit-tested against a loaded assembly). Public
  surface additive; `PublicSurface.baseline` + `docs/api-surface` mirror regenerated;
  `surface --check` coherent — FR-004 (PR 2a)
- [X] T008 `dependency-surface` handler (`HandlersDependencySurface.fs`): a new
  `ReadPackageSurface` effect reads the package's real surface at the edge by loading its
  restored assembly from the global packages cache and reflecting it (settling the ADR-0004
  open decision toward reflection). `--update` `WriteFile`s a canonical capture for every
  reconciled target (drifted/new); `--check` (default) re-reads + diffs the committed digest
  against the real surface, blocking on drift, advising (never blocking) when the surface is
  unreadable. Restore is the workspace's own (a consumer references the package); the verb reads
  what restore left behind — recorded here as the settled v1 mechanism (a verb-owned
  `dotnet restore` is a follow-up) — FR-004, FR-005, FR-006
- [X] T009 [P] CLI surface + report block for `dependency-surface` (`FS.GG.SDD.Cli`
  `Options`/help; `CommandTypes` DU + summary; json/text/rich projections; `dependencySurface.*`
  diagnostics incl. a `rootEscape` containment guard) — FR-004, FR-005
- [X] T010 CI drift-guard step running `dependency-surface --check` in `gate.yml` (inert in
  FS.GG.SDD itself, which commits no captures; fires the moment one is committed and a consumer
  inherits it) — FR-005
- [X] T011 Tests (`DependencySurfaceCommandTests`, real edge over the restored `Spectre.Console`):
  `--update` writes a content-addressed capture; `--check` on a fresh capture matches (exit 0); a
  stale committed digest drifts (exit 1, `dependencySurface.drift`); `--update` reconciles it; an
  uncached package is advisory (exit 0); an escaping `baselineRoot` blocks with no write. Plus
  projection parity (`DependencySurfaceProjectionTests`) — SC-004
- [X] T012 [P] Value-agnostic guard (`DependencySurfaceGuardTests`): no package id / feed / symbol
  literal in the dependency-surface source, with a planted-violation proof — FR-009, SC-005

## Phase 3: `analyze` check

- [ ] T013 Diagnostic constructors `frameworkApiDangling` /
  `frameworkApiDeferralContradicted` (`DiagnosticError`) and
  `frameworkApiSurfaceUnavailable` (advisory `Info`) in
  `CommandReports/DiagnosticConstructors.fs` + remediation pointers — FR-007, FR-008
- [ ] T014 Pure rule `frameworkReferenceDiagnostics (resolve: PackageId -> version ->
  SymbolSet option) planFacts` in `ViewGeneration.fs` with the five verdicts;
  classify the new ids in `analysisFindingSeverity` — FR-007, FR-008
- [ ] T015 Wire the rule into `HandlersAnalyze` `commandDiagnostics` (guarded on
  `planFacts`); bind the oracle at the edge to the committed capture — FR-006, FR-007
- [ ] T016 Fixtures + tests: dangling (block), contradicted deferral (block), clean
  (pass), no-capture (advisory, `analysis.json` still written), asserted by
  diagnostic id (`tests/FS.GG.SDD.Commands.Tests/AnalyzeCommandTests.fs`) — SC-001,
  SC-002, SC-003

## Phase 4 (optional): reconcile the vendored orphan

- [ ] T017 Actively diff the vendored `docs/api-surface/` orphan of a captured
  package against its authoritative capture; surface disagreement, retiring the
  `orphanBaseline` staleness hole end to end — ADR-0004 D2 tail

## Dependencies

- Phase 1 → Phase 3 (the check consumes the grammar).
- Phase 2 → Phase 3 (the oracle reads the capture).
- Phase 4 depends on Phase 2.
- Within a phase, `[P]` tasks are independent; non-`[P]` tasks are ordered.
