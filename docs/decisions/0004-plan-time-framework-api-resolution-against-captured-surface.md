# 0004: Plan-time resolution of framework-API references against a captured authoritative package surface

## Status

Proposed, 2026-07-18.

Scopes **FS.GG.SDD#569**, filed from the Rougue1 RM2 development-cycle
retrospective (2026-07-18, fsgg-sdd 0.14.0). This ADR records the design of the
plan-time check that #569 asks for and the two load-bearing decisions behind it,
so the implementation feature can be specified against a settled shape. It is a
**repo-local** decision: the tooling lives entirely inside FS.GG.SDD; only the
references it *resolves* point at other repos' packages.

## Context

### The incident, and its real cause

RM1's plan deferred the persistence device host to RM2, naming
`runAppWithAudioAndPersistence` as the target. RM2 opened, and the author
concluded **the API does not exist** — "the `.fsi` names it only in a doc
comment; no `val`" — and on that basis scoped the work as *blocked on a framework
gap* and carried it forward.

It was a **false alarm**. `runAppWithPersistence` /
`runAppWithAudioAndPersistence` are real `val`s in the pinned **FS.GG.UI.SkiaViewer
0.12.0** package. The author's two sources were both non-authoritative:

- the product's **vendored** `docs/api-surface/SkiaViewer/SkiaViewer.fsi`, frozen
  at an early scaffold baseline; and
- a grep of an **unrelated** local `SkiaViewer` checkout.

So the failure has **two symmetric modes**, and a useful check must catch both:

1. **Dangling** — a cited API genuinely has no matching member in the pinned
   package. Today this is discovered mid-implementation; it should surface at plan
   time as "blocked on a framework change."
2. **Inverse false alarm** — the API *exists* in the pinned package, but the
   author's local view (a stale vendored snapshot, a wrong-repo grep) says
   otherwise, so the work is mis-scoped as blocked. This is what actually
   happened, and it is the higher-value mode to defeat.

### What the codebase gives us today, and where each stops

A source audit (2026-07-18) established that a **structured** reference is already
within reach, but nothing bridges it to a package's real surface:

- **The reference is already typed.** `plan.md`'s Contract Impact lines parse into
  `PlanFacts.ContractReferences : PlanContractReference list`, each
  `{ ContractId; Kind; Target; SourceIds; SourceLocation }`
  (`src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Plan.fs`). The `Target` field —
  documented as "artifact path or logical surface"
  (`specs/008-plan-command/contracts/plan-artifact.md`) — is free text and points
  at `contracts/` paths, not a resolvable package symbol.
- **The existence-check pattern already exists**, one domain over.
  `Evidence.missingCitedArtifacts : exists:(string -> bool) -> declaration -> string list`
  (`.../LifecycleArtifacts/Evidence.fsi`) extracts every cited path and tests it
  against an **injected** existence oracle. That is structurally the check we
  want, with a package-symbol oracle in place of a file-exists oracle — plus the
  symmetric "asserted-absent but present" verdict.
- **`analyze` is the natural home.** It is the read-only cross-artifact stage that
  already re-derives every upstream diagnostic, emits `analysis.json`, and gates
  implementation (a blocking `DiagnosticError` suppresses `analysis.json`, which
  gates evidence/verify/ship). A new rule slots in beside
  `missingDispositionDiagnostics` with a new diagnostic constructor and one line
  in the `commandDiagnostics` concat.
- **But there is no way to read a package's real surface.** The `surface` command
  and `docs/api-surface/` deal only in **byte-identical `.fsi` text mirrored from
  this repo's own `src/`** — explicitly "no reflection" (spec 086). A vendored
  copy of a *third-party* `.fsi` has no `src/` counterpart, so it registers as an
  inert **`orphanBaseline`** (advisory warning, never compared, never removed).
  That inert orphan *is the trap the incident fell into*. No product code restores
  or reads a NuGet package's public API; the only precedents are tests
  (`PublicSurfaceTests` reflecting `Assembly.Load`) and the opt-in SDK ApiCompat
  gate.
- **The registry gives version, not surface.** `Fsgg.Registry` /
  `registry/dependencies.yml` records a contract's `id` + `version` +
  `package-version`, but its `surface:` / `via:` fields are prose. It resolves
  *which version is pinned*, never *which symbols that version has*.

### The crux

The incident is explicit that the surface must be the package's **authoritative
restored** surface, **not a vendored snapshot** — trusting a snapshot is what
caused the bug. But `analyze` is pure, offline, and deterministic (the hermetic
inner loop), and reading a package's real surface needs a `dotnet restore`
(network, feed access, nondeterminism). Reconciling *authoritative* with
*deterministic* is the whole design problem.

## Decision

Four parts. Decisions **D2** and the severity policy in **D3** were the two
genuine forks; both are settled here.

### D1 — A structured framework-API reference grammar

Give the reference a resolvable, load-bearing form (in the family of the checklist
coverage line and the evidence satisfaction rule). On a plan Contract Impact line,
the `Target` may be a framework reference:

```
framework: <PackageId>[@<version>]#<symbol>
```

- `<PackageId>` — the NuGet id, e.g. `FS.GG.UI.SkiaViewer`.
- `@<version>` — **optional**; defaults to the Central Package Management pin in
  `Directory.Packages.local.props`. The version is single-sourced from the pin so
  a reference never duplicates (and never drifts from) the pinned version.
- `#<symbol>` — the module-qualified `val`/member, e.g.
  `SkiaViewer.runAppWithAudioAndPersistence`.

The **inverse assertion** — "the author believes this API is absent" — is
expressed on a deferral/blocked disposition whose reason carries the same
reference (a `blocked-on-framework: <ref>` tag). That is the structural form of
the mis-scoping the incident produced, and it is what the check contradicts.

### D2 — Resolve against a *captured* authoritative surface, never the vendored snapshot

**Chosen: a captured baseline plus a drift guard.** A new capture verb (working
name `fsgg-sdd dependency-surface`) owns the restore:

- `--update` — discover references across `work/**/plan.md`, resolve explicit versions or the
  workspace CPM pins → `dotnet restore` the workspace → read each package's **real**
  public surface from the restored artifact in `~/.nuget/packages/<id>/<version>/`
  → write a committed, provenance-stamped capture at
  `docs/dependency-surface/<PackageId>/<version>.json` (schema v1:
  `packageId`, `version`, `capturedFrom` feed, content `sha256`, `symbols[]`).
- `--check` (default) — discover the same authored target set, restore, and diff against the
  committed files; a readable target with no capture or any content drift exits 1. This is the CI
  drift-guard, matching the `surface` /
  `skill-manifest` idiom the repo already uses.

This resolves the crux by **confining the restore to the capture verb**:

- `analyze` reads only the committed capture — it stays pure, offline, and
  deterministic; no network or reflection at analyze time.
- the capture is **authoritative by construction** — read from the real published
  package, not hand-vendored — and CI discovers the new target on any pin change, so it cannot
  silently go stale. This actively closes the `orphanBaseline` hole the incident
  exploited.

Rejected alternatives: a **live restore inside `analyze`** (authoritative but
breaks the hermetic inner loop and is nondeterministic); an **advisory
best-effort** read of whatever happens to be in the local cache (smallest change,
but silently passes exactly when it cannot look, and reliably catches neither
mode).

### D3 — The analyze check: injected oracle, symmetric verdicts, fail-open on capability

A pure `PlanFacts`-driven rule in `analyze`, modeled on
`Evidence.missingCitedArtifacts`, with an **injected** surface oracle
`resolve : PackageId -> version -> SymbolSet option` (`Some` = capture present;
`None` = no capture / could not look). For each framework reference:

| reference | symbol in real surface? | verdict |
|---|---|---|
| **use** ref | yes | pass |
| **use** ref | no | **BLOCK** — dangling; surfaced at plan time as blocked-on-framework-change |
| **blocked-on** deferral | yes | **BLOCK** — the incident: the deferral's premise is contradicted |
| **blocked-on** deferral | no | pass — the deferral is legitimate |
| any | oracle `None` (no capture) | **ADVISORY**, exit 0 |

The last row is the settled severity policy: **block on real contradictions,
advise when blind.** A reference that cannot be resolved because no surface was
captured (or the run is offline) is a non-blocking advisory — "I could not look"
is never rendered as a negative verdict (org ADR-0002 / #266 discipline). Blocking
is reserved for the two verdicts backed by an authoritative surface.

New diagnostic constructors in
`src/FS.GG.SDD.Commands/CommandReports/DiagnosticConstructors.fs` (e.g.
`frameworkApiDangling` and `frameworkApiDeferralContradicted` as
`DiagnosticError`, `frameworkApiSurfaceUnavailable` as advisory `Info`),
classified in `ViewGeneration.analysisFindingSeverity` so they do not fall through
to the generic bucket, and appended into the `HandlersAnalyze` `commandDiagnostics`
concat guarded on `planFacts`.

### D4 — Version from the pin, symbol set from the real package

The resolved version is always the CPM pin (or the explicit `@version`); the
symbol set is always the captured real surface. Both failure modes are then
defeated by construction: a dangling reference cannot pass (it is absent from the
real surface), and a false-alarm deferral cannot stand (the real surface
contradicts it).

## Consequences

- **New persisted artifact**: `docs/dependency-surface/<Pkg>/<ver>.json`
  (schema v1), plus a CI drift-guard job. New effect(s) for restore + surface read
  (`RunProcess "dotnet restore"` + a reflection/`.fsi` read) live **only** in the
  capture verb — `analyze`'s effect surface is unchanged.
- **Deliberate divergence** from the "surface is `.fsi` text, never reflection"
  stance (spec 086 / `HandlersSurface`): the *dependency-surface capture* reads a
  restored package's real surface, because defeating a stale text vendoring is the
  entire point. The internal `surface` command is untouched — it still diffs this
  repo's own authored `.fsi` text. This ADR is the record of that divergence.
- **Feed dependency**: FS.GG.UI.* is not wired into this repo's `nuget.config`
  today. The capture verb needs the package's feed (org GitHub Packages, or
  nuget.org per org ADR-0012) reachable at `--update`/`--check` time. `analyze`
  needs neither.
- **Determinism preserved** for the inner loop: `analyze` reads a committed file;
  the nondeterministic restore is a separate, CI-gated step.
- **Generated-consumer owner**: `dependency-surface --update` owns initial capture and pin-change
  refresh; `dependency-surface --check` owns drift/missing-capture enforcement. Both derive targets
  from authored plans and CPM pins, so an empty capture directory is no longer an empty check.

## Scope / phasing (for the implementation feature)

1. Grammar — `framework:` reference parse in `Plan.fs`, the `blocked-on-framework:`
   deferral tag, and the authoring docs (`fs-gg-sdd-plan`,
   `fs-gg-sdd-authoring-contracts`, `docs/reference/authoring-contracts.md`).
2. Capture verb — `dependency-surface --update/--check`, capture schema v1 +
   provenance, drift-guard, CI wiring.
3. Analyze check — the rule, the diagnostic constructors, classification,
   `analysis.json` wiring, and fixtures (dangling / contradicted-deferral /
   clean / no-capture-advisory).
4. *(Optional)* actively reconcile the vendored `docs/api-surface/` orphan against
   the authoritative capture, retiring the staleness hole end to end.

## Touch-set correction for #569

#569 declared `src/FS.GG.SDD.Validation/`, but that project is the `validate`
exhaustive-matrix harness — **not** where lifecycle/analyze checks live. The
implementation touch-set is:

- `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Plan.fs(i)` — reference grammar/parse
- `src/FS.GG.SDD.Commands/CommandWorkflow/` — the analyze rule and the capture handler
- `src/FS.GG.SDD.Commands/CommandReports/DiagnosticConstructors.fs` — diagnostics
- `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Analysis.fs(i)` — category/counter, if first-class
- `src/FS.GG.SDD.Cli/` — the capture verb CLI surface
- `.claude/skills/fs-gg-sdd-plan/`, `.claude/skills/fs-gg-sdd-authoring-contracts/`,
  `docs/reference/authoring-contracts.md` — grammar docs
- `docs/dependency-surface/` and a CI workflow — captures and the drift guard

## Open decisions (for the implementation feature / follow-up)

- **Capture read mechanism**: reflection over the restored ref assembly (general;
  matches `PublicSurfaceTests`) vs. reading a shipped `.fsi` when present (simpler,
  but not every package ships one). Recommendation: reflection, for generality.
- **`@version` default source**: strictly the CPM pin, or also the registry
  `package-version`. Recommendation: CPM pin, with the registry as a later
  cross-check.
- **Verb name**: `dependency-surface` vs `framework-surface` vs `api-pin`.
