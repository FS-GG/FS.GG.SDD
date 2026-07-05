---
description: "Task list for feature 075 — document the clarify decision-tag grammar and per-stage front-matter"
---

# Tasks: Document the clarify decision-tag grammar and per-stage front-matter

**Input**: `specs/075-clarify-grammar-docs/` — plan.md, spec.md, research.md, data-model.md, contracts/documented-grammars.md, quickstart.md

**Tier**: Tier 1 (drift-guarded agent-skill contract + regenerated sha256 manifest). No product `.fs`/`.fsi`/schema change.

**Organization**: Phases run in sequence. `[P]` = parallel-safe within its phase (different files, no intra-phase dependency). Story tags: `[US1]` decision-tag resolution, `[US2]` per-stage front-matter, `[US3]` duplicate-id + sha256 correction. Some tasks serve the whole feature (`[ALL]`).

**MVU applicability**: Not applicable — this feature adds documentation + one parser-validated test extension; no stateful/I/O workflow, no new module surface (Principle V/III N/A, recorded in T012).

## Phase 1: Drift-guarded reference doc + live-parser test (the source of truth)

**Purpose**: Put the authoritative grammars in `docs/reference/authoring-contracts.md` as labelled example blocks and make them fail-if-wrong via the live parser BEFORE writing skill prose that points at them. This is the failing-test-first discipline (Constitution VI) and the fix for "grammar lived only in an example."

- [X] **T001** [US1] Add a "Clarify decision-tag resolution" section to `docs/reference/authoring-contracts.md` with four labelled fenced blocks per `contracts/documented-grammars.md` C1: `clarify-decision:resolved`, `clarify-decision:deferred`, `clarify-decision:answer-does-not-resolve`, `clarify-dup:rejected`. Each block a minimal complete `clarifications.md` (valid front matter + the demonstrated body). Prose must state the load-bearing rule (AMB id on a `DEC-###` line under `## Decisions`/`## Accepted Deferrals`, plus the `## Remaining Ambiguity` interaction) and present `[AMB:AMB-###]` as the canonical form while noting the bracket is a convention (FR-001, FR-003, FR-005).
- [X] **T002** [US2] Add a "Per-stage front matter" section to `docs/reference/authoring-contracts.md` with the gating-vs-defaulted table from `research.md` R1 (all authored stages), the closed `stage` vocabulary, and the free-string note for `changeTier`/`status`. Add two labelled blocks per C2: `front-matter:clarify-minimal` (only `schemaVersion, workId, stage, sourceSpec` → accepted) and `front-matter:clarify-missing-required` (omit `sourceSpec` → `malformedClarificationFrontMatter`). Do NOT add any block asserting a rejected `tier`/`status` value — the parser enforces none (FR-004).
- [X] **T003** [US3] Add the `Source Snapshot`/`sha256` correction to `docs/reference/authoring-contracts.md`: one factual paragraph stating it is a checklist/plan (and tasks/evidence `sources.digest`) concept, optional, 64-hex-format-checked only when present, used only for staleness, never required to author, and absent from clarifications. No new example block (C3) (FR-006).
- [X] **T004** [ALL] Confirm the exact public parser entry points and diagnostic ids the new test handlers will call, by reading `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Clarification.fs` (`parseClarificationFacts`, `parseClarificationFrontMatter`, blocking-ambiguity + `duplicateClarificationId` mapping) and the existing dispatch in `tests/FS.GG.SDD.Commands.Tests/AuthoringDocsContractTests.fs`. Record the resolved names inline in T005 before editing. (De-risks the C4 note "verify exact entry points".)
- [X] **T005** [ALL] Extend `tests/FS.GG.SDD.Commands.Tests/AuthoringDocsContractTests.fs` with handlers for the six new labels (table in `contracts/documented-grammars.md`), each extracting its fenced block, running it through the live parser resolved in T004, and asserting the stated outcome (0 blocking / ≥1 blocking / `duplicateClarificationId` present / Ok / `malformedClarificationFrontMatter`). Reuse the existing label-dispatch pattern (Constitution IV). Depends on T001–T004.
- [X] **T006** [ALL] Run `dotnet test tests/FS.GG.SDD.Commands.Tests --filter "FullyQualifiedName~AuthoringDocsContract"` and confirm the new blocks pass through the live parser (green). If a block's outcome disagrees with the asserted one, the parser wins — fix the example/prose in T001–T003, not the assertion. Depends on T005.

## Phase 2: Skill bodies (human-facing guidance) + byte-identical mirror

**Purpose**: Author the two skill bodies so a first-time author succeeds from the skills alone (SC-001), pointing at the now-drift-guarded reference for the authoritative form. Then mirror each into `.codex` byte-identically.

- [X] **T007** [US1] Edit the canonical `.claude/skills/fs-gg-sdd-clarify/SKILL.md` body: add the decision-tag resolution mechanism (both halves — tag on a decision line AND not blocking under `## Remaining Ambiguity`), the answer-vs-tag distinction, the `duplicateClarificationId` declaration-vs-reference trap with the safe once-per-section deferral pattern, and a worked example mirroring `docs/examples/lifecycle-artifacts/clarifications.md`. Point to `docs/reference/authoring-contracts.md` for the authoritative grammar. Keep front matter (`name`/`description`) unchanged (FR-001, FR-003, FR-005). Depends on Phase 1.
- [X] **T008** [US2][US3] Edit the canonical `.claude/skills/fs-gg-sdd-authoring-contracts/SKILL.md` body: add the per-stage front-matter contract (gating vs defaulted table, `stage` vocabulary, free-string `changeTier`/`status`) and the `Source Snapshot`/`sha256` correction; update the "three load-bearing grammars" framing to include the newly documented ones without misstating the count. Point to the reference doc. Keep front matter unchanged (FR-004, FR-006). Depends on Phase 1. [P] with T007 (different file).
- [X] **T009** [ALL] Mirror T007 and T008 byte-identically into `.codex/skills/fs-gg-sdd-clarify/SKILL.md` and `.codex/skills/fs-gg-sdd-authoring-contracts/SKILL.md` (`diff -q` the pairs → identical). No generator exists; hand-copy (FR-007). Depends on T007, T008.

## Phase 3: Rebuild, regenerate manifest, guard tests

**Purpose**: Refresh the embedded resources, regenerate the per-skill sha256 manifest, and confirm every drift/coherence guard is green.

- [X] **T010** [ALL] Rebuild so the `<EmbeddedResource>`-linked `.claude` bodies recompile: `dotnet build src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj`. Depends on T009.
- [X] **T011** [ALL] Regenerate the manifest: `dotnet run --project src/FS.GG.SDD.Cli -- registry skill-manifest --write`, then `... registry skill-manifest --check` must exit 0. Confirms `.agents/skills/skill-manifest.json` sha256 entries for both edited skills changed (FR-007). Depends on T010.
- [X] **T012** [ALL] Run the guard suite green and record evidence: `dotnet test tests/FS.GG.SDD.Commands.Tests` (SeededSkillsTests, ProcessSkillManifestTests, AuthoringDocsContractTests), `dotnet test tests/FS.GG.SDD.Artifacts.Tests` (ExampleArtifactsContractTests), `dotnet test tests/FS.GG.Contracts.Tests` (SkillMirrorTests). Note in this task line that Principle V (MVU) is N/A for this documentation feature (FR-008, SC-003, SC-004). Depends on T011.

## Phase 4: Acceptance validation

**Purpose**: Prove SC-001 end-to-end and the full suite.

- [X] **T013** [US1] Execute `quickstart.md` §3 (SC-001): reading ONLY the two edited skill bodies, hand-author a scratch `work/<id>/clarifications.md` with an open `AMB-001`, run `dotnet run --project src/FS.GG.SDD.Cli -- clarify --work <id> --text`, and confirm the stage advances first-try (no `malformedClarificationFrontMatter`/`missingClarificationAnswer`/`unresolvedBlockingAmbiguity`/`duplicateClarificationId`; `--text` blocking counters read zero). Remove the scratch work item after. (SC-001, SC-002).
- [X] **T014** [ALL] Run the full `dotnet test` suite green (no regressions from the doc/test/manifest changes). Depends on Phase 3.

## Dependencies (beyond phase ordering)

- T005 depends on T001–T004; T006 on T005.
- T007/T008 depend on Phase 1 (they reference the drift-guarded grammar); T009 on T007+T008.
- T010→T011→T012 strictly ordered (build → manifest → guards).
- T013 depends on T012 (skills final + build green).

## Parallel opportunities

- Phase 1: T001, T002, T003 edit the same file (`authoring-contracts.md`) so serialize them; T004 (read-only research) can run alongside T001–T003.
- Phase 2: T007 [P] T008 (different skill files); T009 must follow both.

## Suggested MVP scope

**US1 (P1)** — the decision-tag resolution documentation (T001, T004, T005, T006, T007, T009–T014). Delivering only US1 already erases the top authoring blocker (4× clarify block) and satisfies SC-001's core. US2 (front-matter, T002/T008) and US3 (sha256 correction, T003) are independent increments layered on top.

## Task count per story

- US1: T001, T005*, T006*, T007, T013 (+ shared T004, T009–T012, T014) — 5 story-specific.
- US2: T002, T008 — 2 story-specific.
- US3: T003, T008 (shared) — 1–2 story-specific.
- Shared/ALL: T004, T005, T006, T009, T010, T011, T012, T014.

(*T005/T006 cover all new labels including US1's; counted under US1 as its primary driver.)
