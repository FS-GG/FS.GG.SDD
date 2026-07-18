# Implementation Plan: Plan-Time Framework-API Reference Resolution

**Spec**: `specs/105-framework-api-reference-check/spec.md`

**Design of record**: repo-local **ADR-0004** (`docs/decisions/0004-*.md`).

**Tracks**: FS.GG.SDD#569.

**Tier**: **Tier 1** — new grammar on an authored artifact, a new CLI verb, a new
committed artifact schema (`dependency-surface` capture v1) with a CI drift-guard,
new blocking diagnostics in the `analyze` contract, and additive public functions/
types in `FS.GG.SDD.Artifacts` and `FS.GG.SDD.Commands`.

## Summary

Turn a framework-API citation from an un-checked backtick token into a structured,
resolvable reference, and resolve it against the pinned package's **authoritative
restored** surface. The restore is confined to a new `dependency-surface` verb that
writes a committed, provenance-stamped capture; `analyze` reads only that capture,
through an injected oracle, and blocks on real contradictions while advising when it
cannot look. This defeats both the dangling reference and the inverse false alarm
(the RM2 incident) at plan time.

## Technical Context

- F# / net10.0, Elmish-MVU command workflow: pure `update` + edge interpreter
  (`CommandEffects.interpret`). All I/O is a planned `CommandEffect`, never a
  `System.IO` call in a handler or in `Artifacts`.
- The framework reference is parsed in `Artifacts` (`Plan.fs`) as pure data. The
  `analyze` check is a pure `PlanFacts -> Diagnostic list` rule with an injected
  surface oracle — modeled on `Evidence.missingCitedArtifacts` (`Evidence.fsi`),
  which takes an injected `exists` and stays deterministic/fixture-testable.
- The authoritative surface is obtained ONLY by the `dependency-surface` verb:
  `RunProcess "dotnet restore"` at the edge, then a read of the restored package's
  real surface. This is a deliberate divergence from the "surface is `.fsi` text,
  never reflection" stance (spec 086), recorded in ADR-0004; the internal `surface`
  command is untouched.

## Constitution Check

| Principle | How this feature satisfies it |
|---|---|
| I. Spec → `.fsi` → tests → impl | This spec; then `Plan.fsi` / new `.fsi` surface; then failing-leg tests; then bodies. |
| II. Structured artifacts are the contract | The `framework:` grammar and the capture schema v1 are machine contracts; prose stays authoring surface. |
| III. Visibility lives in `.fsi` | New public types/functions exported from `Plan.fsi` (and the capture/analyze `.fsi`s); `docs/api-surface/` baselines and `PublicSurface.baseline` regenerated. |
| IV. Idiomatic simplicity | Reuses the existing `PlanContractReference` parse pipeline, the injected-oracle pattern, and the committed-baseline + drift-guard idiom (`surface`, `skill-manifest`). No new effect kind in `analyze`. |
| V. MVU is the boundary for I/O | The `dotnet restore` + surface read are `CommandEffect`s in the `dependency-surface` handler; the pure core reads the interpreted log. No `System.IO` in `Artifacts` or in the `analyze` handler. |
| VI. Test evidence is mandatory | Each verdict (dangling / contradicted-deferral / clean / no-capture-advisory / malformed) asserted by diagnostic id over fixtures; capture drift asserted for `--check`. |
| VIII. Observability / safe failure | Fail-open on capability (no capture ⇒ advisory, never a false block), fail-closed on real contradiction (ADR-0002 / #266). Every verdict names the reference and the pinned version. |

## Design Detail

### The reference grammar (Phase 1)

`framework: <PackageId>[@<version>]#<symbol>` on a `PC-###` Contract Impact line;
`blocked-on-framework: <ref>` on an Accepted Deferral. Parsed in `Plan.fs` alongside
`parsePlanContractReferences` (`Plan.fs:272-294`) into a typed
`FrameworkApiReference` exposed on `PlanFacts`. A malformed token (no `#symbol`,
empty `PackageId`) is a blocking diagnostic (FR-003) rather than a silent non-match.
The version, when omitted, is resolved from the CPM pin
(`Directory.Packages.local.props`) — single-sourced, never duplicated in the
reference.

### The capture verb (Phase 2)

`dependency-surface --update/--check`. `--update` plans
`RunProcess "dotnet" ["restore"; …]`, then reads the restored package's real public
surface (mechanism: reflection over the restored ref assembly — general, matches
`PublicSurfaceTests`; see ADR-0004 open decision), and `WriteFile`s the capture at
`docs/dependency-surface/<PackageId>/<version>.json` (schema v1, provenance-stamped,
content-addressed by `sha256`). `--check` re-reads and diffs, blocking on drift. New
capture artifact model in `Artifacts`; new handler in `CommandWorkflow`; new CLI
surface in `FS.GG.SDD.Cli`.

### The analyze check (Phase 3)

A pure rule in `ViewGeneration` beside `missingDispositionDiagnostics`
(`ViewGeneration.fs:38-50`), consuming `PlanFacts` framework references and an
injected oracle `resolve : PackageId -> version -> SymbolSet option`. New diagnostic
constructors in `CommandReports/DiagnosticConstructors.fs`
(`frameworkApiDangling`, `frameworkApiDeferralContradicted` as `DiagnosticError`;
`frameworkApiSurfaceUnavailable` as advisory `Info`), classified in
`ViewGeneration.analysisFindingSeverity` (`:261-287`), appended into the
`HandlersAnalyze` `commandDiagnostics` concat (`HandlersAnalyze.fs:88-99`) guarded on
`planFacts`. The oracle is bound at the edge to the committed capture; the check
itself does no I/O.

### Why the capture, not the vendored snapshot

The whole incident is a stale vendored `.fsi` believed over the real package.
Resolving against `docs/api-surface/` would reproduce the bug. The capture is read
from the **real restored package** and CI re-captures on pin change, so it is
authoritative-by-construction and cannot silently rot — actively closing the inert
`orphanBaseline` hole (ADR-0004 D2).

## Phasing

Per ADR-0004, dependency-ordered; each phase is its own reviewable PR:

1. **Grammar** — `Plan.fs`/`.fsi` reference parse + `blocked-on-framework:` deferral
   tag + FR-003 malformed diagnostic + authoring docs. *(This PR series starts here.)*
2. **Capture verb** — `dependency-surface --update/--check`, schema v1 + provenance,
   drift-guard, CI wiring.
3. **Analyze check** — the rule, diagnostics, classification, `analysis.json`
   wiring, fixtures.
4. *(Optional)* reconcile the vendored `docs/api-surface/` orphan against the
   authoritative capture.

## Risks / Open Decisions (from ADR-0004)

- **Capture read mechanism** — reflection over the restored ref assembly
  (recommended, general) vs reading a shipped `.fsi`. Settled in Phase 2.
- **`@version` default source** — CPM pin (recommended) vs registry
  `package-version`. Settled in Phase 1/3.
- **Verb name** — `dependency-surface` (working name).
- **Feed availability** — the capture verb needs the package's feed reachable; not a
  concern for `analyze`.
