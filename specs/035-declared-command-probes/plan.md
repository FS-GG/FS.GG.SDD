# Implementation Plan: Declared-or-Default Acceptance Build/Run Probes

**Branch**: `035-declared-command-probes` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/035-declared-command-probes/spec.md`

## Summary

Make the composition acceptance harness's **build probe** and **run probe**
(`tests/FS.GG.SDD.Acceptance.Tests/AcceptanceSupport.fs`) invoke a
**declared-or-default** command: when an optional provider-declared command is
supplied, the probe invokes it; otherwise it falls back to a platform-standard
`dotnet` default. The default form normalizes to `dotnet build` over the product
root (unchanged) and `dotnet run --project <discovered>` over a deterministically
discovered runnable project (today: `dotnet run --no-build` at the root). No
provider declares a command yet, so the opt-in, network-gated suite is observably
unchanged (FR-005 / SC-001).

Technical approach: introduce a small **pure resolver** that maps
`(declared command option, product root)` to a concrete `ProbeCommand`
(executable, arguments, working directory), keeping the existing process-shell
**edge** (`startProcess` / `runToCompletion` and the run-probe grace/overall
loop) as the I/O interpreter. The declared-command record is shaped to be a
1:1 forward-compatible read of the H2 `ProviderDescriptor` build/run fields
(FS-GG/FS.GG.SDD#8), so adopting H2 is pure wiring (SC-004). The harness stays
provider-agnostic: defaults name only `dotnet`, and the T021a no-Governance
invariant continues to hold (FR-009 / SC-003).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: xUnit (v2 API, pinned v3-era VSTest adapter);
`System.Diagnostics.Process`; existing `FS.GG.SDD.Commands` / `FS.GG.SDD.Artifacts`
references already on the acceptance project. No new package dependency.

**Storage**: N/A. Probes operate over a temp product root; the
`composition-acceptance-result` document is written as today (unchanged shape).

**Testing**: `dotnet test` over `FS.GG.SDD.sln`. New coverage runs in the **default
offline inner loop** (pure-resolver unit tests + a synthetic-command execution
test). The real-provider composition path stays network-gated on
`FSGG_SDD_ACCEPTANCE_REGISTRY`.

**Target Platform**: Linux developer/CI host (the acceptance harness is
developer-facing).

**Project Type**: Single project — a test/harness library
(`tests/FS.GG.SDD.Acceptance.Tests`). No production package surface changes.

**Performance Goals**: Bounded execution only — build probe 300 s completion
timeout; run probe 10 s grace + 60 s overall cap (unchanged). No probe path may
hang the suite (SC-005).

**Constraints**: Zero observable change to the default path (FR-005); no
Governance or rendering identity / provider-specific id, template, path, command,
or docs URL in the harness or defaults (FR-009); deterministic run-project
discovery (FR-008).

**Scale/Scope**: Touches only the acceptance project: `AcceptanceSupport.fs` (the
probes + a new resolver), its tests, and the
`specs/034-scaffold-composition-acceptance/contracts/acceptance-protocol.md`
behavioral contract. No change to the scaffold command, provider contract schema,
provenance record, or any lifecycle artifact (spec Scope boundary).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — PASS. Spec exists; the
  public shape (the `ProbeCommand`/declared-command resolver) is sketched in
  `contracts/` and `data-model.md` before implementation; tests precede the `.fs`
  body. The acceptance project is a test harness with **no `.fsi` files today**
  (established pattern, constitution III applies to *public package* modules);
  this feature adds no public `FS.GG.SDD.*` package surface, so no signature file
  or surface-area baseline obligation is triggered.
- **II. Structured Artifacts Are the Machine Contract** — PASS. The machine
  contract here is the **acceptance protocol** (`acceptance-protocol.md`) and the
  emitted `composition-acceptance-result` JSON; the result document shape is
  unchanged, and the protocol's build/run-probe section is updated to describe
  declared-or-default resolution. No prose/data conflict introduced.
- **III. Visibility Lives in `.fsi`** — N/A (no public package module changes; the
  harness declares no `.fsi`, consistent with the existing acceptance project).
- **IV. Idiomatic Simplicity Is the Default** — PASS. Plain F#: one record
  (`ProbeCommand`), one optional declared-command record, one pure resolver
  function per probe, reusing the existing edge helpers. No custom operators,
  SRTP, reflection, or computation expressions.
- **V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows** — PASS with
  justification. The probes live at the **test edge**, not in a lifecycle MVU
  loop (matching today's harness). The change keeps I/O at the edge
  (`startProcess`/`runToCompletion`) and extracts the decision into a **pure**
  resolver (declared-or-default → `ProbeCommand`), which is the testable
  transition. Per principle V, "simple … validators do not need MVU ceremony";
  the resolver is a pure validator/selector and the edge interpreter already
  exists. No new MVU scaffolding is warranted.
- **VI. Test Evidence Is Mandatory** — PASS. New tests fail before / pass after:
  declared-beats-default, empty/whitespace→default, deterministic discovery, and
  the failure-mode diagnostics (cannot-start / non-zero / timeout). Synthetic
  declared commands are disclosed in test names (constitution VI).
- **VII. Agent And Human Workflows Must Share One Contract** — N/A (no
  agent-authored Markdown or generated view changes).
- **VIII. Observability And Safe Failure** — PASS. Every probe failure mode
  yields a diagnosed, non-zero `ProbeResult` within its bound — cannot-start,
  non-zero exit, timeout, and (run default) no-runnable-project discovered — never
  a silent pass and never a hang (FR-007 / SC-005).

**Change tier**: Tier 1 — the change is against the `scaffold-provider` contract
surface (the acceptance protocol's build/run-probe behavior) and must be
forward-compatible with the H2 descriptor fields, even though **no public SDD F#
API or schema changes** in this feature. Migration posture: additive and
backward-compatible; the only state reachable today (no declared command)
resolves to the existing default.

**Result: PASS — no violations; Complexity Tracking left empty.**

## Project Structure

### Documentation (this feature)

```text
specs/035-declared-command-probes/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── probe-command.md # declared-or-default resolution contract + H2 forward-compat read shape
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

The shared, repo-level behavioral contract updated by this feature is
`specs/034-scaffold-composition-acceptance/contracts/acceptance-protocol.md`
(§"The build/run probe"); the new `contracts/probe-command.md` in this feature
specifies the resolver and read shape and points back to it.

### Source Code (repository root)

```text
tests/FS.GG.SDD.Acceptance.Tests/
├── AcceptanceSupport.fs          # CHANGED: add ProbeCommand + DeclaredCommand option,
│                                 #   resolveBuildCommand/resolveRunCommand (pure),
│                                 #   discoverRunnableProject (deterministic),
│                                 #   buildProbe/runProbe gain an optional declared arg
│                                 #   (default = today's behavior); edge unchanged
├── CompositionResult.fs          # UNCHANGED (result schema/verdict)
├── CompositionAcceptanceTests.fs # CHANGED: build/run probe call sites pass declared=None
│                                 #   (no behavior change); T021a invariant still holds
├── ProbeResolutionTests.fs       # NEW: offline pure-resolver + synthetic-command tests
└── FS.GG.SDD.Acceptance.Tests.fsproj # CHANGED: include ProbeResolutionTests.fs
```

**Structure Decision**: Single test-harness project. All production behavior of
this feature is confined to `tests/FS.GG.SDD.Acceptance.Tests`. The pure resolver
and discovery helpers are added to `AcceptanceSupport.fs` alongside the existing
probes; new offline tests go in a dedicated `ProbeResolutionTests.fs` so the
network-gated composition test file stays focused. No `src/` changes.

## Complexity Tracking

> No constitution violations. Section intentionally empty.
