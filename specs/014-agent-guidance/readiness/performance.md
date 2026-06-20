# Agent Guidance Performance Evidence

Local development machine, Release build, `net10.0`.

## In-process command harness (xUnit, real filesystem)

The `AgentsCommandTests` suite (15 tests, including generate, rerun-current,
refresh-after-change, dry-run, divergence, malformed, and two CLI smokes)
completes in ~3 seconds total. Each individual in-process scenario
(`runAgents` over a real lifecycle project) resolves in well under the
two-second per-scenario budget.

## CLI host (`dotnet run`, includes process startup)

| Scenario | Wall clock (incl. host startup) |
|---|---|
| `agents-create` (fresh generate, both targets) | 0.85 s |
| `agents-rerun-current` (NoChange, no writes) | 0.89 s |
| `agents-refreshes-stale` (work model changed → refresh) | 0.88 s |

All three scenarios are under the 2-second budget even including .NET host
startup. The pure derivation + per-target rendering itself is sub-millisecond;
the measured time is dominated by process start.

Determinism: three identical CLI executions over the same project state produce
byte-identical report JSON and byte-identical per-target `guidance.json`
(verified in `CommandReportJsonTests` / `AgentsCommandTests` determinism tests).
