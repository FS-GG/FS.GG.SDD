# SDD/Governance Boundary Review

Reviewed on 2026-06-19 for `fsgg-sdd plan`.

- `Plan` emits the same optional `.fsgg/policy.yml`, `.fsgg/capabilities.yml`,
  and `.fsgg/tooling.yml` compatibility facts as earlier lifecycle commands.
- `Plan` does not parse Governance schemas, select routes, evaluate freshness,
  adjust profiles, select gates, enforce protected boundaries, or produce
  release verdicts.
- Verification: `plan-output-tests.txt`, `command-plan-tests.txt`, and
  `cli-plan-json-smoke.txt`.
