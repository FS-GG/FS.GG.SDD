# Implementation Plan: Document Load-Bearing Authoring Contracts & Self-Correcting Diagnostics

**Branch**: `046-document-authoring-contracts` | **Date**: 2026-06-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/046-document-authoring-contracts/spec.md`

## Summary

Close FS-GG/FS.GG.SDD#38 by **publishing** the two load-bearing authoring contracts that
today require decompiling `fsgg-sdd` — the requirement→acceptance coverage line and the
`evidence.yml` `kind`/`result` vocabulary plus its non-synthetic-`pass` satisfaction rule —
and by making the three relevant diagnostics **self-correcting** so a failure shows the
author the exact expected form inline.

Approach (confirmed with the requester during planning):

- **Documentation** — add a durable authoring reference (`docs/reference/authoring-contracts.md`)
  that states the accepted/rejected coverage forms and the full `evidence.yml` vocabulary,
  disambiguates SDD's `evidence.yml` from any unrelated "evidence" doc a scaffolded product
  may ship, and is mirrored at the relevant lifecycle steps in `docs/quickstart.md`.
- **Diagnostics** — enrich the single shared `Correction`/`Message` string for three
  diagnostics: the `checklist` missing-coverage review (`ParsingMid.fs`), the `evidence`
  unsatisfied-obligation correction (`HandlersEvidence.fs`), and `missingSpecificationIntent`
  (`CommandReports.fs`). These strings are serialized into the default JSON, the work model,
  and `checklist.md`, so the change is **Tier 1** (command-output contract): diagnostic
  **codes/severities/blocking/exit/routing/outcomes stay identical**, only the string values
  change, and golden fixtures are updated deliberately. (Rejected alternative: enriching only
  `--text`/`--rich` to keep JSON byte-identical — splits the source of truth and never reaches
  `checklist.md` or JSON consumers; violates Principles II and VII.)
- **Drift guard** — a doc-driven test (`SC-005`) extracts the documented accepted/rejected
  examples from the reference and runs them through the **public** parse API
  (`Specification.parseSpecificationFacts` → `.RequirementReferences`, and `Evidence.parseEvidence`,
  which delegate to the real `requirementReferences`/`parseEvidenceKind`), applying the
  non-synthetic-`pass` satisfaction rule, and fails if the docs and the tool ever disagree. The
  raw parser functions are `.fsi`-restricted, so the guard reaches them only through these public
  entry points — no public-surface change.
- **Agent parity** — surface the same coverage/evidence guidance in both
  `.claude/skills/fs-gg-sdd-project/SKILL.md` and `.codex/skills/fs-gg-sdd-project/SKILL.md`.

The optional coverage-parser relaxation (issue fix 4) is **out of scope**; grammars are
documented exactly as they are and are not changed (FR-012).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (per constitution Engineering Constraints).

**Primary Dependencies**: Existing SDD libraries only — `FS.GG.SDD.Artifacts`
(`Diagnostics`, `LifecycleArtifacts.Specification`, `LifecycleArtifacts.Evidence`,
`Json.JsonWriters`) and `FS.GG.SDD.Commands` (`CommandReports`, `CommandWorkflow.ParsingMid`,
`CommandWorkflow.ParsingEarly`, `CommandWorkflow.HandlersEvidence`,
`CommandWorkflow.ViewGeneration`). No new package dependency. No Governance dependency.

**Storage**: Authored Markdown (`docs/`, agent `SKILL.md`s) and the existing structured
artifacts (`work-model.json`, `checklist.md`, `analysis.json`, `verify.json`) whose diagnostic
strings change. No schema-version bump (no field add/remove/rename).

**Testing**: xUnit (`[<Fact>]`), as in `tests/FS.GG.SDD.Commands.Tests/*` and
`tests/FS.GG.SDD.Artifacts.Tests/*`. Golden/JSON assertions live in the per-command test files
(`ChecklistCommandTests.fs`, `EvidenceCommandTests.fs`, `SpecifyCommandTests.fs`,
`VerifyCommandTests.fs`, `CommandReportJsonTests.fs`) and `TextProjectionTests.fs`.

**Target Platform**: Cross-platform .NET CLI (`fsgg-sdd`); docs are platform-agnostic.

**Project Type**: Single project — CLI lifecycle product with a documentation surface.

**Performance Goals**: N/A — string-content and documentation change; no hot path touched.

**Constraints**: Diagnostic **codes**, severities, blocking, exit codes, stream routing, JSON
field set, and pass/fail outcomes unchanged (FR-010). No external provider package id /
template id / path / docs URL in generic SDD (FR-012, Principle Engineering Constraints). No
parsing grammar change (FR-012). Claude and Codex surfaces kept equivalent (FR-013, Principle
VII).

**Scale/Scope**: 3 diagnostic correction sites, 1 new reference doc, quickstart edits, 2 agent
skill files, ~1 new drift-guard test plus golden-fixture updates across ≤5 command test files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec→FSI→Tests→Impl | PASS | No public API surface change (no new `.fsi` members). Existing diagnostic constructors are reused; only string arguments change. Tests precede the string edits. |
| II. Structured artifacts are the contract | PASS | The authoritative source for "what establishes coverage / satisfies an obligation" remains the parser/handler code; this feature **documents** that contract and co-verifies the docs against it (drift guard), rather than introducing a second contract. |
| III. Visibility in `.fsi` | PASS | No public surface change; `SurfaceBaselineTests`/`PublicSurface.baseline` expected to stay green (verify in Phase 1). The SC-005 drift guard deliberately drives the **existing** public parse API (`Specification.parseSpecificationFacts` → `.RequirementReferences`; `Evidence.parseEvidence`) rather than the assembly-restricted `requirementReferences`/`parseEvidenceKind`, so it co-verifies the docs without exposing new `.fsi` members. The satisfaction rule is not a public predicate; the guard re-expresses the one-line non-synthetic-`pass` rule and T002 keeps it in sync with the handler ladder. |
| IV. Idiomatic simplicity | PASS | String edits + one doc-reading test; no new abstraction, operator, or framework. |
| V. Elmish/MVU boundary | PASS | No new stateful/I/O workflow; edits sit inside existing handlers/report builders. |
| VI. Test evidence mandatory | PASS | Golden fixtures updated for each enriched diagnostic (fail-before/pass-after); new drift-guard test for SC-005. |
| VII. One contract for agents + humans | PASS | Same guidance lands in docs **and** both agent skills; the enriched correction is the single shared string, not a renderer-only hint. |
| VIII. Observability & safe failure | PASS | This feature **improves** diagnostics (names the offending id and the exact expected form); no failure path is removed or weakened. |
| Tier declaration | PASS | Declared **Tier 1** (command-output contract: diagnostic string values in JSON/checklist.md change). Requires spec, plan, tasks, tests, docs — all in scope. |
| Engineering constraints | PASS | `net10.0`, `FS.GG.SDD.*`, `fsgg-sdd`, no Rendering identity, no Governance dependency, no grammar change. |

**Result**: PASS. No violations; Complexity Tracking left empty.

The one prose/contract conflict found during planning (the spec's original "byte-identical
JSON" goal vs. `correction`/`message` being serialized JSON fields) was resolved per
Principle II ("the plan MUST say which source wins"): the enriched **string values** are an
intended output change captured by golden fixtures; the **structure/codes/outcomes** are the
preserved invariant. The spec's FR-010 and SC-004 were updated to match before this plan.

## Project Structure

### Documentation (this feature)

```text
specs/046-document-authoring-contracts/
├── plan.md              # This file
├── research.md          # Phase 0 — verified source facts + decisions
├── data-model.md        # Phase 1 — entities (coverage line, evidence declaration, diagnostic)
├── quickstart.md        # Phase 1 — how to validate this feature
├── contracts/
│   ├── diagnostic-corrections.md   # exact before/after correction strings (the 3 sites)
│   └── authoring-reference.md      # required content + machine-checkable example tags
└── tasks.md             # Phase 2 (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/
├── CommandReports.fs                      # FR-009: missingSpecificationIntent correction (~line 151)
└── CommandWorkflow/
    ├── ParsingMid.fs                       # FR-007: checklist missing-coverage review correction (~line 163)
    ├── ParsingEarly.fs                     # FR-009 (read-only): missing-fact computation already correct (~line 168)
    └── HandlersEvidence.fs                 # FR-008: unsatisfied-obligation correction (~line 183)

docs/
├── reference/authoring-contracts.md        # FR-001..004, FR-011 (new)
├── quickstart.md                           # FR-005: coverage line + evidence example at the right stages
└── index.md                                # link the new reference

.claude/skills/fs-gg-sdd-project/SKILL.md   # FR-013: coverage + evidence authoring guidance
.codex/skills/fs-gg-sdd-project/SKILL.md    # FR-013: equivalent guidance

tests/FS.GG.SDD.Commands.Tests/
├── ChecklistCommandTests.fs                # golden: enriched coverage correction
├── EvidenceCommandTests.fs                 # golden: enriched obligation correction
├── SpecifyCommandTests.fs                  # golden: enriched intent correction + named missing facts
├── VerifyCommandTests.fs / CommandReportJsonTests.fs / TextProjectionTests.fs  # JSON/text regen
└── AuthoringDocsContractTests.fs           # NEW — SC-005 drift guard (docs examples ↔ real parsers)
```

**Structure Decision**: Single-project layout. Behavior changes are confined to existing
`FS.GG.SDD.Commands` report/handler files (no new module, no `.fsi` change). The only new code
file is the drift-guard test. Documentation is the primary deliverable; the diagnostic edits
are small, localized string changes whose blast radius is golden-fixture regeneration.

## Complexity Tracking

> No constitution violations — section intentionally empty.

## Phase 0 — Research

See [research.md](./research.md). All Technical Context items are resolved (no NEEDS
CLARIFICATION remains): the source facts were re-verified against `main`, the JSON-serialized
`correction`/`message` finding drove the Tier-1 reclassification, and the `missingSpecificationIntent`
message was found to already name the missing facts (so FR-009 reduces to correction-text +
docs, not message logic).

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md) — the authoring entities and their validation rules as the
  docs and drift guard must describe them.
- [contracts/diagnostic-corrections.md](./contracts/diagnostic-corrections.md) — the exact
  before/after `Correction`/`Message` text for the three sites, with the invariants that must
  not change.
- [contracts/authoring-reference.md](./contracts/authoring-reference.md) — the required content
  of the reference doc and the machine-checkable example-tag convention the drift guard reads.
- [quickstart.md](./quickstart.md) — validation steps proving the feature end to end.
- Agent context: the `CLAUDE.md` plan pointer is updated to this plan (Phase 1 step 4).
