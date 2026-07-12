# Feature Specification: An Unauthored Decision Is a Missing Decision

**Feature Branch**: `item/351-unauthored-placeholders`

**Created**: 2026-07-11

**Status**: Draft

**Input**: FS.GG.SDD#351 — "Scaffold placeholders satisfy the gates by construction — a decision-shaped
hole with an id passes every stage." Child of `.github` epic #417 (*the SDD lifecycle fails open*).
Origin: the TankSim1 field report, §3.2/§3.4.

## Overview

`plan` auto-generates one plan decision per requirement:

> `- PD-001 [AC-001] [FR-001] complete: Plan requirement FR-001 through the plan command contract.`

That is not a decision. **It is a decision-shaped hole with an id.** The same is true of the
companion contract (`PC`), verification obligation (`VO`), migration note (`PM`), generated-view
impact (`GV`), and deferral rows.

And nothing ever required an author to replace any of it. There was no `TODO`, no `unauthored`
marker, no placeholder diagnostic anywhere in `src/` — verified by grep across
`DiagnosticConstructors.fs`, `Diagnostics.fs`, and `LintEngine.fs`.

**Worse: the scaffold is built to satisfy the gates.** Each scaffolded `PD-###` carries its
`[FR-###] [AC-###]` refs *by construction*, so the FR→plan→task→evidence traceability chain closes
with **zero human authorship**. The gates verify that the ids line up; the scaffold generates ids
that line up. The check and the thing being checked have the same author.

Demonstrated end-to-end by the field report: `charter → specify → clarify → checklist → plan → tasks
→ analyze` on pure tool-generated boilerplate went green at every stage, `analysisBlocking: 0`.

The code already conceded the problem. `EarlyStageAuthoring.fs:247-255` admits the earlier seeds
"read as boilerplate to delete" — and the response was to make them *better disguised*, not to gate
them. **A plausible-looking filling is worse than an empty one, because it does not itch.**

### The detector is the generator

`plannedPlanEntries` is pure in `(workId, specFacts, clarificationFacts, checklistFacts, None)`, and
`analyze` already holds all four. So the gate **re-derives exactly what `plan` would have written**
and asks whether the authored plan still contains it, verbatim.

This is why there is no marker, no schema field, and nothing for an author to forget to delete: the
reference is the scaffold's own output. The rule cannot drift from the thing it is detecting, because
they are the same function. (It is also an idiom this codebase already uses — the shipped example's
"fixpoint of its generators" test re-derives and compares.)

It is **conservative by construction**: a line the author has touched at all no longer matches. The
gate fires only on prose the tool wrote and the human never read.

### Scope: the plan, not (yet) the spec or charter

`PlanFacts` already carries the parsed prose (`PlanDecision.Text`); `SpecificationFacts` carries only
ids, so gating the spec's seeds needs an additive `Text` field in `Artifacts` (and the `.fsi` +
baseline churn that implies). The plan gate **alone satisfies the acceptance criterion** — a plan is
always 100% scaffold on an untouched lifecycle — so the spec/charter seeds are a separate slice, filed
as a follow-up rather than smuggled in here.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Boilerplate cannot ship (Priority: P1)

An author (or an agent optimising for a green stage) runs the lifecycle and never writes a decision.

**Acceptance**

- `analyze` over a plan still holding the scaffold's prose emits a **blocking**
  `unauthoredScaffoldContent` diagnostic **naming every unauthored entry** (`PD-001`, `PC-001`,
  `VO-001`, `PM-001`, `GV-001`, and any deferral rows).
- No `readiness/<id>/analysis.json` is written (H-4).
- Therefore `evidence` refuses on its missing prerequisite, and `verify`/`ship` never produce a
  verdict: **a lifecycle on untouched scaffold cannot reach `shipReady`.**

### User Story 2 - The green path is untouched (Priority: P1)

**Acceptance**

- Replacing the prose — keeping the id, the refs, and the kind token — clears the block:
  `implementationReady`, `analysisBlocking: 0`.
- A **partially** authored plan blocks on exactly the entries not yet reached, and never on one the
  author has touched.

### User Story 3 - The corpus we publish is authored (Priority: P2)

`docs/examples/lifecycle-artifacts/plan.md` shipped **verbatim scaffold prose** in its `PC`/`VO`/`PM`/
`GV` and deferral rows — and `RemediationPointers` points every blocked author at that very example.

**Acceptance**

- The shipped example holds no scaffold prose, and still passes `analyze → evidence → verify → ship`.
- Its task graph is unchanged (only the prose moves), so it remains a fixpoint of its generators.

### Edge Cases

- **The author's prose contains an id** (`AMB-001`, `DEC-002`): the plan parser harvests ids from
  decision *text*, so naming one adds a `sourceId` and can mint a task. Real behaviour, not a bug —
  but it means the shipped example's authored prose deliberately names those decisions **in words**,
  to keep its graph identical. Recorded here because it surprised the author of this feature.
- **Re-planning re-scaffolds**: `plan --accept-upstream` after a clarify edit writes fresh scaffold
  rows, which correctly block again. Authoring is a step in the loop, not a one-off.
- **Deferral rows key on the source id** (`- DEC-002 acceptedDeferral: …`), not a `PD-###`.

## Requirements *(mandatory)*

- **FR-001**: `analyze` MUST emit a blocking `unauthoredScaffoldContent` diagnostic, naming each
  entry, when the authored plan still contains a line byte-identical to what `plan` would scaffold.
- **FR-002**: The rule MUST key on the **prose** only. The id, the refs, and the kind token are the
  machine contract and MUST NOT be part of the comparison beyond locating the line.
- **FR-003**: A blocked `analyze` MUST write no `analysis.json`, so the lifecycle cannot reach
  `shipReady` (satisfied at one point rather than re-litigated per stage).
- **FR-004**: The expected text MUST be **re-derived from the generator** (`plannedPlanEntries`), not
  duplicated as literals, so the gate cannot drift from the scaffold.
- **FR-005**: The published example corpus MUST contain no scaffold prose.
- **FR-006**: A committed **failure-leg** test MUST assert the refusal on the diagnostic id.

## Success Criteria *(mandatory)*

- **SC-001**: The field report's walk — a full lifecycle on untouched boilerplate — is refused at
  `analyze` and cannot reach ship. Today it is green at every stage.
- **SC-002**: `fsgg-sdd validate` coverage is **unchanged** (`passed=286 skipped=143`, identical to
  the pre-change baseline). The harness must not silently degrade to skipped cells.
- **SC-003**: The shipped example carries zero scaffold prose and stays a fixpoint of its generators.

## Out of Scope

- The `specify` / `charter` seeds (needs a `Text` field on `SpecificationFacts`) — follow-up.
- Collapsing the derived tiers (13 near-identical `PD`/`T`/`EV` per `FR`) — that is #350's fix 3, and
  the ceremony-ratio argument belongs there.
- `result: pass` remains a self-attestation (#350).
