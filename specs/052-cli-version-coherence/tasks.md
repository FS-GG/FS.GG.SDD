# Tasks: CLI Version Coherence in Scaffold Provenance

**Input**: Design documents from `specs/052-cli-version-coherence/`
**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tier**: Tier 1 (contracted change) — schema, provider-contract read, command output, agent/docs.

## Format: `[ID] [P?] [Story] Description`

- `[P]` — parallel-safe (no dependency on another incomplete task in this phase; distinct files).
- `[US1]`/`[US2]`/`[US3]` — user story; unmarked = shared/foundational.
- Phases run in sequence; tasks within a phase may run in parallel where marked.
- Status: `[ ]` pending · `[X]` done w/ real evidence · `[-]` skipped w/ rationale. Never green a failing task.

## Status legend & discipline

Follow `.fsi`-first (Principle I/III), fail-first tests (Principle VI), and refresh
`PublicSurface.baseline` whenever public surface changes. MVU note: the coherence check is a
**pure** function inside the existing scaffold `update` — **no new `Effect`/interpreter edge**
(D11); Principle V ceremony is satisfied by the existing scaffold MVU boundary.

---

## Phase 1: Shared version grammar (`Fsgg.Version`) — foundational

**Purpose**: One repo-wide `major.minor.patch` grammar (D3/E3), reused by scaffold and Registry.
Blocks the comparison logic in every user story.

- [X] T001 Author `src/FS.GG.Contracts/Version.fsi` — `namespace Fsgg`, `module Version`, type `Version = { Major:int; Minor:int; Patch:int }`, `val tryParse: string -> Version option`, `val compare: string -> string -> int option` (per `contracts/version-compare.md`).
- [X] T002 [P] Write fail-first tests in `tests/FS.GG.Contracts.Tests/VersionTests.fs` — parse valid triples; reject non-triples → `None`; `compare` for `<`/`=`/`>` → `Some -1/0/1`; either side unparseable → `None`.
- [X] T003 Implement `src/FS.GG.Contracts/Version.fs` (extract the grammar from `Registry.fs:73-89`; pure, total, BCL-only, no exceptions). Add `Version.fs`/`Version.fsi` to `FS.GG.Contracts.fsproj` before `Registry.fs`.
- [X] T004 Refactor `src/FS.GG.Contracts/Registry.fs` private `SemVer`/`tryParseSemVer`/`compareSemVer` (`:67-89`) to delegate to `Fsgg.Version`; keep range-comparator behavior identical.
- [X] T005 [P] Add a regression test in `tests/FS.GG.Contracts.Tests/` proving `Registry` range checks behave identically after delegation.
- [X] T006 Refresh `tests/FS.GG.Contracts.Tests/PublicSurface.baseline` for the new `Fsgg.Version` surface.

**Checkpoint**: `dotnet test tests/FS.GG.Contracts.Tests` green; one version grammar exists.

---

## Phase 2: Additive contract fields — foundational (blocks US1/US2)

**Purpose**: The additive provider-descriptor field (E2), the additive provenance field (E1),
and the two diagnostic codes (E5). Everything downstream depends on these types existing.

- [X] T007 [P] Extend `src/FS.GG.Contracts/Provider.fsi` and `Provider.fs`: add `MinimumCliVersion: string option` to `ProviderDescriptor` (additive superset; default `None`).
- [X] T008 Parse the scalar in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs` (`parseProviderRegistry`, `:194-205`): `MinimumCliVersion = tryScalarAt [ "minimumCliVersion" ] mapping`. Must NOT affect entry-drop logic (four required scalars unchanged). Per `contracts/provider-registry-minimum-cli.md`.
- [X] T009 [P] Add registry fixtures (with `minimumCliVersion`, without it, and malformed) under the Commands/CLI test fixtures used by scaffold tests.
- [X] T010 Extend `src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fsi` and `.fs`: add `RequiredMinimumCliVersion: string option` to `ScaffoldProvenanceRecord`. `serialize` emits `requiredMinimumCliVersion` **immediately after** the `generator` object as **string-or-null**; `tryParse` reads it, absent/null → `None` (mirror the `effectiveParameters` default at `:102-110`). Schema stays `1`. Per `contracts/scaffold-provenance-v1-additive.md`.
- [X] T011 [P] Fail-first tests in `tests/FS.GG.SDD.Artifacts.Tests/ScaffoldProvenanceTests.fs`: round-trip with `Some`/`None`; **back-compat** — a record JSON without the field parses with `RequiredMinimumCliVersion = None`; byte-stability of `serialize`.
- [X] T012 Add diagnostics in `src/FS.GG.SDD.Artifacts/Diagnostics.fs` (alongside other `scaffold.*`): `scaffoldCliBehindMinimum` (`scaffold.cliBehindMinimum`, `DiagnosticInfo`) and `scaffoldProviderMinimumMalformed` (`scaffold.providerMinimumMalformed`, `DiagnosticWarning`). Per `contracts/advisory.md`. Message names installed/minimum/gap; `Correction` names the re-seed remedy. **A1 note**: the "gap" (amount behind) is derived from `Fsgg.Version.tryParse` of both versions (`Major/Minor/Patch`), since `Fsgg.Version.compare` returns only the sign (`Some -1/0/1`), not a delta. **Verify `Diagnostic.Correction` is actually surfaced across json/text/rich**; if it is not projected, do not rely on it — the authoritative remedy pointer is the T025 `NextAction` branch.
- [X] T013 Refresh `PublicSurface.baseline` for `FS.GG.Contracts` (Provider) and `FS.GG.SDD.Artifacts` (ScaffoldProvenance + Diagnostics).

**Checkpoint**: `dotnet test tests/FS.GG.Contracts.Tests tests/FS.GG.SDD.Artifacts.Tests` green; types + serialization ready.

---

## Phase 3: US1 — Provenance records which CLI produced the scaffold (P1) 🎯 MVP

**Goal**: `.fsgg/scaffold-provenance.json` records the CLI version used **and** the
provider-declared required minimum, side by side, deterministically. (SC-001; independently
valuable even before any advisory.)

- [X] T014 [US1] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs`, compute the resolved `requiredMinimumCliVersion: string option` from `descriptor.MinimumCliVersion`: `None` when absent; `None` when present-but-malformed (`Fsgg.Version.tryParse = None`, D6); the raw string when valid. Thread it into `provenanceWriteEffect` (`:238-251`) and set `ScaffoldProvenanceRecord.RequiredMinimumCliVersion`.
- [X] T015 [US1] [P] Add `RequiredMinimumCliVersion: string option` to `ScaffoldSummary` in `src/FS.GG.SDD.Commands/CommandTypes.fs`/`.fsi` (E4); populate it in the scaffold summary construction in `HandlersScaffold.fs`.
- [X] T016 [US1] Emit the field in JSON: `src/FS.GG.SDD.Commands/CommandSerialization.fs` scaffold block (`:294-325`), string-or-null, deterministic placement.
- [X] T017 [US1] [P] Emit `scaffoldRequiredMinimumCliVersion: …` line in `src/FS.GG.SDD.Commands/CommandRendering.fs` `renderText` scaffold block (`:196-216`); verify rich derives it automatically via the text `key: value` split.
- [X] T018 [US1] Update Commands golden/provenance tests in `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs` (`:245-254`, `:279-294`, `:442-467`, `:498-511`): assert both `generator.version` and `requiredMinimumCliVersion` present; determinism/byte-stability across two runs (US1 scenario 3); no-minimum → `null` (US1 scenario 2).
- [X] T019 [US1] [P] Refresh `PublicSurface.baseline` for `FS.GG.SDD.Commands` (ScaffoldSummary).

**Checkpoint**: US1 independently testable — provenance + report record both facts; determinism holds. **This is the shippable MVP** (delivers audit value with no advisory; degrades to `null` when no minimum).

---

## Phase 4: US2 — Author is warned when the installed CLI is behind the minimum (P1)

**Goal**: A non-blocking `scaffold.cliBehindMinimum` advisory in all three projections when the
installed CLI is strictly behind the declared minimum; silent otherwise; exit code unchanged.
(SC-002/SC-003/SC-004.) Depends on Phase 1–3.

- [X] T020 [US2] Add a **pure** `cliCoherenceDiagnostics (descriptor) (request) -> Diagnostic list` in `HandlersScaffold.fs` (D11): emit `scaffold.cliBehindMinimum` iff `Fsgg.Version.compare request.GeneratorVersion.Version min = Some -1`; emit `scaffold.providerMinimumMalformed` iff min present & unparseable; emit nothing when min absent, equal/above, or installed unparseable (D4/D6/D7). **U1**: build the advisory's "amount behind" from the parsed `Fsgg.Version` records (`tryParse` both sides, format the `Major/Minor/Patch` delta) — `compare`'s sign alone cannot express FR-004/SC-002's "how far behind".
- [X] T021 [US2] Merge `cliCoherenceDiagnostics` into `model.Diagnostics` on **every** descriptor-resolved path (dry-run and real), so the advisory appears in all outcomes without blocking. Confirm no change to `ScaffoldResolution` blocking behavior (advisory is non-blocking).
- [X] T022 [US2] [P] Fail-first pure tests in `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs` (or a new `ScaffoldCliCoherenceTests.fs`): behind → exactly one `scaffold.cliBehindMinimum` (info) naming installed+min+gap; equal → none; above → none; no-minimum → none; malformed → `scaffold.providerMinimumMalformed` (warning) + no staleness; installed unparseable → none. **U2**: in the installed-unparseable case, also assert the provenance still records the producing CLI version **honestly** (the pre-existing `generator` value, never a fabricated version) while the comparison is skipped (spec Edge: "CLI version cannot be determined at runtime").
- [X] T023 [US2] Exit-code / classification parity test: a behind-minimum run's `Outcome` and exit code are **identical** to an at/above run (SC-004) — assert against the exit map (`CommandReports.fs:1473-1482`); neither new code escalates.
- [X] T024 [US2] Three-projection parity in `tests/FS.GG.SDD.Cli.Tests/` (extend `ScaffoldParityTests.fs`, model on `EarlyStageProjectionTests.fs`): behind → advisory fact identical across `--json`/`--text`/`--rich`; rich zero-ANSI when redirected; JSON byte-stable.

**Checkpoint**: US2 independently testable — visible warning at production time, provably non-blocking, byte-identical across projections.

---

## Phase 5: US3 — Author learns the re-seed path (P2)

**Goal**: The advisory carries a next-action pointer to the supported re-seed path, and docs
document it. (SC-006.) **Remedy is `fsgg-sdd init` (not `refresh`) — D8 correction.**

- [X] T025 [US3] Add the `NextAction` branch in `src/FS.GG.SDD.Commands/CommandReports.fs` (after the blocking branch, alongside the `earlyStageGuidance` pattern at `:1191-1213`) keyed on `scaffold.cliBehindMinimum`: `ActionId="reseedSeededSkills"`, `Command=Some Init`, `RequiredArtifacts=[.claude/skills/…, .codex/skills/…, .fsgg/early-stage-guidance.md]` (sorted), `BlockingDiagnosticIds=[]`, `Reason` = upgrade CLI + re-run `init`; note `refresh` does not re-seed. Per `contracts/advisory.md` (E6).
- [X] T026 [US3] [P] Assert `nextAction.actionId == "reseedSeededSkills"` appears in json/text/rich in the behind case (extend the Phase 4 parity tests); confirm the `Correction`/`Reason` names the init re-seed path.
- [X] T027 [US3] Verify (test or manual quickstart Scenario F) that `fsgg-sdd init` re-materializes the 15 seeded skills + `.fsgg/early-stage-guidance.md` idempotently/no-clobber (`initEffects`/`canOverwrite`), and that `refresh` does not.

**Checkpoint**: US3 independently testable — advisory points at a remedy that actually works.

---

## Phase 6: Docs, cross-repo & polish (FR-010, SC-005)

- [X] T028 [P] Document `requiredMinimumCliVersion` in `docs/release/schema-reference.md` (declared-exception scaffold-provenance section, `:59-77`): additive optional, schema stays v1, `tryParse` defaults `None`.
- [X] T029 [P] Add a migration note under `docs/release/migrations/` (feature-050 precedent, `README.md:44-50`): additive field, schema v1, minor bump, backward/forward compatible.
- [X] T030 [P] Document the behind-minimum re-seed remedy for authors (FR-010/US3) under `docs/reference/…`: upgrade `fsgg-sdd`, re-run `fsgg-sdd init`; explicitly note `fsgg-sdd refresh` does **not** re-seed. Keep Claude and Codex guidance aligned (update the getting-started / refresh-agents seeded-skill text equivalently if it references remediation).
- [X] T031 [P] Value-agnostic guard test/check (SC-005): `git grep -nE 'fs-gg-ui-template|0\.3\.0|rendering.*template' -- src/ | grep -v tests/` returns nothing new; no provider-specific literal introduced in generic SDD.
- [X] T032 Cross-repo coordination (`cross-repo-coordination`): confirmed the key against epic
  FS-GG/.github#85. **Reconciliation finding:** the merged sibling work (ADR-0008/#86, registry
  #87, `FS.GG.Templates#43`) uses a **nested `minimumFsggSdd: { version }`** mapping — NOT the
  flat `minimumCliVersion` scalar this feature's spec/contracts originally proposed. Per the
  cross-repo protocol (structured artifacts are the machine contract) SDD was **aligned to the
  merged upstream shape**: `Config.fs` now reads `minimumFsggSdd.version` (YAML-null → `None`,
  the real PENDING-PUBLISH state), the contract doc + fixtures updated, and a
  `min-pending.providers.yml` null-version case added. SDD-internal names
  (`ProviderDescriptor.MinimumCliVersion`, provenance `requiredMinimumCliVersion`) unchanged. Ran
  the contracts version-bump checklist: additive public surface → **minor** bump
  `FS.GG.Contracts` 1.1.1 → 1.2.0 (`ContractVersion` + fsproj + self-report test), no handoff
  `contractVersion` involved. Registry/board update + the concrete-version publish flip remain the
  cross-repo follow-up (SDD#49 / #85).
- [-] T033 [P] [T2] Optional coherence — SKIPPED: `Schemas.fs` `ScaffoldProvenanceSchema` is NOT the serialization authority (the authoritative serializer is `ScaffoldProvenance.serialize` in `FS.GG.SDD.Artifacts`, which this feature updated and tests). The mirror already drifts — it omits feature-050's `effectiveParameters` — and nothing consumes it for the provenance write; adding one field would not reconcile the mirror. Per the [T2]/D10 optional guidance, skipped to avoid expanding scope; the pre-existing `effectiveParameters` mirror-drift is recorded here as the known issue.
- [X] T034 Full-suite regression: `dotnet test FS.GG.SDD.sln`; run `quickstart.md` scenarios A–G; confirm release-conformance tests (`ReleaseConformanceTests.fs`, `ReleaseBoundaryTests.fs`) still pass with the new diagnostic codes / additive field.

---

## Dependencies

- Phase 1 (Version) → Phase 2 (types/diagnostics that don't need Version, but comparison in Phase 4 does) → Phase 3 (US1) → Phase 4 (US2) → Phase 5 (US3) → Phase 6.
- US1 (Phase 3) is the MVP and is shippable alone (no advisory). US2 depends on US1's provenance + Phase 1 comparison. US3 depends on US2's advisory existing.
- T032 (cross-repo key confirmation) should complete before merge but does not block local implementation (feature degrades when the key is absent).

## Summary

- **Task count**: 34. Foundational (Phase 1–2): 13 · US1: 6 · US2: 5 · US3: 3 · Docs/polish: 7.
- **Parallel opportunities**: within Phase 1 (T002/T005/T006), Phase 2 (T007/T009/T011/T013), Phase 3 (T015/T017/T019), Phase 4 (T022), Phase 5 (T026), and Phase 6 (T028–T031, T033) — all `[P]`-marked, distinct files.
- **Suggested MVP scope**: **US1 (Phase 1–3)** — records both CLI-version facts in provenance + report, deterministic, degrades to `null` when no minimum. Delivers standalone audit value before any warning behavior.
- **MVU note**: no new `Effect`/edge — the coherence check is pure inside the existing scaffold `update`; existing provenance `WriteFile` reused.
