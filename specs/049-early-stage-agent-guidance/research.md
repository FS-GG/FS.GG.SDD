# Phase 0 Research: Early-Stage Agent Guidance Bootstrap

All NEEDS CLARIFICATION from the spec are resolved below. Each decision records the
choice, the rationale grounded in the existing code, and the rejected alternatives.

## D1 — How is the static early-stage guidance delivered?

**Decision**: A **seeded skeleton file**, `.fsgg/early-stage-guidance.md`, written by
`fsgg-sdd init` as an embedded F# string literal with write kind
`ArtifactWriteKind.AgentGuidanceTarget`.

**Rationale**:

- SC-001 / FR-010a require the guidance to be obtainable *"from a freshly-initialized
  SDD skeleton"* in a single step. The skeleton is the **target product's** tree, not
  this repo. A `docs/` reference page in FS.GG.SDD would never reach a scaffolded
  product, so it cannot satisfy the requirement.
- US3 AC3 explicitly anticipates *"a skeleton that already carries author-touched
  early-stage guidance"* that must be **no-clobber on re-run**, *"consistent with the
  skeleton constitution / `CLAUDE.md` policy."* That is, verbatim, the
  `AgentGuidanceTarget` mechanism: `canOverwrite` (`CommandEffects.fs:42-48`) permits
  a write only when the file is absent or byte-identical, and **refuses** (preserves)
  an edited `AgentGuidanceTarget`/`StructuredSource` file, surfacing `unsafeOverwrite`.
- `.fsgg/constitution.md` (`Foundation.fs:208`, content `Foundation.fs:86-197`) is an
  exact precedent: a generic, deterministic, date/token-free embedded literal, seeded
  by `initEffects` (`Foundation.fs:199-210`), no-clobber by construction, and — per
  the release-doc grep — **not** a release-catalog entry.

**Alternatives rejected**:

- *A new `fsgg-sdd guidance` subcommand that prints on demand.* Adds CLI surface and
  still needs a content source; does not give the author an editable artifact in their
  skeleton; heavier than reusing `init`.
- *A `docs/reference/` page in this repo.* Does not travel into a scaffolded product
  (fails SC-001); only helps someone reading SDD's own source.
- *Generating the file from `agents`/`refresh`.* Would make it a generated view
  subject to the digest/source-of-truth machinery — the opposite of a stage-zero
  static artifact, and it would not exist before the first `agents` run.

## D2 — How is the generated best-effort guidance (FR-010b) represented?

**Decision**: **In the `CommandReport` only** — best-effort prose plus a `NextAction`
pointing to `.fsgg/early-stage-guidance.md`. The early-stage path writes **no**
on-disk view file (`guidance.json` / `commands.md` / `skills.md`).

**Rationale**:

- FR-008 / FR-011 / SC-006 are the hard constraint: the per-work-item generated views
  must remain a *faithful, digest-backed projection of the work model and the sole
  source of truth once the model is buildable*, and the partial guidance MUST NOT be
  *"marked or digest-stamped as if it were the full work-model-derived projection."*
- Writing any file under `readiness/<id>/agent-commands/<target>/` in early-stage mode
  would either collide with the real generated path or require a parallel
  near-identical path — both invite an incomplete result being mistaken for complete.
- The `CommandReport` is itself *emitted* output (default/`--json`/`--text`/`--rich`),
  so report-borne guidance satisfies *"emitted by `fsgg-sdd agents` / `refresh`"*
  (FR-010b) without minting a second artifact. The best-effort content is a strict
  enumeration of **which early artifacts already exist** plus the **next lifecycle
  command** — derived only from present artifacts, never fabricated (FR-011, edge case
  "partially-authored stage").

**Alternatives rejected**:

- *Write an early-stage `commands.md` to the generated root.* Violates FR-008/FR-011;
  risks shadowing the real projection; breaks the SC-006 byte-identity guarantee if
  the path is later overwritten.
- *Write to a sibling `early-stage/` directory.* Extra artifact, extra catalog
  question, extra clobber surface for marginal benefit over the report pointer.

## D3 — Missing vs malformed work model: what gets reclassified?

**Decision**: Only the **missing** `work-model.json` is reclassified from a blocking
error to a non-blocking **early-stage advisory** (exit 0, with `NextAction`).
`agents.malformedWorkModel`, `agents.staleWorkModel`, `agents.blockedWorkModel`, and a
malformed existing generated view keep blocking exactly as today.

**Rationale**:

- Per the spec assumptions, the work model is produced on the `verify`/`ship` path; a
  *missing* model therefore means the author is legitimately pre-verify — a recognized,
  navigable early state, not a defect (FR-005). A *malformed* model is a genuine defect
  and must still fail fast (Constitution VIII; FR-008's "must not weaken the
  invariant"). Distinguishing them is exactly the existing branch split — the missing
  case is `HandlersAgents.fs:211-212`; malformed/stale are separate arms.
- This keeps SC-006 trivially true: the buildable-work-model path is untouched, so
  generated views stay byte-identical.

**Alternatives rejected**:

- *Reclassify all work-model problems to advisory.* Would let a corrupt work model
  pass silently — a safe-failure regression.

## D4 — One generic guidance file or per-target (Claude/Codex)?

**Decision**: **One generic** `.fsgg/early-stage-guidance.md`. The
`agents`/`refresh` pointer from every target resolves to the same file.

**Rationale**: FR-009 requires *aligned* Claude/Codex behavior with **no** divergence.
The per-stage commands, headings, stable-id formats, and §1.1/§1.2 contracts are
agent-agnostic, so a single file is inherently parity-safe and removes a drift vector.
`.fsgg/constitution.md` is the same single-generic-file pattern.

**Alternatives rejected**: *Per-target files.* Pure duplication of identical content;
adds a divergence risk FR-009 forbids.

## D5 — Release-catalog treatment

**Decision**: The seeded `.fsgg/early-stage-guidance.md` is an **uncatalogued authored
skeleton artifact**, mirroring `.fsgg/constitution.md` (which has no `catalog[]`
entry). `docs/release/schema-reference.md` gains a short note recording the new
early-stage advisory dispositions on the command-report contract and naming the seed
as an authored skeleton file (not a produced view).

**Rationale**: The release `catalog[]` enumerates **produced lifecycle views** under
`readiness/<id>/` (schema-reference.md). Authored skeleton seeds
(`.fsgg/constitution.md`, `CLAUDE.md`, `AGENTS.md`, the `.fsgg/*.yml` configs) are not
catalogued. The early-stage guidance is the same class of artifact, so adding a
`catalog[]` entry would be incorrect. The command-report contract is `additiveOptional`;
the new advisory dispositions are behavior pinned by tests, not a new schema field.

## D6 — Determinism and zero dangling references (FR-007, SC-003, SC-004)

**Decision**: (a) The guidance literal carries **no** date/timestamp/random/repo/
provider token (mirroring `constitutionText` `Foundation.fs:81-85`), guaranteeing
byte-identical `init` output and a no-op no-clobber re-run. (b) A **drift-guard test**
(`EarlyStageGuidanceContractTests`, modeled on feature 046's
`AuthoringDocsContractTests`) asserts that every heading, stable-id prefix, lifecycle
command, file path, and authoring-contract rule the guidance names resolves against
the **live** contract: the standard-section lists
(`Specification/Clarification/Checklist.fs`, `ParsingEarly.charter`), `Identifiers`
id prefixes, the `nextLifecycleCommand` mapping (`CommandTypes.fs:541-556`), and
`docs/reference/authoring-contracts.md`. (c) The early-stage `agents`/`refresh` report
is asserted byte-identical across repeated runs (the existing generate-twice
convention).

**Rationale**: SC-003 ("100% of references resolve, zero dangling") and the original
failure mode (dangling skill reference) demand a mechanical cross-check rather than
review-time vigilance; pinning prose to the structured contract is the same discipline
feature 046 used for the authoring contracts.

## Authoritative source facts consumed (no redefinition)

- **§1.1 acceptance coverage line** and **§1.2 `evidence.yml` rule** —
  `docs/reference/authoring-contracts.md` (the `## Acceptance coverage line` and
  `## evidence.yml declarations` sections), published under FS-GG/FS.GG.SDD#38. The
  guidance restates these; it does not redefine them.
- **Per-stage required headings** — Charter: Identity, Principles, Scope Boundaries,
  Policy Pointers, Lifecycle Notes (`ParsingEarly.fs:288-313`). Spec: User Value,
  Scope, Non-Goals, User Stories, Acceptance Scenarios, Functional Requirements,
  Ambiguities, Public Or Tool-Facing Impact, Lifecycle Notes (`Specification.fs:44-53`).
  Clarify: Source Specification, Clarification Questions, Answers, Decisions, Accepted
  Deferrals, Remaining Ambiguity, Lifecycle Notes (`Clarification.fs:87-94`). Checklist:
  Source Specification, Source Clarifications, Source Snapshot, Checklist Items, Review
  Results, Accepted Deferrals, Blocking Findings, Advisory Notes, Lifecycle Notes
  (`Checklist.fs:63-72`).
- **Stable-id formats** — `Identifiers.fs`: `FR-###`, `US-###`, `AC-###`, `SB-###`,
  `AMB-###`, `CQ-###`, `DEC-###`, `CHK-###`, `CR-###` (all `^PREFIX-\d{3,}$`,
  upper-cased); `workId` lowercase kebab-case.
- **Per-stage commands & ordering** — `charter → specify → clarify → checklist`
  (`Identifiers.allStages`, `nextLifecycleCommand` `CommandTypes.fs:541-556`).
- **Diagnostic seam** — `agents.missingWorkModel` (`CommandReports.fs:775-780`, today
  `errorForPath`); `refresh.blockedUpstreamView` (`CommandReports.fs:874-880`).
