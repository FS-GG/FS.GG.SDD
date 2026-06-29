---
description: "Task list for feature 045 — Accept 4-Segment Versions in the Registry Validator"
---

# Tasks: Accept 4-Segment Versions in the Registry Validator

**Input**: Design documents from `/specs/045-validator-4-segment-versions/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/version-grammar.md, quickstart.md

**Tests**: Included and REQUIRED — the spec mandates a failing-before/passing-after corpus
addition (FR-007) and real-fixture evidence (Constitution VI). Write the failing test before the
regex widening.

**Tier**: Tier 1 (contracted, cross-repo) per plan Constitution Check. Phases run in sequence;
tasks within a phase marked `[P]` may run in parallel (different files, no ordering dependency).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: `US1` / `US2` / `US3` — traceability to spec user stories
- Paths are repo-relative to `/home/developer/projects/FS.GG.SDD`

---

## Phase 1: Setup & Baseline (Shared)

**Purpose**: Pin the current (buggy) behavior and stage the real fixture so the failing-before
evidence is genuine.

- [X] T001 Reproduce the false positive (quickstart S1): with the fixture carrying the 4-segment
  contract (after T002), run
  `dotnet run --project src/FS.GG.SDD.Cli -- registry validate tests/fixtures/registry/dependencies.yml --text`
  and record the pre-fix output (`invalid`, two `MalformedVersion` diagnostics on
  `governance-reference-gate-set`, exit 1) in the implementation notes. Confirms the bug exists before
  any code change.
- [X] T002 Refresh `tests/fixtures/registry/dependencies.yml` to mirror the canonical
  `FS-GG/.github` `registry/dependencies.yml`, adding the `governance-reference-gate-set` contract with
  `version: "1.2.1.1"` and `package-version: "1.2.1.1"` (per ADR-0007). Consult the live `.github`
  registry as source of truth. **The only change T009 requires is the `governance-reference-gate-set`
  addition** — other contracts' pins (e.g. `fsgg-contracts`) need only be grammar-valid, not synced to
  the live Contracts feed version, since the end-to-end test asserts a "valid" verdict (any valid version
  passes) and carries no coupling to the published Contracts version. This makes the end-to-end test
  (T009) real. (FR-005, data-model "governance-reference-gate-set contract")

**Checkpoint**: Bug reproduced over a real fixture; ready for the test-first change.

---

## Phase 2: User Story 1 — Validator accepts a legitimate 4-segment version (Priority: P1) 🎯 MVP

**Goal**: The typed validator returns "valid" for `1.2.1.1` on both `version` and `package-version`,
matching the Python authority.

**Independent Test**: Run the validator over the canonical fixture (4-segment
`governance-reference-gate-set`) → "valid", zero diagnostics; a genuinely malformed 4-ish string
(`1.2.x.4`) still reports `MalformedVersion`.

### Tests for User Story 1 (write FIRST, ensure they FAIL)

- [X] T003 [US1] In `tests/FS.GG.Contracts.Tests/RegistryDocumentTests.fs`, add accepted-case
  assertions for `version: "1.2.1.1"` and `package-version: "1.2.1.1"` (and `1.2.1.1-preview.1`)
  producing **no** `MalformedVersion`. Run `dotnet test tests/FS.GG.Contracts.Tests -c Release` and
  confirm these FAIL against the current `semVerRegex` (FR-001, FR-002, conformance vectors in
  contracts/version-grammar.md).

### Implementation for User Story 1

- [X] T004 [US1] Widen the private `semVerRegex` in `src/FS.GG.Contracts/Registry.fs` (the
  `let private semVerRegex = …` binding, ~line 226) to
  `@"^\d+\.\d+\.\d+(\.\d+)?(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$"` — adding the optional `(\.\d+)?`
  4th numeric segment **before** the prerelease/build groups, byte-for-byte mirroring
  `scripts/validate-registry.py`. Do NOT touch the legacy `validate`/`tryParseSemVer` path,
  `bareIntegerRegex`, or `rangeRegex` (research Decision 2). `Registry.fsi` stays UNCHANGED — the
  binding is `private`. (FR-001, FR-002, FR-006)
- [X] T005 [US1] Re-run `dotnet test tests/FS.GG.Contracts.Tests -c Release` and confirm the T003
  accepted-case tests now PASS (failing-before/passing-after evidence, Constitution VI).

**Checkpoint**: 4-segment versions accepted; MVP behavior delivered and unit-pinned.

---

## Phase 3: User Story 2 — Genuine version defects are still caught (Priority: P1)

**Goal**: The widening admits one numeric segment and nothing else — every previously-accepted form
still passes and every genuine defect still reports `MalformedVersion`.

**Independent Test**: Re-run the version corpus (`1`, `2`, `1.0.0`, `0.1.52-preview.1`, `1.x` range)
→ all pass; run `1.2.x.4`, `abc`, and `1.2.3.4.5` → each still `MalformedVersion`.

### Tests for User Story 2 (write FIRST for the new boundary case)

- [X] T006 [US2] In `tests/FS.GG.Contracts.Tests/RegistryDocumentTests.fs`, extend the malformed
  theory with `1.2.3.4.5` (five numeric segments) and confirm `1.2.x.4` and `abc` remain in it; assert
  each yields `MalformedVersion`. With the T004 regex in place this should PASS (the optional group is
  bounded to one extra segment) — if `1.2.3.4.5` is accepted, the regex is wrong. (FR-004, SC-002,
  boundary table in data-model.md)
- [X] T007 [US2] In the same file, confirm the pre-existing corpus (`1`, `2`, `1.0.0`,
  `0.1.52-preview.1`, and a `1.x` range) is present and still asserted non-malformed — no regression.
  (FR-003, SC-003)

### Verification for User Story 2

- [X] T008 [US2] Run `dotnet test tests/FS.GG.Contracts.Tests -c Release` and confirm the full suite is
  green: 4-segment accepted, boundary/garbage rejected, existing corpus unchanged. No source change
  beyond T004 is expected (the single bounded regex satisfies both stories). (SC-002, SC-003)

**Checkpoint**: Defect detection preserved; widening proven to add exactly one shape.

---

## Phase 4: User Story 3 — Downstream gate can adopt the fixed validator (Priority: P2)

**Goal**: Ship the fix as a published artifact (Contracts `1.1.0→1.1.1`, SDD line `0.2.0→0.2.1`) and
report the new versions so FS-GG/.github#49 can pin them.

**Independent Test**: From a clean consumer environment, install the published tool at `0.2.1` and run
`registry validate` against the canonical file → "valid"/exit 0, with no source build of this repo.

### End-to-end evidence (before publish)

- [X] T009 [US3] End-to-end check: run
  `dotnet run --project src/FS.GG.SDD.Cli -- registry validate tests/fixtures/registry/dependencies.yml --text`
  (and `--json`) over the refreshed fixture → `valid`, zero diagnostics, exit 0. Add/assert this as a
  test in `tests/FS.GG.Contracts.Tests/RegistryDocumentTests.fs` (or the CLI test project as fits the
  existing pattern). (FR-005, SC-001, quickstart S4)
- [X] T010 [US3] Parity check (quickstart S5): run both the typed CLI and
  `FS-GG/.github/scripts/validate-registry.py` over the same canonical file; confirm identical
  "valid"/exit-0 verdicts. Record the parity result. **Prerequisite**: a local `FS-GG/.github` working
  copy and Python 3 are available to run the stand-in script; if absent, obtain the checkout via the
  `cross-repo-coordination` protocol before this task. If the two verdicts diverge in any detail, surface
  it via `cross-repo-coordination` rather than leaving it silent. (FR-006, FR-010, SC-005, edge case
  "Parity drift")

### Coordinated version bump

- [X] T011 [P] [US3] Bump `FS.GG.Contracts` `1.1.0 → 1.1.1`: edit
  `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` `<Version>` and
  `src/FS.GG.Contracts/ContractVersion.fs` (`value` `1.1.0→1.1.1`, `patch` `0→1`) in lockstep.
  (FR-008, data-model version-bump table)
- [X] T012 [P] [US3] Bump the SDD product line `0.2.0 → 0.2.1`: edit `Directory.Build.local.props`
  `<Version>`, `docs/release/release-readiness.json` (`identity.version` + `generatorVersion.version`),
  and `docs/release/versioning-policy.md` ("currently 0.2.0" → "0.2.1"). (FR-009, data-model
  version-bump table)
- [X] T013 [US3] Confirm no breaking change is incurred: build/test pass and the apicompat gate does
  not trip (the widened regex is `private`, no `.fsi`/output-shape change). No migration note is owed
  (additive/non-breaking). (FR-008, contracts/version-grammar.md "Compatibility / stability")

### Publish & verify

- [ ] T014 [US3] Dispatch the two-package producer:
  `gh workflow run release.yml --repo FS-GG/FS.GG.SDD -f version=1.1.1` (satisfies the at-least-one-line
  tag guard, feature 044). Then confirm both versions are live on the org feed:
  `gh api /orgs/FS-GG/packages/nuget/FS.GG.Contracts/versions --jq '.[].name'` → `1.1.1`, and
  `gh api /orgs/FS-GG/packages/nuget/FS.GG.SDD.Cli/versions --jq '.[].name'` → `0.2.1`. (FR-009,
  quickstart S6) — depends on T011, T012.
- [ ] T015 [US3] Consumer install smoke (SC-004): from a clean directory with no SDD source checkout,
  install `fsgg-sdd` at `0.2.1` from the org feed and run `registry validate` against the canonical
  file; expect "valid"/exit 0. (SC-004, quickstart S6) — depends on T014.

**Checkpoint**: Fixed validator published and consumer-verified end-to-end.

---

## Phase 5: Cross-Repo Coherence & Reporting Follow-Through

**Purpose**: Restore registry coherence and report back so the downstream gate swap can land. Uses the
`cross-repo-coordination` skill. Strictly ordered after the feed confirms `1.1.1`/`0.2.1` (bump
checklist step 3 ordering invariant — `package-version` never ahead of the feed).

- [ ] T016 Advance `FS-GG/.github` `registry/dependencies.yml` `fsgg-contracts`
  `version`/`package-version` to `1.1.1` (via `cross-repo-coordination`) so `contract-coherence` stays
  green — only after T014 confirms `1.1.1` is live on the feed. (data-model "Ordering invariant",
  quickstart S7)
- [ ] T017 [P] Report Contracts `1.1.1` / CLI `0.2.1` on FS-GG/FS.GG.SDD#32, and set Coordination board
  item #32 to `In progress`, linked to #32 and FS-GG/.github#49. (FR-009, spec Assumptions)
- [ ] T018 [P] Note for the downstream consumer (FS-GG/.github#49, tracked separately): pin `0.2.1`,
  swap the `contract-coherence` gate to the typed CLI, and flip `registry-validator-typed` toward
  `coherent: true`. Not in this feature's scope to complete; record the handoff. (FR-010, data-model
  "Coherence-id outcome")

---

## Dependencies & Execution Order

### Phase order

- **Phase 1 (Setup)** → no deps; start immediately. T002 should precede T001 so the reproduction is over
  the real fixture.
- **Phase 2 (US1)** → after Phase 1. T003 (failing test) before T004 (regex); T005 after T004.
- **Phase 3 (US2)** → after T004 (relies on the widened regex). T006/T007 are sequential (same file,
  `RegistryDocumentTests.fs`); T008 after both.
- **Phase 4 (US3)** → after US1+US2 green. T009/T010 after T004; T011/T012 are `[P]`; T013 after the
  bumps; T014 after T011+T012; T015 after T014.
- **Phase 5 (Coherence)** → after T014 confirms the feed. T016 strictly after the feed; T017/T018 `[P]`.

### Cross-task dependencies (non-obvious)

- T001 depends on T002 (reproduce over the real fixture).
- T006/T008 depend on T004 (the regex must exist to verify the boundary holds).
- T014 depends on T011 + T012 (both versions must be bumped before the producer runs).
- T015, T016 depend on T014 (feed must serve the new versions first).

### Parallel opportunities

- T011 / T012 (Contracts bump vs. SDD-line bump — different files).
- T017 / T018 (independent reporting/handoff actions).

---

## Implementation Strategy

### MVP (Stories 1 + 2 — both P1)

1. Phase 1: refresh fixture, reproduce the bug.
2. Phase 2: failing 4-segment test → widen `semVerRegex` → green.
3. Phase 3: confirm boundary (`1.2.3.4.5`/`1.2.x.4`/`abc` rejected) and no regression.
4. **STOP and VALIDATE**: full `FS.GG.Contracts.Tests` green + end-to-end fixture "valid" (T009).

The source fix and its evidence are complete at this point — the validator is correct.

### Delivery (Story 3 — P2)

5. Parity check, coordinated bump, publish, consumer smoke.
6. Cross-repo coherence advance + report on #32.

---

## Story summary

| Story | Priority | Tasks | Count |
|---|---|---|---|
| Setup/Baseline | — | T001–T002 | 2 |
| US1 — accept 4-segment | P1 (MVP) | T003–T005 | 3 |
| US2 — defects still caught | P1 | T006–T008 | 3 |
| US3 — publish & adopt | P2 | T009–T015 | 7 |
| Cross-repo follow-through | — | T016–T018 | 3 |

**Total**: 18 tasks. **Suggested MVP**: US1 + US2 (T001–T008) — the source fix and its full test
evidence; US3 delivers the published artifact that closes the cross-repo request.

## Notes

- The single behavioral change is the one-line `semVerRegex` widening (T004). Everything else is
  evidence, version bookkeeping, publish, and cross-repo coherence.
- `Registry.fsi`, `bareIntegerRegex`, `rangeRegex`, and the legacy `tryParseSemVer` path are explicitly
  **out of scope** and must not change.
- Never mark a failing task `[X]`; never weaken an assertion to green a build.

## Implementation notes (evidence)

**Fixture refresh (T002).** `tests/fixtures/registry/dependencies.yml` was replaced with a verbatim
copy of the canonical `FS-GG/.github` `registry/dependencies.yml` (HEAD `219525b`), which carries
`governance-reference-gate-set@1.2.1.1` on both `version` and `package-version`. This makes the
end-to-end and parity evidence run over the real registry shape.

**T001 — pre-fix reproduction (before the `semVerRegex` widening).** Over the refreshed fixture:

```text
registry validate: tests/fixtures/registry/dependencies.yml → invalid (2 diagnostics)
  - MalformedVersion [governance-reference-gate-set]: Contract 'governance-reference-gate-set' has a malformed 'version': '1.2.1.1'.
  - MalformedVersion [governance-reference-gate-set]: Contract 'governance-reference-gate-set' has a malformed 'package-version': '1.2.1.1'.
exit: 1
```

The two new accepted-case unit tests (`1.2.1.1`, `1.2.1.1-preview.1`) also FAILED before the
widening (`Failed: 2, Passed: 38`), confirming genuine failing-before evidence (T003).

**T004 — the one-line widening.** `semVerRegex` →
`@"^\d+\.\d+\.\d+(\.\d+)?(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$"` (optional 4th numeric segment
before pre-release/build), byte-for-byte mirroring the Python authority's `(?:\.\d+)?`.

**T005/T008 — post-fix.** Full `FS.GG.Contracts.Tests` green (40/40); the malformed theory with
`1.2.3.4.5`/`1.2.x.4`/`abc` still reports `MalformedVersion`. Whole solution green (595 passed,
6 skipped, 0 failed).

**T009 — end-to-end.** `fsgg-sdd registry validate <fixture>` → `--text`: `valid (0 diagnostics)`,
exit 0; `--json`: `{ "valid": true, "diagnostics": [] }`, exit 0.

**T010 — parity (SC-005).** Both validators over the same canonical
`FS-GG/.github/registry/dependencies.yml`: typed CLI → `valid`, exit 0; Python authority
(`scripts/validate-registry.py`, PyYAML in a throwaway venv) → `Registry coherence OK`, exit 0. No
behavioral disagreement — the FR-006 "cannot disagree" invariant is restored.

**T013 — non-breaking.** No `.fsi` / surface baseline / `--json` shape change (the widened
`semVerRegex` is `private`); `scripts/apicompat-check.sh` reports `BREAK=0` (NoBaselineYet from local
feed-auth, which never fails the gate). No migration note owed.

**Version-coupling bookkeeping touched by the SDD-line bump (beyond the plan's headline list):**
`ContractVersionTests.fs` (1.1.0→1.1.1 self-report), `ReleaseContract.fs` + the
`tests/.../baselines/release-readiness.json` golden + `SchemaVersion.fs` fallback (0.2.0→0.2.1), and
the two `normalized-work-model` fixtures' embedded `generator.version` (+ the `valid-work-item`
self-referential `outputDigest` recomputed to stay self-consistent). `RegistryDocumentParseTests.fs`
was updated for the refreshed fixture (5 dependency edges; `fsgg-contracts` package-version now
`1.1.0`) and gained a 4-segment assertion.
