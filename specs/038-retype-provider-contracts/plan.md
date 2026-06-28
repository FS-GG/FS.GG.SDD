# Implementation Plan: Re-type Provider Registry onto FS.GG.Contracts & Honor Declared Probe Commands

**Branch**: `038-retype-provider-contracts` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/038-retype-provider-contracts/spec.md`

## Summary

Retire the local provider re-encoding in `FS.GG.SDD.Artifacts` and re-type
`parseProviderRegistry` (and every scaffold-path consumer) onto the canonical
`Fsgg.Provider` types shipped in `FS.GG.Contracts` 1.0.0 (feature 036). The
re-typed parser additionally reads the contract's extended optional fields —
declared `build`/`test`/`run`/`verify` commands and `nameParameter` — from
`.fsgg/providers.yml` with behavior-preserving defaults. Finally, flow the
resolved descriptor's declared `Build` and `Run` commands into the opt-in
composition acceptance probes (which feature 035 already made declared-or-default
ready) and retire the harness's local `DeclaredCommand` copy in favor of the
canonical one.

Technical approach: add a `ProjectReference` to `FS.GG.Contracts` from
`FS.GG.SDD.Artifacts` (a clean leaf dependency — Contracts is BCL-only), delete
`Config.ProviderDescriptor` / `Config.ProviderParameterSpec`, `open Fsgg.Provider`,
and extend the existing YamlDotNet-based parse to populate the four declared-command
fields and `NameParameter`. The scaffold handler and acceptance harness recompile
against the canonical record (identical field names for the five preserved fields,
so the only edits are the new namespace and the new reads). No new public schema
version is introduced; every registry expressible under today's shape parses and
scaffolds byte-identically.

This is a **Tier 1** change against the `scaffold-provider` contract surface that
*adopts* the already-published `FS.GG.Contracts` 1.0.0 provider types.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (per `Directory.Build.local.props`).

**Primary Dependencies**: `FSharp.Core` (10.1.301, centrally managed), `YamlDotNet`
(16.3.0) for registry parsing, `FS.GG.Contracts` 1.0.0 (`Fsgg.Provider`) — adopted
by this feature.

**Storage**: Files only — `.fsgg/providers.yml` (provider registry, YAML);
`.fsgg/scaffold-provenance.json` and `composition-acceptance-result` JSON
(unchanged by this feature).

**Testing**: xUnit 2.9.3 (`dotnet test FS.GG.SDD.sln -c Release`). Offline inner
loop plus the opt-in, network-gated acceptance suite
(`FSGG_SDD_ACCEPTANCE_REGISTRY`).

**Target Platform**: Cross-platform .NET CLI (`fsgg-sdd`); Linux dev/CI.

**Project Type**: Single .NET solution — multiple F# library/CLI/test projects
(`src/`, `tests/`).

**Performance Goals**: N/A (parse + process-spawn, not on a hot path). Probe
bounded-execution windows are unchanged from feature 035 (build 300 s; run 10 s
grace / 60 s overall).

**Constraints**: Zero JSON-byte / report change for any registry expressible under
today's shape (FR-006, SC-002); no new public schema version (FR-012); the
acceptance harness stays provider-agnostic — no Governance/rendering identity and
no provider-specific package id / template id / path / command / docs URL
(invariant T021a, FR-011, SC-006).

**Scale/Scope**: Touches three production files (`Config.fsi`/`.fs`,
`FS.GG.SDD.Artifacts.fsproj`), the scaffold handler recompile
(`HandlersScaffold.fs`), the acceptance harness
(`AcceptanceSupport.fs`, `CompositionAcceptanceTests.fs`,
`FS.GG.SDD.Acceptance.Tests.fsproj`), plus tests and YAML fixtures. No CLI surface,
provenance record, or report projection changes.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|-----------|------------|
| **I. Spec → FSI → FSI exercise → tests → impl** | PASS — `Config.fsi` is edited first (remove local types, re-type the `parseProviderRegistry` return to the canonical `ProviderDescriptor`); the canonical surface was already exercised/shipped in 036. |
| **II. Structured artifacts are the machine contract** | PASS — the provider registry is structured-only (no competing prose source); the schema-version gate and drop-incomplete rules are preserved exactly (FR-007). |
| **III. Visibility lives in `.fsi`** | PASS — `Config.fsi` is the sole surface edit. The method-name-only surface baselines (`PublicSurface.baseline` for Artifacts & Commands capture only static-method *names*) are unaffected because `parseProviderRegistry`'s name is unchanged; the canonical types' baseline already lives in `FS.GG.Contracts.Tests` and is unchanged. |
| **IV. Idiomatic simplicity** | PASS — plain records, `Option`, and the existing YamlDotNet helpers; no custom operators, SRTP, reflection, or new computation expressions. |
| **V. Elmish/MVU boundary** | PASS — `parseProviderRegistry` is a pure parser (no MVU needed); the scaffold handler is already MVU; the probes already own their process-spawning edge interpreter (feature 035). No new I/O boundary is introduced. |
| **VI. Test evidence is mandatory** | PASS — new tests fail before / pass after: registry parsing of the extended fields, blank-executable handling, and probe-honors-declared-command (synthetic, disclosed). The existing scaffold/provenance golden matrix proves zero regression. |
| **VII. Agent & human share one contract** | PASS — no agent-command or skill behavior changes; no lifecycle command behavior changes. Only the agent context plan pointer is refreshed. |
| **VIII. Observability & safe failure** | PASS — provider diagnostics and schema-version diagnostics are unchanged; a blank declared executable degrades to the default rather than launching an empty process (FR-005). |

**Engineering Constraints:**

- `net10.0`, F# — unchanged. PASS.
- Namespace: adopting the org-shared `FS.GG.Contracts` (`Fsgg`) package is the
  explicit constitutional carve-out (v1.1.0); this feature is exactly its intended
  use — retiring an SDD-local re-encoding onto the shared source of truth. PASS.
- `FS.GG.Contracts` is consumed via `ProjectReference` (same in-repo form as
  `FS.GG.Contracts.Tests`), not a versioned `PackageReference`. PASS.
- SDD remains useful without Governance; no Governance runtime added. PASS.
- No FS.GG.Rendering identity enters generic SDD or the acceptance harness. PASS.

**Change Classification: Tier 1.** Spec ✓, plan ✓, tasks (next), `.fsi` updated ✓,
tests ✓, docs (no schema-reference change — adopt-only, no new artifact), migration
notes: none required (additive optional YAML keys; existing registries unaffected).

**Result: PASS — no violations; Complexity Tracking not required.**

## Project Structure

### Documentation (this feature)

```text
specs/038-retype-provider-contracts/
├── plan.md              # This file
├── research.md          # Phase 0 output — resolved design decisions
├── data-model.md        # Phase 1 output — canonical descriptor & registry mapping
├── quickstart.md        # Phase 1 output — validation scenarios
├── contracts/           # Phase 1 output
│   ├── provider-registry-encoding.md   # .fsgg/providers.yml key encoding (v1)
│   └── probe-command-flow.md           # descriptor → probe declared-command flow
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Contracts/                       # canonical Fsgg.Provider types (036) — referenced, unchanged
│   ├── Provider.fsi                        #   ProviderDescriptor / ProviderParameterSpec / DeclaredCommand
│   └── Provider.fs                         #   defaultNameParameter / resolveNameParameter / isMalformed
└── FS.GG.SDD.Artifacts/
    ├── FS.GG.SDD.Artifacts.fsproj          # ADD ProjectReference → FS.GG.Contracts
    └── LifecycleArtifacts/
        ├── Config.fsi                      # REMOVE local types; re-type parseProviderRegistry return
        └── Config.fs                       # open Fsgg.Provider; read build/test/run/verify + nameParameter

src/FS.GG.SDD.Commands/CommandWorkflow/
└── HandlersScaffold.fs                     # recompile against canonical descriptor (no behavior change)

tests/
├── FS.GG.SDD.Artifacts.Tests/
│   └── ScaffoldProvenanceTests.fs          # extend: parse declared commands + nameParameter + blank exec
├── FS.GG.SDD.Acceptance.Tests/
│   ├── FS.GG.SDD.Acceptance.Tests.fsproj   # ADD ProjectReference → FS.GG.Contracts
│   ├── AcceptanceSupport.fs                # DELETE local DeclaredCommand; retype probes to Fsgg.Provider.DeclaredCommand
│   ├── CompositionAcceptanceTests.fs       # resolve descriptor; flow Build/Run into probes (was None)
│   └── ProbeResolutionTests.fs             # extend: synthetic Fsgg.Provider descriptor → probe honors declared
└── fixtures/scaffold-provider/registries/
    └── *.providers.yml                     # ADD a fixture declaring build/run/nameParameter
```

**Structure Decision**: Existing single-solution layout (`FS.GG.SDD.sln`). The
work is concentrated in `FS.GG.SDD.Artifacts` (the one owner of provider parsing)
plus the acceptance test project; the scaffold handler and CLI recompile without
edits beyond namespace resolution.

## Complexity Tracking

> No constitution violations — section intentionally empty.
