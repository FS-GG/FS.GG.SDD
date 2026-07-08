# Implementation Plan: Prompt the Coherent-Set Version Bump on a Classified Shipped-Surface Mutation

**Branch**: `item/171-publishing-prompt-the-coherent-set-versi` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/094-surface-version-bump/spec.md`

**Tracks**: FS.GG.SDD#171 · Design: FS-GG/.github **ADR-0025** (reconcile step 3a) · Epic FS-GG/.github#235, issue #236

## Summary

`fsgg-sdd surface` already **detects** a shipped-surface mutation (086) and **classifies** it
additive-vs-breaking (087). This feature adds the third step ADR-0025 assigns to the publishing layer:
tell the operator what that classification implies for the repo's **coherent-set version axis**, and
what the axis currently reads — then stop, and let the operator confirm.

The implementation is small because the classification already exists and is already computed *before*
the `--update` baseline writes are applied (research R1). The work is: read one MSBuild property out of
one file, apply a three-case bump function, nest the result in the `surface` report block, and emit one
advisory warning.

Three decisions carry the design:

1. **The axis is workspace-declared, never embedded.** Two `--param` keys (`versionAxisFile`,
   `versionAxisProperty`) with convention defaults, resolved through the existing `Foundation.surfaceParam`.
   `FsGgAudioVersion` and its siblings never enter `src/**` (FR-003 / SC-003).
2. **`--update` prompts too.** It is the run that *erases* the drift; a prompt only on `--check` would
   never be seen by the normal PR workflow (AMB-002 / US2).
3. **The prompt is a warning, not an error** — because SDD genuinely cannot prove the bump was not
   already applied (AMB-004). Making it blocking would be dishonest, and would break 087's explicit
   "never changes the exit code" contract.

## Technical Context

**Language/Version**: F# on .NET 10 (`net10.0`, `LangVersion=preview`)

**Primary Dependencies**: BCL only. `System.Xml.Linq` (`XDocument`) for the axis read — the first XML
consumption in `src/` (R8), no package reference needed. Reuses `Fsgg.Version.tryParse` from
`FS.GG.Contracts` (read-only; no surface change there).

**Storage**: N/A — nothing is persisted. `surface` writes only `docs/api-surface/**` baselines under
`--update`, unchanged from 086.

**Testing**: xUnit. `tests/FS.GG.SDD.Commands.Tests/SurfaceCommandTests.fs` (handler + MVU effect set),
`tests/FS.GG.SDD.Cli.Tests/SurfaceProjectionTests.fs` (three projections),
`tests/FS.GG.SDD.Artifacts.Tests/DiagnosticTests.fs` (id/severity contract). Real filesystem fixtures
per Principle VI.

**Target Platform**: CLI (`fsgg-sdd`), Linux/macOS/Windows

**Project Type**: CLI + libraries (Elmish/MVU command workflow)

**Performance Goals**: N/A. One additional `ReadFile` effect per `surface` invocation.

**Constraints**: Zero writes to the version axis (FR-012). Zero exit-code change (FR-013). Byte-deterministic
`--json`/`--text` (FR-014). No provider literal in `src/**` (FR-003).

**Scale/Scope**: ~8 non-spec source/test files. No persisted schema change; no new top-level report block.

## Constitution Check

*GATE: passed before Phase 0; re-checked after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | PASS | `CommandTypes.fsi` and `Diagnostics.fsi` are authored before the `.fs`. Tests precede implementation per the task order below. |
| **II. Structured Artifacts Are the Machine Contract** | PASS | The `--json` `surface.versionBump` object is the contract; `--text`/`--rich` are projections. Stable key set, explicit `null` (R6). |
| **III. Visibility Lives in `.fsi`** | PASS | `VersionBumpPrompt` is declared in `CommandTypes.fsi`; `applyBump`/`readVersionAxis` stay `private` inside `HandlersSurface` (no `.fsi` — that module has none, matching 086/087). |
| **IV. Idiomatic Simplicity** | PASS | One `XDocument.Parse` + one `Seq.tryFind` on `LocalName`. No reflection, no type provider, no MSBuild API, no custom operator, no SRTP. **No justification section required.** |
| **V. Elmish/MVU Is the Boundary** | PASS | The axis read is a `ReadFile` **effect** appended to `Foundation.surfaceReadEffects` (the existing first wave). `computeSummary` stays pure over interpreted snapshots — no `File.ReadAllText` in the handler. |
| **VI. Test Evidence Is Mandatory** | PASS | Real temp-directory fixtures with real `.fsi` + real `Directory.Build.props`. Six axis states are each covered. No mocks. |
| **VII. Agent And Human Workflows Share One Contract** | PASS | Same `CommandReport` for CLI, agent, CI. No new agent surface; no skill change (`surface` is cross-cutting, not a lifecycle stage). |
| **VIII. Observability And Safe Failure** | PASS | Three explicit `axisState` values; the unresolvable path degrades loudly and names the `--param` that fixes it (SC-007) rather than dead-ending or asserting a false version. Mirrors `Fsgg.Version.compare` returning `None` over a false ordering. |

**Change tier: Tier 1** (command output contract + a new diagnostic id + two `--param` keys). Requires
spec, plan, tasks, `.fsi`, tests, and docs — all present. No migration note: the change is purely
additive to the report, and `ReportVersion` bumps `1.3.0 → 1.4.0` to say so.

**Complexity Tracking**: nothing to declare. No constitutional deviation is requested.

## Project Structure

### Documentation (this feature)

```text
specs/094-surface-version-bump/
├── plan.md              # This file
├── research.md          # Phase 0 — nine verified findings (R1–R9)
├── data-model.md        # Phase 1 — VersionBumpPrompt, state table, bump algebra, projections
├── quickstart.md        # Phase 1 — how to drive it
├── spec.md              # The specification (six clarifications, FR-001..FR-017)
└── tasks.md             # Phase 2 — /speckit-tasks output
```

### Source Code (repository root)

```text
src/
├── FS.GG.SDD.Artifacts/
│   ├── Diagnostics.fs           # + surfaceVersionBumpRequired (DiagnosticWarning)
│   └── Diagnostics.fsi          # + its val
└── FS.GG.SDD.Commands/
    ├── CommandTypes.fs          # + VersionBumpPrompt; + SurfaceSummary.VersionBump
    ├── CommandTypes.fsi         # + both, documented
    ├── CommandSerialization.fs  # + the "versionBump" object in writeSurface
    ├── CommandRendering.fs      # + five surfaceVersion* text lines
    ├── CommandHelp.fs           # + the two --param keys (FR-016)
    ├── CommandReports/
    │   └── ReportAssembly.fs    # ReportVersion 1.3.0 -> 1.4.0
    └── CommandWorkflow/
        ├── Foundation.fs        # + versionAxisFile/Property params; + ReadFile in surfaceReadEffects
        └── HandlersSurface.fs   # + readVersionAxis, applyBump, versionBumpPrompt; wire the warning

tests/
├── FS.GG.SDD.Artifacts.Tests/
│   ├── DiagnosticTests.fs       # id + severity contract
│   └── PublicSurface.baseline   # + Diagnostics.surfaceVersionBumpRequired (a public val)
├── FS.GG.SDD.Commands.Tests/
│   ├── SurfaceCommandTests.fs   # US1/US2/US3/US4 handler + effect-set assertions
│   └── HelpCommandTests.fs      # FR-016
└── FS.GG.SDD.Cli.Tests/
    └── SurfaceProjectionTests.fs # json/text/rich + determinism (FR-014)

docs/release/schema-reference.md # the versionBump block, prose (what 087 did)
```

**Not touched, deliberately** — each was a plausible candidate ruled out by research:

| Candidate | Ruled out by |
|---|---|
| `ReleaseContract.fs` / `.fsi` | R5 — `bumpRule` and `bumpFor` are not duplicates; unifying is a behavior change (AMB-005). *Also removes a file collision with #177.* |
| `docs/release/release-readiness.json` + its test baseline | R9 — the `inventory` list names only top-level report blocks; this nests inside `surface`. |
| `src/FS.GG.SDD.Cli/Program.fs` | R3 — `--param` is already parsed generically for every command. |
| `src/FS.GG.SDD.Cli/Rendering.fs` / `.fsi` | R6 — rich auto-derives from the text `key: value` lines. |
| `src/FS.GG.Contracts/Version.fs` / `.fsi` | R4 — `applyBump` stays private; promoting it widens the ApiCompat-gated Contracts surface for one caller. |

## Design Detail

### 1. Resolve the axis (`Foundation.fs`)

Two params beside the existing roots, plus a containment predicate:

```fsharp
let versionAxisFile request = surfaceParam "versionAxisFile" "Directory.Build.props" request
let versionAxisProperty request = surfaceParam "versionAxisProperty" "Version" request

// FR-017. No such guard exists for sourceRoot/baselineRoot today (research R7); this
// introduces one for the param this feature adds, and does not retrofit the other two.
let private escapesRoot (path: string) =
    let normalized = normalizeRelativePath path
    normalized = ""
    || Path.IsPathRooted normalized
    || normalized.Split('/') |> Array.contains ".."
```

The read joins the **first** wave, so it is interpreted by the time `computeSummary` runs (the second
wave is `readGate`'s body reads; `computeSummary` already depends on first-wave `EnumerateDirectory`
snapshots — R1/R2):

```fsharp
let surfaceReadEffects (request: CommandRequest) =
    [ EnumerateDirectory(surfaceSourceRoot request)
      EnumerateDirectory(surfaceBaselineRoot request)
      // Feature 094: the version axis. A missing file interprets to `Snapshot = None`
      // (research R2) — that *is* the `undeterminable` state; no Exists probe is needed.
      if not (escapesRoot (versionAxisFile request)) then
          ReadFile(versionAxisFile request) ]
```

An escaping path plans **no read at all** — nothing outside the root is ever opened (FR-017).

### 2. Read the property (`HandlersSurface.fs`, pure over the snapshot)

```fsharp
// Feature 094. `.Value` concatenates text nodes and ignores comments, so
// `<Version>0.8.0<!-- pinned --></Version>` resolves cleanly. Matched on `LocalName` because some
// repos' Directory.Build.props declares the legacy MSBuild 2003 namespace (research R8).
// MSBuild is NOT evaluated: no imports, no `$(…)`, no conditions, no property functions (FR-002).
let private readAxisText (property: string) (text: string) : string option =
    try
        XDocument.Parse(text).Descendants()
        |> Seq.tryFind (fun element -> element.Name.LocalName = property)
        |> Option.map (fun element -> element.Value.Trim())   // trim is load-bearing — research R4
    with :? XmlException ->
        None                                                   // malformed props ⇒ undeterminable
```

The three states then fall out of two `Option`s — the snapshot, and the parse:

| `snapshot axisFile model` | `readAxisText` | `Version.tryParse` | `AxisState` |
|---|---|---|---|
| `None` (absent, or read never planned) | — | — | `undeterminable` |
| `Some _` | `None` (malformed, or no such element) | — | `undeterminable` |
| `Some _` | `Some text` | `None` | `unparseable` |
| `Some _` | `Some text` | `Some v` | `resolved` |

### 3. Apply the bump

```fsharp
// Pure, total. `bumpFor` (above) supplies the bump; see its comment for why this is NOT
// ReleaseContract.bumpRule (cosmetic ⇒ none, vs Clarifying ⇒ patch) — research R5.
let private applyBump (version: Version.Version) bump =
    match bump with
    | "major" -> { Major = version.Major + 1; Minor = 0; Patch = 0 }
    | "minor" -> { version with Minor = version.Minor + 1; Patch = 0 }
    | _ -> version
```

### 4. Wire the prompt (`computeSurfaceNext`)

`computeSummary` gains the `VersionBump` field. `computeSurfaceNext` gains a third diagnostic list,
alongside the existing two — and crucially it is **not** gated on the mode (contrast `driftDiagnostics`,
which is `--check`-only):

```fsharp
// Feature 094 / ADR-0025 step 3a. Emitted under BOTH modes: `--update` is the run that
// rewrites the baselines and thereby erases the drift, so gating on `--check` would mean the
// normal PR workflow never sees the prompt (spec AMB-002 / US2). Advisory — a WARNING, never an
// error: SDD cannot see the published version, so it cannot prove the bump was not already
// applied (spec AMB-004). Exit code is untouched (FR-013).
let versionDiagnostics =
    match summary.VersionBump.RequiredBump with
    | "major" | "minor" -> [ surfaceVersionBumpRequired … ]
    | _ -> []
```

### 5. Why no write effect can exist

`computeSummary`'s `writes` list is built solely by `List.choose` over `classified`, whose paths are
`baselinePathFor sourceRoot baselineRoot source` — every element is under `baselineRoot`. The version
axis is never a member, so FR-012 holds *structurally*, not by convention. The test asserts it on the
planned effect set anyway (SC-005, I5), because a structural argument is not a regression test.

## Verification Plan

Each row is a real-filesystem fixture; no mocks (Principle VI).

| # | Scenario | Fixture | Asserts | Covers |
|---|---|---|---|---|
| V1 | additive drift, axis `0.8.0` | one `.fsi` adds a member; `Directory.Build.props` = `0.8.0` | `requiredBump=minor`, `currentVersion=0.8.0`, `suggestedVersion=0.9.0`, one warning | US1-1, FR-004/005/008 |
| V2 | breaking drift, axis `0.8.0` | one `.fsi` removes a member | `requiredBump=major`, `suggestedVersion=1.0.0` | US1-2 |
| V3 | cosmetic drift | `.fsi` comment-only change | `requiredBump=none`, `suggested=current`, **no** warning | US1-3, FR-008, I3 |
| V4 | coherent tree | no drift | `requiredBump=none`, no warning | US1-4 |
| V5 | exit codes | each of V1–V4, `--check` and `--update` | exit code identical to 086+087 | US1-5, FR-013, SC-004 |
| V6 | `--update` prompts | V2's fixture, `--update` | baselines rewritten, exit 0, warning + `major`/`1.0.0` present | US2-1, FR-011, SC-002 |
| V7 | `--update` idempotence | V6, run twice | second run: verdict `none`, no warning | US2-2 |
| V8 | no axis write | V6 | **no planned effect targets `Directory.Build.props`** — asserted on the effect set | US2-3, FR-012, SC-005, I5 |
| V9 | axis file absent | delete `Directory.Build.props` | `undeterminable`, `current`/`suggested` = `null`, `requiredBump` present, diagnostic names `--param versionAxisFile` | US3-1, FR-006/007/010 |
| V10 | no such property | props with no `<Version>` | `undeterminable`, diagnostic names `--param versionAxisProperty` | US3-2 |
| V11 | unparseable | `<Version>not-a-version</Version>` | `unparseable`, `currentVersion=null` — the bad text is **not** echoed | US3-3 |
| V12 | malformed XML | truncated props file | `undeterminable`, exit code unchanged, no exception escapes | edge case, R8 |
| V13 | pre-release version | `<Version>1.2.3-beta</Version>` | `unparseable` | edge case, R4 |
| V14 | whitespace / comment | `<Version>\n 0.8.0 <!--x--></Version>` | `resolved`, `0.8.0` | edge case, R4/R8 |
| V15 | duplicate element | two `<Version>` elements | first in document order wins, `resolved` | edge case |
| V16 | non-default axis | `--param versionAxisProperty=FsGgAudioVersion`, `2.3.1`, breaking | `currentVersion=2.3.1`, `suggestedVersion=3.0.0` | US4-1, FR-001 |
| V17 | non-default file | `--param versionAxisFile=Directory.Build.local.props` | resolves there | US4-2 |
| V18 | **no provider literal** | `grep -rE 'FsGg[A-Za-z]+Version' src/` | zero matches | US4-3, FR-003, SC-003 |
| V19 | root escape | `--param versionAxisFile=../outside.props` | `undeterminable`, **and no `ReadFile` is planned for it** | FR-017 |
| V20 | projections | V1's fixture | `--json` object shape, five `--text` lines, `--rich` degrades to the same text | FR-014 |
| V21 | determinism | V1, run twice | byte-identical `--json` and `--text` | FR-014, SC-006 |
| V22 | diagnostic contract | — | id `surface.versionBumpRequired`, severity `DiagnosticWarning` | FR-008 |
| V23 | help | `surface --help` | documents both `--param` keys and defaults | FR-016 |
| V24 | `bumpFor` ≠ `bumpRule` | — | both exist, each comments the other; `cosmetic→none`, `Clarifying→patch` | FR-015, AMB-005 |

V18 is a source-tree assertion, not a behavior test — it is the mechanical guard on the constitutional
constraint that makes this feature safe to generalize (SC-003).

## Agent-facing behavior

`surface` is cross-cutting: `nextLifecycleCommand Surface = None`, no `--work-id`, no lifecycle stage.
Nothing about that changes. No `fs-gg-sdd-*` skill is regenerated, and no agent-command view is
affected. Agents consume the new fields the same way CI does — out of `--json`.

## Governance integration

None. This feature computes no Governance verdict, reads no Governance runtime, and writes no evidence.
It emits the *version* half of ADR-0025's reconcile; the registry/projection/ADR half (3b) and the
consumer-impact flag (3c) are the `.github` slice of #236 and are explicitly out of scope (spec §Out of
Scope). Publish-before-flip (018 FR-007) is untouched — the package still leads the registry pin.

## Sequencing note (intra-repo parallel work, ADR-0021)

This feature's touch-set is **not** disjoint from the two items in flight:

- **FS.GG.SDD#164** holds `CommandTypes.fs`/`.fsi`, `CommandSerialization.fs`, `CommandRendering.fs`,
  and `SurfaceCommandTests.fs`.
- **FS.GG.SDD#177** holds `Foundation.fs` and `docs/release/`. (Design decision AMB-005 removed
  `ReleaseContract.fs`/`.fsi` from this feature's touch-set, dropping one of #177's collisions.)

Implementation is therefore **`Blocked by` #164 and #177** on the Coordination board. This authored-spec
slice (`specs/094-surface-version-bump/**`) is disjoint and lands independently. Rebase onto both before
starting Phase 3.
