# Quickstart: Framework-aware required test skill

Runnable validation that the generated verification-obligation task carries a
framework-aware (or neutral) required test skill, never `xunit`. Maps to the
spec's three user stories and SC-001…SC-006.

## Prerequisites

- .NET SDK (`net10.0`) and a build of the `fsgg-sdd` CLI.
- An SDD-managed work item that reaches task generation (spec + plan facts
  present), as produced by the lifecycle inner loop.

Details of the field and resolution rule:
[contracts/project-config-test-framework.md](./contracts/project-config-test-framework.md),
[contracts/verification-obligation-skill.md](./contracts/verification-obligation-skill.md).
Field/skill shapes: [data-model.md](./data-model.md).

## Scenario 1 — Declared framework yields the matched skill (US1, SC-001/SC-003/SC-006)

1. In the product's `.fsgg/project.yml`, declare:
   ```yaml
   project:
     testFramework: expecto
   ```
2. Run task generation (`fsgg-sdd tasks <id>` / the generation command).
3. Inspect a verification-obligation task in `readiness/<id>/work-model.json`.

**Expected**: its `requiredSkills` is `["expecto", "readiness-evidence"]`; the
generated task metadata contains **zero** `xunit` tokens.

## Scenario 2 — No declaration yields the neutral skill (US2, SC-002)

1. Use a product whose `.fsgg/project.yml` has **no** `project.testFramework`
   (or a blank value).
2. Run task generation.

**Expected**: the verification-obligation task's `requiredSkills` is
`["automated-tests", "readiness-evidence"]`; no framework-specific token
(`xunit`, `expecto`, …) appears anywhere in the generated task metadata.

## Scenario 3 — Custom framework is trusted (edge case)

1. Declare `project.testFramework: My Custom Runner`.
2. Run task generation.

**Expected**: the verification-obligation task's `requiredSkills` is
`["my-custom-runner", "readiness-evidence"]` — SDD trusts and normalizes the
declared value without validating it.

## Scenario 4 — No regression / determinism (US3, SC-004/SC-005)

1. With identical inputs, run task generation **twice**.

   **Expected**: byte-identical task metadata across runs.

2. Compare the generated work model against prior behavior for the non-test
   categories.

   **Expected**: `requiredSkills` of requirement, plan-decision, contract,
   migration, generated-view, and deferral tasks are unchanged; the only diff is
   the verification-obligation test skill.

## Scenario 5 — Verify obligation re-keyed (US1 scenario 2, FR-008)

1. With `testFramework: expecto`, run `fsgg-sdd verify <id>` on a state where the
   verification-obligation task lacks supporting evidence.

   **Expected**: the `evidence.missingRequiredSkill` diagnostic lists the
   `expecto` skill (not `xunit`).

2. Supply evidence covering the verification-obligation task and re-run verify.

   **Expected**: the skill becomes visible and the diagnostic clears.

## Cross-projection check (FR-009)

Run any of the above with `--json` (default), `--text`, and `--rich`.

**Expected**: the same skill value is observable in all three; JSON bytes are
unchanged beyond the skill value; `--rich` degrades to plain text when
non-interactive / `NO_COLOR` / `TERM=dumb`.
