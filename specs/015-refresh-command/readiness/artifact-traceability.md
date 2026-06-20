# Artifact Traceability — 015-refresh-command

Maps the feature's contracts to the implementation and the test/CLI/FSI evidence.
All tests pass in `full-suite.txt` (83 artifact + 223 command = 306 tests, 0
failures; up from the 281-test baseline).

## Public surface (Constitution I/III)

| Surface | Location | Evidence |
|---|---|---|
| `expectedSummaryOutputPath`, `createSummaryManifest` | `GenerationManifest.fsi/.fs` | `RefreshSummaryViewTests`, `fsi-public-surface.txt`, baseline |
| `SddCommand.Refresh`, `commandName/Stage/parseCommand/nextLifecycleCommand` | `CommandTypes.fsi/.fs` | prelude, `RefreshCommandTests`, baseline |
| `RefreshDisposition`, `refreshDispositionValue` | `CommandTypes.fsi/.fs` | prelude, baseline |
| `RefreshSummary`, `CommandReport.Refresh`, `CommandModel.Refresh` | `CommandTypes.fsi/.fs` | `CommandReportJsonTests`, baseline |
| refresh diagnostics (`refreshMissingSource` … `refreshUnrenderableSummary`) | `CommandReports.fsi/.fs` | prelude, `RefreshCommandTests` |
| refresh JSON serialization (`refresh` block) | `CommandSerialization.fs` | `refresh report exposes the refresh block` |
| refresh text projection | `CommandRendering.fs` | `refresh text projection surfaces refresh facts` |
| CLI dispatch (`refresh --work --json/--text --dry-run`) | `Cli/Program.fs` | `refresh CLI smoke/text/dry-run` (real binary) |

## Requirements coverage

| Requirement(s) | Behavior | Evidence |
|---|---|---|
| FR-001, FR-002, FR-015 | one initialized project + one valid work id; outside-project/malformed/duplicate id block | `refresh blocks views whose declared source is missing`; reused validation tests |
| FR-003, FR-004 | reuse existing generators (work model, agent guidance) from current declared sources; byte-identical output | `refresh produces a byte-identical report/summary across repeated runs` |
| FR-005, FR-006, SC-008 | `summary.md` rendered only from structured readiness data; no extra facts | `RefreshSummaryViewTests`; `refresh summary per-view table matches the report` |
| FR-007 | generated marker with sources, digests, generator | `sample-summary.md`; `refresh records generated views with sources and generator identity` |
| FR-008, FR-009, FR-010, FR-011 | per-view currency (current/stale/missing/malformed/blocked); blocked-upstream names the upstream; no fabrication from bad sources | `refresh names the upstream view when a dependent view is blocked`; `refresh refreshes a malformed existing generated view` |
| FR-012, FR-013 | authored sources / `.fsgg/*.yml` / `CLAUDE.md` / `AGENTS.md` preserved | `refresh preserves authored sources and hand-owned guidance files` |
| FR-014, FR-016 | disposition mapping (`refreshed-current`/`partially-blocked`/`blocked`) + next action | `RefreshCommandTests` disposition assertions |
| FR-018, SC-004 | determinism (no host-specific fields) | `refresh produces a byte-identical report/summary across repeated runs` |
| FR-019 | text projection adds no facts beyond JSON | `refresh text projection surfaces refresh facts`; `TextProjectionTests` |
| FR-021, SC-006 | dry-run reports proposed changes, mutates nothing | `refresh dry-run writes zero files but reports proposed changes`; `refresh CLI dry-run smoke` |
| FR-022–FR-024, SC-009 | runs without Governance; advisory pointers only | `refresh succeeds without governance installed`; `sdd-governance-boundary.md` |

## Deferred / deviated (disclosed)

- **Structured downstream regeneration (US1 literal "re-run analysis/verify/ship
  generators", tasks T028–T029 / the cascade in US1):** reduced to
  currency-reporting because re-running those generators out of lifecycle order
  corrupts the project via the pre-existing evidence-freshness coupling in
  `verify`/`ship` (reproduced standalone; see `sdd-governance-boundary.md` and the
  plan Implementation Notes). Work model, agent guidance, and summary ARE
  regenerated. This is recorded honestly in `tasks.md` rather than marked green.
- **Static fixture roots** under `tests/fixtures/lifecycle-commands/` (Phase 1
  T003–T005): per the `014-agent-guidance` precedent, refresh scenarios are
  covered by real-evidence `RefreshCommandTests` over disposable shipped project
  trees, which exercise the real CLI binary and real filesystem — stronger
  evidence than static golden fixtures. The static roots are deferred.
