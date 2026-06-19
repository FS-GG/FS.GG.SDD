# SDD/Governance Boundary Review

Reviewed after the `011-evidence-command` implementation.

- SDD owns `work/<id>/evidence.yml`, evidence declaration parsing, current
  obligation disposition summaries, deterministic report fields, and generated
  SDD work-model refresh.
- Governance remains optional. The evidence command does not read or require
  `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, or `.fsgg/tooling.yml`.
- Focused boundary coverage is captured in `output-boundary-tests.txt`; the
  explicit test `evidence does not require Governance files` asserts optional
  Governance compatibility facts remain `notEvaluated`.
- Evidence reports do not emit route, freshness, profile, gate, protected
  boundary, effective evidence, verify readiness, ship readiness, or release
  verdict fields.
- Effective evidence freshness, synthetic taint propagation, routing, profiles,
  and gate enforcement remain Governance-owned future work.
