# Quickstart: validating feature 062

A behaviour-preserving refactor, so validation is dominated by "prove nothing
changed", plus focused checks on the three newly-typed behaviours.

## Prerequisites

- .NET SDK (`net10.0`), repo restored: `dotnet restore`.
- Baselines committed on `main` for before/after diffing.

## 1. Regression net — nothing observable changed (US2)

```bash
dotnet build -c Release
dotnet test                       # full suite: golden/determinism/projection + PublicSurface
```

Expected: **green with zero baseline edits** (FR-010). If any JSON/text golden or
`PublicSurface.baseline` diff appears, the refactor changed a contract — stop and
fix. See contracts A1–A5.

### Byte-diff spot check (optional, high-confidence)

Capture representative outputs before the change (from `main`) and after, per
command × projection, and diff:

```bash
for cmd in charter specify clarify checklist plan tasks analyze evidence verify ship agents refresh scaffold doctor upgrade validate; do
  fsgg-sdd $cmd --help >/dev/null 2>&1   # replace with representative real invocations/fixtures
done
```

Expected: `diff` of default/`--json` and `--text` streams is empty (FR-007/FR-008).

## 2. Typed defect bit → exit 2 without a registry (US1 / FR-001–003)

- A blocked command emitting a `markToolDefect` diagnostic exits **2**.
- A blocked command emitting only user-input diagnostics exits **1**.
- Each of the seven historically-escalating ids still exits **2**.
- **New-diagnostic test**: a defect-marked diagnostic whose id is absent from any
  list still exits 2 (there is no list) — the regression that motivated the feature.

Suggested home: `tests/FS.GG.SDD.Artifacts.Tests/DiagnosticTests.fs` (bit +
`markToolDefect`) and a Commands-level exit-code test over `exitCodeForReport`.

## 3. Staleness independent of id spelling (US3 / FR-004)

- `signalsStaleView` returns the same boolean as `Id.IndexOf("stale") >= 0` for the
  current stale ids.
- Agent-refresh over an embedded work-model containing a stale-signalling diagnostic
  still emits `agentsStaleWorkModel` — driven by `signalsStaleView`, not the
  substring.

## 4. Tuple → StagePlan equivalence (US4 / FR-005)

- For each lifecycle command, `nextLifecycleEffects` yields the same report/model as
  before. Covered transitively by the golden suite; add a direct
  `CommandWorkflowTests` case if a targeted assertion is cheap.

## 5. Split is cohesive and surface-stable (US5 / FR-006)

- Build is green with the new `CommandReports/` modules and the facade.
- `grep -rn "providerDefectIds" src/` → no matches (contract E).
- `grep -rn 'IndexOf("stale"' src/FS.GG.SDD.Commands/CommandWorkflow/HandlersAgents.fs`
  → no matches.
- `PublicSurface.baseline` diffs: Commands unchanged; Artifacts adds only
  `markToolDefect` + `signalsStaleView`.

## 6. Deep matrix (CI parity)

```bash
fsgg-sdd validate            # determinism, degradation, release baseline-conformance, handoff compat
```

Expected: same verdict as before the feature (SC-006).
