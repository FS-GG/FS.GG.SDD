# Contract: Plan Fixtures

## Fixture Root

Plan command fixtures live under:

```text
tests/fixtures/lifecycle-commands/
```

Each fixture uses the existing lifecycle-command manifest pattern and creates a
temporary project tree for command tests or CLI smoke validation.

## Valid Fixture Families

| Fixture | Purpose | Expected Outcome |
|---|---|---|
| `plan-create` | Creates a new plan for a checklist-ready work item. | `work/<id>/plan.md` created, plan summary emitted, next action `tasks`. |
| `plan-rerun-preserves-decisions` | Reruns with existing complete plan decisions. | Existing ids and authored content preserved, no unsafe changes. |
| `plan-adds-missing-entries` | Adds compatible new decisions, contract references, or obligations. | Safe update records added entries and preserves existing ids. |
| `plan-preserves-stable-ids` | Verifies id allocation across repeated reruns. | `PD-###`, `PC-###`, `VO-###`, `PM-###`, and `GV-###` remain stable. |
| `plan-accepted-deferral` | Carries accepted deferrals from clarification/checklist into plan. | Deferral remains visible and does not disappear from report or artifact. |
| `plan-stale-decision` | Source fact changes after a plan decision was recorded. | Affected plan decision is stale or needs review; blocked if stale state prevents planning. |
| `deterministic-report` | Runs same plan request repeatedly. | Three JSON reports are byte-identical. |
| `text-projection` | Renders human summary from report. | Text includes the same facts as JSON and no extra lifecycle facts. |
| `governance-boundary` | Optional Governance pointers present or absent. | Plan succeeds without Governance runtime and emits advisory compatibility facts only. |

## Blocked Fixture Families

| Fixture | Purpose | Expected Diagnostic |
|---|---|---|
| `outside-project` | Command runs outside initialized SDD project. | Outside-project diagnostic; no authored write. |
| `missing-specification` | Selected work item lacks spec prerequisite. | Missing specification prerequisite; no plan write. |
| `missing-clarification` | Selected work item lacks clarification prerequisite. | Missing clarification prerequisite; no plan write. |
| `missing-checklist` | Selected work item lacks checklist prerequisite. | Missing checklist prerequisite; no plan write. |
| `failed-checklist` | Checklist has failed blocking or stale results. | Failed checklist prerequisite; no plan write. |
| `malformed-work-id` | Selected work id is invalid. | Malformed work id diagnostic; no plan write. |
| `malformed-plan` | Existing plan front matter or sections cannot be parsed safely. | Malformed plan diagnostic; no unsafe write. |
| `duplicate-work-id` | Multiple source paths claim the selected logical id. | Duplicate work id diagnostic; no unsafe write. |
| `duplicate-plan-id` | Plan contains duplicate `PD`, `PC`, `VO`, `PM`, or `GV` ids. | Duplicate plan id diagnostic; no unsafe write. |
| `unknown-source-reference` | Plan references unknown requirement, decision, or checklist result. | Unknown source reference diagnostic; no unsafe write. |
| `plan-identity-mismatch` | Existing plan belongs to another work id. | Plan identity mismatch diagnostic; no unsafe write. |
| `unsafe-overwrite` | Proposed update would remove or semantically change existing plan decisions. | Unsafe overwrite or unsafe decision change diagnostic. |
| `stale-generated-view` | Generated work model cannot be proven current. | Stale, malformed, or blocked generated-view diagnostic. |

## Manifest Expectations

Each fixture manifest records:

- command arguments;
- expected exit code;
- expected output format;
- expected changed artifact paths;
- expected generated-view state;
- expected diagnostic ids;
- expected next action;
- mutation expectations for dry-run and blocked cases.

## Evidence Expectations

Implementation readiness evidence records:

- focused artifact parser tests for plan front matter, ids, source snapshots,
  decisions, contract references, obligations, migration notes, generated-view
  impacts, and diagnostics;
- focused command tests for valid and blocked fixtures;
- deterministic JSON report tests;
- text projection tests;
- generated-view refresh or diagnostic tests;
- no-Governance boundary tests;
- CLI smoke output from a disposable project;
- FSI or prelude transcript for the public command surface;
- full test suite output.
