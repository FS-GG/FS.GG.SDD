# Implementation Plan: Pre-flight authoring lint

**Branch**: `076-lint-preflight` | **Date**: 2026-07-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/076-lint-preflight/spec.md`

## Summary

TD1 field feedback (#123, epic #127) showed the load-bearing SDD authoring grammars failing
**silently or with under-specified errors** — a defect surfaces only when the stage blocks, after
a full stage run. This feature adds a **read-only pre-flight** that statically reports those
defects up front, with a fix hint and a pointer to the grammar of record, over two surfaces that
share one check engine:

1. `fsgg-sdd lint <artifact>` — a standalone cross-cutting verb (not a lifecycle stage;
   `nextLifecycleCommand Lint = None`).
2. `<stage> --explain` — a non-blocking dry run that runs the same checks against the stage's own
   artifact, advancing no state and mutating nothing.

**Approach — a thin command over existing seams (Constitution II & IV).** The lint engine does not
re-derive any grammar. It routes an artifact to the **live stage parser(s)** in
`FS.GG.SDD.Artifacts` and surfaces the `Diagnostic list` those parsers already produce for the
four load-bearing classes:

| Defect class (spec FR) | Reused live parser / detector | Diagnostic id(s) |
|---|---|---|
| FR-003 malformed checklist coverage line ("counted but uncovered") | `Specification.requirementReferences` / `missingIdDiagnostics` + `Checklist.parseChecklistFacts` + `ChecklistPlanAuthoring.requirementCoverage` | `failedRequirementsQuality` |
| FR-004 missing `[AMB:AMB-###]` decision tag | `Clarification.parseClarificationFacts` (blocking-ambiguity resolution) | `missingClarificationAnswer`, `unresolvedBlockingAmbiguity` |
| FR-005 incomplete per-stage front matter | each stage parser's gating-field check (`Specification`/`Clarification`/`Checklist`/`Plan`/`WorkItemMetadata`) | `malformed*FrontMatter` |
| FR-006 duplicate stable ids | `Internal.duplicateScopedDiagnostics` (remapped per stage) | `duplicate{WorkId,Specification,Clarification,Checklist,Plan,Task,Evidence}Id` |

The diagnostics already carry `Location { Line; Column }` and a `Correction` fix-hint slot
(`FS.GG.SDD.Artifacts/Diagnostics.fs`), so FR-007's fix hint is already present. The engine then
surfaces them through the standard `CommandReport` three-projection pipeline (json/text/rich),
mirroring the **read-only `doctor`** MVU handler (a single `ReadFile` effect, never a `WriteFile`).

**The three genuinely new pieces** (everything else is reuse): (a) FR-002 **artifact-kind
auto-detection** (front-matter `stage:` first, filename/extension fallback); (b) FR-007
**diagnostic-class → grammar-anchor pointer map** into `docs/reference/authoring-contracts.md`
(no precedent today), drift-guarded by a test like `AuthoringDocsContractTests`; (c) FR-011 the
**0/1/2 exit polarity** (clean / defects / unusable-input), which is the *opposite* of the shared
`exitCodeForReport` (there 2 = tool defect), so `lint` gets a bespoke exit mapping — legitimate for
a cross-cutting peer verb (as `validate` already has its own).

## Technical Context

**Language/Version**: F# on .NET `net10.0`. New public surface in `FS.GG.SDD.Commands` (a lint
handler) and additive `CommandRequest` fields; one new `SddCommand` case. `.fsi` updated where
public modules change (Constitution III).

**Primary Dependencies**: existing `FS.GG.SDD.Artifacts` parsers (`Specification`, `Clarification`,
`Checklist`, `Plan`, `Task`, `Evidence`, `WorkItemMetadata`, `Core.frontMatter`,
`Internal.duplicateScopedDiagnostics`); the MVU command core (`CommandWorkflow` + `CommandEffects`
edge, `CommandReports` assembly + diagnostic constructors, `CommandSerialization`/`CommandRendering`,
CLI `Rendering`); the grammar-of-record doc `docs/reference/authoring-contracts.md` (feature 046,
tagged fenced blocks).

**Storage**: Files only, **read-only**. Lint reads one artifact; it writes nothing and emits no
readiness/state file (FR-008/FR-009).

**Testing**: xUnit — `FS.GG.SDD.Commands.Tests` (lint handler + report projections + exit mapping +
`--explain`), `FS.GG.SDD.Artifacts.Tests` (`ExampleArtifactsContractTests` extended for FR-013
clean pass), `FS.GG.SDD.Cli.Tests` (verb + `--explain` flag wiring + golden json/text). New
on-disk broken fixtures under `tests/fixtures/lint/` for the SC-001 4/4 fixture; a
grammar-pointer drift-guard test.

**Target Platform**: Cross-platform CLI/library (Linux CI).

**Project Type**: Single project (F# lifecycle CLI + libraries).

**Performance Goals**: N/A — single-artifact static parse; interactive latency.

**Constraints**: strictly read-only (no write effect ever planned); deterministic byte-identical
JSON + stable defect ordering (FR-012/SC-005); zero false positives on the canonical examples
(FR-013/SC-002); generic SDD only — no product/provider/rendering identity; the four classes reuse
live parsers so lint cannot diverge from what the stage enforces.

**Scale/Scope**: one new verb + one flag; one new handler module (`HandlersLint.fs` + `.fsi`
delta); additive `CommandRequest` fields (`Artifact`, `Explain`); a small pure kind-detector and
pointer table; ~4 test files touched/added. No schema version change, no persisted-artifact change.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — Spec authored and clarified. Public surface
  (new `SddCommand` case, additive `CommandRequest` fields, lint handler entry) is sketched in
  `.fsi` before `.fs`; semantic tests (broken-fixture 4/4, canonical clean pass, exit-code, golden
  projections) precede the body. **PASS**.
- **II. Structured Artifacts Are the Machine Contract** — Lint adds **no new machine contract**; it
  reuses the live parsers as the single source of truth and surfaces their existing `Diagnostic`
  contract. The only new stable surface is the JSON lint report (a `CommandReport` projection,
  golden-tested) and the grammar-pointer map (drift-guarded against the doc). Where prose and
  parser could disagree, the **parser wins** (lint is defined as "what the stage parser reports").
  **PASS**.
- **III. Visibility Lives in `.fsi`** — Every touched public module gets its `.fsi` updated
  (`CommandTypes.fsi` for the `SddCommand` case + `CommandRequest` fields; the new lint handler's
  signature). Surface-area baselines updated. **PASS**.
- **IV. Idiomatic Simplicity** — Plain functions and records: a pure kind-detector, a pure
  pointer-lookup table, a read-only handler mirroring `doctor`. No custom operators, SRTP,
  reflection, or CEs. **PASS**.
- **V. Elmish/MVU Is the Boundary** — Lint is I/O (reads a file), so it goes through the existing
  MVU boundary: `HandlersLint` plans a single `ReadFile` effect; `CommandEffects.interpret` does
  the real read at the edge; `update`/`buildReport` are pure. No new edge interpreter. **PASS**.
- **VI. Test Evidence Is Mandatory** — Behavior is proven by tests that fail before the change:
  the 4/4 broken-fixture (SC-001), the canonical clean pass (FR-013/SC-002), the 0/1/2 exit
  mapping (SC-006), determinism/golden (SC-005), and the pointer drift guard (FR-007). Golden
  json/text coverage for the new report projection (Constitution VI). **PASS**.
- **VII. Agent & Human Workflows Share One Contract** — Both surfaces (`lint`, `<stage> --explain`)
  and CI operate over the same artifacts and the same parser truth; no second source of truth is
  introduced. The `fs-gg-sdd-troubleshooting`/`authoring-contracts` skills gain a pointer to
  `lint` as the pre-flight (agent + human parity). **PASS**.
- **VIII. Observability & Safe Failure** — Lint's whole purpose is actionable diagnostics with fix
  hints + grammar pointers. It distinguishes malformed **user input** (exit 1 defects / exit 2
  unusable input) from success; it emits no tool-defect-class escalation of its own. Deterministic,
  read-only, degrade-safe rich output. **PASS**.

**Change tier: Tier 1** — a new command verb, a new CLI flag, additive request fields, and a new
JSON report projection are all contracted/tool-facing surface. Requires spec, plan, tasks, `.fsi`,
tests, docs, and a note on the bespoke exit mapping. No schema migration (no persisted artifact
changes). See Complexity Tracking for the one justified divergence.

## Project Structure

### Documentation (this feature)

```text
specs/076-lint-preflight/
├── plan.md              # This file
├── spec.md              # Feature spec (FR-001..017, SC-001..006, Clarifications 2026-07-05)
├── research.md          # Phase 0 — reuse map, kind-detection, exit polarity, pointer map, scope decisions
├── data-model.md        # Phase 1 — lint report / defect / kind / pointer entities
├── quickstart.md        # Phase 1 — end-to-end validation (broken 4/4, clean examples, exit codes, --explain)
├── contracts/
│   ├── lint-cli.md      # `lint <artifact>` + `<stage> --explain` command/flag/exit contract
│   └── lint-report.md   # The lint CommandReport JSON projection + grammar-pointer map
└── tasks.md             # Phase 2 (/speckit-tasks — not created here)
```

### Source Code (repository root — files this feature touches)

```text
src/FS.GG.SDD.Commands/CommandTypes.fsi            # + Lint SddCommand case; + CommandRequest.Artifact/Explain
src/FS.GG.SDD.Commands/CommandTypes.fs             # + "lint" parseCommand arm; commandName/commandStage/nextLifecycleCommand=None
src/FS.GG.SDD.Commands/CommandWorkflow/HandlersLint.fs      # NEW — read-only lint handler (mirrors HandlersDoctor)
src/FS.GG.SDD.Commands/CommandWorkflow.fs          # dispatch Lint -> computeLintNext; --explain gating on stage handlers
src/FS.GG.SDD.Commands/CommandWorkflow/LintEngine.fs        # NEW — pure kind-detector + parser routing + defect classification + pointer attach
src/FS.GG.SDD.Commands/CommandWorkflow/LintEngine.fsi       # NEW — public signature
src/FS.GG.SDD.Commands/CommandReports/NextActionRouting.fs  # Lint NextAction (None / fix-guidance)
src/FS.GG.SDD.Commands/CommandTypes.fs (LintSummary)        # + LintSummary model on CommandReport (kind, defect list, outcome)
src/FS.GG.SDD.Cli/Program.fs                       # parse `lint <artifact>` positional + `--explain`; bespoke exitCodeForLint branch
docs/reference/authoring-contracts.md              # grammar-pointer anchors are stable targets (already tagged; verify anchors)
docs/reference/lint.md                             # NEW — the lint command reference (surfaces, defect classes, exit codes, pointers)
```

```text
tests/FS.GG.SDD.Artifacts.Tests/ExampleArtifactsContractTests.fs   # extend: canonical examples lint clean (FR-013/SC-002)
tests/FS.GG.SDD.Commands.Tests/LintTests.fs                        # NEW — engine: 4/4 classes, kind detection, pointers, --explain, determinism
tests/FS.GG.SDD.Commands.Tests/LintExitCodeTests.fs                # NEW — 0/1/2 mapping (SC-006)
tests/FS.GG.SDD.Cli.Tests/…                                        # verb + --explain wiring + golden json/text projection
tests/FS.GG.SDD.Commands.Tests/LintGrammarPointerTests.fs          # NEW — every pointer resolves to a real doc anchor (FR-007 drift guard)
tests/fixtures/lint/broken-all/                                    # NEW — one artifact per class + a combined 4/4 fixture (SC-001)
tests/fixtures/lint/unparseable/                                   # NEW — parse-level single-defect fixture (FR-015)
```

Unchanged and relied upon: `FS.GG.SDD.Artifacts` parsers and `Diagnostics` type;
`CommandEffects.interpret`/`driveToReport` (edge run loop); `CommandSerialization`/`CommandRendering`
and CLI `Rendering.resolve` (three projections); `HandlersDoctor.fs` (read-only skeleton);
`docs/examples/lifecycle-artifacts/*` (clean exemplars).

**Structure Decision**: Single-project layout. `lint` is a real `SddCommand` (so it inherits the
MVU run loop and the three-projection report pipeline for free), with a bespoke exit branch in
`Program.fs` for its 0/1/2 polarity. `<stage> --explain` reuses the stage handlers with mutation
gated off and `NextAction` forced to `None`.

## Ordered implementation approach (feeds /speckit-tasks)

1. **Contracts first (`.fsi` + spec-of-record).** Sketch the public surface: the `Lint`
   `SddCommand` case and additive `CommandRequest.Artifact`/`Explain` fields in `CommandTypes.fsi`;
   the `LintEngine.fsi` (kind-detect → route → classify → attach pointer) and `LintSummary` report
   model. Write `contracts/lint-cli.md` and `contracts/lint-report.md`.
2. **Grammar-pointer table + drift guard.** Define the pure `defect-class → authoring-contracts.md
   anchor (+ example tag)` map. Add `LintGrammarPointerTests` asserting every pointer resolves to a
   real heading/tag in the doc (mirror `AuthoringDocsContractTests` extraction). This test fails
   until the map + any missing anchors exist.
3. **Broken fixtures + clean-pass test (failing evidence).** Add `tests/fixtures/lint/broken-all/`
   (one artifact per class + a combined 4/4) and `unparseable/`. Write `LintTests` (4/4 caught,
   each with fix hint + pointer; parse-level single defect; determinism) and extend
   `ExampleArtifactsContractTests` for the canonical clean pass. These fail before the engine.
4. **LintEngine (pure).** Implement kind-detection (front-matter `stage:` closed vocab first, then
   filename/extension: `evidence.yml`/`tasks.yml`), route to the live parser(s), collect their
   Error-severity `Diagnostic list` (the four classes are Errors; non-defects like optional
   `sha256:` are never Errors → not reported), attach the grammar pointer, and order deterministically.
5. **HandlersLint (read-only MVU).** Mirror `HandlersDoctor`: plan a single `ReadFile <artifact>`
   effect; on the snapshot call `LintEngine`; assemble a `LintSummary` + diagnostics onto the
   `CommandReport`; emit no write. Unusable input (missing/unreadable/unrecognized kind) → a
   distinct outcome the exit mapping reads as 2. Wire dispatch in `CommandWorkflow.fs` and
   `NextActionRouting.fs` (`Lint` NextAction: fix-guidance when defects, else None).
6. **`--explain` on stages.** Thread `CommandRequest.Explain`; when set, the stage handler runs the
   same `LintEngine` over its own artifact, gates off all mutating effects, and forces
   `NextAction = None` (non-blocking dry run, FR-016).
7. **CLI wiring + bespoke exit.** In `Program.fs`: parse `lint <artifact>` positional and
   `--explain rest`; add `exitCodeForLint report` = 0 clean / 1 defects / 2 unusable-input, applied
   only for `Lint` (and `--explain`); all other commands keep `exitCodeForReport`. Golden json/text
   projection tests in `FS.GG.SDD.Cli.Tests`.
8. **Serialization/rendering.** Ensure `LintSummary` serializes deterministically (json contract)
   and renders in `--text`/`--rich` (rich adds/drops no facts, changes no json byte; degrade-safe).
9. **Docs + skills.** Add `docs/reference/lint.md`; point `fs-gg-sdd-troubleshooting` and
   `fs-gg-sdd-authoring-contracts` skill bodies at `lint`/`--explain` as the pre-flight (both
   `.claude` + `.codex` byte-identical; regenerate the skill-manifest if a seeded body changes).
10. **Baselines + full green.** Update public surface-area baselines; run the SC-001..006 quickstart
    and a full `dotnet test`.

## Complexity Tracking

| Divergence | Why needed | Simpler alternative rejected because |
|---|---|---|
| `lint` uses a bespoke `exitCodeForLint` instead of the shared `exitCodeForReport` | Spec FR-011 (clarified) requires 0/1/2 = clean/defects/unusable-input; the shared helper reserves 2 for the tool-defect class, the opposite polarity. `lint` is a cross-cutting peer verb (like `validate`, which already has bespoke exits), not a lifecycle stage. | Reusing `exitCodeForReport` cannot express "unusable input = 2" without abusing the `IsToolDefect` bit (semantically wrong — bad user input is not a tool defect) and would collapse "found defects" and "couldn't run" onto exit 1, which the clarify decision explicitly rejected. |
