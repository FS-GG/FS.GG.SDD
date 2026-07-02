# Implementation Plan: Skill-manifest contract types (P0.D0.2)

**Branch**: `057-skill-manifest-contract` · **Spec**: [spec.md](./spec.md) · **Issue**:
FS-GG/FS.GG.SDD#60 · **Decision**: ADR-0014 (extends ADR-0011)

## Summary

Additive, types-only contract change in `FS.GG.Contracts`, plus an additive optional field on the
runtime `scaffold-provenance` record and its registered contract-version bump. No behavior change,
no new code path, no digest computation — those are P1.

## Technical context

- **Language/stack**: F# (`Fsgg` namespace), `net9.0`, BCL-only in `FS.GG.Contracts` (FSharp.Core
  only — no serialization/I/O in the contract package).
- **Contract package**: `src/FS.GG.Contracts` (`ContractVersion.fs`, `Schemas.fs`, paired `.fsi`).
- **Runtime provenance**: `src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs` (serialize/parse; owns
  I/O). `HandlersScaffold.fs` constructs the record.
- **Tests**: xUnit. `tests/FS.GG.Contracts.Tests` (SchemaVersionConstantTests, ContractVersionTests,
  PublicSurface.baseline golden). `tests/FS.GG.SDD.Artifacts.Tests/ScaffoldProvenanceTests.fs`.
- **Cross-repo registry**: `FS-GG/.github` `registry/dependencies.yml` +
  `docs/registry/compatibility.md`, validated by `fsgg-sdd registry validate` (the same typed
  validator the coherence gate runs).

## Design decisions

1. **Manifest shape**: `SkillManifest = { SchemaVersion: int; Skills: SkillManifestEntry list }`
   modeled as a record (consistent with the other `*Schema` records that carry `SchemaVersion`),
   not a bare list alias — versionable and surfaces cleanly in the golden baseline.
   `SkillManifestEntry = { Id; Scope: SkillScope; Sha256: string; Body: string option;
   ResolvablePath: string option }`. Both body sources optional; the "exactly one" resolution rule
   belongs to P1's library, not this shape.
2. **SkillScope**: a two-case DU `Process | Product` — the `scope: process|product` of ADR-0014.
3. **AGENT_SKILL_ROOTS**: `let agentSkillRoots = [".claude"; ".codex"; ".agents"]` — F# camelCase
   module value (matching `providersVersion` et al.), documented as the `AGENT_SKILL_ROOTS`
   contract constant. Bare repo-root names; the `skills/` subdir is appended by consumers.
4. **entries registration**: add `{ Name = "skill-manifest"; SchemaVersion = skillManifestVersion;
   ContractVersion = None; Owner = Sdd }` (count 10 → 11); add `skillManifestVersion = 1`.
5. **Provenance sha256**: add `Sha256: string option` to both the contract
   `ScaffoldProducedPathEntry` and the runtime `ScaffoldProducedPath`. Serialize **omits** the key
   when `None` (byte-identical to today's output — every current path is `None`); emits
   `"sha256": "<hex>"` when `Some`. Parse reads an optional `sha256` (absent/blank ⇒ `None`).
   In-code `scaffoldProvenanceVersion` stays `1`.
6. **Version bumps**: `ContractVersion` `1.2.0 → 1.3.0` (`.fs` + `.fsi` unchanged signature +
   `.fsproj` `<Version>`); registry `scaffold-provenance` `1.0.0 → 1.1.0`. Package publish is P1.

## Constitution check

- **Additive-only**: every delta is a new type/value/optional field; no removal, no rename, no
  behavior change. Additive-tolerant parser preserved (constitution: additive schema evolution).
- **One fact one place**: `agentSkillRoots` and `skillManifestVersion` are single authoritative
  values; the provenance schema version stays in one place; the guard test pins it.
- **BCL-only contract package**: no new dependency; no serialization added to `FS.GG.Contracts`
  (serialization stays in `Artifacts`).
- **Markdown authoring / structured contracts**: spec + data-model are authoring; the F# types and
  the golden baseline are the machine contract.

## Phases

- **P1 (this plan)** — types in `FS.GG.Contracts`; provenance field + round-trip in `Artifacts`;
  version bumps; tests + baseline; registry bump PR in `.github`.
- Out of scope (later ADR-0014 phases): the `mirror`/`verify` library (SDD P1), routing
  scaffold/refresh/doctor through it, computing digests, the provider manifest (Rendering P2), the
  composition assertion (`.github`/Templates P3), and the enforcing flip (P4).

## Verification plan

- `dotnet build` + `dotnet test` green across the solution.
- `FSGG_UPDATE_BASELINE=1` re-capture of `PublicSurface.baseline`; diff reviewed to confirm the
  delta is additive only.
- Registry: `fsgg-sdd registry validate registry/dependencies.yml` → `"valid": true` in the
  `.github` PR.
