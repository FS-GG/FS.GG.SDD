# Feature Specification: Unify generated-view-state construction

**Feature Branch**: `029-unify-view-state`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "next item in docs/reports/2026-06-26-074428-refactor-analysis.md" — the seven-row roadmap in that report is 7/7 complete, so a fresh refactor-analysis pass was run against `main` (commit `7a6280f`). The build is clean (0 warnings; FS3261/FS0025 both 0), so the warning-based smells are gone. The highest payoff-to-risk *new* opportunity is the residual §5.5 "scattered micro-duplication" the original report predicted would "fall out naturally once §5.1/§5.3 land" — but did not: four byte-identical `*generatedViewState` constructors and the view/effect helper patterns clustered around them in the command handler layer.

## Context: refactor-analysis findings

This is an R8-class follow-on to the completed R1–R7 roadmap. A fresh three-way analysis (parsing layer, handler/view layer, Artifacts/Validation/Cli layer) against `main` surfaced these candidate clusters:

| Candidate | Approx. payoff | Risk | Notes |
|---|---|---|---|
| **Generated-view-state construction (this spec)** | ~70–110 LOC | Low | 4 byte-identical constructors + adjacent handler helpers; byte-stable output provable |
| Per-artifact parsing skeletons (identity/prereq/ensure-sections) | ~255–350 LOC | Mixed (some High) | Larger aggregate but semantically varied; deferred to a later spec |
| Per-summary JSON writers in `CommandSerialization.fs` | ~200 LOC | Medium | Structurally similar but each writes a distinct field set; deferred |

This spec takes the **generated-view-state cluster** because it is the cleanest: the duplication is literal (identical function bodies), one of the four copies (`shipGeneratedViewState`) already proves the parameterized form type-checks, and the output is provably byte-identical because the bodies — including the `List.sortBy _.Path` and `List.distinct |> List.sort` normalizations — are character-for-character the same.

The other two candidates are recorded here as deferred follow-ons, not abandoned.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One canonical generated-view-state constructor (Priority: P1)

A maintainer adding or changing a generated lifecycle view (e.g. introducing a
new `Kind`, or adjusting how `Sources`/`DiagnosticIds` are normalized) needs to
change that construction logic in exactly one place. Today the same record
literal exists four times — `generatedViewState` (`Kind = "workModel"`),
`analysisGeneratedViewState` (`Kind = "analysis"`), `verifyGeneratedViewState`
(`Kind = "verification"`), and `shipGeneratedViewState` (`Kind` already a
parameter) — so a change to the normalization (sort, distinct, schema-version
default) must be hand-applied to all four or they silently drift.

**Why this priority**: This is the core of the refactor and the single largest
source of drift risk. It is independently shippable on its own and delivers the
maintainability win even if the adjacent helper extractions (P2/P3) are not done.

**Independent Test**: Collapse the four constructors into one
`kind`-parameterized constructor and route every call site through it; the full
test suite passes and `charter`/`analyze`/`verify`/`ship`/`refresh` `--json`
output is byte-identical to the pre-change baseline.

**Acceptance Scenarios**:

1. **Given** the four `*generatedViewState` constructors on `main`, **When** the refactor lands, **Then** exactly one generated-view-state constructor definition remains and it accepts the view `Kind` as a parameter.
2. **Given** any command that emits a generated view (`workModel`, `analysis`, `verification`, `ship`/handoff, agent guidance, refresh), **When** it is run before and after the refactor with identical inputs, **Then** the `--json` and `--text` output bytes are identical.
3. **Given** the public `CommandWorkflow.fsi` facade, **When** the refactor lands, **Then** the `.fsi` and every `PublicSurface.baseline` are byte-identical (all affected bindings are `internal`/`[<AutoOpen>]`).

---

### User Story 2 - Single blocking-diagnostic-id helper (Priority: P2)

A maintainer who changes how a handler decides which diagnostics block view
emission needs one definition to edit. The pattern
`diagnostics |> List.filter (fun d -> d.Severity = DiagnosticSeverity.DiagnosticError) |> List.map _.Id`
is copy-pasted 10 times across the handler and view-generation modules (count
verified against `main` @ `7a6280f`).

**Why this priority**: High-frequency, zero-semantic-variation duplication that
is trivially and safely extractable, but secondary to the constructor unification.

**Independent Test**: Replace the inline filter/map occurrences with a single
shared `blockingDiagnosticIds` helper; tests pass and JSON output is
byte-identical.

**Acceptance Scenarios**:

1. **Given** the 10 inline `Error`-filter→`.Id`-map occurrences, **When** the refactor lands, **Then** they route through one shared helper and no handler retains the inline form.
2. **Given** identical inputs, **When** any affected command runs before and after, **Then** the diagnostic-id ordering and content in the output are unchanged.

---

### User Story 3 - Single blocked-view construction helper (Priority: P3)

A maintainer changing how a handler emits the "prerequisites missing → blocked
view" state needs one place to edit. The construction
`generatedViewState path model.Request.GeneratorVersion [] None GeneratedViewCurrency.Blocked ids`
appears ~9 times across the early/analyze/evidence/verify/ship handlers with
only `path` and `ids` varying.

**Why this priority**: Real but lowest-leverage of the three; depends on P1
(the canonical constructor) existing first. Optional within this feature.

**Independent Test**: Extract a `blockedWorkModelView`-style helper taking the
varying `path`/`ids` and route the ~9 sites through it; tests pass and output is
byte-identical.

**Acceptance Scenarios**:

1. **Given** the ~9 blocked-view construction sites, **When** the refactor lands, **Then** they route through one shared helper.
2. **Given** a command whose prerequisites are unmet, **When** it runs before and after, **Then** its blocked-view JSON output is byte-identical.

---

### Edge Cases

- **`shipGeneratedViewState` already takes a `kind` parameter** — the unified constructor MUST preserve that call shape (the ship/handoff path passes a computed kind, not a literal), so the canonical signature is the ship one (`path → kind → generator → sources → outputDigest → currency → diagnosticIds`).
- **Definition ordering** — F# compiles files in order; the canonical constructor MUST live in a module that compiles before every call site (e.g. `Foundation.fs`, which already opens `WorkModel` for the `GeneratedViewState`/`GeneratedViewCurrency`/`GeneratorVersion`/`GeneratedViewSource` types). If placement forces a different home, that is an internal-only decision with no surface impact.
- **Sort/distinct normalization must not change** — the unified body MUST keep `Sources |> List.sortBy _.Path` and `DiagnosticIds |> List.distinct |> List.sort` exactly; any deviation would alter output bytes and fail the byte-identical gate.
- **Refresh's `"summary"` view stays inline** — `HandlersRefresh.fs` builds its `GeneratedViewState` (`Kind = "summary"`) as an inline record whose `Sources` deliberately follow `structuredSourcePaths` order (the same order the rendered summary Markdown uses) and are *not* `List.sortBy _.Path`-normalized. Routing it through the unified constructor would impose that sort and risk output drift, so it is **excluded** from this refactor. `computeRefreshPlan` likewise keeps its self-contained distinct dedup/effect-gating (per R1/R2) untouched.
- **No new `Kind` values** — this is a pure internal reorganization; it introduces no new view kinds, fields, or schema versions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose exactly one generated-view-state constructor, parameterized by the view `Kind`, replacing the four current copies (`generatedViewState`, `analysisGeneratedViewState`, `verifyGeneratedViewState`, `shipGeneratedViewState`).
- **FR-002**: Every existing call site of the four named constructors MUST route through the unified constructor, passing its `Kind` (`"workModel"`, `"analysis"`, `"verification"`, `"ship"`, `"governance-handoff"`, `"agent-commands"`) explicitly. The inline `GeneratedViewState` record that builds refresh's `"summary"` view is **not** one of the four named constructors and is **out of scope** (see Edge Cases).
- **FR-003**: The unified constructor MUST preserve the existing record-building behavior exactly: `SchemaVersion = Some 1`, `Sources` sorted by `Path`, and `DiagnosticIds` de-duplicated then sorted.
- **FR-004**: The system MUST provide one shared helper for blocking-diagnostic-id extraction (`Error`-severity filter → `.Id` map) and route the existing inline occurrences through it.
- **FR-005**: The system MUST provide one shared helper for the "blocked generated-view" construction and route the existing blocked-view sites through it. *(P3 — optional within this feature; may be deferred to a follow-on without blocking FR-001–FR-004.)*
- **FR-006**: All affected bindings MUST remain `internal`/`[<AutoOpen>]`; the public `CommandWorkflow.fsi` facade and all `PublicSurface.baseline` files MUST be byte-identical to `main`.
- **FR-007**: Command output MUST be byte-identical to `main` for every command across `--json` and `--text` projections (the deterministic contracts); `--rich` is presentation-only and excluded.
- **FR-008**: The Release build MUST stay clean — 0 errors, no new warning category, and the FS3261/FS0025 ratchet still at 0 — with no `#nowarn` added and `Directory.Build.props` unchanged.
- **FR-009**: The existing test suite MUST pass; any new test added MUST be a totality/regression assertion that locks in the single-constructor invariant, not a behavior change.

### Key Entities *(include if feature involves data)*

- **GeneratedViewState**: The lifecycle view-state record (`Path`, `Kind`, `SchemaVersion`, `Generator`, `Sources`, `OutputDigest`, `Currency`, `DiagnosticIds`) emitted by command handlers. This refactor changes only *how* the record is constructed (one constructor vs four), never its shape, fields, or serialized form.
- **Generated view `Kind`**: The string discriminator (`"workModel"`, `"analysis"`, `"verification"`, `"ship"`, `"governance-handoff"`, `"agent-commands"`) that is the *only* difference between the four named constructors and therefore becomes the unified constructor's parameter. (Refresh's `"summary"` view is built by a separate inline record and is out of scope — see Edge Cases.)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The count of generated-view-state constructor *definitions* drops from 4 to 1.
- **SC-002**: The inline `Error`-filter→`.Id`-map occurrences (10, verified against `main` @ `7a6280f`) drop to 0 (all routed through one helper).
- **SC-003**: The blocked-view construction sites drop from ~9 to 0 inline forms (P3; counted only if FR-005 is included).
- **SC-004**: Net `src` line count decreases by ≥ 60 LOC with zero behavior change.
- **SC-005**: 100% of the existing test suite passes (currently 434 test attributes; 438 reported assertions), and `--json`/`--text` output is byte-identical to `main` for all commands.
- **SC-006**: The public `CommandWorkflow.fsi` facade and all four `PublicSurface.baseline` files (Cli, Commands, Artifacts, Validation) are byte-identical to `main`; Release build is clean with the FS3261/FS0025 ratchet at 0.

## Assumptions

- There are **no external consumers** of the internal command-workflow bindings (consistent with the R2/R3 stakeholder decisions), so the binding correctness gate is **build + the existing test suite pass + byte-identical deterministic output**.
- The canonical signature follows `shipGeneratedViewState` (`path → kind → generator → sources → outputDigest → currency → diagnosticIds`), since it is the only current copy that already generalizes over `Kind`.
- `Foundation.fs` (or an equally early-compiling internal module that already has the `WorkModel` types in scope) is an acceptable home for the unified constructor; exact placement is an implementation detail with no surface impact.
- P3 (FR-005, blocked-view helper) may be split into a follow-on if it would otherwise risk touching `computeRefreshPlan`'s deliberately self-contained guard; P1/P2 stand on their own.
- The two deferred candidates from the fresh analysis (per-artifact parsing skeletons; per-summary `CommandSerialization` writers) are out of scope for this feature and recorded above for a future spec.
