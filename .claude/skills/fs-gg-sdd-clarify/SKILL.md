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

## Next

- `checklist` — verify requirements quality and coverage: [[fs-gg-sdd-checklist]].

## Related

- [[fs-gg-sdd-lifecycle]], [[fs-gg-sdd-specify]].

## Sources

- `docs/quickstart.md`; the seeded `.fsgg/early-stage-guidance.md`.
