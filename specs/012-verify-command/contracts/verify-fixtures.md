# Contract: Verify Fixtures

Fixtures live under `tests/fixtures/lifecycle-commands/`. Each fixture is a
project tree advanced through the evidence stage with one selected work item.
Valid fixtures assert a generated verification view or proposed dry-run change,
a successful or warning outcome, and the correct next action. Blocked fixtures
assert unchanged authored content and at least one actionable diagnostic.

## Valid Fixture Families

| Fixture | Asserts |
|---|---|
| `verify-create` | Generates `readiness/<id>/verify.json` for an evidence-ready work item; outcome succeeds; next action `verify.next.ship`. |
| `verify-rerun-current` | Second run over unchanged sources is `noChange` or current; view and counts are stable. |
| `verify-preserves-authored` | Authored spec/clarifications/checklist/plan/tasks/evidence remain byte-identical. |
| `verify-refreshes-work-model` | Refreshes `readiness/<id>/work-model.json` when verification facts make the model refresh valid. |
| `verify-refreshes-analysis` | Treats a current analysis as the prerequisite gate and reports analysis currency without regenerating it. |
| `verify-accepted-deferral` | Accepted deferrals visible to later stages do not block verification readiness. |
| `dry-run` | Reports proposed generated changes while mutating zero files. |
| `deterministic-report` | Three identical runs produce byte-identical JSON and proposed payloads. |
| `text-projection` | Text output contains only facts present in the JSON report. |
| `governance-boundary` | With Governance files absent, verification still succeeds and reports no freshness/route/profile/gate/audit/release verdict. |

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
| `failed-analysis` | Analysis not implementation-ready blocks verification readiness. |
| `failed-tasks` | Failed task graph validation blocks verification readiness. |
| `malformed-work-id` | Malformed selected work id blocks with a diagnostic. |
| `malformed-verify-view` | Malformed existing `verify.json` blocks safe refresh. |
| `duplicate-work-id` | Duplicate logical work ids block readiness. |
| `unknown-source-reference` | Evidence or task reference to an unknown source blocks readiness. |
| `dependency-cycle` | Task dependency cycle blocks readiness. |
| `unsupported-task-status` | Unsupported task status transition blocks readiness. |
| `missing-required-skill` | Task requires a skill or capability tag not visible in lifecycle artifacts. |
| `missing-required-test` | A derived required test obligation is missing or unsatisfied. |
| `missing-required-evidence` | A completed task lacks required evidence or an accepted deferral. |
| `stale-analysis` | Analysis source digests no longer match current sources. |
| `stale-tasks` | Task source snapshots no longer match current sources. |
| `stale-evidence` | Evidence source snapshots no longer match current sources. |
| `undisclosed-synthetic-evidence` | Synthetic evidence without disclosure blocks readiness. |
| `invalid-deferral` | Deferral without rationale, owner, scope, or later visibility blocks readiness. |
| `stale-generated-view` | A stale work-model, analysis, or verification view is diagnosed rather than treated as current. |

## Fixture Invariants

- Each fixture pins project-relative paths only; no absolute host paths appear in
  expected output.
- Expected JSON payloads are stored as golden files and compared byte-for-byte.
- Blocked fixtures assert that authored lifecycle files are unchanged after the
  run.
- Valid fixtures that write generated views assert source digests and generator
  identity are recorded so staleness stays detectable.
