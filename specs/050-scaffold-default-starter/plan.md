# Implementation Plan: Honor Provider-Declared Default Starter Selection in Scaffold

**Branch**: `050-scaffold-default-starter` | **Date**: 2026-06-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/050-scaffold-default-starter/spec.md`

## Summary

Issue FS-GG/FS.GG.SDD#44 asks SDD (owner of scaffold default-selection) to honor a
provider-declared default starter so a Templates-side `app → game` default flip takes
effect with zero generic-SDD code change. `game`/`app` are provider-specific values
generic SDD is constitutionally forbidden to carry, so this feature delivers SDD's
**in-boundary half**: lock and prove the generic *default-starter-selection capability*,
record the chosen starter durably for audit, document it value-agnostically, and prove it
end-to-end against the real published provider — while redirecting the literal data flip
cross-repo (FR-009).

The *selection mechanism* already exists and satisfies FR-001/FR-002 in code:
`effectiveParameters` (`HandlersScaffold.fs:85-92`) seeds the effective map from each
descriptor parameter's declared `Default`, then folds author `--param` over it so an
explicit value always wins, and forwards the result verbatim as `--<key> <value>` to the
provider. The **gap** is FR-003 auditability: neither `.fsgg/scaffold-provenance.json`
(`ScaffoldProvenanceRecord`) nor the scaffold report (`ScaffoldSummary`) records the
*effective parameter values* today — they are only materialized into the dry-run
planned-command string. Because Story 1 scenario 3 lets the registry default change over
time, recording only provider/templateRef cannot reconstruct which starter a past run
used. **Decision (confirmed with spec author): add an additive effective-parameters field
to both the provenance record and the scaffold report**, so the chosen starter is
auditable and reproducible.

Net scope: one additive Tier-1 contract change (effective-params field on provenance +
scaffold report across json/text/rich), regression tests that lock default-apply and
`--param` override precedence, a generic default-declaring test fixture, value-agnostic
provider-authoring docs, the network-gated composition-acceptance default-starter path,
the FR-004/SC-003 grep boundary guard, and a cross-repo response on #44 redirecting the
`app → game` data flip to FS.GG.Templates.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (constitution Engineering Constraints)

**Primary Dependencies**: System.Text.Json (deterministic `Utf8JsonWriter` contracts),
YamlDotNet-equivalent snapshot parsing already in `LifecycleArtifacts.Config`,
Spectre.Console (rich projection only), xUnit v2 (tests).

**Storage**: Files only — `.fsgg/providers.yml` (author/provider-owned registry input),
`.fsgg/scaffold-provenance.json` (SDD-owned produced artifact, schema v1).

**Testing**: xUnit across `FS.GG.SDD.Artifacts.Tests`, `FS.GG.SDD.Commands.Tests`,
`FS.GG.Contracts.Tests`, and the opt-in/network-gated `FS.GG.SDD.Acceptance.Tests`.
Real filesystem/process fixtures under `tests/fixtures/scaffold-provider/*`.

**Target Platform**: Linux/macOS/Windows CLI (`fsgg-sdd`); offline inner loop is the default.

**Project Type**: Single CLI product (F# library projects + CLI entry), structure below.

**Performance Goals**: N/A — deterministic generation; no hot path introduced.

**Constraints**: Deterministic byte-stable JSON; `--rich` excluded from golden contracts and
degrades to zero-ANSI when non-interactive/`NO_COLOR`/`TERM=dumb`; offline default touches
no network; **zero** provider-specific values (`game`, `app`-as-starter, rendering package
ids/template ids/paths/docs URLs) in generic SDD source or generic-contract tests/fixtures.

**Scale/Scope**: One additive field on two record types + three projections + parser; ~1 new
fixture registry; regression + golden test updates; 1 composition-acceptance assertion;
docs edits to two pages; 1 cross-repo issue response. No new command, no new lifecycle stage.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — PASS. Public surface changes are in
  `.fsi` first: `ScaffoldProvenance.fsi` (record field) and `CommandTypes.fsi`
  (`ScaffoldSummary` field). Semantic tests (default-apply, override precedence, provenance
  round-trip, projection goldens) precede the `.fs` body changes.
- **II. Structured Artifacts Are the Machine Contract** — PASS. The effective-parameters map
  is structured data on the provenance artifact and the report JSON (authoritative); docs
  prose mirrors it read-only. The registry `parameters[].default` is the authoritative source
  of the default; `--param` override precedence and the recorded effective value are the
  machine contract. Conflict rule stated in research.md.
- **III. Visibility in `.fsi`** — PASS. Both new fields are declared in the paired `.fsi`
  signatures; surface-area baselines updated (see Phase 1).
- **IV. Idiomatic Simplicity** — PASS. Field is a `Map<string,string>` / sorted `(string*string)
  list`; no new abstraction, framework, or advanced F# feature. Deterministic ordering by key.
- **V. Elmish/MVU Boundary** — PASS. No new I/O edge; the change rides the existing scaffold
  MVU finalize path (`finalizeScaffold` → `provenanceWriteEffect` / summary constructors).
  Provenance write stays a single `WriteFile` effect; field is computed in the pure transition.
- **VI. Test Evidence Is Mandatory** — PASS. Real-fixture tests fail before, pass after:
  a new default-declaring registry fixture + provenance/JSON goldens. Golden coverage for the
  additive field on provenance + scaffold JSON + text projection.
- **VII. Agent And Human Workflows Share One Contract** — PASS. Same provenance/report contract
  for CLI/CI/agents; provider-authoring docs updated for both Claude and Codex surfaces; no
  second source of truth.
- **VIII. Observability And Safe Failure** — PASS. Edge cases (required param with a declared
  default still surfaces `scaffold.providerParamMissing`; blank/whitespace default surfaced not
  masked; unknown values forwarded verbatim, never interpreted) preserved and tested.

**Change tier**: **Tier 1** (contracted change — schema/generated-view/command-output:
additive field on `scaffold-provenance.json` and scaffold report JSON). Requires spec, plan,
tasks, `.fsi`, tests, docs, and migration notes. Recorded in spec; migration posture in
research.md.

**Boundary gate (FR-004 / SC-003, feature 030 re-assertion)**: PASS by construction — no
provider-specific value enters generic SDD; a grep guard test enforces it. No violation.

**Result**: PASS. No entries in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/050-scaffold-default-starter/
├── plan.md              # This file (/speckit-plan output)
├── spec.md              # Feature specification
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── scaffold-provenance-effective-parameters.md
│   ├── scaffold-report-effective-parameters.md
│   └── provider-default-starter-selection.md
├── checklists/          # (pre-existing)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Contracts/
│   └── Provider.fs / .fsi              # ProviderParameterSpec { Key; Required; Default } — UNCHANGED (already carries `default`)
├── FS.GG.SDD.Artifacts/
│   ├── ScaffoldProvenance.fs / .fsi    # ScaffoldProvenanceRecord: ADD EffectiveParameters; serialize + tryParse
│   ├── LifecycleArtifacts/Config.fs    # parseProviderRegistry reads parameters[].default — UNCHANGED
│   └── Diagnostics.fs / .fsi           # scaffold.* catalog — UNCHANGED
├── FS.GG.SDD.Commands/
│   ├── CommandTypes.fsi                # ScaffoldSummary: ADD EffectiveParameters field
│   ├── CommandWorkflow/HandlersScaffold.fs  # effectiveParameters (precedence — UNCHANGED); populate field in summary + provenance constructors
│   ├── CommandSerialization.fs         # writeScaffold: emit effective params (json contract)
│   └── CommandRendering.fs             # scaffold text/plain projection: emit effective params
└── FS.GG.SDD.Cli/                      # Program.fs — UNCHANGED

tests/
├── FS.GG.SDD.Artifacts.Tests/         # ProviderRegistryParseTests (default parse), ScaffoldProvenance round-trip + golden
├── FS.GG.SDD.Commands.Tests/          # ScaffoldCommandTests: default-apply, --param override, JSON/text goldens
├── FS.GG.Contracts.Tests/             # schema-version constant (additive posture)
├── FS.GG.SDD.Acceptance.Tests/        # AcceptanceSupport.fs scaffoldRequest + CompositionResult + golden (default-starter path)
└── fixtures/scaffold-provider/registries/
    └── default-declaring.providers.yml # NEW generic fixture: a non-required param WITH a declared `default` (value-agnostic)

docs/
├── release/schema-reference.md         # document provenance effectiveParameters + providers.yml parameter `default`
├── release/migrations/                 # NEW Tier-1 migration note: additive effectiveParameters on scaffold-provenance.json (schema stays v1)
└── reference/authoring-contracts.md    # add value-agnostic default-starter selection + --param precedence (FR-005)

CLAUDE.md / AGENTS.md                    # Constitution VII lockstep: note scaffold records effective forwarded parameters (value-agnostic)
```

**Structure Decision**: Single CLI product (Option 1), reusing the established
`FS.GG.Contracts` / `FS.GG.SDD.Artifacts` / `FS.GG.SDD.Commands` / `FS.GG.SDD.Cli` layout.
The provider *contract* type (`ProviderParameterSpec`) already carries `Default` and needs
no change; the additive field lands on the SDD-owned **produced** artifact
(`ScaffoldProvenanceRecord`) and the SDD-owned **report** (`ScaffoldSummary`), keeping the
cross-repo contract surface stable.

## Complexity Tracking

> No constitution violations. Section intentionally empty.
