# Clarify command readiness evidence

This directory records implementation evidence for `006-clarify-command`.

Expected evidence files:

- `clarify-create-tests.txt`: focused create, no-Governance, dry-run, rerun,
  diagnostics, generated-view, and deterministic clarify command tests.
- `clarify-rerun-tests.txt`: rerun preservation, safe section insertion,
  stable-id, accepted-deferral, and unsafe-decision-change tests.
- `clarify-diagnostics-tests.txt`: invalid-context, MVU boundary, and
  generated-view diagnostic tests.
- `generated-view-tests.txt`: focused generated-view missing, malformed, and
  refresh evidence.
- `clarify-traceability-tests.txt`: deterministic JSON, text projection,
  dry-run, and optional Governance-boundary tests.
- `build-release.txt`: Release solution build.
- `fsi-session.txt`: public FSI/prelude transcript exercising clarify through
  `CommandWorkflow.init` and `CommandWorkflow.update`.
- `cli-smoke.txt`: disposable-directory CLI smoke path through init, charter,
  specify, and clarify.
- `performance.txt`: local timing evidence for focused clarify create and
  rerun paths.
- `human-summary-review.txt`: text output review from the authoritative report.
- `sdd-governance-boundary-review.md`: SDD/Governance scope review.
- `full-suite.txt`: full solution test suite.
- `artifact-traceability.md`: spec, plan, tasks, fixtures, tests, and evidence
  traceability.
