# Implementation Plan: Harden malformed-reference handling & digest normalization

**Feature**: `060-harden-malformed-reference-handling` · **Spec**: [spec.md](./spec.md) ·
**Issue**: FS-GG/FS.GG.SDD#70

## Approach

Three localized, behavior-only groups of edits. Only one new diagnostic id is introduced
(`malformedReference`); the shared grammars keep their signatures.

### 1. Malformed authored ids → diagnostics (FR-001)

`Internal.parse{Task,Requirement,Decision,Evidence}Ids` drop invalid values via
`Result.toOption`. Add a sibling `Internal.malformedRefs (create) values` that returns the
raw strings the smart constructor rejects. Add `Diagnostics.malformedReference artifact kind
value` (a blocking `DiagnosticError`).

At the two authored parse sites that carry cross-references:

- **`Task.fs`** (`parseTaskFacts`): for each task mapping, alongside the parsed id lists,
  collect the malformed raws for `dependencies`/`requirements`/`decisions`/`requiredEvidence`
  and fold them into the task's diagnostics. The task record shape is unchanged (still typed
  id lists); the malformed values become diagnostics rather than vanishing.
- **`Evidence.fs`** (`parseEvidenceFacts`): same for `taskRefs`/`requirementRefs`/
  `clarificationDecisionRefs`.

Diagnostics are appended to the artifact's existing `Diagnostics` list and pass through the
same `Diagnostics.sort`. `WorkModel.referenceDiagnostics` (which only sees ids that parsed)
is unchanged — malformed ids are now caught one layer earlier, at parse.

### 2. One schema-version policy (FR-002, FR-003)

- **`WorkModel.parseWorkModel`**: replace `Some version when version >= 1` with a guard that
  routes the integer through the canonical classifier —
  `SchemaVersion.classifyRaw (Some (string version))` — and accepts only when
  `not (SchemaVersion.isBlocking compatibility)`, emitting the existing schema diagnostics
  otherwise. Accepts major 0/1 (current/deprecated), rejects 2+ (unsupported/future).
- **`ScaffoldProvenance.tryParse`**: replace `SchemaVersion.isSupported (SchemaVersion.create
  version)` with the same `classifyRaw`/`isBlocking` gate.

All shipped artifacts are schemaVersion 1, so valid inputs are byte-unchanged; only the
previously-loose acceptance of 2+ tightens.

### 3. Digest CRLF normalization (FR-004)

`Fsgg.SkillMirror.sha256` hashes `Encoding.UTF8.GetBytes body` directly. Normalize first:
`body.Replace("\r\n", "\n")`, matching `SchemaVersion.sha256Text`. Idempotent for LF content,
so existing manifests (authored LF in-repo) are unaffected; only CRLF checkouts stop
mismatching.

## Verification

- New semantic tests through the public surface (see tasks.md): malformed task/evidence refs
  → diagnostic; valid refs → none; work-model/provenance schemaVersion 2/3 rejection with
  schemaVersion 1 regression; CRLF/LF digest equality plus agreement with
  `SchemaVersion.sha256Text`.
- Full offline test suite green (`dotnet test FS.GG.SDD.sln -c Release`), including
  golden/JSON contracts, byte-unchanged for valid inputs (FR-005).

## Files

| File | Change |
|---|---|
| `src/FS.GG.SDD.Artifacts/Diagnostics.fsi` / `.fs` | new `malformedReference` diagnostic |
| `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Internal.fs` | `malformedRefs` helper |
| `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Task.fs` | emit malformed-ref diagnostics |
| `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Evidence.fs` | emit malformed-ref diagnostics |
| `src/FS.GG.SDD.Artifacts/WorkModel.fs` | canonical schemaVersion gate in `parseWorkModel` |
| `src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs` | canonical schemaVersion gate in `tryParse` |
| `src/FS.GG.Contracts/SkillMirror.fs` | CRLF→LF normalization in `sha256` |
| `tests/FS.GG.SDD.Artifacts.Tests/MalformedReferenceTests.fs` | new: US1 + US2 cases |
| `tests/FS.GG.Contracts.Tests/SkillMirrorTests.fs` | US3 CRLF/LF equality (new or extend) |

## Risks

- **New diagnostic surfacing on existing fixtures**: a fixture with a genuinely malformed ref
  would newly emit `malformedReference` and could change a golden. Audited: authored fixtures
  use canonical ids; any surfaced case is a real latent defect to fix in the fixture.
- **schemaVersion tightening**: `parseWorkModel` no longer accepts 2+. All work-model
  fixtures are schemaVersion 1 (audited), so no valid fixture regresses.
- **Digest change**: only affects CRLF content; LF content (the repo norm) hashes identically,
  so the 057/058 manifests are unchanged.
