# SDD/Governance Boundary Review

Review date: 2026-06-19

Result:

- `fsgg-sdd tasks` runs without `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, or `.fsgg/tooling.yml`.
- Governance compatibility facts are reported as advisory `notEvaluated` facts.
- The task report excludes Governance-owned route, profile, freshness, gate, audit, protected-boundary, evidence-freshness, and release-verdict fields.
- Task diagnostics stay inside the SDD lifecycle contract: missing/failed plan prerequisites, malformed task artifacts, duplicate task ids, unknown task references, dependency cycles, stale task links, unsafe task status changes, missing done evidence, and skipped-task rationale.

Evidence:

- `output-boundary-tests.txt`
- `cli-json-smoke.txt`
- `cli-text-smoke.txt`
