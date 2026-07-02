# Tasks: Harden malformed-reference handling & digest normalization

**Feature**: `060-harden-malformed-reference-handling` · Issue: FS-GG/FS.GG.SDD#70

Ordered; each task is behavior-only and independently testable.

- [x] **T001** — `SkillMirror.sha256` (FR-004): normalize `CRLF→LF` before hashing.
  `src/FS.GG.Contracts/SkillMirror.fs`.
- [x] **T002** — Tests for T001: `SkillMirror.sha256 "a\r\nb" = SkillMirror.sha256 "a\nb"`, and
  equality with `SchemaVersion.sha256Text`'s value. `tests/FS.GG.Contracts.Tests/`.
- [x] **T003** — `WorkModel.parseWorkModel` (FR-002): gate `schemaVersion` through
  `SchemaVersion.classifyRaw`/`isBlocking` instead of `version >= 1`.
  `src/FS.GG.SDD.Artifacts/WorkModel.fs`.
- [x] **T004** — `ScaffoldProvenance.tryParse` (FR-003): same canonical gate instead of
  Major-only `isSupported`. `src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs`.
- [x] **T005** — `Diagnostics.malformedReference` (FR-001): new blocking `DiagnosticError`
  constructor + `.fsi`. `src/FS.GG.SDD.Artifacts/Diagnostics.{fsi,fs}`.
- [x] **T006** — `Internal.malformedRefs` (FR-001): helper returning the raw strings a smart
  constructor rejects. `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Internal.fs`.
- [x] **T007** — `Task.fs` (FR-001): emit `malformedReference` diagnostics for malformed
  dependency/requirement/decision/requiredEvidence values per task.
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Task.fs`.
- [x] **T008** — `Evidence.fs` (FR-001): emit `malformedReference` diagnostics for malformed
  taskRefs/requirementRefs/clarificationDecisionRefs.
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Evidence.fs`.
- [x] **T009** — Tests for T003–T008: new `MalformedReferenceTests.fs` — malformed task dep +
  evidence ref → diagnostic; valid refs → none; work-model schemaVersion 3 → `Error`,
  schemaVersion 1 → `Ok`; provenance schemaVersion 2 → `None`; register in the test fsproj.
  `tests/FS.GG.SDD.Artifacts.Tests/`.
- [x] **T010** — Verification: full offline suite green
  (`dotnet test FS.GG.SDD.sln -c Release`); JSON/golden contracts byte-unchanged for valid
  inputs (FR-005).
