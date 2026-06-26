# Implementation Plan: Scaffold Runnable Products via Template Providers

**Branch**: `030-scaffold-template-provider` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/030-scaffold-template-provider/spec.md`

## Summary

Add a dedicated, cross-cutting `fsgg-sdd scaffold` command that takes a product author
from an empty directory to a **buildable, runnable, SDD-managed product** in one
invocation. Scaffold first establishes the SDD lifecycle skeleton (reusing `init`'s
effects unchanged), then delegates runtime materialization to an **external template
provider** selected by reference, then records what was produced and who owns it.

Generic SDD owns only the **contract, the invocation protocol, the provenance record,
the diagnostics, and the three report projections** — never any provider-specific
package id, template id, path, or docs URL (FR-002 / SC-005). The provider is invoked
through a generic **`dotnet new` wrapper**: SDD resolves a provider *descriptor*
(author-supplied config/option, never hardcoded) to a template id + source + declared
contract version + parameter spec, shells `dotnet new <templateId> -o <target> -p:k=v …`,
diffs the target to enumerate produced paths, and maps the outcome.

Decisions locked with the user (see [research.md](./research.md)):

1. **Surface** — a new `scaffold` command (not a flag on `init`); `init` stays
   byte-identical, so SC-003 holds trivially.
2. **No-provider path** — `scaffold` **requires** `--provider`; with none it emits an
   actionable error pointing to `fsgg-sdd init` for skeleton-only. FR-005/US2 are
   delivered by `init`, left untouched.
3. **Invocation** — generic `dotnet new` wrapper driven by a provider-authored
   descriptor; no template-engine specifics leak into the generic contract surface.
4. **Reference provider** — SDD ships a **fixture provider** (a throwaway local
   `dotnet new` template under `tests/fixtures/`) that exercises the generic contract
   incl. every failure mode; **and** a real adapter is delivered in the **FS.GG.Rendering
   repo** wrapping its existing `dotnet new fs-gg-ui` template (cross-repo workstream,
   §Cross-repo deliverable).

This is a **Tier 1** change: new CLI command, new schema-versioned provider-descriptor
and scaffold-provenance contracts, a new `RunProcess` effect at the MVU edge, `.fsi`
surface + baselines, golden tests, docs, and migration/compat notes.

## Technical Context

**Language/Version**: F# on .NET, target `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: standard library + existing `System.Text.Json` writers and
`System.Diagnostics.Process` (for the `dotnet new` edge). No new NuGet dependency in
generic SDD. The reference provider depends on the .NET SDK template engine at runtime,
discovered through `dotnet`, never referenced as a package by SDD.

**Storage**: Filesystem only. New project-level artifact `.fsgg/scaffold-provenance.json`;
optional author-supplied `.fsgg/providers.yml` registry. Provider materializes runtime
files into the target directory (owned outside SDD).

**Testing**: `dotnet test FS.GG.SDD.sln` (4 test projects: Artifacts, Validation, Cli,
Commands), each carrying a `PublicSurface.baseline` snapshot. New: scaffold golden
fixtures under `tests/fixtures/` and a fixture `dotnet new` template provider. Real
end-to-end Rendering scaffolding is validated in the FS.GG.Rendering repo, not in the
SDD suite (keeps SDD green without the .NET template engine / native GL assets present).

**Target Platform**: Linux/cross-platform CLI (`fsgg-sdd`). The `dotnet new` edge is
cross-platform; provider availability is environment-sensed and degrades with a
diagnostic, never a crash (constitution VIII).

**Project Type**: Single solution, CLI + libraries (`src/FS.GG.SDD.*`), plus a
cross-repo deliverable in the sibling `FS.GG.Rendering` repo.

**Performance Goals**: N/A for SDD orchestration (one short-lived child process). The
provider's own build/run time is out of SDD's control and out of scope.

**Constraints**:
- `init` output stays **byte-identical** (SC-003): scaffold reuses `initEffects`
  verbatim; no edit to the init path changes a byte.
- Deterministic `--json` automation contract: the scaffold report and provenance are
  byte-stable (canonical key order, no clock/abs-path/ANSI). Produced-path lists are
  sorted; provider stdout/stderr is captured but excluded from the deterministic
  contract (sensed, environment-dependent) — surfaced only in diagnostics text.
- `--rich` is a pure projection (no JSON byte change; degrades per `NO_COLOR`/`TERM=dumb`/
  non-interactive).
- `WarningsAsErrors=FS3261;FS0025` ratchet stays at 0 (`Directory.Build.props:19`); no
  `#nowarn` introduced.
- **Zero** FS.GG.Rendering-specific package id / template id / path / docs URL in generic
  SDD source or in the generic contract's tests (SC-005, grep-verifiable).

**Scale/Scope**: 1 new command; ~2 new schema-versioned artifacts; 1 new MVU effect
(`RunProcess`); 1 new handler module (`HandlersScaffold.fs`); ~7 new diagnostics; 1 new
report summary (`ScaffoldSummary`) wired through all 3 projections; refresh-exclusion
read from provenance; agent surfaces (CLAUDE/AGENTS/2× SKILL) updated equivalently; 1
cross-repo provider adapter in FS.GG.Rendering.

### Grounded inventory (current tree, verified 2026-06-26 @ `a1c9847`)

| Concern | Anchor | Disposition |
|---|---|---|
| Command DU | `CommandTypes.fs:8` / `.fsi:8` (`SddCommand`) | + `Scaffold` case |
| Command name/stage/parse | `CommandTypes.fs:399,415,420` | + `scaffold` mappings |
| Lifecycle successor | `CommandTypes.fs:491` `nextLifecycleCommand` | + `Scaffold -> None` (cross-cutting, like `Agents`/`Refresh`; FR-015) |
| MVU effects | `CommandTypes.fs:357` `CommandEffect` | + `RunProcess of command * args * workingDir`; + capture in `CommandEffectResult` |
| Effect interpreter | `CommandEffects.fs:61` `interpret` | + `RunProcess` edge via `System.Diagnostics.Process`; honors `DryRun` |
| Skeleton effects | `Foundation.fs:81` `initEffects` | reused **unchanged** by scaffold |
| Plan dispatch | `Foundation.fs:275` `plan` | + `Scaffold` → skeleton effects + provider plan |
| Scaffold handler | new `CommandWorkflow/HandlersScaffold.fs` | provider resolve → validate → invoke → diff → provenance |
| Report type | `CommandTypes.fsi:335` `CommandReport` | + `Scaffold: ScaffoldSummary option` |
| Report build | `CommandReports.fs:1303` `buildReport` | + scaffold summary + produced-path changes (owner `generatedProduct`) |
| Ownership marking | `ArtifactRef.fs:6` `ArtifactOwner.GeneratedProduct` | **reused** for produced files |
| Provenance artifact | new `.fsgg/scaffold-provenance.json` | schema v1; serialized via `Json/JsonWriters.fs` conventions |
| Provider descriptor | new `.fsgg/providers.yml` (+ `--provider`/`--param`) | author/provider-owned; parsed in `LifecycleArtifacts/Config.fs` |
| Refresh exclusion | `CommandWorkflow/HandlersRefresh.fs` (`authoredPreserved`, canonical views) | + provenance paths excluded as externally owned (FR-007) |
| Diagnostics | `Artifacts/Diagnostics.fs(i)` + `CommandReports.fs` | + `scaffold.*` factories; user-input vs provider-defect split |
| JSON / text / rich | `CommandSerialization.fs:354`, `CommandRendering.fs:7`, `Cli/Rendering.fs:74` | + scaffold summary in all three |
| CLI parsing | `Cli/Program.fs:13-26,109-126` | + `--provider`, repeated `--param k=v`, `--force` |
| Release catalog | `docs/release/release-readiness.json`, `schema-reference.md` | + provenance + descriptor entries; declared posture |
| Agent surfaces | `CLAUDE.md`, `AGENTS.md`, `.claude/.../SKILL.md`, `.codex/.../SKILL.md` | describe `scaffold` + provider contract, equivalently |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → Tests → Impl | ✅ | New public surface (`Scaffold` case, `ScaffoldSummary`, descriptor/provenance types, scaffold diagnostics) declared in `.fsi` first; exercised via fixture-provider FSI/prelude before `.fs` hardens; semantic tests through the public command surface; then implementation. |
| II. Structured artifacts are the contract | ✅ | `scaffold-provenance.json` (schema v1) is authoritative; `providers.yml` is the machine selection contract. Prose↔structured conflicts resolved in [data-model.md](./data-model.md): the provenance JSON wins; the scaffold report records any divergence diagnostic. |
| III. Visibility lives in `.fsi` | ✅ | Every new public binding lands in the relevant `.fsi`; all 4 `PublicSurface.baseline` snapshots updated as part of the change (Tier 1). Internal handler bindings stay `internal`/`[<AutoOpen>]`. |
| IV. Idiomatic simplicity | ✅ | Records + DUs; one new effect; a thin process wrapper. No reflection/plugin loading, no template-engine library dependency. Mutation only inside the process-edge interpreter, commented. |
| V. Elmish/MVU boundary | ✅ | Scaffold is multi-step + external I/O: pure `plan`/`update` produce effects (`CreateDirectory`/`WriteFile`/`EnumerateDirectory`/`RunProcess`); the **edge interpreter** in `CommandEffects.fs` performs real fs + process I/O. New `RunProcess` effect keeps I/O at the edge. |
| VI. Test evidence | ✅ | Fail-before/pass-after semantic tests over real fixtures: a real local `dotnet new` fixture template (no mocks) drives success, empty-output, mid-run failure, unknown provider, unsupported version, missing param, collision, and SDD-tree-intrusion. Golden JSON/text snapshots for the new report + provenance. Synthetic stand-ins disclosed in test names. |
| VII. One contract for agents + humans | ✅ | CLAUDE.md, AGENTS.md, and both `SKILL.md` updated equivalently (SC-008); guidance describes the command + contract but is not a second source of truth. |
| VIII. Observability & safe failure | ✅ | Seven distinct, actionable `scaffold.*` diagnostics; malformed user input (exit 1) vs provider defect (exit 2) split reuses `exitCodeForReport`. Missing `dotnet`/template engine degrades with a diagnostic. Partial scaffold never reported as complete (FR-009). |

**Change tier**: **Tier 1 (contracted change)** — new command, schema, artifact layout,
and agent-skill contract. No Constitution Check violations → Complexity Tracking empty.

**Lifecycle-feature plan checklist** (constitution §Development Workflow):
- *Authored artifacts*: none new authored by scaffold (it consumes provider params); the
  SDD skeleton authored files are init's, unchanged.
- *Structured machine contracts*: `scaffold-provenance.json` (v1), `providers.yml` (v1),
  `ScaffoldSummary` in the report JSON.
- *Generated views*: none new; scaffold marks produced files **externally owned**, which
  refresh must **exclude** (FR-007) — the opposite of a generated view.
- *Schema version & migration*: both new artifacts start at `schemaVersion: 1`; additive
  to the report; migration/compat notes in `docs/release/` (§Phase 1).
- *Agent behavior (Claude & Codex)*: equivalent updates to all four surfaces.
- *Optional Governance integration*: none required; Governance remains optional and
  receives no new obligation. Produced files marked `generatedProduct` are out of SDD's
  and Governance's freshness scope.
- *Tests/fixtures for stale/conflicting artifacts*: malformed/stale provenance and a
  provider writing into SDD trees are covered by fixtures.

## Project Structure

### Documentation (this feature)

```text
specs/030-scaffold-template-provider/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions: surface, invocation, no-provider, reference scope, outcome model
├── data-model.md        # Phase 1 — entities, schema versions, diagnostics catalog, outcome state machine
├── quickstart.md        # Phase 1 — fixture-provider walkthrough + real Rendering end-to-end demo
├── contracts/           # Phase 1 — provider contract, provenance + descriptor schemas, CLI contract
│   ├── template-provider-contract.md
│   ├── scaffold-provenance.schema.md
│   ├── providers-descriptor.schema.md
│   └── cli-scaffold.md
├── checklists/
│   └── requirements.md  # Spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Artifacts/
├── ArtifactRef.fs(i)                 # reuse ArtifactOwner.GeneratedProduct (no change)
├── Diagnostics.fs(i)                 # + scaffold.* diagnostic factories
├── LifecycleArtifacts/
│   └── Config.fs(i)                  # + parse .fsgg/providers.yml (descriptor registry)
├── ScaffoldProvenance.fs(i)          # NEW — provenance record type + (de)serialization
└── Json/JsonWriters.fs               # reuse writers (no change)

src/FS.GG.SDD.Commands/
├── CommandTypes.fs(i)                # + Scaffold case, name/stage/parse, RunProcess effect,
│                                     #   nextLifecycleCommand Scaffold -> None, ScaffoldSummary,
│                                     #   CommandReport.Scaffold, CommandModel.Scaffold
├── CommandEffects.fs                 # + RunProcess edge interpreter (Process); DryRun honored
├── CommandWorkflow/
│   ├── Foundation.fs                 # + Scaffold dispatch in `plan` (initEffects + provider plan)
│   └── HandlersScaffold.fs           # NEW — resolve descriptor → validate version/params →
│                                     #   snapshot → RunProcess(dotnet new) → diff → SDD-tree guard →
│                                     #   provenance write → summary
│   └── HandlersRefresh.fs            # + exclude provenance paths (externally owned; FR-007)
├── CommandSerialization.fs           # + scaffold summary JSON (deterministic)
├── CommandRendering.fs               # + scaffold summary text
└── CommandReports.fs                 # + scaffold summary build, produced-path ArtifactChanges,
                                      #   outcome mapping, scaffold.* diagnostics wiring

src/FS.GG.SDD.Cli/
├── Program.fs                        # + scaffold dispatch; parse --provider/--param/--force
└── Rendering.fs                      # + scaffold summary rich projection

tests/
├── FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs   # NEW — fixture-driven semantics + outcomes
├── FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs # + scaffold byte-stable golden
├── FS.GG.SDD.Cli.Tests/                               # + scaffold rich/degradation parity
├── FS.GG.SDD.Artifacts.Tests/                         # + provenance + descriptor round-trip
├── */PublicSurface.baseline                           # updated (Tier 1)
└── fixtures/scaffold-provider/                         # NEW — real local `dotnet new` fixture template
                                                        #   + variants: ok / empty / fails-midway /
                                                        #   bad-version / writes-into-.fsgg

docs/release/
├── release-readiness.json            # + scaffold-provenance.json + providers.yml catalog entries
├── schema-reference.md               # + scaffold artifacts; declared posture for externally-owned output
├── compatibility-matrix.md           # + Governance-handoff note (produced files out of freshness scope)
└── migrations/                       # + additive note (new command + artifacts; init unchanged)

CLAUDE.md / AGENTS.md
.claude/skills/fs-gg-sdd-project/SKILL.md
.codex/skills/fs-gg-sdd-project/SKILL.md   # all four: describe `scaffold` + provider contract, equivalently
```

### Cross-repo deliverable (FS.GG.Rendering)

Delivered and owned **outside** generic SDD (FR-014), validated by a Rendering-repo test,
not the SDD suite:

```text
FS.GG.Rendering/
├── <provider descriptor>             # provider-authored: declares contract version, template id
│                                     #   (`fs-gg-ui`), source, and param spec for SDD to resolve
├── .template.config/template.json    # existing fs-gg-ui template (ensure no writes into .fsgg/work/readiness)
└── tests/ (Rendering repo)           # end-to-end: `fsgg-sdd scaffold --provider …` → build + run the app
```

**Structure Decision**: Single-solution F# layout retained. New work is confined to the
modules above plus the four agent surfaces and `docs/release/`. The provider descriptor
and the runnable template live in FS.GG.Rendering; SDD's tests depend only on the
in-repo fixture provider, so the SDD suite never imports Rendering specifics (SC-005).

## Complexity Tracking

No Constitution Check violations — this section is intentionally empty.
