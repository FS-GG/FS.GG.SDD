# Contract: Ship Fixtures

Fixtures live under `tests/fixtures/lifecycle-commands/`. Each fixture is a
project tree advanced through the verify stage with one selected work item. Valid
fixtures assert a generated ship view or proposed dry-run change, a successful or
warning outcome, and the correct next action. Blocked fixtures assert unchanged
authored content (and an unchanged verification view) and at least one actionable
diagnostic.

Existing fixture roots reused from earlier slices (`outside-project`,
`missing-specification`, `missing-clarification`, `missing-checklist`,
`missing-plan`, `missing-tasks`, `missing-analysis`, `missing-evidence`,
`malformed-work-id`, `duplicate-work-id`, `unknown-source-reference`,
`stale-analysis`, `stale-evidence`, `dry-run`, `deterministic-report`,
`text-projection`, `governance-boundary`) are reused with ship-specific expected
output rather than duplicated.

## Valid Fixture Families

| Fixture | Asserts |
|---|---|
| `ship-create` | Generates `readiness/<id>/ship.json` for a verification-ready work item; outcome succeeds; next action `ship.next.protectedBoundary`. |
| `ship-rerun-current` | Second run over unchanged sources is `noChange` or current; view and counts are stable. |
| `ship-preserves-authored` | Authored spec/clarifications/checklist/plan/tasks/evidence and the verification view remain byte-identical. |
| `ship-refreshes-work-model` | Refreshes `readiness/<id>/work-model.json` when ship facts make the model refresh valid. |
| `ship-refreshes-verification` | Treats a current verification view as the prerequisite gate and reports verification currency without regenerating it. |
| `ship-accepted-deferral` | Accepted deferrals visible to later stages do not block ship readiness. |
| `dry-run` | Reports proposed generated changes while mutating zero files. |
| `deterministic-report` | Three identical runs produce byte-identical JSON and proposed payloads. |
| `text-projection` | Text output contains only facts present in the JSON report. |
| `governance-boundary` | With Governance files absent, ship still succeeds and reports no freshness/route/profile/gate/audit/protected-boundary/release verdict. |

## Blocked Fixture Families

| Fixture | Asserts |
|---|---|
| `outside-project` | Command run outside an initialized SDD project blocks with a diagnostic. |
| `missing-specification` | Missing spec prerequisite blocks; no view written. |
| `missing-clarification` | Missing clarifications prerequisite blocks; no view written. |
| `missing-checklist` | Missing checklist prerequisite blocks; no view written. |
| `missing-plan` | Missing plan prerequisite blocks; no view written. |
| `missing-tasks` | Missing tasks prerequisite blocks; no view written. |
| `missing-analysis` | Missing analysis prerequisite blocks; no view written. |
| `missing-evidence` | Missing evidence prerequisite blocks; no view written. |
| `missing-verification` | Missing `verify.json` prerequisite blocks; no view written. |
| `failed-verification` | Verification view reports unresolved blocking findings; ship blocks. |
| `not-verification-ready` | Verification view reports `needsVerificationCorrection`; ship blocks. |
| `malformed-work-id` | Malformed selected work id blocks with a diagnostic. |
| `malformed-ship-view` | Malformed existing `ship.json` blocks safe refresh. |
| `duplicate-work-id` | Duplicate logical work ids block readiness. |
| `unknown-source-reference` | Aggregated reference to an unknown source blocks readiness. |
| `stale-analysis` | Analysis source digests no longer match current sources. |
| `stale-evidence` | Evidence source snapshots no longer match the verification view snapshot. |
| `stale-verification` | Verification view source digests no longer match current sources. |
| `undisclosed-synthetic-evidence` | Synthetic evidence without disclosure (surfaced by the verification view) blocks readiness. |
| `invalid-deferral` | Deferral no longer visible or no longer accepted at ship time blocks readiness. |
| `stale-generated-view` | A stale work-model, analysis, verification, or ship view is diagnosed rather than treated as current. |

## Fixture Invariants

- Each fixture pins project-relative paths only; no absolute host paths appear in
  expected output.
- Expected JSON payloads are stored as golden files and compared byte-for-byte.
- Blocked fixtures assert that authored lifecycle files and the verification view
  are unchanged after the run.
- Valid fixtures that write generated views assert source digests and generator
  identity are recorded so staleness stays detectable.
