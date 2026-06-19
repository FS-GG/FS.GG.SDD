# Performance Evidence

Review date: 2026-06-20

Scope:

- `analysis-create`
- `analysis-rerun-current`

Evidence:

- `AnalyzeCommandTests.analyze create and rerun complete under local harness
  budget` asserts both analysis creation and rerun complete under the
  two-second local harness budget.
- `command-analyze-tests.txt` passed 12 analyze-focused command tests,
  including the create/rerun performance assertion and CLI smoke tests.
- `full-suite.txt` passed the full Release suite with 203 total tests.

Result:

- Create path: within the two-second test budget.
- Rerun path: within the two-second test budget.
- No host-specific timing data is serialized into the command JSON contract.
