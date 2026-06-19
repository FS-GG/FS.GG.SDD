# SDD/Governance Boundary Review

Review date: 2026-06-20

Result:

- `fsgg-sdd analyze` runs without `.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, or `.fsgg/tooling.yml`.
- Governance compatibility facts are reported as advisory `notEvaluated`
  facts.
- The analysis report excludes Governance-owned route, profile, freshness,
  gate, audit, protected-boundary, evidence-freshness, and release-verdict
  fields.
- Analysis diagnostics stay inside the SDD lifecycle contract: missing or
  failed prerequisites, malformed analysis view, analysis identity mismatch,
  missing dispositions, task dependency/state/evidence findings, stale source
  links, and generated-view refresh findings.

Evidence:

- `output-boundary-tests.txt`
- `cli-json-smoke.txt`
- `cli-text-smoke.txt`
