# Implementation Plan: Scaffold lifecycle-parameter pass-through & app-only provenance

**Branch**: `031-scaffold-lifecycle-passthrough` | **Date**: 2026-06-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/031-scaffold-lifecycle-passthrough/spec.md`

## Summary

Close the Coordination board **P2 · sdd** gate by publishing an *enforced verification*
of the existing `fsgg-sdd scaffold` composition path for the specific real-world shape
the org roadmap depends on: `scaffold --provider <fixture> --param lifecycle=sdd`
producing an **app-only** product tree alongside the unchanged SDD skeleton, with
provenance recording only the app-only paths, and **no** rendering-specific identifier
or `lifecycle`-value special-casing in generic SDD.

This feature ships **no public API, schema, command, or artifact-layout change** (FR-010 /
SC-007). It treats `lifecycle` as it already treats every `--param`: an opaque,
provider-owned `key=value` carried verbatim to the `dotnet new` edge. The deliverable is
three things, all owned by this repo:

1. A repo-owned, **rendering-agnostic recording fixture provider** that declares a
   `lifecycle` template symbol and echoes the parameters it received into an app-only
   produced file, so the wrapper's verbatim forwarding can be asserted end-to-end through
   the real process edge (FR-001, US1).
2. **Verification scenarios** pinning forwarded-parameter set equality, app-only
   provenance precision/recall, the three-projection fact parity, determinism, and every
   edge case (FR-002…FR-006, FR-008, US1/US2).
3. An **enforced leak-invariant guard** that fails the build on any provider-specific
   identifier leak *and* on any `lifecycle`-value special-casing in the scaffold source
   path, complemented by a behavioral value-agnosticism test and an automated
   planted-violation proof (FR-007, US3).

Decisions locked in [research.md](./research.md):

1. **Reuse, don't reshape** — the current scaffold already forwards arbitrary `--param`
   verbatim (`-p:{key}={value}`, `HandlersScaffold.fs:175-178`) and already marks produced
   paths `generatedProduct` while excluding the SDD skeleton. This feature *proves* that
   behavior for the `lifecycle=sdd` shape; it does not re-implement it. If a verification
   surfaces a real defect, the corrective change stays inside the existing scaffold
   contract and is called out here (none anticipated).
2. **Two-level forwarding proof** — set equality and order-independence are asserted at the
   *planned-effect* level (dry-run inspection of the real `RunProcess` create-arg vector,
   the same MVU surface `ScaffoldCommandTests.fs:74-78` already uses); verbatim arrival is
   asserted *end-to-end* by reading the recording fixture's echoed app file after a real
   run. Both go through the public scaffold surface (FR-009).
3. **Leak scan is identifier-grep + value-agnosticism behavior** — a source grep for
   *lifecycle values* (`sdd`/`spec-kit`/`none`) is rejected as unreliable: those tokens
   collide with ubiquitous SDD vocabulary (`fsgg-sdd`, `FS.GG.SDD`, the `None` successor,
   `(none)` projections). Instead the scan enforces (a) the provider-identifier deny-list
   (extending `ScaffoldGuardTests.fs:12`) and (b) the literal token `lifecycle` never
   appearing in the **scaffold source path** (which owns no lifecycle vocabulary), and a
   behavioral test proves an *arbitrary* lifecycle value forwards identically — a stronger
   "no value branching" guarantee than a value grep can give.

**Change tier**: **Tier 1 (contracted change)** — per spec, this adds a *published
conformance obligation* (fixture + scenarios + leak-invariant scan) to SDD's verifiable
provider-contract surface and gates downstream P4 Templates work. It does **not** touch
`.fsi`, baselines, or golden outputs (SC-007); the only diffs are new fixtures and tests.

## Technical Context

**Language/Version**: F# on .NET, target `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: standard library only. Verification reuses the existing
`System.Diagnostics.Process` `dotnet new` edge (`CommandEffects.fs:68-110`) and
`System.Text.Json` provenance serializer (`ScaffoldProvenance.fs:33-60`). No new NuGet
dependency. The recording fixture is a local `dotnet new` template (no package, no
network), mirroring the existing `tests/fixtures/scaffold-provider/` machinery.

**Storage**: Filesystem only. No new artifact type. Reads/asserts the existing
`.fsgg/scaffold-provenance.json` (schema v1, unchanged) and the existing
`.fsgg/providers.yml` registry (unchanged). New committed test fixtures only.

**Testing**: `dotnet test FS.GG.SDD.sln` (xUnit; 4 test projects: Artifacts, Validation,
Cli, Commands). New scenarios land in the existing scaffold test modules and a new
recording-fixture template under `tests/fixtures/scaffold-provider/`. All assertions run
over **real** filesystem/process fixtures through the public scaffold surface (FR-009); no
mocks of internal stages.

**Target Platform**: Linux/cross-platform CLI. The `dotnet new` edge is environment-sensed
and degrades with a diagnostic (constitution VIII); the real Rendering provider is **not**
a dependency of this repo (verified via the in-repo fixture, as in 030).

**Project Type**: Single solution, CLI + libraries (`src/FS.GG.SDD.*`). No cross-repo
deliverable is owned here — the real `lifecycle`-aware provider lives in FS.GG.Rendering
(P1) and is repointed by FS.GG.Templates (P4); this P2 feature unblocks them with a
verified contract.

**Performance Goals**: N/A. One short-lived child process per scenario; the fixture
template is trivial.

**Constraints**:
- **No public-surface change** (FR-010 / SC-007): no edit to any `.fsi`, no
  `PublicSurface.baseline` diff, no provenance-schema/diagnostics/projection change. The
  guard test `tests/**/*.baseline` snapshots stay byte-identical.
- `init` stays **byte-identical** (FR-005): the skeleton a `lifecycle=sdd` scaffold writes
  is asserted equal, file-for-file, to a plain `init` run (`Foundation.fs:initEffects`
  reused unchanged).
- **Deterministic** `--json` and provenance (FR-006 / SC-004): two identical
  `lifecycle=sdd` runs into clean targets yield byte-identical provenance and JSON report
  output (sorted paths, no clock, no absolute paths — `ScaffoldProvenance.fs:33-60`).
- `--rich` remains a pure projection (no JSON byte change); rich is excluded from
  deterministic/golden contracts (constitution / CLAUDE.md).
- `WarningsAsErrors` ratchet stays at 0; no `#nowarn` introduced.
- **Zero** rendering-specific package id / template id / provider name / path / docs URL in
  generic SDD source or in the generic-contract tests (SC-005, grep-verifiable). The
  recording fixture and its registry use only neutral identifiers
  (`fsgg-fixture-*`, `__FIXTURE__`).

**Scale/Scope**: 0 new commands; 0 new schema/artifact types; 0 `.fsi` edits. ~1 new
recording fixture template (+ 1–3 registry/fixture variants for edge cases); new scenarios
across `ScaffoldCommandTests.fs`, `ScaffoldGuardTests.fs`, and `ScaffoldParityTests.fs`;
agent surfaces unchanged (no workflow change — the scaffold contract is unchanged).

### Grounded inventory (current tree, verified 2026-06-27 @ `ae8f862`)

| Concern | Anchor | Disposition (this feature) |
|---|---|---|
| Param forwarding | `HandlersScaffold.fs:175-178` (`--{key} {value}` over `effective: Map`; corrected from `-p:k=v` per research Decision 8) | **assert verbatim/opaque**; Map canonicalizes order → order-independence (FR-008) |
| Effective overlay | `HandlersScaffold.fs:84-96` (defaults ⊕ author `--param`) | **assert** forwarded set == overlay, no add/drop/rename (FR-003 / SC-001) |
| Create-arg vector | `HandlersScaffold.fs:180-190` (`new <id> -o . -p:… [--force]`) | **inspect** at dry-run plan level for set/order assertions |
| Process edge | `CommandEffects.fs:68-110` (`runProcess`) | reused unchanged; real-run echo proof |
| Skeleton effects | `Foundation.fs:initEffects` | reused unchanged; **assert** byte-identical to `init` (FR-005) |
| Provenance writer | `ScaffoldProvenance.fs:33-60`, `HandlersScaffold.fs:205-215` | **assert** produced set == app files, all `generatedProduct`, no skeleton path (FR-004) |
| SDD-tree guard | `HandlersScaffold.fs:52-60,217-285` (`isSddOwned`, intrusion → exit 2) | **exercise** with `lifecycle=sdd` (FR-008 edge) |
| Required-param guard | `HandlersScaffold.fs:84-96` (`scaffold.providerParamMissing`) | **exercise** with `lifecycle` declared required + omitted (FR-008 edge) |
| Empty-product outcome | `HandlersScaffold.fs:25-36` (`ProviderSucceededEmpty`) | **exercise** with `lifecycle=sdd` (FR-008 edge) |
| Projections | `CommandSerialization.fs:291-311`, `CommandRendering.fs:196-209`, `Cli/Rendering.fs` | **assert** three-way produced-path fact parity (FR-006) |
| Leak guard | `ScaffoldGuardTests.fs:12-57` (deny-list scan of `src` + generic-contract tests) | **extend**: scope a `lifecycle`-literal scan to the curated scaffold-source union (`HandlersScaffold.fs` + scaffold branches of `CommandSerialization.fs` / `CommandRendering.fs` / `CommandReports.fs` / `Cli/Rendering.fs`) + automate planted-violation proof (FR-007) |
| Fixture machinery | `tests/fixtures/scaffold-provider/` (`ok`/`empty`/`fails-midway`/`writes-into-fsgg` + `registries/`, `__FIXTURE__` token) | **add** a recording fixture + lifecycle registries (FR-001) |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → Tests → Impl | ✅ | No new public surface, so no `.fsi` sketch is required; the order collapses to *spec → semantic tests over the existing public scaffold surface → (only if a defect is found) implementation within the existing contract*. Tests are authored to fail before the verification exists and pass against current behavior. |
| II. Structured artifacts are the contract | ✅ | The authoritative machine contract under test is the existing `scaffold-provenance.json` (v1); verifications read it as truth and compare it to the provider's actual file set (FR-004). No prose↔structured ambiguity is introduced; none is changed. |
| III. Visibility lives in `.fsi` | ✅ | **No** public binding added or changed → all 4 `PublicSurface.baseline` snapshots stay byte-identical (SC-007). The guard explicitly asserts this. |
| IV. Idiomatic simplicity | ✅ | New code is test-only: a trivial `dotnet new` fixture (data) + xUnit facts over real I/O. No new abstraction, no reflection, no framework. |
| V. Elmish/MVU boundary | ✅ | No production code path added; verifications drive the existing MVU loop (`init`/`update`/edge interpret) exactly as `ScaffoldCommandTests.fs` does. Plan-level assertions inspect the real planned `RunProcess` effect, not a mocked stage. |
| VI. Test evidence | ✅ | Real local `dotnet new` fixture (no mocks) drives forwarding, app-only provenance, determinism, and every edge case; the recording fixture's echoed file is the real-evidence channel for "verbatim arrival" (FR-009). Synthetic stand-ins disclosed in test/fixture names. |
| VII. One contract for agents + humans | ✅ | No workflow change → agent surfaces (CLAUDE/AGENTS/2× SKILL) need no update; the scaffold contract they describe is unchanged. (Confirmed in Phase 1; if a defect fix changes user-visible behavior, surfaces update equivalently.) |
| VIII. Observability & safe failure | ✅ | Edge cases assert the existing actionable diagnostics fire correctly under `lifecycle=sdd`: `scaffold.providerParamMissing` (exit 1, pre-invocation), `providerEmpty` (exit 0), `providerWroteSddTree` (exit 2, no path laundering). Malformed-input vs provider-defect split unchanged. |

**Change tier**: **Tier 1 (contracted change)** — adds an enforced conformance obligation to
the verifiable provider-contract surface. The *only* contract that changes is the
verification/guard surface itself; no user-facing API/schema/command/layout changes →
Complexity Tracking empty.

**Lifecycle-feature plan checklist** (constitution §Development Workflow):
- *Authored artifacts*: none new (scaffold authors none; the skeleton is init's, unchanged).
- *Structured machine contracts*: none changed; `scaffold-provenance.json` (v1) and
  `providers.yml` (v1) are read/asserted, not modified.
- *Generated views*: none changed. Verifications confirm refresh continues to **exclude**
  the app-only provenance paths (FR-007 of 030; re-asserted here for `lifecycle=sdd`).
- *Schema version & migration*: no schema touched → no migration. Release catalog
  (`docs/release/`) unchanged (no new produced artifact; the recording fixture is test data).
- *Agent behavior (Claude & Codex)*: unchanged (no workflow change).
- *Optional Governance integration*: none; no new obligation. Produced files stay
  `generatedProduct`, out of SDD's and Governance's freshness scope.
- *Tests/fixtures for stale/conflicting artifacts*: SDD-tree intrusion, empty product,
  required-but-missing param, malformed/stale provenance all exercised under `lifecycle=sdd`.

## Project Structure

### Documentation (this feature)

```text
specs/031-scaffold-lifecycle-passthrough/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions: two-level forwarding proof, leak-scan strategy,
│                         #   fixture design, "no production change unless defect" posture
├── data-model.md        # Phase 1 — entities under verification (opaque param, app-only tree,
│                         #   skeleton, provenance, recording fixture, leak scan) + the
│                         #   forwarded-set overlay model and leak-scan scope
├── quickstart.md        # Phase 1 — run the lifecycle=sdd verification suite + leak scan;
│                         #   expected outcomes; how to plant a violation and watch it fail
├── contracts/           # Phase 1 — verification contracts (not new interfaces)
│   ├── recording-fixture-provider.md   # the rendering-agnostic fixture's recording behavior
│   ├── forwarding-invariant.md         # forwarded set == overlay; verbatim; order-independent
│   ├── app-only-provenance.md          # produced set == app files; generatedProduct; no skeleton
│   └── leak-invariant-scan.md          # deny-list scope + lifecycle-literal scope + planted proof
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

No `src/` change was *planned* (FR-010); one corrective `src/` change was made after a
verification surfaced a genuine forwarding defect — the `-p:k=v` → `--k v` wire-form fix in
`HandlersScaffold.fs` (research Decision 8), within the existing scaffold contract, with no
public-surface/`.fsi`/baseline/golden impact. Otherwise work is confined to test fixtures and
test modules:

```text
tests/fixtures/scaffold-provider/
├── lifecycle/                          # NEW — recording fixture (declares `lifecycle` + `productName`
│   ├── .template.config/template.json  #   symbols; produces an app-only tree)
│   ├── App.fsproj                      #   app file (substitutes PRODUCT_NAME)
│   ├── Program.fs                      #   app file
│   └── scaffold-manifest.txt           #   echoes "lifecycle=LIFECYCLE_VALUE" etc. → verbatim proof
├── lifecycle-empty/                    # NEW (or reuse) — declares `lifecycle`, produces nothing (empty edge)
│   └── .template.config/template.json
├── lifecycle-intrusion/                # NEW (or reuse) — declares `lifecycle`, writes into .fsgg/work/readiness
│   └── …                               #   (SDD-tree-intrusion edge under lifecycle=sdd)
└── registries/
    ├── lifecycle.providers.yml         # NEW — fixture provider, lifecycle NOT required (default path)
    ├── lifecycle-required.providers.yml# NEW — marks `lifecycle` required (required-but-missing edge)
    ├── lifecycle-empty.providers.yml   # NEW — points at lifecycle-empty
    └── lifecycle-intrusion.providers.yml # NEW — points at lifecycle-intrusion

tests/FS.GG.SDD.Commands.Tests/
├── ScaffoldCommandTests.fs   # + US1 forwarding (plan-level set/order + real-run verbatim echo),
│                              #   US2 app-only provenance (precision/recall, no skeleton, init byte-identical),
│                              #   FR-008 edges under lifecycle=sdd, FR-006 determinism, value-agnosticism
└── ScaffoldGuardTests.fs      # + lifecycle-literal scan scoped to scaffold source;
                               #   + automated planted-violation proof (scan catches + locates)

tests/FS.GG.SDD.Cli.Tests/
└── ScaffoldParityTests.fs     # + three-projection produced-path fact parity for a lifecycle=sdd run
```

**Structure Decision**: Single-solution F# layout retained. This is a **verification-only**
feature: the entire deliverable lands under `tests/`. Production `src/` stays byte-stable
unless a verification surfaces a genuine defect — in which case the corrective change is
made within the existing scaffold contract, called out explicitly in `research.md`, and
the affected `.fsi`/baseline/golden updated as a Tier 1 follow-through. None is anticipated,
because the behavior already exists and 030's suite exercises the generic path.

## Complexity Tracking

No Constitution Check violations — this section is intentionally empty.
