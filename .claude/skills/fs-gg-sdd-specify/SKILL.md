---
name: fs-gg-sdd-specify
description: Stage 2 of the FS.GG SDD lifecycle — fsgg-sdd specify drafts work/<id>/spec.md from three labeled --input facts (value, scope, requirement) and establishes the stable FR/AC/US ids the rest of the lifecycle references. Use to write the specification.
---

# Specify (stage 2)

`specify` drafts the specification — the source of truth for the plan, tasks,
evidence, and ship audit. It establishes the **stable ids** (`FR-###`, `AC-###`,
`US-###`) that every later stage references, so getting the ids right here matters
more than anywhere else.

## Command

```text
fsgg-sdd specify --work <id> --input "<intent>"
```

## The `--input` intent grammar (gating)

`specify` drafts a spec **only** when `--input` supplies three labeled facts on
their own lines:

- `value:` — the user value (what the user can now do).
- `scope:` — the scope boundary.
- `requirement:` — a single measurable requirement.

An **unlabeled** line is read as the user value only; free prose therefore reports
`scope` and `measurable requirement` as **missing** and blocks. Supply all three:

```text
value: A player can rally against a second human at one keyboard.
scope: Two-player local volley; no AI opponent, no online play.
requirement: A rally of 20 consecutive volleys completes without a dropped frame.
```

## Produces / consumes

- **Consumes:** `work/<id>/charter.md`.
- **You author:** `work/<id>/spec.md`.
- **Tool refreshes:** `readiness/<id>/work-model.json`.
- **Next:** `clarify` ([[fs-gg-sdd-clarify]]).

## Required headings and id prefixes in `spec.md`

Sections: **User Value, Scope, Non-Goals, User Stories, Acceptance Scenarios,
Functional Requirements, Ambiguities, Public Or Tool-Facing Impact, Lifecycle
Notes.**

Id prefixes: `US-###` (user stories), `AC-###` (acceptance), `FR-###` (functional
requirements), `SB-###` (scope boundaries), `AMB-###` (ambiguities).

## Example: a functional-requirements block

Write requirements with MUST language and stable ids — these are the ids
`checklist`, `tasks`, and `evidence` will reference:

```markdown
### Functional Requirements

- **FR-001**: The system MUST move the left paddle up/down on W/S key events.
- **FR-002**: The system MUST serve the ball toward the player who lost the prior
  rally.

### Acceptance Scenarios

- **AC-001**: Given a running volley, when the left player holds W, the left paddle
  rises at a constant rate until release.
- **AC-002**: Given a point just scored by the right player, when the next serve
  begins, the ball travels toward the left player.
```

> The `FR-###` ids here are referenced by the **checklist coverage line** later
> (`- FR-001: … (covers AC-002)`). Keep the numbering stable from here on — see
> [[fs-gg-sdd-checklist]] and [[fs-gg-sdd-authoring-contracts]].

## Pitfalls

- Free-prose `--input` with no `value:`/`scope:`/`requirement:` labels → blocked
  with scope/requirement missing.
- Renumbering FRs after `checklist`/`tasks` exist — the cross-references go stale.
- Putting HOW (implementation) in the spec. Spec is WHAT and WHY; HOW is `plan`.

## Next

- `clarify` — resolve the ambiguities you listed: [[fs-gg-sdd-clarify]].

## Related

- [[fs-gg-sdd-lifecycle]], [[fs-gg-sdd-authoring-contracts]].

## Sources

- `docs/reference/authoring-contracts.md` (`specify --input` grammar);
  `docs/quickstart.md`; the seeded `.fsgg/early-stage-guidance.md`.
