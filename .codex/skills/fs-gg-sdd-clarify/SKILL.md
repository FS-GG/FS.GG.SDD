---
name: fs-gg-sdd-clarify
description: Stage 3 of the FS.GG SDD lifecycle — fsgg-sdd clarify records clarification questions, answers, and decisions (CQ/DEC/AMB ids) in work/<id>/clarifications.md, resolving spec ambiguities before checklist and plan. Use after specify.
---

# Clarify (stage 3)

`clarify` resolves the ambiguities the spec surfaced — turning open questions into
recorded **decisions** before they harden into plan and tasks. Skipping it leaves
ambiguity to be discovered (expensively) during implementation.

## Command

```text
fsgg-sdd clarify --work <id>
```

## Produces / consumes

- **Consumes:** `work/<id>/spec.md`.
- **You author:** `work/<id>/clarifications.md`.
- **Tool refreshes:** `readiness/<id>/work-model.json`.
- **Next:** `checklist` ([[fs-gg-sdd-checklist]]).

## Required headings and id prefixes

Sections: **Source Specification, Clarification Questions, Answers, Decisions,
Accepted Deferrals, Remaining Ambiguity, Lifecycle Notes.**

Id prefixes: `CQ-###` (clarification questions), `DEC-###` (decisions), `AMB-###`
(ambiguities, carried from the spec).

## Example

```markdown
# Clarifications

## Source Specification
work/001-two-player-volley/spec.md

## Clarification Questions
- **CQ-001**: Does a serve after a point go to the loser or alternate?
- **CQ-002**: Is there a win condition, or is volley endless?

## Answers
- CQ-001 → serve goes to the player who lost the prior rally (resolves AMB-001).
- CQ-002 → endless volley for this work item; scoring tracked but no match end.

## Decisions
- **DEC-001**: Serve targets the prior-rally loser. Drives FR-002 and AC-002.
- **DEC-002**: No match-end condition in scope; revisit in a later work item.

## Accepted Deferrals
- Win condition deferred (DEC-002) — recorded, not dropped.

## Remaining Ambiguity
- None blocking.
```

## Pitfalls

- Answering a question without recording a `DEC-###` — decisions are what later
  stages and the work model link to; an answer with no decision is not durable.
- Dropping an ambiguity instead of **deferring** it. Record the deferral; do not
  silently delete the open question.
- **`## Remaining Ambiguity` empty-section rule.** A bullet under this heading that
  names an `AMB-###`/`CQ-###` id is read as **still unresolved** and blocks
  `checklist`/`plan`. To say none remain, write a disclaimer bullet (`- None.`,
  `- No remaining ambiguities.`) — do **not** re-list the resolved ids
  (`- None. AMB-001…AMB-005 resolved.` is fine because it starts with `None`, but a
  bare `- AMB-001 resolved.` counts AMB-001 as blocking). Full grammar:
  `docs/reference/authoring-contracts.md`.

## Next

- `checklist` — verify requirements quality and coverage: [[fs-gg-sdd-checklist]].

## Related

- [[fs-gg-sdd-lifecycle]], [[fs-gg-sdd-specify]].

## Sources

- `docs/quickstart.md`; the seeded `.fsgg/early-stage-guidance.md`.
