# Feature Specification: Plan Upstream Snapshot (refresh own snapshot; never mutate authored plan.md)

**Feature Branch**: `090-plan-upstream-snapshot`

**Created**: 2026-07-08

**Status**: Draft

**Input**: User description: "Close the plan-stage contract-integrity defect in `fsgg-sdd` reported by FS.GG.Game SDD workflow-feedback (§WD1/§WD5/§WD6, the 'sharpest new finding'), tracked as FS-GG/FS.GG.SDD#163 under epic FS-GG/FS.GG.SDD#159. When an operator edits `spec.md` or `clarifications.md` after `plan` has run, downstream stages recover non-uniformly: `checklist` self-heals by rewriting its own `## Source Snapshot` digests, but `plan` leaves its digests stale and injects a tool-authored `PD-### … stale: Source specification, clarification, or checklist facts changed …` line into the operator's authored `plan.md`; `tasks` then blocks on `failedPlanPrerequisite` two stages later. Recovery costs three hand-edited digests plus deleting the injected `PD-###`. Root cause: `plan` mutates a file the artifact model itself designates *authored*, violating the authored-vs-generated split, and no stage owns a 'my source digest went stale' rule. Fix: `plan` refreshes its **own** snapshot rather than editing authored prose; add `fsgg-sdd plan --accept-upstream` as the explicit refresh gesture; detect the staleness at `plan` rather than letting it surface at `tasks`. Constraints: no versioned-contract change; `plan.md` stays authored source; output stays deterministic."

## Overview

The SDD artifact model draws one load-bearing line: some files are **authored** by the operator and some are **generated** by the tool. `work/<id>/plan.md` is authored. `fsgg-sdd plan` breaks that line.

When the upstream sources change after a plan exists — the ordinary consequence of fixing a real spec bug discovered during planning — `plan` re-runs and does two wrong things. It leaves its own `## Source Snapshot` digests pointing at content that no longer exists, and it *writes a sentence into the operator's prose*: a synthesized `- PD-007 [DEC-003] stale: Source specification, clarification, or checklist facts changed since prior plan decisions were recorded.` appended to `## Plan Decisions`. That line is indistinguishable, on the page and to the parser, from a design decision the operator made. It is not one. It is a diagnostic wearing a decision's clothes.

The staleness is then reported as a *warning*, so `plan` exits 0 and the operator moves on. Two stages later `tasks` refuses to run — `failedPlanPrerequisite: Plan contains stale decisions.` — because the parser reads the injected line's `stale:` marker back as a decision with `Status = "stale"`. Recovery is manual and undocumented: hand-edit three sha256 digests in `## Source Snapshot`, then delete the `PD-###` line the tool wrote. Nothing at `plan` ever warned that advancing the lifecycle freezes the upstream authoring window.

`checklist` already models the correct behaviour. `rederiveChecklist` rewrites its own `## Source Snapshot` body from current sources on every re-run, touching no authored section. `plan` should own its snapshot the same way — but a plan is not a checklist. A checklist result is re-derivable from its sources; a plan *decision* is not. Silently re-baselining a plan against a changed spec would tell the operator their recorded decisions still hold when nobody has checked. So `plan` gets the ownership without the silence: it detects that its snapshot went stale, **blocks** with a `stalePlanSnapshot` error naming the changed sources, and writes nothing. The operator reviews, then runs `fsgg-sdd plan --accept-upstream`, which rewrites the `## Source Snapshot` digests and leaves every authored byte alone.

The review gate the injected `PD-###` line was clumsily trying to create is preserved. It simply moves to the stage that discovered the problem, becomes an error instead of a warning, and stops being written into the operator's prose.

## Clarifications

### Session 2026-07-08

- Q: When `plan` re-runs and the upstream digests have moved, what should a bare `fsgg-sdd plan` (no flag) do? → A: **Warn and block.** A bare re-run emits a blocking `stalePlanSnapshot` `DiagnosticError` naming the changed source paths, writes zero bytes, and exits 1. `plan --accept-upstream` is the explicit gesture that rewrites the `## Source Snapshot` digests. This preserves the human review gate that the injected stale `PD-###` line was approximating, while honoring the authored-vs-generated split absolutely. Silent self-heal (uniform with `checklist`) was rejected: a checklist result re-derives from its sources, a plan decision does not, so re-baselining a plan without an operator gesture would assert a review that never happened.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A backward spec edit no longer corrupts the plan (Priority: P1)

An operator plans work item `042`, then — while planning — discovers a genuine bug in `spec.md` and fixes it. They re-run `fsgg-sdd plan`. Today the command exits 0, injects a fabricated decision into their prose, and hides a failure that surfaces two stages later at `tasks`. They want the command to tell them, immediately and at the stage that knows, that the plan's upstream window has moved — and to leave their file exactly as they wrote it.

**Why this priority**: This is the contract-integrity defect. Everything else in this feature is the ergonomics of recovering from it. Shipping only this story already stops `fsgg-sdd` from writing into a file it declares authored, and already moves the failure from `tasks` to `plan`.

**Independent Test**: Create a work item through `plan`, mutate `spec.md`, re-run `fsgg-sdd plan`, and assert that `plan.md` is byte-identical to its pre-run content, that the report carries a `stalePlanSnapshot` `DiagnosticError` naming `work/042/spec.md`, that `changedArtifacts` is `0`, and that the exit code is `1`.

**Acceptance Scenarios**:

1. **Given** a work item whose `plan.md` records a `## Source Snapshot` over `spec.md`/`clarifications.md`/`checklist.md`, **When** `spec.md` is edited and `fsgg-sdd plan` re-runs, **Then** the command emits a blocking `stalePlanSnapshot` diagnostic whose related identifiers are exactly the sources whose digests moved, writes no bytes to `plan.md`, reports `changedArtifacts: 0`, and exits `1`.
2. **Given** the same stale state, **When** `fsgg-sdd plan` re-runs, **Then** no `PD-###` line is appended to `## Plan Decisions` and the `## Source Snapshot` digests are left untouched — the file is unchanged in full.
3. **Given** a work item whose upstream sources have **not** changed, **When** `fsgg-sdd plan` re-runs, **Then** behaviour is unchanged from today: no `stalePlanSnapshot` diagnostic, and the outcome is `noChange`.
4. **Given** a work item with no `plan.md` yet, **When** `fsgg-sdd plan` runs for the first time, **Then** the plan is created with a fresh `## Source Snapshot` exactly as today — creation is not a stale path.

---

### User Story 2 - One gesture accepts the new upstream (Priority: P1)

Having reviewed the spec change and confirmed their recorded plan decisions still hold, the operator wants a single command that re-baselines the plan against the current sources — without hand-editing three sha256 digests and without disturbing a word of their prose.

**Why this priority**: Story 1 without Story 2 replaces a corrupting recovery with a *blocked* one — the operator would still hand-edit digests, only now to escape an error instead of a warning. The pair is the shippable unit; they are split because they are independently testable, not because either ships alone happily.

**Independent Test**: From the blocked state of Story 1, run `fsgg-sdd plan --accept-upstream` and assert exit `0`, that the three `## Source Snapshot` digests now match the current source contents, and that every other section of `plan.md` is byte-identical to its pre-run content.

**Acceptance Scenarios**:

1. **Given** a plan blocked by `stalePlanSnapshot`, **When** `fsgg-sdd plan --accept-upstream` runs, **Then** the `## Source Snapshot` section body is rewritten to the current source digests, `changedArtifacts` is `1`, and the command exits `0`.
2. **Given** that same run, **When** the resulting `plan.md` is compared to its pre-run content, **Then** the `## Source Snapshot` body carries the current digests, no `PD-###` line contains a tool-synthesized `stale:` marker, and no *existing* line in any authored section has been altered or removed. (New derived rows for genuinely-new upstream ids may be appended — see FR-004.)
3. **Given** a plan whose snapshot is **already** current, **When** `fsgg-sdd plan --accept-upstream` runs, **Then** it is a no-op: outcome `noChange`, `changedArtifacts: 0`, exit `0`, and no diagnostic is emitted about the flag being unnecessary.
4. **Given** a plan that does not exist yet, **When** `fsgg-sdd plan --accept-upstream` runs, **Then** the plan is created exactly as a bare `fsgg-sdd plan` would create it — the flag is inert on the creation path.
5. **Given** a plan blocked by `stalePlanSnapshot` **and** carrying an unrelated blocking diagnostic (a malformed front matter, an unknown source reference), **When** `fsgg-sdd plan --accept-upstream` runs, **Then** the unrelated diagnostic still blocks, no snapshot is rewritten, and no bytes are written — `--accept-upstream` accepts the upstream, it does not force a write.

---

### User Story 3 - Downstream stages stop inheriting a stale plan (Priority: P2)

`tasks` and `analyze` read `plan.md` as a prerequisite. Once `plan` stops injecting the `stale:` marker that `tasks` was keying on, nothing downstream would notice a stale snapshot — an operator who edits `spec.md` and skips straight to `tasks` would generate a task graph against a plan that no longer matches its sources. The downstream stages must detect the staleness themselves, from the digests, and block on it.

**Why this priority**: It closes the hole that Stories 1–2 would otherwise open. It is P2 only because the hole requires the operator to skip the `plan` re-run entirely; Stories 1–2 are correct on their own for anyone who re-runs `plan`.

**Independent Test**: From a state where `plan.md` exists with a current snapshot, mutate `spec.md`, run `fsgg-sdd tasks` (never re-running `plan`), and assert it blocks on `stalePlanSnapshot` naming `work/042/spec.md`, with a next action pointing at `fsgg-sdd plan --accept-upstream`.

**Acceptance Scenarios**:

1. **Given** a plan whose recorded snapshot no longer matches the current sources, **When** `fsgg-sdd tasks` runs, **Then** it blocks with a `stalePlanSnapshot` `DiagnosticError` naming the changed sources and pointing the operator at `fsgg-sdd plan --accept-upstream`.
2. **Given** that same state, **When** `fsgg-sdd analyze` runs, **Then** it blocks identically.
3. **Given** a `plan.md` that an operator has *hand-authored* with a literal `stale:` decision line, **When** `fsgg-sdd tasks` runs, **Then** the pre-existing `failedPlanPrerequisite: Plan contains stale decisions.` diagnostic still blocks — the safety net for authored staleness is retained even though the tool no longer writes such lines.

---

### User Story 4 - The authoring window is announced before it closes (Priority: P3)

Nothing today tells an operator that running `plan` freezes the spec/clarify/checklist authoring window, and that later upstream edits will require an explicit re-baseline. They discover it by tripping over it.

**Why this priority**: Purely preventive. The other stories make the failure safe, legible, and cheap to recover from; this one makes it less likely to be reached. It is the smallest slice and the last one that should block a release.

**Independent Test**: Run `fsgg-sdd plan` successfully on a fresh work item and assert the report carries a non-blocking advisory naming the three snapshotted sources and the `--accept-upstream` recovery, and that the advisory changes neither the exit code nor `changedArtifacts`.

**Acceptance Scenarios**:

1. **Given** a successful `fsgg-sdd plan` (creation or a current re-run), **When** the report is projected, **Then** it carries a single non-blocking advisory stating that the plan has snapshotted `spec.md`, `clarifications.md`, and `checklist.md`, and that later edits to those sources require `fsgg-sdd plan --accept-upstream`.
2. **Given** that advisory, **When** the exit code and `changedArtifacts` are compared against the pre-feature behaviour for the same inputs, **Then** they are identical — the advisory adds a fact, not an outcome.

---

### Edge Cases

- **A recorded snapshot entry has no digest** (`digest: null` or absent). `sourceDigestsStale` treats an absent recorded digest as not-stale today. That behaviour is preserved: a missing digest is not evidence of change, and this feature must not turn an old plan into a blocked plan on upgrade.
- **A recorded snapshot names a path that no longer exists.** The source is missing, which the existing `missing…Prerequisite` diagnostics already report; `stalePlanSnapshot` must not also fire, and must not mask the missing-source error.
- **The snapshot records a path the current run does not compute a digest for**, or vice versa (a snapshot section hand-edited to add a fourth source). The comparison is over the recorded entries; an unrecognized recorded path with a digest that cannot be matched to a current source is not reported as stale by this feature.
- **Two sources change at once.** The diagnostic names all changed sources, sorted deterministically — not just the first one found.
- **`--accept-upstream` on a plan that cannot be parsed.** The malformed-front-matter error blocks first; nothing is rewritten.
- **`--accept-upstream` passed to a command other than `plan`.** The flag is `plan`-only and inert elsewhere. `fsgg-sdd` has no unknown-flag rejection today; adding one is out of scope (see FR-012).
- **A plan whose `## Source Snapshot` section is missing entirely.** `ensurePlanSections` inserts the heading; an empty snapshot has no recorded entries, therefore nothing is stale, therefore a bare `plan` does not block. `--accept-upstream` populates it.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `fsgg-sdd plan` MUST NEVER write a tool-synthesized line into any authored section of `work/<id>/plan.md`. The existing injection of a `PD-###` "stale: Source specification, clarification, or checklist facts changed…" line into `## Plan Decisions` MUST be removed.
- **FR-002**: On a re-run where any digest recorded in `plan.md`'s `## Source Snapshot` differs from the current content of the source it names, `fsgg-sdd plan` MUST emit a blocking `stalePlanSnapshot` `DiagnosticError` whose related identifiers are exactly the changed source paths, in deterministic sorted order.
- **FR-003**: On that blocking path `fsgg-sdd plan` MUST write zero bytes to `plan.md`, report `changedArtifacts: 0`, and exit `1`.
- **FR-004**: `fsgg-sdd plan --accept-upstream` MUST rewrite the body of the `## Source Snapshot` section from the current sources. It MUST NOT synthesize any `PD-###` line. It MAY append the derived entries `plan` already appends for genuinely-new upstream ids (`plannedPlanEntries`/`appendPlanEntries`, which diff against the existing plan facts) — that is pre-existing, intended behavior and not part of this defect. It MUST leave the front matter and all authored prose otherwise untouched.
- **FR-005**: `fsgg-sdd plan --accept-upstream` MUST be a no-op (`noChange`, `changedArtifacts: 0`, exit `0`, no flag-related diagnostic) when the recorded snapshot already matches the current sources.
- **FR-006**: `fsgg-sdd plan --accept-upstream` MUST NOT suppress any diagnostic other than `stalePlanSnapshot`; a plan carrying an independent blocking diagnostic MUST still block, and MUST NOT have its snapshot rewritten.
- **FR-007**: The plan **creation** path (no `plan.md` present) MUST be unchanged, and MUST behave identically with and without `--accept-upstream`.
- **FR-008**: `fsgg-sdd tasks` and `fsgg-sdd analyze` MUST detect a stale `plan.md` snapshot from the digests and block with the same `stalePlanSnapshot` `DiagnosticError`, independent of any `stale:` marker in the plan's prose.
- **FR-009**: The pre-existing `failedPlanPrerequisite: "Plan contains stale decisions."` diagnostic MUST be retained for plans whose `## Plan Decisions` contain an operator-authored `stale:` line.
- **FR-010**: Every `stalePlanSnapshot` diagnostic MUST carry a remediation pointing at `fsgg-sdd plan --accept-upstream`, surfaced through the existing next-action routing.
- **FR-011**: A successful `fsgg-sdd plan` MUST emit exactly one non-blocking advisory stating which sources it has snapshotted and that later edits to them require `fsgg-sdd plan --accept-upstream`. The advisory MUST NOT change the exit code, the outcome, or `changedArtifacts`.
- **FR-012**: `--accept-upstream` MUST be listed in `fsgg-sdd plan`'s help output. It is read only by `plan`; on every other command it is inert, exactly as `--update`, `--from-tests`, and `--force` are inert outside the commands that read them. (`fsgg-sdd` has no unknown-flag rejection layer — `hasFlag`/`optionValue` scan the argument list per command — and introducing one is a Tier 1 change across every command, out of scope here.)
- **FR-013**: All three report projections (`--json` default, `--text`, `--rich`) MUST carry the new diagnostic and advisory. The JSON projection remains the contract; text and rich add and drop no facts.
- **FR-014**: Output MUST remain byte-deterministic across runs for identical inputs, on the blocking path and the `--accept-upstream` path alike.
- **FR-015**: No versioned cross-repo contract changes. `plan.md` remains `AuthoredSource`; no persisted schema version is bumped; `registry/dependencies.yml` is untouched.
- **FR-016**: A recorded snapshot entry with an absent digest MUST NOT be treated as stale, preserving today's `sourceDigestsStale` semantics so that pre-existing plans do not become blocked on upgrade.

### Key Entities

- **Source Snapshot**: the `## Source Snapshot` section of `plan.md`, a list of `(path, sha256 digest)` entries over `spec.md`, `clarifications.md`, and `checklist.md`. It is the plan's record of *which upstream content it was planned against*. This feature makes the plan its sole owner: only `plan` writes it, and once recorded only `--accept-upstream` changes it.
- **Plan Decision (`PD-###`)**: an operator-authored line in `## Plan Decisions`. After this feature, no `PD-###` line is ever tool-authored. Its `stale:` marker becomes purely an authored signal.
- **`stalePlanSnapshot` diagnostic**: a new blocking `DiagnosticError`, emitted by `plan`, `tasks`, and `analyze`, naming the source paths whose digests moved and pointing at `fsgg-sdd plan --accept-upstream`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Recovering from a backward upstream edit requires **zero** hand-edits to `plan.md` — down from three hand-edited sha256 digests plus one deleted `PD-###` line — and completes in exactly one command, `fsgg-sdd plan --accept-upstream`.
- **SC-002**: Across the full lifecycle test matrix, `fsgg-sdd` writes **zero** tool-synthesized `stale:` decision lines into `work/<id>/plan.md`. A byte-level test asserts that (a) on the blocked path `plan.md` is byte-identical, and (b) under `--accept-upstream` no pre-existing line in any authored section is altered or removed — only the `## Source Snapshot` body is rewritten, plus any append of derived rows for new upstream ids that `plan` already performs.
- **SC-003**: A stale upstream is reported at `plan` — the stage that owns the snapshot — rather than at `tasks`, two stages later. Measured as: the first command to emit a blocking diagnostic after an upstream edit is `plan`, in 100% of the matrix's stale-state cells.
- **SC-004**: No stale plan reaches task generation: for every stale-state cell, `tasks` and `analyze` block with `stalePlanSnapshot`, and `changedArtifacts` is `0`.
- **SC-005**: Both the blocking path and the `--accept-upstream` path are byte-identical across repeated runs on identical inputs (existing determinism harness), and `fsgg-sdd validate` reports no new non-passing cell.

## Assumptions

- The three snapshotted sources remain exactly `spec.md`, `clarifications.md`, and `checklist.md`; this feature does not add, remove, or generalize the snapshot's membership.
- `checklist`'s existing self-heal (`rederiveChecklist` rewriting its own `## Source Snapshot` every re-run) is correct for a checklist and stays as-is. This feature deliberately does **not** unify `plan` onto that behaviour; the divergence is the clarified decision, and the shared rule is narrower: *a stage owns its own snapshot and never writes another file's authored prose.*
- Detecting staleness inside `planPrerequisiteDiagnosticsTextSummaryAndFacts` can read the upstream source texts from the interpreted-effect `model` directly, as it already reads `plan.md` itself, so no prerequisite-cascade signature changes and `Prerequisites.fs` is untouched.
- `runHandler` (`Prerequisites.fs:139`) already discards **every** write effect when any diagnostic has severity `DiagnosticError`. Raising the tool-detected staleness from today's `stalePlanDecision` *warning* to a `stalePlanSnapshot` *error* therefore delivers FR-003's zero-byte guarantee through the existing effect gate, with no change to `HandlersEarly.fs` and no new suppression logic. This is also the mechanism of the reported bug: because `stalePlanDecision` is only a warning, the mutated plan text survives the gate and is written.
- Exit-code conventions are the established ones: user-input/state failures resolve at exit `1`. A stale snapshot is a state failure, not a defect, so it is exit `1`, not `2`.
- `stalePlanDecision` (today a warning) is superseded by `stalePlanSnapshot` (an error) for the tool-detected case. Whether the warning constructor is deleted or retained for the authored-`stale:` case is a plan-stage decision, not a spec-level one.
