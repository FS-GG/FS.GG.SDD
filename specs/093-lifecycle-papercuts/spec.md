# Feature Specification: Lifecycle Authoring Papercuts (Counters, Task Refs, Atomic Write, Decision Refs, Clarify Title)

**Feature Branch**: `093-lifecycle-papercuts`

**Created**: 2026-07-08

**Status**: Draft

**Input**: User description: "SDD lifecycle papercuts from the 2026-07-07 FS.GG.Game and FS.GG.Audio workflow-feedback reports. Implements FS-GG/FS.GG.SDD#164 (epic #159), reduced to five bullets — the sixth (`outcome: noChange`) was split to FS-GG/FS.GG.SDD#183 because a new `CommandOutcome` case cascades through `Cli/Rendering.fs`, the `ReleaseContract.fs` report inventory, and ~20 test files, and collides with the touch-set of FS-GG/FS.GG.SDD#177 (ADR-0021 intra-repo parallel work)."

## Overview

Five independent authoring-surface defects, each reported by a product team driving the lifecycle
for real work. They share no code, but they share a shape: **an artifact says something the model
does not mean.** A counter reports unresolved ambiguities that were resolved. A task carries two
fields for one fact and the consumers disagree about which is canonical. A half-written `spec.md`
is observable on disk. A decision tag names four ids and three are dropped. A clarifications file
titles itself after a work id while pointing at a spec with a different title.

Investigation sharpened three of the five beyond what the feedback reported:

1. **The ambiguity counter cannot be fixed downstream.** `SpecificationFacts.UnresolvedAmbiguityCount`
   (`Specification.fs:245-250`) is a regex over `spec.md`'s own body counting lines that mention
   `AMB-###` and do not say `resolved` or `deferred`. It never reads `clarifications.md`. Resolving an
   ambiguity in `clarify` therefore *cannot* move a number derived only from `specify`'s text — no
   amount of re-running helps. The two counters that actually gate (`remainingAmbiguityCount`,
   `blockingAmbiguityCount`, from `Clarification.fs:359`) are correct. `SpecifyCommandTests.fs:454`
   already carries a comment flagging the incoherence.

2. **The multi-ref decision refs are not "ignored downstream" — they are never read.**
   `ClarificationQuestion` and `ClarificationDecisionFact` both populate `RelatedRequirementIds`,
   `RelatedStoryIds`, and `RelatedAcceptanceScenarioIds` (`Clarification.fs:202-204`, `:256-258`).
   Grepping `src/` for those three names returns *only* the record definitions, their `.fsi`
   signatures, and those two assignment sites. **There are no read sites.** Separately,
   `RequirementModel.parseDecisions` (`:98-130`) constructs a `Decision` with no FR/AC refs at all,
   `parseRemainingAmbiguity` (`Clarification.fs:265`) does `ambiguityIdsInLine line |> List.tryHead`
   and drops every AMB after the first, and `TaskGraphAuthoring.clarificationDecisionTasks`
   (`:271-278`) passes `requirements = []`. So the reported "extra refs silently ignored" is four
   independent drops on three different paths, not one lookup bug.

3. **`sourceIds` and `decisions` are not two spellings of one field — they are a generated bucket and
   an authored typed field, and the consumers disagree.** The shipped
   `docs/examples/lifecycle-artifacts/tasks.yml` authors `requirements: [FR-001]` and
   `decisions: [DEC-001]` and **no `sourceIds:` at all**, and it validates against the live parser on
   every build (`ExampleArtifactsContractTests`). Meanwhile `HandlersEvidence` (`:212`) and
   `HandlersVerify` (`:37, :154, :344`) read **only** `task.SourceIds`, so a hand-authored task in the
   documented shape is invisible to both. `analyze` reads the union
   (`allTaskDispositionIds` = `SourceIds ∪ Requirements ∪ Decisions`, `:717-724`), which is why
   nobody noticed. And `WorkModel.deriveGuidanceModel` (`:879`) computes
   `relatedIds = task.Requirements @ task.Decisions`, ignoring `SourceIds` — the exact mirror-image
   of the evidence/verify bug. `TaskGraphAuthoring.clarificationDecisionTasks` writes the same
   `DEC-###` into `sourceIds` *and* `decisions`, which is the duplication the feedback saw.

The `spec.md` atomic-write defect is exactly as reported and is a one-line-class fix at a single
site: `CommandEffects.fs:312-321` interprets every `WriteFile` effect with `File.WriteAllText`
(truncate-then-write). Because *all* authored artifacts flow through that one interpreter, one fix
covers `spec.md`, `clarifications.md`, `tasks.yml`, `evidence.yml`, and every generated view.

The `clarify` title defect is as reported: `clarificationTemplate` (`EarlyStageAuthoring.fs:1107`)
does `let title = requestTitle request workId`, and `requestTitle` (`:61-65`) reads *this
invocation's* `--title` or falls back to the humanized work id. The function already receives
`specFacts: SpecificationFacts`, whose `FrontMatter.Title` holds the real title.

**Change tier: Tier 1** (artifact-layout change to the authored `tasks.yml` surface and to
`work-model.json`'s decision entries; `CommandReport` counter surface change). It touches `.fsi`
signatures (`Clarification.fsi`, `Task.fsi`, `WorkModel.fsi`, `RequirementModel.fsi`,
`CommandTypes.fsi`, `LintEngine.fsi`) and therefore requires the `.fsi`-first discipline of
Constitution §I and §III. No `schemaVersion` bump is proposed (see Clarifications).

## Clarifications

### Session 2026-07-08

- Q: Should the five bullets be one feature or five? → A: **One feature, five independently
  testable user stories.** They are one board item (FS.GG.SDD#164), one declared touch-set, and one
  PR. Each story is separately verifiable and separately revertible; none depends on another. The
  sixth bullet was split out precisely because it *was* separable and had a wider blast radius.

- Q: Which of `sourceIds` / `decisions` is canonical? → A: **The typed fields (`requirements:`,
  `decisions:`) are the authored surface; `sourceIds:` is a derived untyped superset.** This is
  what the shipped example already documents and what the parser already accepts. Therefore:
  `sourceIds` becomes **derived on parse** as `distinct(authored sourceIds ∪ requirements ∪ decisions)`,
  every consumer reads the derived value, and the emitter stops writing a `sourceIds:` line that
  merely restates the typed fields. The alternative — deleting `decisions:` and making `sourceIds`
  authoritative — was rejected: it would break the shipped example, discard the FR/DEC type
  distinction that `analyze` relationships rely on, and make a hand-authored task graph less legible.

- Q: Does making `sourceIds` derived bump `tasks.yml`'s `schemaVersion`? → A: **No.** Deriving is a
  strict widening: every previously valid `tasks.yml` still parses to the same or a superset
  `SourceIds`, and a file carrying an explicit `sourceIds:` is still accepted (it is unioned, not
  ignored). No existing document becomes invalid, so `schemaVersion` stays `1`. The observable
  consequence is a one-time normalization diff on the next `tasks` re-run, and *more* tasks becoming
  visible to `evidence`/`verify` — which is the bug being fixed, not a regression.

- Q: Does fixing `deriveGuidanceModel.relatedIds` change generated agent guidance bytes? → A:
  **Yes, and that is intended.** `relatedIds` gains the `SourceIds` it should always have had. The
  `readiness/<id>/agent-commands/**` views and their digests move. The generated-view currency guard
  (`GeneratedModelCurrencyTests`) must be re-baselined as part of the change, not worked around.

- Q: What replaces `unresolvedAmbiguityCount`? → A: It is **removed from the report surface**, not
  recomputed. It is derivable-in-principle from `clarifications.md`, but `remainingAmbiguityCount`
  and `blockingAmbiguityCount` already carry that meaning correctly and *do* gate. A third counter
  that agrees with them adds no information; one that disagrees is the bug. `SpecificationFacts`
  keeps the field only if a non-report consumer needs it — and none does (see FR-002).

- Q: Is removing a `CommandReport` JSON key a breaking change for Governance? → A: **No — removal is
  safe, and this was verified rather than assumed.** Three checks, all negative:
  (a) `ReleaseContract.fs`'s `jsonInventory` freezes only the **top-level** report keys
  (`schemaVersion`, `reportVersion`, `command`, `context`, `invocation`, `outcome`,
  `changedArtifacts`, `specification`, `clarification`, …). `specification` is frozen as a key; the
  counters *nested inside it* are not enumerated, so `unresolvedAmbiguityCount` is not part of the
  frozen surface. (b) `grep -rn "ambiguit" src/FS.GG.SDD.Artifacts/ReleaseContract.fs docs/release/`
  returns nothing. (c) `GovernanceHandoff.fs` never mentions ambiguity, so the one artifact that
  crosses the Governance boundary does not carry the counter. **FR-002 therefore resolves to removal**,
  and `ReleaseConformanceTests` is the regression guard that keeps that true.

- Q: Should the atomic write be `File.Replace` or temp + `File.Move(overwrite: true)`? → A: **Temp
  sibling + `File.Move(source, dest, overwrite = true)`.** `File.Replace` requires the destination to
  exist and fails on first write; `File.Move` with `overwrite` is atomic on the same volume on both
  Linux and Windows and handles create-and-replace uniformly. The temp file must be a sibling (same
  directory, therefore same volume) and must be cleaned up on failure. See FR-008.

- Q: Does the atomic write change `dryRun` behavior or the `NoChange` snapshot logic? → A: **No.**
  `canOverwrite`/`snapshotIfExists` run before the write and are untouched; `dryRun` still performs no
  I/O. Only the mechanics of the byte-committing step change. A no-op `WriteFile` (identical content)
  must still record `ArtifactOperation.NoChange` and must not rewrite the file.

- Q: Do the four dropped decision-ref paths all land here? → A: **Yes** — they are one defect with
  four symptoms (unread `Related*Ids`, ref-less `parseDecisions`, `tryHead` on `parseRemainingAmbiguity`,
  `requirements = []` in `clarificationDecisionTasks`). Fixing any one alone leaves the tag's refs
  dropped somewhere else, so the acceptance test is end-to-end: a `DEC-###` naming `FR-007`, `FR-001`,
  and `AC-005` reaches `work-model.json` and the task graph with all three refs.

- Q: "Error on truly-invalid refs" (from the issue) — in scope? → A: **Already implemented; this
  feature adds regression coverage only.** `unknownReferenceDiagnostics` (`EarlyStageAuthoring.fs:882-916`)
  already resolves every `AMB-`/`CQ-`/`FR-`/`US-`/`AC-` id in the `clarify --input` lines against the
  spec's declared id sets and emits the blocking `unknownClarificationReference` (`errorForRef`,
  asserted at `ClarifyCommandTests.fs:369`). The issue's "error on truly-invalid refs" is therefore
  satisfied for the path an author actually uses. Two residual gaps are **out of scope**: a ref
  hand-typed directly into a committed `clarifications.md` (never re-validated), and a malformed token,
  which the id regex does not match and so cannot be distinguished from prose. See FR-013.

- Q: Does the `clarify` title fix change the `fs-gg-sdd-clarify` process skill (and hence its pinned
  `sha256` in `skill-manifest.json` / `registry/skills.yml`)? → A: **No.** The skill documents the
  `clarify` command's contract and the CQ/DEC/AMB grammar; it does not state where the front-matter
  `title:` is sourced from. No skill body changes, so no manifest reconcile.

- Q: Does `--title` still work on `clarify`? → A: **Yes, and it still wins.** The precedence becomes
  explicit `--title` → `specFacts.FrontMatter.Title` → humanized work id. Only the middle rung is new.
  The existing `clarify --title "…"` workaround therefore keeps behaving identically.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The clarify skeleton inherits the spec's title (Priority: P1)

An author runs `fsgg-sdd specify` for a work item titled `Ambient audio bed`, then runs
`fsgg-sdd clarify` with no `--title`. The emitted `work/<id>/clarifications.md` should title itself
`Ambient audio bed`, matching the `spec.md` its own `sourceSpec:` line points at.

**Why this priority**: Smallest, most local fix (one expression). It is the defect that blocks
verification of FS-GG/FS.GG.Templates#118, and feature 089's blocked-clarify seed path newly exposes
it in exactly the situation where an author has no reason to pass `--title`.

**Independent Test**: Run `specify --title "Ambient audio bed"` then `clarify` with no title; assert
`clarifications.md`'s front-matter `title:` and `# … Clarifications` heading both read
`Ambient audio bed`, not `Demo`.

**Acceptance Scenarios**:

1. **Given** a `spec.md` with front-matter `title: Ambient audio bed`, **When** `clarify` runs with no
   `--title`, **Then** `clarifications.md` front matter reads `title: Ambient audio bed`.
2. **Given** the same `spec.md`, **When** `clarify --title "Override"` runs, **Then** the front matter
   reads `title: Override` — the explicit flag still wins.
3. **Given** a `spec.md` whose front-matter title is absent or blank, **When** `clarify` runs with no
   `--title`, **Then** the front matter falls back to the humanized work id, exactly as today.
4. **Given** the 089 blocked-clarify seed path (a spec with unresolved blocking ambiguities), **When**
   the skeleton is emitted, **Then** it carries the spec's title.

---

### User Story 2 - A partially written spec.md is never observable (Priority: P1)

An author (or an agent watching the file) never sees a `spec.md` containing only the boilerplate
`FR-001` placeholder. The file transitions from its old content to its new content in one step.

**Why this priority**: A data-integrity defect, not an ergonomic one. A concurrent reader — the
agent harness, a file watcher, a second `fsgg-sdd` process — can observe and act on a torn artifact.
It is a single-site fix that protects every authored artifact at once.

**Independent Test**: Drive the `WriteFile` interpreter against a path that already holds known
content, with a writer that fails partway; assert the original content is intact and no temp file
remains. Assert the same for the success path via content equality.

**Acceptance Scenarios**:

1. **Given** an existing `spec.md`, **When** `specify` rewrites it, **Then** at no point does the path
   hold a prefix of the new content — an observer sees either the complete old bytes or the complete
   new bytes.
2. **Given** a `WriteFile` whose commit step throws, **When** the effect is interpreted, **Then** the
   destination retains its original bytes and no sibling temp file survives.
3. **Given** a `WriteFile` to a path that does not yet exist, **When** the effect is interpreted,
   **Then** the file is created with the full content (`File.Replace` semantics are not required).
4. **Given** a `WriteFile` whose content equals the file's current content, **When** the effect is
   interpreted, **Then** `ArtifactOperation.NoChange` is recorded and the file's mtime is unchanged.
5. **Given** `dryRun = true`, **When** the effect is interpreted, **Then** no file — temp or
   destination — is created or modified.

---

### User Story 3 - The ambiguity counters agree with each other (Priority: P2)

An author who has resolved every ambiguity in `clarifications.md` sees no counter claiming
unresolved ambiguities remain.

**Why this priority**: The reported symptom (`unresolvedAmbiguities: 4` alongside
`blocking/remaining = 0`) makes an author distrust the two counters that *are* correct and *do* gate.
Trust in the gate signal is the product.

**Independent Test**: Author a spec declaring `AMB-001..AMB-004`, resolve all four in
`clarifications.md`, run `clarify --text`; assert no counter in the report reports a nonzero
unresolved/remaining/blocking ambiguity count.

**Acceptance Scenarios**:

1. **Given** a spec declaring four ambiguities and a `clarifications.md` resolving all four, **When**
   `clarify` runs, **Then** every ambiguity counter in the report reads `0`.
2. **Given** a spec declaring four ambiguities and a `clarifications.md` resolving two, **When**
   `clarify` runs, **Then** the reported remaining count is `2` and no other counter contradicts it.
3. **Given** a spec with declared ambiguities and **no** `clarifications.md` yet, **When** `specify`
   runs, **Then** the report's ambiguity signal reflects the declared-and-unresolved count and does
   not claim a resolution the author has not made.
4. **Given** any of the above, **When** the report is projected `--json`, `--text`, and `--rich`,
   **Then** the three projections agree on every counter.

---

### User Story 4 - A decision tag's every reference survives to the work model (Priority: P2)

An author writes a decision that resolves an ambiguity and settles three requirements at once:
`- DEC-003 [AMB:AMB-002] Resolves FR-007, FR-001 and AC-005 by …`. All four ids reach the work model
and the task graph.

**Why this priority**: Traceability is the artifact's purpose. A ref that is parsed and then dropped
is worse than one never parsed — it looks recorded. Four separate drops on three paths mean an author
cannot tell which of their refs took effect.

**Independent Test**: Author a `clarifications.md` whose `DEC-003` names `FR-007`, `FR-001`, and
`AC-005`; run `tasks` then `refresh`; assert `work-model.json`'s `DEC-003` entry carries all three
refs and the derived task's refs include them.

**Acceptance Scenarios**:

1. **Given** a decision line naming `FR-007`, `FR-001`, and `AC-005`, **When** the work model is built,
   **Then** the `DEC-003` entry carries all three refs, sorted and deduplicated.
2. **Given** the same line, **When** the task graph is generated, **Then** the derived task's
   `requirements:` includes `FR-001` and `FR-007` — not `[]`.
3. **Given** a `Remaining Ambiguity` line naming `AMB-002` and `AMB-004`, **When** it is parsed,
   **Then** both are recorded as remaining — not only `AMB-002`.
4. **Given** a decision naming `FR-999` where the spec declares only `FR-001..FR-008`, **When**
   `clarify` runs, **Then** it blocks with a diagnostic naming `FR-999` and the file is not advanced.
5. **Given** a decision naming no FR/AC refs at all, **When** the work model is built, **Then** its
   ref lists are empty and no diagnostic fires — the refs are optional.

---

### User Story 5 - A task's references have one meaning (Priority: P3)

An author hand-writes a task in the shape the shipped example documents — `requirements: [FR-001]`,
`decisions: [DEC-001]`, no `sourceIds:` — and `evidence` and `verify` see its references.

**Why this priority**: Largest change (parser, emitter, work model, four consumers, fixtures) and the
one with an observable normalization diff. The two consumers that read only `SourceIds`
(`evidence`, `verify`) are silently blind to the documented authoring shape today, and
`deriveGuidanceModel` is blind in the opposite direction. Sequenced last so the smaller fixes are not
held behind it.

**Independent Test**: Parse the shipped `docs/examples/lifecycle-artifacts/tasks.yml` (which has no
`sourceIds:`); assert each task's derived `SourceIds` contains its `requirements` and `decisions`, and
that `evidence` and `verify` resolve those tasks' obligations.

**Acceptance Scenarios**:

1. **Given** a task authored with `requirements: [FR-001]` and `decisions: [DEC-001]` and no
   `sourceIds:`, **When** it is parsed, **Then** its `SourceIds` is `[DEC-001; FR-001]`.
2. **Given** a task authored with an explicit `sourceIds: [SB-002]` *and* typed refs, **When** it is
   parsed, **Then** its `SourceIds` is the sorted union of all three — the explicit value is kept.
3. **Given** the shipped example `tasks.yml`, **When** `evidence` and `verify` run, **Then** each
   task's obligations resolve against its typed refs.
4. **Given** a work model built from that task, **When** agent guidance is derived, **Then**
   `relatedIds` includes ids that appear only in `sourceIds` — `deriveGuidanceModel` no longer ignores
   them.
5. **Given** an existing `tasks.yml` carrying a `sourceIds:` line that merely restates the typed refs,
   **When** `tasks` re-runs, **Then** the redundant line is dropped and two consecutive re-runs are
   byte-identical.
6. **Given** a `DEC-###`-derived task, **When** the graph is generated, **Then** the id is not written
   twice (once to `sourceIds`, once to `decisions`).

---

### Edge Cases

- A `spec.md` whose `AMB-###` id appears inside a fenced code block or a `Non-Goals` line — the
  counter's line-regex counts it today. Does the replacement inherit that? (It must not regress:
  behavior is pinned by existing tests.)
- A decision line naming the same `FR-001` twice — refs must dedupe, not double-count.
- A decision line naming an `AC-###` that belongs to a *different* FR than the one it also names.
  Both refs are recorded; no cross-consistency check is implied.
- Case sensitivity: `allTaskDispositionIds` upper-cases before the membership test. Derived
  `SourceIds` must not change that comparison's outcome.
- A `WriteFile` to a path whose parent directory does not exist — `Directory.CreateDirectory` still
  precedes the temp write.
- A `WriteFile` on a filesystem where the temp sibling and destination differ in volume — impossible
  by construction (same directory), but the failure must surface as a `DiagnosticError`, not a silent
  partial write.
- Two `fsgg-sdd` processes writing the same artifact concurrently. Out of scope: atomicity is
  per-write, not a lock. The guarantee is "no torn read", not "no lost update".
- `tests/fixtures/**` `tasks.yml` documents that carry `sourceIds:` — these are inputs to golden
  tests and must keep parsing to the same model.

## Requirements *(mandatory)*

### Functional Requirements

**Clarify title (US1)**

- **FR-001**: `clarificationTemplate` MUST source its title with the precedence: explicit
  `request.Title` → `specFacts.FrontMatter.Title` → humanized work id. The resolved title MUST be used
  for both the front-matter `title:` field and the `# … Clarifications` heading.

**Ambiguity counters (US3)**

- **FR-002**: `unresolvedAmbiguityCount` MUST be removed from the `CommandReport` surface (JSON key
  `unresolvedAmbiguityCount`, text key `unresolvedAmbiguities`). It is not part of the frozen release
  inventory and no Governance-boundary artifact carries it (see Clarifications). After removal, no
  `CommandReport` counter may report a nonzero unresolved-ambiguity count for a work item whose
  declared ambiguities are all resolved or deferred in `clarifications.md`. The `spec.md`-only regex
  MUST NOT survive as a report-facing value.
- **FR-003**: `remainingAmbiguityCount` and `blockingAmbiguityCount` MUST retain their current
  values and their current gating behavior. This feature changes no gate.
- **FR-004**: The `--json`, `--text`, and `--rich` projections MUST agree on every ambiguity counter
  they render. (`--rich` derives from `--text`, so this is a consequence, not an independent check.)

**Atomic write (US2)**

- **FR-005**: The `WriteFile` effect interpreter MUST commit bytes atomically: no observer may read a
  destination path holding a proper prefix of the intended content.
- **FR-006**: The atomic commit MUST apply to every `WriteFile`, not only to `spec.md` — the fix
  belongs at the interpreter, not at any single artifact's producer.
- **FR-007**: When the commit step fails, the destination MUST retain its prior bytes (or remain
  absent if it did not exist), no temp artifact may survive, and the failure MUST surface as the
  existing `DiagnosticError` path.
- **FR-008**: The temp artifact MUST be created in the destination's own directory (guaranteeing a
  same-volume rename) and MUST NOT be mistaken for a lifecycle artifact by any glob (`readiness/**`,
  `work/**`, `docs/api-surface/**`).
- **FR-009**: `canOverwrite`, `snapshotIfExists`, and the `ArtifactOperation.NoChange` determination
  MUST be unchanged. An identical-content `WriteFile` MUST still be a no-op that writes nothing.
- **FR-010**: `dryRun = true` MUST perform no filesystem write of any kind, temp included.

**Decision refs (US4)**

- **FR-011**: A `DEC-###` line's every `FR-###`, `US-###`, and `AC-###` reference MUST reach the work
  model, deduplicated and sorted. `RequirementModel.parseDecisions` MUST populate the refs it
  currently discards.
- **FR-012**: `parseRemainingAmbiguity` MUST record every `AMB-###` a line names, not only the first.
- **FR-013**: A `clarify --input` line naming a well-formed but undeclared id (e.g. `FR-999` against a
  spec declaring `FR-001..FR-008`) MUST block with a diagnostic naming the offending id. **This already
  holds** via `unknownReferenceDiagnostics`; this feature adds a regression test pinning it for a
  *multi-ref decision line* specifically (the case the issue reported), and MUST NOT weaken it when
  FR-011 threads the extra refs through.
- **FR-014**: `clarificationDecisionTasks` MUST pass the decision's `RelatedRequirementIds` through to
  the derived task rather than `[]`. This is also the read site that discharges FR-015.
- **FR-015**: `ClarificationQuestion`'s and `ClarificationDecisionFact`'s `RelatedRequirementIds`,
  `RelatedStoryIds`, and `RelatedAcceptanceScenarioIds` MUST either be consumed or be removed. A field
  that is parsed, stored, exposed in `.fsi`, and never read MUST NOT survive this feature.

**Task refs (US5)**

- **FR-016**: `WorkTask.SourceIds` MUST be derived on parse as the sorted, deduplicated union of the
  authored `sourceIds:` list, the `requirements:` list, and the `decisions:` list.
- **FR-017**: Every existing `tasks.yml` MUST continue to parse. An explicit `sourceIds:` entry MUST
  be retained in the union, never discarded. `schemaVersion` MUST remain `1`.
- **FR-018**: The `tasks.yml` emitter MUST NOT write a `sourceIds:` line that only restates the typed
  `requirements:`/`decisions:` fields. Two consecutive `tasks` runs MUST be byte-identical.
- **FR-019**: `clarificationDecisionTasks` MUST NOT write the same `DEC-###` into both `sourceIds` and
  `decisions`.
- **FR-020**: `WorkModel.deriveGuidanceModel` MUST compute `relatedIds` from the derived `SourceIds`,
  so an id reachable only via `sourceIds` appears in generated agent guidance.
- **FR-021**: `HandlersEvidence` and `HandlersVerify` MUST resolve obligations for a task authored in
  the shipped example's shape (typed refs, no `sourceIds:`).
- **FR-022**: `allTaskDispositionIds` MUST produce the same set as today for every existing
  `tasks.yml`, and its case-insensitive membership test MUST be unaffected by the derivation.

**Cross-cutting**

- **FR-023**: Every `.fsi` signature touched MUST be updated before its `.fs` implementation
  (Constitution §I, §III).
- **FR-024**: The generated-view goldens (`work-model.json`, `agent-commands/**`, `summary.md`) MUST
  be re-baselined, and their diffs MUST be reviewed as a deliverable — a digest-only diff for the
  views this feature does not intend to move, and a reviewed content diff for `relatedIds` (FR-020)
  and the `tasks.yml` normalization (FR-018).
- **FR-025**: `docs/examples/lifecycle-artifacts/tasks.yml` and `docs/reference/authoring-contracts.md`
  MUST describe the reconciled field semantics, and `AuthoringDocsContractTests` /
  `ExampleArtifactsContractTests` MUST keep enforcing them.

### Key Entities

- **`SpecificationFacts.UnresolvedAmbiguityCount`**: a `spec.md`-body regex count. The subject of
  FR-002 — removed from the report, or corrected; never left as-is.
- **`ClarificationDecisionFact` / `ClarificationAnswerFact`**: carry `RelatedRequirementIds`,
  `RelatedStoryIds`, `RelatedAcceptanceScenarioIds` — populated and, today, never read (FR-015).
- **`WorkTask.SourceIds`**: today an authored, independently-stored list. After this feature, a
  derived union over the authored list and the typed ref fields (FR-016).
- **`WorkTask.Requirements` / `WorkTask.Decisions`**: the authored, typed, human-facing reference
  surface. Canonical.
- **`WriteFile` effect**: the single interpreter site through which every authored artifact and
  generated view is committed (FR-005, FR-006).
- **Decision ref set**: the `FR-###`/`US-###`/`AC-###` ids a `DEC-###` line names. Currently dropped
  on four distinct paths (FR-011, FR-012, FR-014, FR-015).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With a `spec.md` titled `Ambient audio bed`, `fsgg-sdd clarify` with no `--title`
  produces a `clarifications.md` whose front-matter `title:` equals `Ambient audio bed`. Asserted
  directly; the pre-fix value (`Demo`, the humanized work id) is asserted absent.
- **SC-002**: Zero `WriteFile` code paths call `File.WriteAllText` directly against a destination
  path. Enforced structurally (a grep-equivalent test or a single funnel function), not by review.
- **SC-003**: A forced mid-write failure leaves the destination byte-identical to its pre-write
  content, and the destination directory contains no residual temp file. Asserted with a fault-injected
  interpreter, in a new `tests/FS.GG.SDD.Commands.Tests/CommandEffectsTests.fs` — the first direct test
  of the effect interpreter.
- **SC-004**: For a work item whose four declared ambiguities are all resolved, the `clarify` report
  contains **no** counter with a nonzero value naming ambiguities, across all three projections.
- **SC-005**: A `DEC-003` line naming `FR-007`, `FR-001`, and `AC-005` yields exactly
  `["AC-005"; "FR-001"; "FR-007"]` on the `work-model.json` decision entry, and the derived task's
  `requirements:` is `[FR-001, FR-007]`. A `Remaining Ambiguity` line naming two AMB ids records both.
- **SC-006**: A `clarify --input` decision line naming `FR-007`, `FR-001`, and `FR-999` against a spec
  declaring `FR-001..FR-008` blocks with `unknownClarificationReference` naming `FR-999`, and writes
  nothing — the multi-ref threading of FR-011 does not smuggle an undeclared ref past the existing gate.
- **SC-007**: `grep -r "RelatedRequirementIds" src/` returns either read sites or nothing — never
  only definitions and assignments (FR-015).
- **SC-008**: The shipped `docs/examples/lifecycle-artifacts/tasks.yml`, unmodified in its `sourceIds`
  absence, parses to tasks whose `SourceIds` equal their typed refs, and `evidence` + `verify` resolve
  their obligations. Asserted by extending `ExampleArtifactsContractTests`.
- **SC-009**: `fsgg-sdd tasks` run twice over a normalized `tasks.yml` produces byte-identical output,
  and the emitted file contains no `sourceIds:` line that restates the typed fields.
- **SC-010**: Every previously committed `tests/fixtures/**/tasks.yml` parses to a `SourceIds` that is
  a superset of its pre-change value, and to an identical `allTaskDispositionIds` set (FR-022).
- **SC-011**: `dotnet test` is green with no `PublicSurface.baseline` drift beyond the `.fsi` changes
  this feature declares, and `fsgg-sdd surface --check` exits 0.
- **SC-012**: `readiness/**` goldens change only as FR-024 permits: digest-only where no semantics
  moved; a reviewed content diff for `relatedIds` and the `tasks.yml` normalization.

## Assumptions

- `File.Move(source, dest, overwrite = true)` is atomic for a same-directory rename on every platform
  this CLI targets (Linux, macOS, Windows / NTFS). The temp sibling construction guarantees
  same-volume. This is relied upon rather than re-implemented; it is the same primitive .NET's own
  `File.Replace` builds on, without `File.Replace`'s must-already-exist precondition.
- No FS-GG consumer reads `unresolvedAmbiguityCount` from a `CommandReport`. Verified against
  `ReleaseContract.fs`'s frozen top-level inventory, `docs/release/`, and `GovernanceHandoff.fs` — all
  three are silent on ambiguity counters (see Clarifications). `ReleaseConformanceTests` is the
  standing guard.
- The `tasks.yml` reader's acceptance of an absent `sourceIds:` key is intentional and load-bearing —
  the shipped example depends on it and is validated on every build.
- The 2026-07-07 FS.GG.Game (§WD3, §WD4) and FS.GG.Audio (§3.7, §3.9) feedback reports are the
  authoritative statements of the reported symptoms; this spec supersedes their *diagnosis* where
  source investigation contradicted it (Overview items 1–3).
- FS-GG/FS.GG.SDD#183 (`outcome: noChange`) lands after this feature and rebases on it. This feature
  must not pre-emptively restructure `resolveOutcome` or the `CommandOutcome` DU on #183's behalf.
- FS-GG/FS.GG.SDD#177 runs concurrently in a separate worktree with a disjoint touch-set
  (`fsgg-coord overlap FS.GG.SDD#164 FS.GG.SDD#177` → `DISJOINT`). The one shared file outside both
  declarations is `.specify/feature.json`, which every feature branch rewrites; its one-line conflict
  is resolved by taking the merging branch's value.

## Dependencies

- No cross-repo dependency. No Governance runtime. No new package.
- Blocks FS-GG/FS.GG.SDD#183 (shares `CommandTypes.fs`, `CommandSerialization.fs`,
  `CommandRendering.fs`).
- Unblocks verification of FS-GG/FS.GG.Templates#118 (US1).

## Out of Scope

- `outcome: noChange` disambiguation — split to FS-GG/FS.GG.SDD#183.
- Concurrency control between two `fsgg-sdd` processes writing one artifact. FR-005 guarantees no
  torn read, not no lost update.
- Atomicity for the three non-interpreter `File.WriteAllText` sites (`Cli/Program.fs:138`,
  `Cli/RegistrySkillManifest.fs:75`, `Validation/ValidationRunner.fs:103`). Same bug class, different
  touch-set; note them for a follow-up rather than widen this item's declared `Paths:`.
- Any `schemaVersion` bump, migration flag, or Governance coordination.
- Cross-consistency checking between a decision's `FR-###` and `AC-###` refs.
