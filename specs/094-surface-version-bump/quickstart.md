# Quickstart: The Coherent-Set Version Bump Prompt

**Feature**: `094-surface-version-bump` | **Date**: 2026-07-08

## What it does

When `fsgg-sdd surface` finds that an already-shipped `.fsi` baseline has changed, it now also tells
you what that change costs on your repository's version axis.

```console
$ fsgg-sdd surface --check --text
surfaceMode: check
surfaceChecked: 12
surfaceDrifted: 1
surfaceDrifted: src/FS.GG.Audio.Core/Mixer.fsi
surfaceCoherent: false
surfaceClassificationVerdict: additive
surfaceClassificationBump: minor
surfaceClassified: 1
surfaceClassified: src/FS.GG.Audio.Core/Mixer.fsi=additive (minor)
surfaceVersionAxis: Directory.Build.props:Version
surfaceVersionAxisState: resolved
surfaceVersionCurrent: 0.8.0
surfaceVersionRequiredBump: minor
surfaceVersionSuggested: 0.9.0

warning surface.versionBumpRequired: Shipped-surface mutation classified `additive`. The coherent-set
version axis `Directory.Build.props:Version` reads `0.8.0`; a minor bump to `0.9.0` is required —
unless it is already applied in this change. fsgg-sdd does not write the axis
(ADR-0009: detect-and-remediate).
```

Exit code is unchanged: `1` here, because the tree drifted (feature 086), not because of the prompt.

## It fires on `--update` too

This is the point. `--update` is the run that rewrites the baselines and erases the drift — so it is
the run that must tell you about the bump, because the *next* `--check` will see a coherent tree and
say nothing.

```console
$ fsgg-sdd surface --update --text
surfaceUpdated: 1
surfaceUpdated: docs/api-surface/FS.GG.Audio.Core/Mixer.fsi
surfaceVersionRequiredBump: minor
surfaceVersionSuggested: 0.9.0
# ... same warning ...
$ echo $?
0
```

Then you edit the axis yourself. `fsgg-sdd` never writes it (ADR-0009).

## Pointing it at your axis

The defaults are `Directory.Build.props` and the property `Version`. Generic SDD knows no repo's real
axis name, so declare yours:

```console
# FS.GG.Audio
$ fsgg-sdd surface --check --param versionAxisProperty=FsGgAudioVersion

# FS.GG.Game
$ fsgg-sdd surface --check --param versionAxisProperty=FsGgGameVersion

# This repo — <Version> lives in the .local overlay
$ fsgg-sdd surface --check --param versionAxisFile=Directory.Build.local.props
```

Both keys compose with the existing `--param sourceRoot=… --param baselineRoot=…`.

## When the axis can't be found

It never dead-ends. You still get the bump the classification implies, plus the override that would
resolve the axis:

```console
$ fsgg-sdd surface --check --text
surfaceVersionAxis: Directory.Build.props:Version
surfaceVersionAxisState: undeterminable
surfaceVersionCurrent: (none)
surfaceVersionRequiredBump: major
surfaceVersionSuggested: (none)

warning surface.versionBumpRequired: Shipped-surface mutation classified `breaking`; a major bump is
required. The axis could not be resolved (undeterminable). Point at it with
`--param versionAxisFile=… --param versionAxisProperty=…`.
```

Three states: `resolved`, `undeterminable` (file absent/malformed/no such property/outside the root),
`unparseable` (present, but not a `major.minor.patch` triple — pre-release tags like `1.2.3-beta` land
here).

## The JSON contract

```console
$ fsgg-sdd surface --check --json | jq '.surface.versionBump'
{
  "axisFile": "Directory.Build.props",
  "axisProperty": "Version",
  "axisState": "resolved",
  "currentVersion": "0.8.0",
  "requiredBump": "minor",
  "suggestedVersion": "0.9.0"
}
```

Stable key set: `currentVersion` and `suggestedVersion` are `null` — never absent — when the axis is
unresolved.

## What it deliberately does not do

- **It does not write the version.** Detect-and-remediate (ADR-0009). You confirm and edit.
- **It does not know whether you already bumped.** The previously *published* version lives in the
  package feed and the `.github` registry pin, which `surface` does not read. So the prompt says
  "…unless it is already applied in this change" rather than accusing you. If you bumped in the same
  PR, the prompt is satisfied and you ignore it.
- **It does not touch the registry, the projection, or the consumer flags.** Those are ADR-0025
  reconcile steps 3b/3c, owned by the `.github` slice of #236 (`scripts/fsgg-surface-impact`).
- **It never changes the exit code.** A `cosmetic` or `none` verdict emits no warning at all.
