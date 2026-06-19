# Performance Evidence

Measured through the focused evidence command tests in
`command-evidence-tests.txt` and CLI smoke transcripts.

- `evidence creates authored evidence artifact with real filesystem evidence`
  completed inside the xUnit run; the focused evidence suite passed 25 tests in
  5 seconds including three CLI process smoke tests.
- Create path: `cli-json-smoke.txt` creates `work/011-evidence-command/evidence.yml`
  and refreshes `readiness/011-evidence-command/work-model.json`.
- Dry-run path: `cli-dry-run-smoke.txt` reports `dryRunOnly` without mutating
  the authored evidence artifact.
- Rerun/current behavior is covered by deterministic dry-run and repeated
  report tests in `EvidenceCommandTests`; the JSON report is byte-stable across
  three identical requests.

No performance regression was observed under the local harness budget.
