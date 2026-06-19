# Artifact Traceability

Feature: `007-checklist-command`

Spec coverage:

- FR-001 through FR-006: checklist command surface, artifact creation, stable
  checklist item/result ids, and source links are covered by
  `ChecklistCommandTests`, `ChecklistArtifactTests`, and public surface
  baselines.
- FR-007 through FR-014: requirements-quality checks, failed-quality output,
  stale review results, safe reruns, unsafe-write refusal, and next-action
  selection are covered by `checklist-create-tests.txt`,
  `checklist-rerun-tests.txt`, and `checklist-diagnostics-tests.txt`.
- FR-015 through FR-020: deterministic command reports, text projection,
  generated-view state, and diagnostics are covered by
  `checklist-traceability-tests.txt` and `checklist-diagnostics-tests.txt`.
- FR-021 through FR-023: no-Governance operation and non-responsibilities are
  covered by `checklist-traceability-tests.txt` and
  `sdd-governance-boundary-review.md`.

Plan/contract coverage:

- `contracts/checklist-artifact.md`: parser tests and command create/rerun tests.
- `contracts/checklist-command.md`: workflow/effect tests, CLI smoke, and FSI transcript.
- `contracts/checklist-report-json.md`: deterministic JSON and text projection tests.
- `data-model.md`: public command/artifact records and generated-view state tests.

Evidence files:

- Build: `build-release.txt`
- Focused tests: `checklist-artifact-tests.txt`, `checklist-create-tests.txt`,
  `checklist-rerun-tests.txt`, `checklist-diagnostics-tests.txt`,
  `checklist-traceability-tests.txt`
- Vertical slices: `fsi-session.txt`, `cli-smoke.txt`
- Full suite: `full-suite.txt`
