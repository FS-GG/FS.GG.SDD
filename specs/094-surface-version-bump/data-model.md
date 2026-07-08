# Phase 1 Data Model: Prompt the Coherent-Set Version Bump

**Feature**: `094-surface-version-bump` | **Date**: 2026-07-08

One new record, nested into the existing `SurfaceSummary`. No persisted schema changes; no lifecycle
artifact is written.

## `VersionBumpPrompt` (new, `FS.GG.SDD.Commands/CommandTypes.fs` + `.fsi`)

```fsharp
/// Feature 094 (FS-GG/.github ADR-0025 reconcile step 3a): the coherent-set version obligation
/// implied by a classified shipped-surface mutation. Advisory — emits a `surface.versionBumpRequired`
/// warning and never changes the exit code. SDD cannot see the previously *published* version (that
/// lives in the feed and the `.github` registry pin), so this states facts and an implication, never
/// an accusation: the bump may already be applied in the change under review.
type VersionBumpPrompt =
    {
        /// The workspace-relative file the axis was read from. `--param versionAxisFile`,
        /// default `Directory.Build.props`. Reported even when unresolved, so the operator
        /// can see what was looked for.
        AxisFile: string
        /// The MSBuild property element name. `--param versionAxisProperty`, default `Version`.
        /// Generic SDD embeds no concrete axis name (FR-003).
        AxisProperty: string
        /// `resolved` | `undeterminable` | `unparseable`.
        AxisState: string
        /// The axis value, present only when `AxisState = "resolved"`.
        CurrentVersion: string option
        /// `major` | `minor` | `none`, mirroring the run verdict's `RecommendedBump`. Depends only
        /// on the classification, so it is reported in every axis state.
        RequiredBump: string
        /// `CurrentVersion` with `RequiredBump` applied. `None` whenever `CurrentVersion` is `None`.
        SuggestedVersion: string option
    }
```

### State table

`AxisState` is a total function of the read result. `RequiredBump` is a total function of the
classification verdict, independent of the axis.

| Condition on `{AxisFile}` | `AxisState` | `CurrentVersion` | `SuggestedVersion` |
|---|---|---|---|
| escapes root — **raw** param is absolute, or contains a `..` segment (FR-017; test the *raw* string, never `normalizeRelativePath`'s output, which strips the leading `/` first) | `undeterminable` | `None` | `None` |
| absent (`snapshot` → `None`) | `undeterminable` | `None` | `None` |
| present, not well-formed XML (`XmlException`) | `undeterminable` | `None` | `None` |
| present, well-formed, no `{AxisProperty}` element | `undeterminable` | `None` | `None` |
| element present, text fails `Version.tryParse` | `unparseable` | `None` | `None` |
| element present, text parses | `resolved` | `Some v` | `Some (applyBump v bump)` |

`AxisFile` and `AxisProperty` are echoed verbatim in **every** row — the operator must be able to see
what was searched for in order to correct it with `--param` (FR-010).

### Bump algebra

```
applyBump {M;m;p} "major" = {M+1; 0;   0}
applyBump {M;m;p} "minor" = {M;   m+1; 0}
applyBump v       _       = v            // "none"
```

Rendered as `$"{Major}.{Minor}.{Patch}"`. Pure, total, no I/O. Lives private in `HandlersSurface`
alongside `bumpFor` — **not** promoted into `Fsgg.Version`, which would widen the ApiCompat-gated
`FS.GG.Contracts` public surface for a single caller (R4, R5).

### Invariants

- **I1**: `RequiredBump ∈ {major, minor, none}` and equals `summary.Classification.RecommendedBump`.
  No second mapping exists (FR-004).
- **I2**: `SuggestedVersion.IsSome ⟺ CurrentVersion.IsSome ⟺ AxisState = "resolved"`.
- **I3**: `RequiredBump = "none" ⟹ SuggestedVersion = CurrentVersion` (the bump is the identity).
- **I4**: the `surface.versionBumpRequired` warning is emitted ⟺ `RequiredBump ∈ {major, minor}`.
  Independent of `AxisState` — an unresolvable axis still warns, and names the `--param` that would
  resolve it (FR-008, FR-010, SC-007).
- **I5**: no planned **mutating** effect targets `AxisFile`, and the only effect that targets it at all
  is the `ReadFile` the prompt is derived from (FR-012). Asserted on the effect set, not on mtimes.

## `SurfaceSummary` (extended, additive)

One field appended, mirroring how feature 087 appended `Classification`:

```fsharp
        /// Feature 087: the additive-vs-breaking classification of the drifted set. …
        Classification: SurfaceClassification
        /// Feature 094: the coherent-set version obligation the classification implies. Always
        /// present; inert (`RequiredBump = "none"`, no warning) when nothing drifted.
        VersionBump: VersionBumpPrompt
```

Always present — never `option` — so the automation contract keeps the stable shape `writeSurface`
already promises. When nothing drifted, it is inert: `RequiredBump = "none"`, no warning.

## Projections

`surface` block, appended after `classification` (R6):

**`--json`** — stable key set; optional scalars as explicit `null`:

```json
"versionBump": {
  "axisFile": "Directory.Build.props",
  "axisProperty": "Version",
  "axisState": "resolved",
  "currentVersion": "0.8.0",
  "requiredBump": "minor",
  "suggestedVersion": "0.9.0"
}
```

**`--text`** — `key: value` lines, `defaultArg … "(none)"`, always emitted:

```
surfaceVersionAxis: Directory.Build.props:Version
surfaceVersionAxisState: resolved
surfaceVersionCurrent: 0.8.0
surfaceVersionRequiredBump: minor
surfaceVersionSuggested: 0.9.0
```

**`--rich`** — no code. `Cli/Rendering.fs` auto-derives its rows from the text `key: value` lines
(R6); this is why the text keys above are flat scalars rather than a nested structure.

## Diagnostic (new, `FS.GG.SDD.Artifacts/Diagnostics.fs` + `.fsi`)

```fsharp
val surfaceVersionBumpRequired:
    verdict: string ->
    axisFile: string ->
    axisProperty: string ->
    axisState: string ->
    currentVersion: string option ->
    requiredBump: string ->
    suggestedVersion: string option ->
        Diagnostic
```

- id `surface.versionBumpRequired`, severity **`DiagnosticWarning`** (FR-008; never blocking, FR-013).
- Resolved: *"Shipped-surface mutation classified `breaking`. The coherent-set version axis
  `Directory.Build.props:Version` reads `0.8.0`; a **major** bump to `1.0.0` is required — unless it is
  already applied in this change. `fsgg-sdd` does not write the axis (ADR-0009: detect-and-remediate)."*
- Unresolved: the same, plus *"The axis could not be resolved (`undeterminable`). Point at it with
  `--param versionAxisFile=… --param versionAxisProperty=…`."* (FR-010)

Adding a public `val` to `FS.GG.SDD.Artifacts` changes
`tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` (public static module functions are what the
reflection baseline captures — records and fields are not, which is why 087 touched no baseline).
Re-capture with `FSGG_UPDATE_BASELINE=1`.

**No remediation pointer is required.** `RemediationPointersTests` never enumerates the diagnostic
catalog — every invariant iterates `RemediationPointers.registry`, a hand-curated authoring-grammar
subset that excludes even the blocking `surface.drift`. A new diagnostic of any severity cannot break
it. Adding `surface.versionBumpRequired` to that registry would *fail* the suite, because
`RemediationPointersTests.fs:122-132` requires every key to appear in `DiagnosticConstructors.fs` while
`surface.*` ids live in `Artifacts/Diagnostics.fs`. See tasks.md T040.
