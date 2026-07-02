# Implementation Plan: Harden authored-input parsing

**Feature**: `059-harden-authored-input-parsing` · **Spec**: [spec.md](./spec.md) ·
**Issue**: FS-GG/FS.GG.SDD#66

## Approach

Three localized, behavior-only edits. Each keeps its function's existing signature, so no
`.fsi` changes and no downstream call-site churn.

### 1. `Internal.parseYaml` — catch `YamlException` (FR-001)

`YamlStream.Load` throws `YamlDotNet.Core.YamlException` on malformed documents. Wrap the
`Load` + document extraction in `try … with :? YamlDotNet.Core.YamlException -> None`. Every
caller already pattern-matches `parseYaml` and turns `None` into a diagnostic (e.g.
`parseProjectConfig` → "Project config is empty."; `parseTaskFacts` → "Tasks file is empty."),
so malformed input now reaches the same exit-1 path as an empty document. This mirrors the
existing local defense in `WorkItem.rawSchemaVersion` (`try … with _ -> None`), narrowed to
the YAML exception type.

### 2. `SchemaVersion.parse` — `Int32.TryParse` (FR-002)

The `^(\d+)(?:\.(\d+))?$` regex already constrains each group to digits, so the only failure
mode is overflow. Replace both `Int32.Parse` calls with `Int32.TryParse(_, NumberStyles.None,
CultureInfo.InvariantCulture)`; on failure return the same
`Error "Schema version must be an integer or major.minor value."` the malformed branch already
returns. `classifyRaw` maps that `Error` to `Malformed` (blocking), unchanged.

### 3. `Fsgg.Version.tryParse` — strict grammar (FR-003)

Replace the default `System.Int32.TryParse` with `Int32.TryParse(_, NumberStyles.None,
CultureInfo.InvariantCulture)`, which rejects leading/trailing whitespace and signs. The
existing `major/minor/patch >= 0` guards remain (now redundant but harmless). Pure, total,
BCL-only — the module's documented contract is preserved and strengthened.

## Verification

- New semantic tests through the public surface (see tasks.md): overflow, tab-indent YAML,
  duplicate-key YAML, whitespace/sign version strings, plus regression cases proving valid
  inputs still parse.
- Full offline test suite green (`dotnet test FS.GG.SDD.sln`), including golden/JSON
  contracts, which must be byte-unchanged (FR-004).

## Files

| File | Change |
|---|---|
| `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Internal.fs` | catch `YamlException` in `parseYaml` |
| `src/FS.GG.SDD.Artifacts/SchemaVersion.fs` | `Int32.TryParse` in `parse` |
| `src/FS.GG.Contracts/Version.fs` | strict `Int32.TryParse` in `tryParse` |
| `tests/FS.GG.SDD.Artifacts.Tests/AuthoredInputHardeningTests.fs` | new: YAML + overflow cases |
| `tests/FS.GG.Contracts.Tests/VersionTests.fs` | new: whitespace/sign rejection cases |

## Risks

- **Message precision**: malformed YAML now reuses the "is empty" diagnostic. Acceptable
  (exit-1 with a diagnostic is the doctrine); a more precise message is out of scope.
- **Redundant guards**: the `>= 0` checks in `Version.tryParse` become dead under
  `NumberStyles.None`. Kept for defensive clarity; no behavior change.
