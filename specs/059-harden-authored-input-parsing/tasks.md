# Tasks: Harden authored-input parsing

**Feature**: `059-harden-authored-input-parsing` · Issue: FS-GG/FS.GG.SDD#66

Ordered; each task is behavior-only and independently testable.

- [x] **T001** — `Version.tryParse` (FR-003): parse each component with
  `Int32.TryParse(_, NumberStyles.None, CultureInfo.InvariantCulture)`; reject whitespace/signs.
  `src/FS.GG.Contracts/Version.fs`.
- [x] **T002** — Tests for T001: extend `VersionTests.fs` with a theory rejecting
  `"1. 2.+3"`, `"1. 2.3"`, `"+1.2.3"`, `"1.+2.3"`, `" 1.2.3"`, `"1.2.3 "`; keep the valid-triple
  cases. `tests/FS.GG.Contracts.Tests/VersionTests.fs`.
- [x] **T003** — `SchemaVersion.parse` (FR-002): `Int32.TryParse(_, NumberStyles.None,
  invariant)` for major and minor; overflow → existing malformed `Error`.
  `src/FS.GG.SDD.Artifacts/SchemaVersion.fs`.
- [x] **T004** — `Internal.parseYaml` (FR-001): `try … with :? YamlDotNet.Core.YamlException ->
  None`. `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Internal.fs`.
- [x] **T005** — Tests for T003+T004: new `AuthoredInputHardeningTests.fs` — overflow major,
  overflow minor, valid `"1"`/`"1.2"`, tab-indent `parseProjectConfig`, duplicate-key
  `parseProjectConfig`; register in the test fsproj.
  `tests/FS.GG.SDD.Artifacts.Tests/`.
- [x] **T006** — Verification: full offline suite green
  (`dotnet test FS.GG.SDD.sln`); JSON/golden contracts byte-unchanged (FR-004).
