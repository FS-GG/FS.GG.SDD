# Implementation Plan: One materialize-and-verify library + content-aware drift (P1)

**Branch**: `058-materialize-verify-library` · **Spec**: [spec.md](./spec.md) · **Issue**:
FS-GG/FS.GG.SDD#61 · **Decision**: ADR-0014 (extends ADR-0011)

## Summary

Add one pure `Fsgg.SkillMirror` library to `FS.GG.Contracts`; reroute the seeded fan-out, the
scaffold provider mirror, and the refresh re-mirror through it (byte-identical output); populate the
provenance per-skill `sha256`; and make `doctor`/`upgrade` drift content-aware over process **and**
product skills via `SkillMirror.verify`. Advisory first: detect divergence, re-seed the missing,
never clobber a divergent copy.

## Technical context

- **Language/stack**: F# (`Fsgg` namespace in Contracts; `FS.GG.SDD.Commands.Internal` for the
  handlers), `net10.0`. `FS.GG.Contracts` is BCL-only (FSharp.Core + BCL `SHA256`).
- **Contract package**: `src/FS.GG.Contracts` — new `SkillMirror.fs`/`.fsi` (module `Fsgg.SkillMirror`),
  `ContractVersion.fs` `1.3.0 → 1.4.0`.
- **Commands**: `src/FS.GG.SDD.Commands` gains a direct `ProjectReference` to `FS.GG.Contracts`
  (`open Fsgg`). Rerouted: `CommandWorkflow/SeededSkills.fs`, `HandlersScaffold.fs`,
  `HandlersRefresh.fs`, `Drift.fs`, `HandlersDoctor.fs`, `HandlersUpgrade.fs`, `Foundation.fs`
  (remediation reads).
- **Provenance**: `src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs` already serializes `Sha256 = Some`;
  only the scaffold record literals change (populate digests).
- **Report surface**: `CommandTypes.fsi` (DriftReport/DoctorSummary/UpgradeSummary additive field),
  `CommandSerialization.fs` (emit it).
- **Version-of-truth**: `Directory.Build.local.props` `<Version>` `0.4.0 → 0.5.0`.
- **Tests**: xUnit across `FS.GG.Contracts.Tests`, `FS.GG.SDD.Commands.Tests` (Scaffold/Refresh/
  Seeded/Remediation), `FS.GG.SDD.Artifacts.Tests`; public-surface baseline.

## Design decisions

1. **`Fsgg.SkillMirror` API** (pure, BCL-only, root-set-parameterized):
   - `providerSourceRoot = ".agents"` — the one root a provider owns (ADR §Decision 6).
   - `sha256 (body) : string` — lowercase-hex SHA256 of UTF-8 bytes.
   - `skillPath root id` = `root + "/skills/" + id + "/SKILL.md"`; `skillIdOfPath` extracts `<id>`.
   - `mirrorTargetRoots roots` = `roots` minus `providerSourceRoot` (the roots a provider copy fans
     into); `retargetSkillPath targetRoot sourcePath` rewrites `.agents/skills/REST` →
     `targetRoot + "/skills/REST"` verbatim (byte-identity with today's `mirrorTargetsFor`).
   - `type MirrorWrite = { Path; Body }`; `mirror roots skills` = one write per `(id,body)` per root,
     sorted by id (matches today's effect order).
   - `verify roots expected actual : SkillDrift list` — `ExpectedSkill = { Id; Scope; Sha256 }`,
     `ActualCopy = { Root; Id; Body: string option }`, `SkillDrift = { Id; Scope; MissingRoots;
     Divergent; HashMismatchRoots }`. Per skill: `MissingRoots` = roots with no copy; `Divergent` =
     present copies not all byte-identical; `HashMismatchRoots` = present roots whose `sha256 body ≠
     expected.Sha256` (only when `expected.Sha256` non-empty). Returns only drifted skills, sorted.
2. **Seeded fan-out** (`SeededSkills.skillEffects`): build `(name, body)` pairs and emit
   `SkillMirror.mirror agentSkillRoots pairs |> List.map (fun w -> WriteFile(w.Path, w.Body,
   AgentGuidanceTarget))`. Byte-identical to the old triple-write (same paths, order, kind).
3. **Scaffold provider mirror**: replace `agentsSkillsPrefix`/`mirrorTargetsFor`/`plannedMirroredPaths`
   with `SkillMirror` (`providerSourceRoot`, `mirrorTargetRoots agentSkillRoots`, `retargetSkillPath`).
   Populate `Sha256 = Some (SkillMirror.sha256 body)` for produced `.agents/skills/*` (the read body)
   and mirrored copies; non-skill produced paths stay `None`. Provenance digests are computed in the
   MIRROR tick where the source bodies are already read.
4. **Refresh re-mirror**: replace the inline `.claude`/`.codex` writes with `retargetSkillPath` over
   `mirrorTargetRoots agentSkillRoots`; keep `SeededSkills.skillEffects @ reMirror`.
5. **Content-aware drift**: `Drift.compute` gains the snapshotted skill bodies + provenance; it builds
   `expected` (process: seeded manifest, digest = `sha256` of embedded body; product: provenance skill
   ids, digest = recorded `sha256`) and `actual` (each skill × root body from snapshots), calls
   `SkillMirror.verify agentSkillRoots`, and folds the result into a new `SkillDriftPaths: string list`
   (the concrete drifted root/skill paths) + `IsCoherent`. The presence-only `MissingArtifactPaths`
   machinery (seeded + early-stage-guidance) is retained unchanged; content drift is additive on top.
6. **Two-tick remediation reads**: `Foundation.remediationReadEffects` stays phase 1 (provenance,
   registry, seeded expected). `HandlersDoctor`/`HandlersUpgrade` add a provenance-driven phase-2 read
   of the product-skill copies across `agentSkillRoots` (a read-gate mirroring scaffold's MIRROR gate),
   then compute drift once those are interpreted. `doctor` remains write-free.
7. **`upgrade` re-materialize**: unchanged mechanism — re-seed the **missing** seeded copies via
   `initEffects` filtered to the missing set (no-clobber). Product-skill loss and cross-root divergence
   are surfaced as advisory drift; `residualDrift`/hint reflect any un-repaired content drift so an
   incomplete reconciliation is never reported complete (FR-013 preserved).
8. **Report additive field**: `DriftReport`/`DoctorSummary`/`UpgradeSummary` gain
   `SkillDriftPaths: string list`, serialized after `missingArtifactPaths`. Additive.
9. **Versions**: Contracts `1.3.0 → 1.4.0` (`.fs` + `.fsproj`), CLI/packages `0.4.0 → 0.5.0`
   (`Directory.Build.local.props`). No persisted schema-version change (`scaffoldProvenanceVersion`
   stays `1`, `skillManifestVersion` stays `1`).

## Constitution check

- **One fact one place**: the root set is `agentSkillRoots` alone; four hardcoded lists deleted.
  The mirror/verify algorithm exists once (Contracts).
- **Additive-only**: new public module; one additive report field; provenance digest populated into a
  field 057 already added. No removed/renamed public surface (baseline delta additive).
- **BCL-only contract package**: `SkillMirror` uses only FSharp.Core + BCL `SHA256`; no new package.
- **MVU/effect discipline**: no new effect kind; the phase-2 remediation read reuses `ReadFile` and the
  established interpreted-effect gate pattern; `doctor` stays read-only.
- **Markdown authoring / structured contracts**: spec + data-model are authoring; the `.fsi` surface,
  the golden baseline, and the tests are the machine contract.

## Verification plan

- `dotnet build` + `dotnet test` green across the solution at each phase.
- `FSGG_UPDATE_BASELINE=1` re-capture of `PublicSurface.baseline`; diff confirmed additive-only.
- Golden: existing 056 scaffold/refresh/seeded byte-identity tests stay green **unchanged** (SC-003).
- Red-then-green: a new `doctor` test that edits one root copy and deletes a provider copy asserts both
  are detected (SC-005), failing before the content-aware `Drift` change and passing after.
- `/verify` the `doctor`/`upgrade`/`scaffold` flows end-to-end on a real temp scaffold.

## Phases

- **This plan (P1)** — the library, the four reroutes, provenance digests, content-aware drift, the
  red test, the coherent version bump.
- Out of scope (later ADR-0014 phases): the provider product manifest + single standalone materialize
  (Rendering P2); the reusable composition skill-union assertion (`.github`/Templates P3); the
  enforcing flip + clobber-repair of divergent copies (P4). The publish-before-flip release of `0.5.0`
  and the registry orchestrator-axis minimum flip are the separate release dance.
