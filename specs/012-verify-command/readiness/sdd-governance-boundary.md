# SDD / Governance Boundary Review — Verify Command

The verify command is SDD-owned and stays inside the FS.GG.SDD boundary:

- It generates `readiness/<id>/verify.json` and refreshes
  `readiness/<id>/work-model.json` only; it authors no lifecycle source.
- It reports optional Governance pointers (`.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, `.fsgg/tooling.yml`) as `notEvaluated` compatibility
  facts and never parses Governance-owned schemas.
- It produces **no** route, freshness, profile, gate, audit, protected-boundary,
  effective-evidence, or release verdict, and it does not implement ship.

Evidence:

- `verify does not require Governance files` asserts the serialized report
  contains no `route` and no `"ship"` token, and that Governance compatibility
  facts remain `notEvaluated`.
- `output-boundary-tests.txt` (32 passed) covers the
  `GovernanceBoundaryCommandTests`, `CommandReportJsonTests`,
  `TextProjectionTests`, and `GeneratedViewCommandTests` suites.
- The next action is `verify.next.ship` with `command = null`; no `Ship` command
  exists in this slice.
