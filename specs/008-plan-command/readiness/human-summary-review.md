# Human Summary Review

Reviewed on 2026-06-19 for `fsgg-sdd plan --text`.

The text projection is rendered from `CommandReport` fields only and exposes:

- command, outcome, changed artifact count, generated view count, diagnostics,
  and next action;
- plan decision count, contract reference count, verification obligation count,
  accepted deferral count, stale decision count, blocking finding count, and
  advisory count.

Verification: `plan-output-tests.txt` and `cli-plan-text-smoke.txt`.
