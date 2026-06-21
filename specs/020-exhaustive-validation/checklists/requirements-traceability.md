# Requirements Traceability Checklist: Scheduled Exhaustive Validation

Traceability of every spec requirement to a design artifact. Verified at plan time;
re-verified by `/speckit-analyze` after `/speckit-tasks`. (Companion to the
spec-quality checklist in [requirements.md](./requirements.md).)

| Req | Covered by |
|---|---|
| FR-001 declared enumerable matrices | data-model `Matrix`; contracts/matrix-runner §matrices |
| FR-002 lifecycle-output matrix | matrix-runner matrix 1; data-model lifecycle-output row |
| FR-003 determinism + degradation | matrix-runner matrix 2 / C-3 / C-3a / C-4; data-model INV-3 / INV-3a |
| Edge case: host-variance determinism (locale/TZ/cwd/ordering) | `PerturbedHostEnvironment` class; matrix-runner C-3a; data-model INV-3a; tasks T011/T016 |
| FR-004 baseline conformance | matrix-runner matrix 3 / C-5; data-model INV-4; research Decision 5 |
| FR-005 compatibility entry | matrix-runner matrix 4 / C-6 (handoff `contractVersion`; Spec Kit range = presence/parseable check); data-model INV-8; tasks T012/T017 |
| FR-006 single deterministic report + diagnostics | contracts/validation-report; data-model exit-code |
| FR-007 byte-stable, sensed fenced | validation-report C-1; data-model INV-2/INV-5 |
| FR-008 scheduled/on-demand, inner loop unchanged | cli-validate-command; matrix-runner §isolation |
| FR-009 skip ≠ gap ≠ not-validated | data-model `CellStatus`/INV-6; research Decision 7 |
| FR-010 no Governance runtime / verdict | cli-validate-command §no-Governance; data-model INV-8; research Decision 8 |
| FR-011 no new stage, no contract change | plan Change Tier + Structure Decision; research Decision 2 |
| FR-012 real surface authoritative | matrix-runner C-7 (independent per-dimension source: DU match / catalog / dir listing); data-model INV-7; tasks T022 |
| SC-001 100% command×projection×state coverage | data-model INV-1; quickstart S1 |
| SC-002 every view/--json determinism | matrix-runner matrix 2; quickstart S3 |
| SC-003 100% catalog baseline/conformance | matrix-runner C-5; quickstart S4 |
| SC-004 byte-identical double run | validation-report C-1; quickstart S6 |
| SC-005 coverage gap detected | matrix-runner C-2/C-7; quickstart S5 |
| SC-006 runs without Governance | cli-validate-command §no-Governance; quickstart S7 |
| SC-007 inner loop unchanged | matrix-runner §isolation; quickstart S7 |
| SC-008 compatibility as optional fact | matrix-runner C-6; quickstart S8 |

All requirements map to at least one design artifact; no orphan requirements.
