# Implementation Plan: Blocking diagnostics point to their shipped example / grammar section

**Branch**: `078-diagnostic-remediation-pointers` | **Date**: 2026-07-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/078-diagnostic-remediation-pointers/spec.md`

## Summary

The ~101 blocking (error-severity) diagnostics are constructed in
`src/FS.GG.SDD.Commands/CommandReports/DiagnosticConstructors.fs`; each carries an inline
`Correction` string. For the **authoring-grammar** subset — the diagnostics an author trips by
violating a load-bearing grammar (per-stage front matter, the FR→AC coverage line, stable-id
declarations/references, duplicate ids, the clarify `[AMB:…]` decision-tag rule, the
`evidence.yml` declaration/disclosure/deferral rules) **plus** the grammar-rooted aggregate
readiness blocks (`failedChecklistPrerequisite`, `failedPlanPrerequisite`,
`evidence.missingRequiredEvidence`, `evidence.missingRequiredSkill`, `verify.missingRequiredTest`)
— the correction names *what* is wrong but not *how* to satisfy the grammar. This feature appends
a deterministic **remediation pointer** to those corrections: the shipped example path
(`docs/examples/lifecycle-artifacts/<stage>`) **and** the grammar-section anchor
(`docs/reference/authoring-contracts.md#<section>`), citing both wherever both exist.

**Approach**: introduce one internal, data-only pointer registry
(`RemediationPointers`) in `FS.GG.SDD.Commands` that maps each covered diagnostic id to its
`(examplePath option, grammarAnchor option)` and renders the deterministic pointer-suffix string.
`DiagnosticConstructors` appends `RemediationPointers.suffixFor <id>` to the covered corrections;
untouched constructors keep their exact current text (FR-008). A guard test in
`FS.GG.SDD.Commands.Tests` (which already has `InternalsVisibleTo`) asserts, over the registry,
that every covered id has a pointer, every cited example path exists on disk, and every cited
anchor resolves to a real heading in `authoring-contracts.md` (FR-006). Separately, three new
shipped examples — `charter.md`, `spec.md`, `plan.md` — are added under
`docs/examples/lifecycle-artifacts/` and validated by the live parsers, extending
`ExampleArtifactsContractTests` (FR-004/FR-005). No persisted-schema change, no new diagnostic
field, no output-stream or exit-code change — only correction string *values* change (FR-003).

## Technical Context

**Language/Version**: F# on .NET (`net10.0`), per constitution engineering constraints.

**Primary Dependencies**: existing `FS.GG.SDD.Commands` (`DiagnosticConstructors`, `CommandReports`)
and `FS.GG.SDD.Artifacts` (the `Diagnostics.Diagnostic` record, the live stage parsers
`Specification.parseSpecificationFacts` / `Plan.parsePlanFacts`, and the Commands-level charter
front-matter parser). No new packages.

**Storage**: two documentation surfaces are the pointer targets — `docs/examples/lifecycle-artifacts/`
(shipped example artifacts) and `docs/reference/authoring-contracts.md` (grammar sections). Both
are in-repo files; no DB. The three new examples are new files under the examples dir.

**Testing**: xUnit. New/updated tests: (a) `ExampleArtifactsContractTests` gains charter/spec/plan
cases (spec/plan via the public `parse*Facts`; charter via the Commands front-matter parser, so its
case lives in `FS.GG.SDD.Commands.Tests`); (b) a new pointer-resolution guard test in
`FS.GG.SDD.Commands.Tests` over the `RemediationPointers` registry; (c) per-stage assertions that a
representative covered diagnostic's `Correction` contains the expected example path + anchor.
Existing correction-substring assertions (8 files) update to expect the appended pointer.

**Target Platform**: Linux/macOS/Windows CLI (`fsgg-sdd`).

**Project Type**: single-project CLI + libraries (`src/FS.GG.SDD.*`).

**Performance Goals**: N/A — pointer append is a constant-time string concat at diagnostic
construction; the guard/contract tests run bounded file reads. No hot path.

**Constraints**: deterministic output (no timestamps/absolute paths/env-dependent content, FR-007);
the JSON automation contract byte-shape is unchanged except the affected `correction` string values
(FR-005/SC-005); `--text`/`--rich` are pure projections over the same corrections and gain no
projection-only facts; anchors cite the canonical `authoring-contracts.md`, not the
`.fsgg/early-stage-guidance.md` mirror.

**Scale/Scope**: one new internal module (+ `.fsi`); edits to the covered subset of ~40–50
constructors in one file; 3 new example artifacts; ~3 new tests + golden/assertion updates in ≤8
existing test files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation**: Followed. The new `RemediationPointers`
  module ships with its `.fsi` first; the guard + per-stage correction tests are written before the
  corrections are wired. Charter/spec/plan example tests are written before (or with) the examples.
- **II. Structured Artifacts Are the Machine Contract**: The machine contract is the diagnostic
  JSON. The pointer rides inside the existing `Correction` string field — no new field, no schema
  version bump. Authoritative mapping of covered-id → pointer lives in one place
  (`RemediationPointers`); the guard test is the coherence check that keeps it non-dangling. The
  shipped examples remain parser-validated so they cannot contradict the tool.
- **III. Visibility Lives in `.fsi`**: `RemediationPointers` gets a `.fsi`. Recommended: keep it
  **internal** (it is a Commands-internal wiring detail, like `DiagnosticConstructors` itself),
  reached from tests via the existing `InternalsVisibleTo` — so no `FS.GG.Contracts` public-surface
  baseline change and no change to `Diagnostics.fsi`. See research Decision 4.
- **IV. Idiomatic Simplicity Is the Default**: plain F# — a `Map`/list of records and simple string
  concatenation. No custom operators, SRTP, reflection, or CE machinery.
- **V. Elmish/MVU Is the Boundary**: no new `Effect`. Pointer rendering is a pure function of the
  diagnostic id; diagnostic construction is already pure. The guard/contract tests read files at
  **test** time only (the product code embeds the pointer strings as constants; it performs no
  filesystem I/O to resolve them at runtime).
- **VI. Test Evidence Is Mandatory**: new tests fail before / pass after — the guard fails if any
  covered id lacks a pointer or cites a missing path/anchor; the correction tests fail until the
  suffix is wired; the example contract tests fail until the three examples exist and validate.
- **VII. Agent And Human Workflows Must Share One Contract**: the pointer is in the shared CLI
  diagnostic surface, so `--json`/`--text`/`--rich` and `fsgg-sdd lint`/`--explain` all present the
  same remediation guidance (FR-009). The new examples are the same artifacts agents and humans
  copy-adapt; the `fs-gg-sdd-*` skills already reference the examples/grammars.
- **VIII. Observability And Safe Failure**: this feature *improves* observability — every covered
  blocking diagnostic now names a concrete, in-repo resolution path. Safe-failure posture is
  unchanged (no exit-code or blocking-classification change); the guard prevents the new guidance
  from rotting into dead links.

**Change tier**: **Tier 1** — command output contract (correction strings on the shared diagnostic
surface) + new (internal) module + shipped-doc artifacts. Requires spec, plan, tasks, `.fsi`,
tests, and docs. **No migration notes** — no persisted schema changes.

**Gate result**: PASS. No violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/078-diagnostic-remediation-pointers/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── remediation-pointer.md   # the pointer-suffix format + covered-set contract
├── checklists/
│   └── requirements.md  # /speckit-specify output
└── tasks.md             # /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.SDD.Commands/
│   ├── CommandReports/
│   │   ├── RemediationPointers.fsi      # NEW — covered-id → (example, anchor) registry + suffix renderer
│   │   ├── RemediationPointers.fs       # NEW
│   │   └── DiagnosticConstructors.fs    # EDIT — append RemediationPointers.suffixFor to covered corrections
│   └── FS.GG.SDD.Commands.fsproj        # EDIT — add the two new files (before DiagnosticConstructors)

docs/
├── examples/lifecycle-artifacts/
│   ├── charter.md                       # NEW — build-validated charter example
│   ├── spec.md                          # NEW — build-validated specify example
│   └── plan.md                          # NEW — build-validated plan example
└── reference/authoring-contracts.md     # (target of anchors; edited only if an anchor is missing)

tests/
├── FS.GG.SDD.Artifacts.Tests/
│   └── ExampleArtifactsContractTests.fs # EDIT — add spec.md + plan.md parser-validation cases
└── FS.GG.SDD.Commands.Tests/
    ├── RemediationPointersTests.fs      # NEW — guard: every covered id has a resolving, non-dangling pointer
    ├── CharterExampleContractTests.fs   # NEW — charter.md front-matter validates (parser is in Commands)
    └── {Clarify,Checklist,Evidence,Plan,Specify,...}CommandTests.fs  # EDIT — expect appended pointer text
```

**Structure Decision**: Single-project CLI + libraries (matches the repo). The one new unit of
code is the `RemediationPointers` module under `CommandReports/` beside `DiagnosticConstructors`,
kept `internal` and reached from tests via the existing `InternalsVisibleTo`. Everything else is
doc artifacts (three examples) and tests.

## Complexity Tracking

> No constitution violations. Section intentionally empty.
