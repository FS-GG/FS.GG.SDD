# SDD/Governance Boundary Review

Feature: `006-clarify-command`

Result: PASS

The implementation adds SDD-owned clarification behavior:

- `fsgg-sdd clarify` command routing through the existing command MVU boundary.
- `work/<id>/clarifications.md` authored Markdown with structured front matter.
- Stable `CQ-###` clarification question ids and durable `DEC-###` decisions or
  accepted deferrals.
- Clarification summary facts in deterministic command reports and text
  projection.
- Generated work-model currency reporting and refresh where existing source
  contracts are complete.
- Optional Governance compatibility facts reused from the existing command
  report surface.

The implementation does not add:

- checklist, plan, tasks, analyze, evidence update, verify, ship, generated
  agent guidance, release, or package publishing commands;
- Governance route selection;
- evidence freshness evaluation;
- profile adjustment;
- gate selection or enforcement;
- audit or protected-boundary verdicts;
- release policy evaluation.

Governance files remain optional compatibility pointers. Clarify succeeds when
`.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` are
absent.
