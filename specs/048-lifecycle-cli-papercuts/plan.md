# Implementation Plan: Lifecycle/CLI Semantics Papercuts

**Branch**: `048-lifecycle-cli-papercuts` | **Date**: 2026-06-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/048-lifecycle-cli-papercuts/spec.md`

## Summary

Five reported lifecycle/CLI papercuts (FS-GG/FS.GG.SDD#39 §3.1–§3.5) each make a
correct run *look* wrong, a stale run *look* fixed, or block a benign authoring
gesture. All five are **reporting-clarity / re-evaluation-semantics** fixes that
are surgical and SDD-local; none touches a Governance contract (FR-013). Each
maps to an isolated seam already discovered in the code:

1. **§3.1 stale `checklist` rows (US1/FR-001).** On a stale re-run,
   `checklistDiagnosticsTextAndSummary` (`ParsingMid.fs:347-417`) preserves prior
   rows via `ensureChecklistSections` + an `existingSourceIds` filter and merely
   *appends* one `stale:` row (`appendStaleChecklistResult`), emitting only
   **warning**-severity diagnostics → `succeededWithWarnings` while the old `fail`
   rows survive. Fix: when `sourceSnapshotStale` is true, **purge and re-derive
   all rows from current sources** (the same derivation the fresh `checklistTemplate`
   path uses), rewriting the source snapshot, so no row reviewed against the
   superseded snapshot survives. When not stale, the existing preserve/`noChange`
   path is unchanged (FR-012).

2. **§3.2 `specify` silent no-op (US1/FR-002).** `specify` has **no** snapshot/digest
   concept; on re-run it only calls `ensureSpecificationSections` (adds missing
   sections), so an edited-but-section-complete `spec.md` yields a bare `NoChange`
   with no signal. Resolution (research.md): **document-by-reporting** — `specify`
   already re-parses the live `spec.md` into its `SpecificationSummary` every run;
   we add a deterministic report statement (a `NextAction`/advisory fact) that
   `specify` promotes only the first draft and that downstream stages read the
   live file, so the author always knows the edit is consumed downstream (FR-002,
   SC-002). No authored bytes are clobbered.

3. **§3.3 ambiguity-disclaimer blocks `clarify` (US3/FR-003,004).** A bullet under
   `## Ambiguities` is forced into a blocking state by two cooperating pieces in
   `Specification.fs`: a bullet without an `AMB-###` id → `missingSpecificationId`
   error (`missingIdDiagnostics`, `:84-102`); a bullet with one → a real
   `AmbiguityId` that `clarify` turns into a blocking item. Only the non-bullet
   prose `No material ambiguities recorded.` escapes today. Fix: teach the
   `## Ambiguities` parse (`missingIdDiagnostics` and the id extraction at
   `Specification.fs:176`) to recognize a **no-outstanding sentinel** (prose or
   bullet, e.g. `None outstanding` / `No … ambiguit…`), mirroring the existing
   `StartsWith "No "` convention in `Internal.fs:211-218`, so the disclaimer is
   treated as empty-of-questions while genuine `AMB-###` bullets still parse and
   block (FR-004).

4. **§3.4 self-inflicted `staleGeneratedView` (US2/FR-005,006,007).** Root cause is
   **not** the readiness write (readiness `.json` files are filtered out of
   work-model sources at `WorkItem.fs:158-164`; staleness is digest-based, no
   mtime). It is a **snapshot-set mismatch**: the work model is *generated* from
   `workModelSnapshots` (`ViewGeneration.fs:476-502`) which includes `plan.md` and
   `charter.md`, but `existingGeneratedViewDiagnostic`'s currency check builds
   `currentSnapshots` (`ViewGeneration.fs:452-461`) **omitting** `plan.md`/`charter.md`.
   So `sourceStale`'s "recorded source absent from current set → stale" branch
   (`Serialization.fs:248-253`) fires on every verify/ship run regardless of
   authored change. Fix: make `currentSnapshots` mirror the exact generation source
   set (add `planPath`/`charterPath`). Genuine upstream edits (e.g. an edited
   `spec.md` digest) still flag via `sourceStale` (FR-007). The fix lands at the
   shared currency seam, so earlier stages benefit too; acceptance is scoped to
   verify/ship (FR-005/006).

5. **§3.5 `--help` returns `unknownCommand` (US4/FR-008–011).** `--help`/`-h` fall
   through to `printUnknown` (`Program.fs:139`); `<command> --help` is silently
   ignored. There is **no per-command flag metadata** anywhere. Fix: introduce a
   static flag-metadata table (new `CommandHelp` module in `FS.GG.SDD.Commands`)
   covering every command — the lifecycle `SddCommand` cases plus the CLI-level peers
   `--version`/`validate`/`registry` (`Program.fs:130,131,136`) — so FR-009's "every
   command" holds, add a help branch in `Program.run` (peer of `--version`/`validate`/
   `registry`, dispatched before/around `parseCommand`), and project help through the existing
   `CommandReport` three ways via a new additive `Help: HelpSummary option` field.
   Unknown commands still resolve to `unknownCommand` even with `--help` (FR-011).

**Change tier**: **Tier 1 (contracted)** — adds a `CommandReport` jsonField
(`help`), a new public `CommandHelp` module, changes generated `checklist.md`
re-run content, and updates the release catalog, schema-reference, and golden
fixtures.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: Existing in-repo only — `FS.GG.SDD.Artifacts`
(lifecycle artifacts, work model, currency engine), `FS.GG.SDD.Commands`
(MVU command workflow, report/serialization/rendering), `FS.GG.SDD.Cli`
(`Program.fs` dispatch, Spectre rendering). No new packages.

**Storage**: Files only — authored `spec.md`/`checklist.md`, generated
`readiness/<id>/work-model.json`, and command-report stdout/stderr.

**Testing**: xUnit 2.9.3 with hand-written `Assert.*`; inline triple-quoted
golden strings plus on-disk fixture trees under `tests/fixtures/**`. Spectre rich
output rendered against a fixed-width, color-off console; excluded from golden
contracts.

**Target Platform**: Cross-platform CLI (`fsgg-sdd`).

**Project Type**: Single project — CLI lifecycle product (libraries + CLI).

**Performance Goals**: N/A (deterministic, offline; not a hot path).

**Constraints**: Deterministic, byte-stable default/`--json` and `--text` output
(FR-012); no new JSON facts beyond the removed stale rows/advisory and the
additive `help` field; `--rich` stays presentation-only and degrades to zero ANSI
(FR-010); no Governance contract or dependency change (FR-013); no
rendering/provider-specific identity in generic SDD.

**Scale/Scope**: Five independent surgical fixes across three projects; the
largest blast radius is the additive `help` jsonField, which adds `"help": null`
to every command's JSON golden (mechanical, exactly as `scaffold`/`refresh` were
added).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation**: Public-surface changes are
  the additive `CommandReport.Help: HelpSummary option` field + `HelpSummary` type
  (`CommandTypes.fsi`) and a new `CommandHelp` module (`CommandHelp.fsi`). Tests
  precede implementation (each papercut gets a failing-first test; the two tests
  that encode current buggy behavior are rewritten — see Phase 1). **PASS**.
- **II. Structured Artifacts Are the Machine Contract**: `checklist.md` rows stay
  the authoritative review record and are re-derived from current sources on stale
  re-run; for §3.2 the plan states the authoritative rule — `spec.md` is authored
  and **read live by downstream stages**, `specify` promotes only the first draft,
  and the report says so. The `staleGeneratedView` advisory keeps `work-model.json`
  honest about genuine source drift. **PASS**.
- **III. Visibility Lives in `.fsi`**: `CommandTypes.fsi` gains the `Help` field +
  `HelpSummary`; new `CommandHelp.fs`/`.fsi` declares the flag table; `Program.fs`
  help dispatch lives in the global `Program` module (no `.fsi`, not surface-baselined).
  `PublicSurface.baseline` for `FS.GG.SDD.Commands` updated for the new module;
  `serializeReport`/`renderText`/`resolve` signatures are unchanged. **PASS**.
- **IV. Idiomatic Simplicity**: Pure functions and a static record table; no clever
  abstractions, custom operators, or reflection. **PASS**.
- **V. Elmish/MVU Boundary**: No new I/O edge. §3.1/§3.2/§3.3/§3.4 are pure
  transitions over already-loaded snapshots; help is a pure CLI dispatch + pure
  projection over `CommandReport`. **PASS**.
- **VI. Test Evidence Mandatory**: Failing-first tests for every papercut over real
  fixture trees (purge-and-re-derive, partial-fix, specify edit-report, disclaimer
  prose+bullet, genuine-ambiguity-still-blocks, clean verify/ship no-advisory,
  genuine-upstream-staleness-still-flags, top-level + per-command help, unknown
  still blocks, determinism). **PASS**.
- **VII. Agent + Human Share One Contract**: checklist re-derivation, the specify
  report statement, and help all flow through the same `CommandReport` consumed by
  CLI, agents, and CI; help metadata is generic. **PASS**.
- **VIII. Observability And Safe Failure**: `staleGeneratedView` still fires for
  genuine staleness (FR-007); a still-failing checklist re-run now truthfully
  reflects current sources; `--help` never masquerades as `unknownCommand` and a
  genuinely unknown command is never masked by `--help` (FR-011). **PASS**.

Engineering-constraint checks: stays `net10.0`; package namespaces unchanged; no
FS.GG.Rendering/provider identity introduced; SDD remains useful without
Governance and adds no Governance dependency (FR-013). **No violations →
Complexity Tracking omitted.**

## Project Structure

### Documentation (this feature)

```text
specs/048-lifecycle-cli-papercuts/
├── plan.md              # This file
├── research.md          # Phase 0 output (the five decisions + §3.2 resolution)
├── data-model.md        # Phase 1 output (entities + state transitions)
├── quickstart.md        # Phase 1 output (US1–US4 validation scenarios)
├── contracts/           # Phase 1 output
│   ├── checklist-rerun-semantics.md
│   ├── ambiguity-disclaimer.md
│   ├── workmodel-currency-snapshot-set.md
│   └── help-report.md
├── checklists/          # (pre-existing) requirements.md
├── spec.md
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Artifacts/
└── LifecycleArtifacts/
    └── Specification.fs          # §3.3: missingIdDiagnostics + Ambiguities id
                                  #   extraction recognize a no-outstanding sentinel
    (Internal.fs)                 #   reuse/extend the StartsWith "No " disclaimer
                                  #   convention; possibly a shared sentinel helper

src/FS.GG.SDD.Commands/
├── CommandTypes.fs / .fsi        # §3.5: add HelpSummary type + CommandReport.Help field
├── CommandHelp.fs / .fsi         # §3.5: NEW — global + per-command flag/description table
├── CommandSerialization.fs       # §3.5: writeHelp (emitted like scaffold: object|null)
├── CommandRendering.fs           # §3.5: renderText emits help lines (rich auto-derives)
└── CommandWorkflow/
    ├── ParsingMid.fs             # §3.1: stale checklist re-run purges + re-derives rows
    ├── ParsingEarly.fs           # §3.2: specify re-run report statement (live-read advisory)
    └── ViewGeneration.fs         # §3.4: existingGeneratedViewDiagnostic currentSnapshots
                                  #   mirrors workModelSnapshots (add plan/charter)

src/FS.GG.SDD.Cli/
└── Program.fs                    # §3.5: help branch (top-level --help/-h/help and
                                  #   <command> --help), built via buildReport → resolve

tests/FS.GG.SDD.Commands.Tests/
├── ChecklistCommandTests.fs      # rewrite the append-stale test → purge-and-re-derive;
│                                 #   add partial-fix case
├── SpecifyCommandTests.fs        # add edited-content re-run reports live-read statement
├── ClarifyCommandTests.fs        # add disclaimer-bullet non-blocking + genuine-still-blocks
├── VerifyCommandTests.fs         # clean run carries no staleGeneratedView advisory
├── ShipCommandTests.fs           # rewrite the "advisory" expectation → clean shipReady;
│                                 #   add genuine-upstream-staleness-still-flags
└── HelpCommandTests.fs           # NEW — top-level + per-command help, unknown still blocks
tests/FS.GG.SDD.Artifacts.Tests/
├── SpecificationArtifactTests.fs # disclaimer bullet under ## Ambiguities → no AmbiguityIds
└── GeneratedModelCurrencyTests.fs# (covers snapshot-set parity expectation)
tests/FS.GG.SDD.Cli.Tests/
├── HelpRenderingTests.fs         # NEW — help json/text/rich projection + degradation
└── PublicSurface.baseline        # FS.GG.SDD.Commands baseline updated for CommandHelp
docs/release/
├── release-readiness.json        # add `help` to the command-report inventory
└── schema-reference.md           # document the additive `help` field
```

**Structure Decision**: Single-project layout (existing). The five fixes touch
five distinct seams already located in Phase 0; only §3.5 adds a new module
(`CommandHelp`). No new project is created.

## Phase 0 — Research

See [research.md](./research.md). All NEEDS CLARIFICATION resolved, including the
spec's open §3.2 (a) re-ingest vs (b) document choice (resolved to a
document-by-reporting variant that satisfies FR-002 under either reading), the §3.3
sentinel form, and the §3.5 help representation (additive always-emitted `help`
field vs omitted-when-None — resolved to **always-emitted**, matching the
`scaffold`/`refresh` precedent and the catalog's "documented field always present"
conformance model).

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md) — checklist result row + source-snapshot
  re-derivation, the ambiguity no-outstanding sentinel, the work-model currency
  snapshot-set parity, and the `HelpSummary` + flag-metadata shapes.
- [contracts/checklist-rerun-semantics.md](./contracts/checklist-rerun-semantics.md)
- [contracts/ambiguity-disclaimer.md](./contracts/ambiguity-disclaimer.md)
- [contracts/workmodel-currency-snapshot-set.md](./contracts/workmodel-currency-snapshot-set.md)
- [contracts/help-report.md](./contracts/help-report.md)
- [quickstart.md](./quickstart.md) — runnable validation scenarios proving US1–US4.
- Agent context: `CLAUDE.md` SPECKIT marker repointed to this plan.

**Post-design Constitution re-check**: still **PASS** — the design adds exactly
one additive public jsonField (`help`), one new internal-facing public module
(`CommandHelp`), and one `HelpSummary` type; no new I/O edge, no Governance
contract, no provider/rendering identity, determinism preserved.
