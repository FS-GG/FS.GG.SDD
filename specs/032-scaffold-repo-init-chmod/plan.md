# Implementation Plan: Scaffold owns repo-init & script-executable post-instantiation steps

**Branch**: `032-scaffold-repo-init-chmod` | **Date**: 2026-06-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/032-scaffold-repo-init-chmod/spec.md`

## Summary

The reference template went **side-effect-free** (Rendering Feature 205): provider
instantiation now spawns no process, creates no git repository, and sets no executable
bit. Per the published `fs-gg-ui-template-generation` contract (§5 S1–S3) and ADR-0002,
**SDD must now own those convenience steps itself**, as explicit, observable
post-instantiation actions on the scaffold path, for **any** provider.

This feature adds two generic post-instantiation steps to the existing `fsgg-sdd scaffold`
staged driver, run only after a **successful** provider instantiation on the **real**
execution path:

1. **Repository initialization** — after the provider succeeds (including the
   empty-but-successful outcome), initialize a git repository at the scaffolded product
   root, *unless* the target is already inside a git work tree (no nesting) or git is
   unavailable (skip non-fatally). Decided by **exit code alone** from a
   `git rev-parse --is-inside-work-tree` probe (FR-001/002/003/004/013, US1/US3).
2. **Make scaffolded shell scripts executable** — set the executable bit on each produced
   `.sh` script via a new permission edge; a no-op (count 0) when there are none, and a
   reported skip/partial when a bit cannot be applied (FR-005/006, US2).

Both steps are **generic orchestration** driven only by the scaffolded tree: the product
root and the shell-script set are derived from the already-computed produced-path diff, with
**zero** provider-, template-, or rendering-specific identifier (FR-006/007, US4, SC-005).
SDD performs the steps itself and never passes provider git options (`initGit`,
`allow-scripts`) to obtain them (FR-007).

Every run reports, in all three projections, which steps ran/were skipped and why, and how
many scripts were made executable (FR-011, SC-006). The steps are filesystem side-effects
only and introduce **no** nondeterministic bytes into `scaffold-provenance.json` or the
report JSON (FR-012).

Decisions locked in [research.md](./research.md):

1. **Exit-code-only git sensing.** `ProcessRunResult` captures `Started` + `ExitCode`
   only (stdout is drained and excluded from the deterministic contract,
   `CommandTypes.fsi:391-396`). The standard work-tree check
   `git rev-parse --is-inside-work-tree` distinguishes all three repo-init cases by exit
   code without reading stdout: `Started=false` → *git unavailable*; `ExitCode=0` →
   *inside a work tree, skip*; `ExitCode≠0` (128) → *not a repo, initialize*. No new
   process-result field is needed.
2. **A new `SetExecutable` permission edge**, not `chmod` via `RunProcess`. The spec names
   "the existing MVU process/**permission** edges"; SDD has a process edge but no
   permission edge. A first-class `SetExecutable of path` effect interpreted with
   `File.SetUnixFileMode` is cross-platform, deterministic-safe (a pure filesystem
   side-effect, no JSON bytes), and degrades gracefully (read-only FS / non-Unix →
   reported skip/partial, FR-005 US2-AC3). `RunProcess("chmod", …)` is rejected: Unix-only,
   silent on Windows, and an unnecessary second process per script.
3. **Stateless recomputation, three new staged ticks.** The scaffold driver already
   recomputes everything from `InterpretedEffects` each tick
   (`HandlersScaffold.fs:294-324`). Repo-init needs a probe→decide→init dependency chain,
   so the post-instantiation phase adds three sensing/acting ticks after a successful
   create: **(a)** plan provenance write + the `git rev-parse` probe + the `SetExecutable`
   batch; **(b)** once the probe is interpreted, plan `git init` iff exit≠0 & started;
   **(c)** once init is interpreted-or-skipped, compute and set the final `ScaffoldSummary`.
   No new `CommandModel` staging field — each tick re-derives its decision from the
   interpreted effect log, exactly as the create-diff already does.
4. **`.sh`-shape discovery over the produced set.** "Shell scripts" are the produced
   (app-only, non-SDD) paths ending in `.sh` — generic file *shape*, carrying no
   provider-specific script name (FR-006). The produced set is already computed in
   `finalizeScaffold` (`HandlersScaffold.fs:268-273`).
5. **Repo-init outcome is sensed metadata; goldens pin structure, not the verdict.** Like
   the validation report's sensed fields, the repo-init outcome depends on the host (git
   present? inside a tree?). Deterministic golden coverage runs in environment-controlled
   temp dirs (outside any work tree, git present → `initialized`); the determinism contract
   (FR-012) asserts byte-identical **provenance** and that two identical runs in the *same*
   environment yield identical JSON, not a host-independent repo-init verdict.

**Change tier**: **Tier 1 (contracted change)** — implements the SDD-side S1–S3 obligations
of the Accepted cross-repo contract (registry `fs-gg-ui-template.behavior-break`). It adds
new observable scaffold behavior, a new `CommandEffect` case, new `ScaffoldSummary` fields,
and new diagnostics → `.fsi`, baselines, tests, golden/snapshot fixtures, and the agent
surfaces are all in scope. The provider contract, the invocation protocol, and the
`scaffold-provenance.json` schema (v1) are **unchanged** (FR-012).

## Technical Context

**Language/Version**: F# on .NET, target `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: standard library only. Repo-init reuses the existing
`System.Diagnostics.Process` `RunProcess` edge (`CommandEffects.fs:68-110`) with a new
external tool, **git** (treated as optional/sensed). Make-executable adds a `SetExecutable`
edge interpreted via `System.IO.File.SetUnixFileMode` (BCL, no new package). Provenance and
report serialization reuse `System.Text.Json` (`ScaffoldProvenance.fs`,
`CommandSerialization.fs`) unchanged in byte-shape except the additive new report fields.

**Storage**: Filesystem only. No new artifact type. The `.fsgg/scaffold-provenance.json`
schema (v1) and `.fsgg/providers.yml` registry (v1) are **unchanged**. Repo-init creates a
`.git` directory at the product root (not an SDD artifact; not catalogued). New committed
test fixtures only.

**Testing**: `dotnet test FS.GG.SDD.sln` (xUnit; projects: Artifacts, Validation, Cli,
Commands). New scenarios land in `ScaffoldCommandTests.fs` (US1/US2/US3/US4 over the real
MVU loop + real `git`/filesystem), `ScaffoldGuardTests.fs` (leak scan extension, US4),
`ScaffoldParityTests.fs` (three-projection fact parity, FR-011), and surface/baseline
tests. All assertions run over **real** filesystem/process fixtures through the public
scaffold surface (constitution VI); no mocks of internal stages. New `dotnet new` fixture
emitting a `.sh` script drives US2; a non-rendering fixture drives US4.

**Target Platform**: Linux/cross-platform CLI. git is environment-sensed and degrades with
a reported skip (constitution VIII, FR-003). `SetUnixFileMode` is a no-op-equivalent on
non-Unix hosts → reported as skipped/partial (FR-005). The real Rendering provider is **not**
a dependency of this repo (verified via in-repo fixtures).

**Performance Goals**: N/A. One short-lived `git rev-parse` probe and at most one `git init`
per scaffold; the make-executable batch is in-process BCL calls.

**Project Type**: Single solution, CLI + libraries (`src/FS.GG.SDD.*`). The cross-repo
side-effect-free template and the published contract are delivered by FS.GG.Rendering; this
P2 feature is the SDD-side execution that closes the scaffold-path-parity gap (Scenario H /
SC-003 of the contract).

**Constraints**:
- **Determinism (FR-012)**: repo-init and chmod are filesystem side-effects only; the
  `scaffold-provenance.json` bytes and the report-JSON **schema/byte shape** are unchanged
  except the additive sensed fields. Two identical runs into clean targets in the same
  environment yield byte-identical provenance and JSON.
- **`init` stays byte-identical**: post-instantiation steps run *after* the skeleton +
  create; `initEffects` (`Foundation.fs`) is reused unchanged. A plain `init` run still
  emits no git repo and no chmod (the steps are scaffold-only).
- **`--rich` is a pure projection**: the rich view reuses the plain-text projection
  (`Cli/Rendering.fs:74` consumes `CommandRendering.renderText`); the new repo-init/exec
  facts appear in rich automatically with **no** JSON byte change. Rich is excluded from
  deterministic/golden contracts.
- **No false complete/incomplete (FR-009/010)**: post-instantiation runs only after
  `ProviderSucceeded`/`ProviderSucceededEmpty`; a skipped convenience step never flips a
  success to failure, and a failed instantiation is never reported complete.
- **Zero provider-specific identifier (SC-005)**: the provider-identifier guard is the
  repo-wide **C1** scan (`ScaffoldGuardTests.fs:32-39`, `sourceFiles()` over `src/**/*.fs(i)`),
  which already covers every file this feature touches — including `CommandEffects.fs` and
  `Diagnostics.fs` — so no curated-list extension is needed for SC-005. The narrower curated
  scaffold-source union (`scaffoldSourceFiles()` at `:54-61`) feeds the **C2** lifecycle-value
  scan; the new handler/projection code lands inside it. No package id / template id / path /
  script name / docs URL in either scope.
- `WarningsAsErrors` ratchet stays at 0; no `#nowarn` introduced.

**Scale/Scope**: +1 `CommandEffect` case (`SetExecutable`); +new `ScaffoldSummary` fields
(repo-init outcome + executable-script count + skipped indicator); +new repo-init / exec
diagnostics (advisory facts, not failures); 0 new commands; 0 schema/artifact-type changes;
0 provenance-schema change. `.fsi` edits: `CommandTypes.fsi` (effect case + summary fields),
`Diagnostics.fsi` (new advisory diagnostics). Agent surfaces (CLAUDE/AGENTS/2× SKILL) gain a
one-line scaffold-behavior note (no workflow-shape change).

### Grounded inventory (current tree, verified 2026-06-27 @ `3ed0c77`)

| Concern | Anchor | Disposition (this feature) |
|---|---|---|
| Staged driver | `HandlersScaffold.fs:294-324` (`computeScaffoldNext`: resolve → invoke → finalize) | **extend** with post-instantiation ticks (probe → init-decision → final summary) after a successful create |
| Instantiation finalize | `HandlersScaffold.fs:222-290` (`finalizeScaffold`: outcome + produced diff + provenance) | **split**: keep outcome/diff; on **failure**, emit provenance + terminal summary as today; on **success**, defer **both** the provenance write and the final summary to the post-instantiation phase (TICK A owns the single provenance write, before `git init`; TICK C owns the summary) — no double provenance write |
| Produced-path diff | `HandlersScaffold.fs:268-273` (`produced` = after − before − skeleton − provenance) | **reuse** as the source of truth for product root + `.sh` script set (FR-006) |
| Process edge | `CommandEffects.fs:68-110` (`runProcess`, captures `Started`+`ExitCode`) | **reuse unchanged** for the `git rev-parse` probe and `git init` |
| Effect interpreter | `CommandEffects.fs:112-161` (`interpret`) | **add** a `SetExecutable` case → `File.SetUnixFileMode`, try/skip on failure (FR-005) |
| Effect DU | `CommandTypes.fsi:381-389` (`CommandEffect`) | **add** `SetExecutable of path: string` |
| Scaffold summary | `CommandTypes.fsi:328-336` (`ScaffoldSummary`) | **add** repo-init outcome + executable-script count (+ skipped/partial indicator) fields |
| JSON projection | `CommandSerialization.fs:291-311` (`writeScaffold`) | **add** the new fields (deterministic automation contract) |
| Text projection | `CommandRendering.fs:196-209` | **add** repo-init + exec lines (rich inherits via `renderText`) |
| Diagnostics | `Diagnostics.fs:140-250`, `Diagnostics.fsi:49-61` | **add** advisory repo-init-skipped / exec-skipped facts (non-fatal, FR-010) |
| Provenance writer | `HandlersScaffold.fs:210-220`, `ScaffoldProvenance.fs` | **unchanged bytes** (FR-012); repo-init captures it in the work tree (FR-004) |
| Leak guard | `ScaffoldGuardTests.fs:32-39` (C1, repo-wide `sourceFiles()`) + `:54-61` (C2, curated `scaffoldSourceFiles()`) | SC-005 (provider identifiers) is enforced by the repo-wide C1 scan — already covers the new `CommandEffects.fs`/`Diagnostics.fs`; the curated C2 union (lifecycle values) also covers the new handler/projection code |
| Fixtures | `tests/fixtures/scaffold-provider/` (`ok`/`empty`/`lifecycle`/… + `registries/`) | **add** a fixture emitting a `.sh` script; reuse `empty`/`ok` for the no-script & happy paths |
| MVU loop driver | `CommandWorkflow.fs:129-133` (`Scaffold` → `computeScaffoldNext`) + test loop `ScaffoldCommandTests.fs:35-54` | **unchanged**: the loop already re-ticks while new effects are returned |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → FSI-exercise → Tests → Impl | ✅ | New public surface (`SetExecutable` case, `ScaffoldSummary` fields, new diagnostics) is sketched in `.fsi` first; the post-instantiation behavior is exercised through the public scaffold surface (the MVU loop) before `.fs` hardens. Tests authored to fail before the steps exist (a scaffolded product with no `.git` / non-executable `.sh`) and pass after. |
| II. Structured artifacts are the contract | ✅ | The repo-init/exec outcomes are surfaced in the report JSON (the automation contract); the authoritative produced-path source remains `scaffold-provenance.json` (v1, **bytes unchanged**). Prose↔structured: the text/rich projections add no fact the JSON lacks (FR-011). |
| III. Visibility lives in `.fsi` | ✅ | `CommandTypes.fsi` (effect case + summary fields) and `Diagnostics.fsi` (new diagnostics) are the sole declaration of the new surface; `PublicSurface.baseline` snapshots are re-generated and the surface test re-asserts them. No top-level `private`/`internal` used as policy. |
| IV. Idiomatic simplicity | ✅ | Plain F#: one new DU case, one `File.SetUnixFileMode` interpreter arm, three additional `match` arms in the existing staged driver. No new abstraction, reflection, or framework. Mutation-free; the driver stays a pure transition over recomputed state. |
| V. Elmish/MVU boundary | ✅ | Repo-init and chmod are modeled as **effects** (`RunProcess` for git, the new `SetExecutable`) produced by the pure `update`/finalize and performed only at the edge interpreter (constitution V). Sensing (probe exit code) flows back as `EffectInterpreted`; the decision to `git init` is a pure transition. No I/O in `update`. |
| VI. Test evidence | ✅ | Real `git` + real filesystem fixtures (no mocks): US1 asserts a real `.git` at the product root; US2 asserts a real executable bit on a produced `.sh`; US3 drives a real "already inside a work tree" and a git-absent path; determinism over real provenance bytes. Synthetic fixtures disclosed by name. |
| VII. One contract for agents + humans | ✅ | New observable behavior on the scaffold path → the CLAUDE/AGENTS/2× SKILL scaffold descriptions gain a one-line "scaffold now owns repo-init + script executability post-instantiation" note, kept aligned across Claude and Codex. No new agent *workflow*; the lifecycle shape is unchanged (`nextLifecycleCommand Scaffold = None`). |
| VIII. Observability & safe failure | ✅ | Every step emits an actionable, **non-fatal** fact: repo-init initialized / skipped-existing-repo / skipped-git-unavailable; exec count + any skipped/partial. Malformed-input vs provider-defect split is unchanged; the convenience steps degrade explicitly and never fail the scaffold (FR-003/010, constitution VIII). |

**Change tier**: **Tier 1 (contracted change)** — see Summary. The added contract surface
(effect case, summary fields, diagnostics, projections, behavior) is justified by the
Accepted cross-repo contract; **Complexity Tracking is empty** (no constitution violation —
the new DU case and edge are the idiomatic MVU expression of the required I/O).

**Lifecycle-feature plan checklist** (constitution §Development Workflow):
- *Authored artifacts*: none new (scaffold authors none; the skeleton is `init`'s, unchanged).
- *Structured machine contracts*: `scaffold-provenance.json` (v1) and `providers.yml` (v1)
  **unchanged**; the report JSON gains additive sensed fields (no schema-version bump — the
  report is a versioned `CommandReport`, and additive optional facts are within `ReportVersion`
  policy; confirmed in Phase 1).
- *Generated views*: unchanged. `refresh` continues to **exclude** `generatedProduct` paths;
  the new `.git`/exec side-effects touch no SDD generated view.
- *Schema version & migration*: no schema touched → no migration. Release catalog
  (`docs/release/`) unchanged (no new produced artifact; `.git` is not a lifecycle artifact).
- *Agent behavior (Claude & Codex)*: one-line behavior note added to both surfaces.
- *Optional Governance integration*: none; produced files stay `generatedProduct`, out of
  SDD's and Governance's freshness scope; `.git` is not catalogued.
- *Tests/fixtures for stale/conflicting artifacts*: provider-failure, SDD-tree intrusion,
  empty-success, already-in-work-tree, git-absent, read-only-FS, dry-run, and re-run/`--force`
  idempotence are all exercised (Edge Cases).

## Project Structure

### Documentation (this feature)

```text
specs/032-scaffold-repo-init-chmod/
├── plan.md              # This file
├── research.md          # Phase 0 — exit-code-only git sensing; SetExecutable vs chmod;
│                         #   stateless three-tick staging; .sh discovery; sensed-metadata goldens
├── data-model.md        # Phase 1 — repo-init step outcome, make-executable step outcome,
│                         #   the SetExecutable effect, the post-instantiation staging state machine
├── quickstart.md        # Phase 1 — run the repo-init/exec suite; expected outcomes;
│                         #   how to reproduce each skip path (existing repo, git absent, no scripts)
├── contracts/           # Phase 1 — behavior contracts (not new external interfaces)
│   ├── repo-init-step.md           # exit-code sensing, the three outcomes, FR-004 capture, idempotence
│   ├── make-executable-step.md     # .sh discovery, SetExecutable edge, no-op & skip/partial
│   ├── post-instantiation-staging.md # the probe→decide→init→finalize tick sequence; success-only gate
│   └── report-projection.md        # the new fields across JSON/text/rich; determinism (FR-011/012)
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/
├── CommandTypes.fsi / CommandTypes.fs
│   ├── CommandEffect            # + SetExecutable of path: string
│   └── ScaffoldSummary          # + RepoInitOutcome + ExecutableScriptCount (+ skipped/partial)
├── CommandEffects.fs            # interpret: + SetExecutable arm (File.SetUnixFileMode, try/skip)
├── CommandWorkflow/HandlersScaffold.fs
│   ├── finalizeScaffold         # split: success defers final summary; failure terminal as today
│   └── computeScaffoldNext      # + post-instantiation ticks (probe → init-decision → finalize)
├── CommandSerialization.fs      # writeScaffold: + repo-init outcome + exec count fields
└── CommandRendering.fs          # renderText: + repo-init + exec lines (rich inherits)

src/FS.GG.SDD.Artifacts/
├── Diagnostics.fsi / Diagnostics.fs   # + advisory repo-init-skipped / exec-skipped facts

tests/fixtures/scaffold-provider/
├── with-script/                 # NEW — fixture producing an app tree incl. a `.sh` script (US2)
│   ├── .template.config/template.json
│   ├── App.fsproj
│   └── run.sh                   # neutral script name; substitutes no provider identity
└── registries/with-script.providers.yml  # NEW — points at the with-script fixture

tests/FS.GG.SDD.Commands.Tests/
├── ScaffoldCommandTests.fs      # + US1 repo-init (real .git at root, captures tree+provenance),
│                                #   US2 exec (real x-bit on produced .sh; no-op when none),
│                                #   US3 safeguards (already-in-work-tree, git-absent),
│                                #   FR-008 dry-run describes-not-performs, FR-009 failure paths,
│                                #   FR-012 determinism, FR-013 re-run/--force idempotence
└── ScaffoldGuardTests.fs        # re-assert SC-005 over the new handler/projection code

tests/FS.GG.SDD.Cli.Tests/
├── ScaffoldParityTests.fs       # + three-projection repo-init/exec fact parity (FR-011)
└── PublicSurface.baseline       # regenerated (effect case + summary fields)

tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline   # regenerated
```

**Structure Decision**: Single-solution F# layout retained. Unlike feature 031 (verification
only), this feature **adds production behavior** to the scaffold path. The change is confined
to the scaffold handler, the effect edge, and the scaffold report projections; the public
surface deltas (one effect case, the summary fields, the new diagnostics) are declared in
`.fsi` first and re-baselined. No new project, command, or schema is introduced.

## Complexity Tracking

No Constitution Check violations — the new `SetExecutable` effect and the additional staged
ticks are the idiomatic MVU expression of the contract-required I/O. This section is
intentionally empty.
</content>
</invoke>
