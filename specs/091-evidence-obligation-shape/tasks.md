---
description: "Task list for 091 — slim the evidence declaration shape (omit always-null optional fields)"
---

# Tasks: Slim the Evidence Declaration Shape (Omit Always-Null Optional Fields)

**Input**: Design documents from `specs/091-evidence-obligation-shape/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tier**: Tier 1 (artifact-layout change to the authored `evidence.yml` surface). Tests are REQUIRED (Constitution VI).

**Organization**: Phases run in sequence; `[P]` tasks within a phase touch different files and may run in parallel. Stories: `[US1]` slim scaffold, `[US2]` populated fields preserved, `[US3]` backward compatibility & idempotence.

**No-`.fsi` note**: Per plan.md §Constitution Check III, this feature changes no public surface. `renderEvidenceDeclaration`, `renderOptionalScalar`, and `renderSyntheticDisclosure` are members of the `internal` `HandlersEvidence` module; `Evidence.fsi` and every other signature file are untouched, and no surface baseline is refreshed. Principle I's "FSI before implementation" step is therefore vacuous here — the semantic tests through the public surface (`parseEvidenceArtifact`, the `evidence` command) still come first.

**MVU note**: `renderEvidenceDeclaration` is a pure `EvidenceDeclaration -> string`; the write remains an existing `WriteFile` effect resolved at the edge. No new `Model`/`Msg`/`Effect` boundary is introduced, so per-Principle-V no MVU-contract tasks apply.

**Touch-set (ADR-0021)**: disjoint from in-flight FS.GG.SDD#163 and FS.GG.SDD#174. Verified with `scripts/fsgg-coord overlap`. Do not widen without re-checking.

---

## Phase 1: Setup

- [X] T001 Confirm the build baseline is green before changes: `dotnet build FS.GG.SDD.sln`, then `dotnet test tests/FS.GG.SDD.Artifacts.Tests tests/FS.GG.SDD.Commands.Tests`. Record the pre-change pass count for regression comparison.

---

## Phase 2: Foundational — pin the current reader semantics (BLOCKS all stories)

**Purpose**: The whole feature rests on the claim that the reader maps absent-key ≡ plain-`null` (research.md R1). Prove it against the *unchanged* reader before touching the writer. If this phase fails, the feature is invalid and needs a schema-version bump after all.

- [X] T002 [US3] In `tests/FS.GG.SDD.Artifacts.Tests/EvidenceArtifactTests.fs`, add a test that parses two evidence documents differing **only** in that one carries explicit `syntheticDisclosure: null`, `rationale: null`, `owner: null`, `scope: null`, `laterLifecycleVisibility: null` lines and the other omits all five keys. Assert the two `EvidenceDeclaration list` values are **equal**. This MUST pass against the current, unmodified reader (FR-005, SC-003).
- [X] T003 [US3] In the same file, add a test pinning the feature-161 boundary against the unmodified reader: a *quoted* `rationale: "null"` parses to `Some "null"`, while a bare `rationale: null`, `rationale: ~`, and an empty `rationale:` each parse to `None` (FR-006).

**Checkpoint**: The reader's absent≡null equivalence is now a test, not an assumption. Both tests pass before any writer change (they characterize existing behavior).

---

## Phase 3: User Story 1 — Slim scaffold (Priority: P1) 🎯 MVP

**Goal**: A scaffolded `evidence.yml` whose declarations carry no `null` boilerplate.

**Independent Test**: Scaffold a work item; `grep -c` for the five keys returns 0; the file still parses.

### Tests for US1 (write first, ensure they FAIL)

- [X] T004 [US1] In `tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs`, add a test that runs `evidence` on `initializedAnalyzedProject ()` and asserts the emitted `work/<id>/evidence.yml` contains **none** of `syntheticDisclosure:`, `rationale:`, `owner:`, `scope:`, `laterLifecycleVisibility:`, while still containing `id:`, `kind:`, `subject:`, `result:`, `synthetic:`, and `notes:` (FR-001, FR-002, SC-001). MUST fail before the writer change.
- [X] T005 [US1] In the same file, add a test asserting the emitted text has **no blank line and no trailing whitespace** anywhere in the `evidence:` block, and that `parseEvidenceArtifact` accepts it (FR-004). This is the guard against the naive `| None -> ""` fix, which would leave five blank lines per declaration. MUST fail before the writer change.

### Implementation for US1

- [X] T006 [US1] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersEvidence.fs`, change `renderOptionalScalar` to return `string option` (`value |> Option.map (fun v -> $"    {name}: {yamlString v}")`) instead of emitting `"    {name}: null"` for `None`.
- [X] T007 [US1] In the same file, change `renderSyntheticDisclosure` to return `string option`, `Some` only when a disclosure is present, rendering the same 3-line nested `standsInFor`/`reason` mapping as today.
- [X] T008 [US1] In `renderEvidenceDeclaration`, replace the five fixed template lines with a single spliced `optionalLines` block: collect the five `string option` renderers in their existing order, `List.choose id`, and emit `""` when empty or `"\n" + String.concat "\n" lines` when not — with the **leading newline carried by the block**, so `notes:` follows `synthetic:` directly when every optional is `None` (FR-004).

**Checkpoint**: T004 and T005 pass. Scaffolded evidence is slim and well-formed. This is the MVP — shippable alone.

---

## Phase 4: User Story 2 — Populated optionals are preserved (Priority: P1)

**Goal**: Omission applies to `None`, never to a value. Nothing an author wrote is eaten.

**Independent Test**: Author each of the five fields, re-run `evidence`, assert every value survives verbatim.

### Tests for US2 (write first, ensure they FAIL against a buggy writer)

- [X] T009 [P] [US2] In `EvidenceCommandTests.fs`, add a test that authors a declaration with `synthetic: true` and a populated `syntheticDisclosure` (`standsInFor` + `reason`), re-runs `evidence`, and asserts the nested mapping is re-emitted with **both** child keys and their exact values (FR-003).
- [X] T010 [P] [US2] In `EvidenceCommandTests.fs`, add a test that authors populated `rationale`, `owner`, `scope`, and `laterLifecycleVisibility`, re-runs `evidence`, and asserts all four scalar values are re-emitted unchanged (FR-003).
- [X] T011 [P] [US2] In `EvidenceCommandTests.fs`, add a test that authors `rationale: "null"` (quoted) and asserts that after a re-run the emitted text still contains the quoted `rationale: "null"` — neither omitted nor unquoted (FR-006, the 161 boundary). This is the assertion the current 161 test only makes negatively.
- [X] T012 [US2] ~~Add a test that a declaration with `synthetic: true` and no `syntheticDisclosure` key still raises the synthetic-without-disclosure diagnostic (FR-009).~~ **Not written — already covered.** The pre-existing `evidence blocks undisclosed synthetic evidence without mutation` feeds `undisclosedSyntheticInput`, which carries no `syntheticDisclosure` key at all; the gate tests `Option.isNone declaration.SyntheticDisclosure` on the parsed model. A duplicate would have been green-from-birth and proved nothing. Replaced by a comment in `EvidenceCommandTests.fs` pointing at the existing test.
- [X] T012a [US2] **(added after adversarial review)** The same review noted that four of the five omitted fields are *gate-required* for deferrals (`RequiredKeys.requiredDeferralKeys`, `evidence.missingDeferralRationale`) — the one shape where omission could plausibly bite, and one the plan never analysed. Pin both directions in `EvidenceCommandTests.fs` (FR-010): a fully-populated `kind: deferral` declaration re-emits all four fields verbatim, and an under-specified one **blocks with `changedArtifacts: []`** so the writer is never reached. See research.md R3a.

**Checkpoint**: Authored content round-trips. Story 1 is now safe to ship.

---

## Phase 5: User Story 3 — Backward compatibility & idempotence (Priority: P2)

**Goal**: Old verbose files parse; the file settles to a stable slim form; re-runs change no byte.

**Independent Test**: Parse a verbose fixture (T002 covers it); run `evidence` twice and diff.

- [X] T013 [US3] Adapt the existing `evidence re-run is byte-idempotent — bare null optional scalars are not rewritten to "null"` test in `EvidenceCommandTests.fs`. Keep the `Assert.Equal(first, second)` byte-idempotence core (FR-007). Replace `Assert.Contains("rationale: null", second)` — now false by design — with `Assert.DoesNotContain("rationale:", second)`. Keep the three `DoesNotContain("… \"null\"")` assertions, and update the comment to record that the field is now *omitted* rather than emitted as `null`, with T011 carrying the positive quoted-`"null"` guard the removed `Contains` used to imply.
- [X] T014 [US3] Update the four verbose fixture strings in `EvidenceArtifactTests.fs` (the existing `syntheticDisclosure: null` … `laterLifecycleVisibility: null` block at lines ~46–50, and the `.Replace("rationale: null", …)` / `.Replace("owner: null", …)` mutations at ~98–99) so they continue to exercise the **verbose input** path deliberately — these are reader fixtures and MUST keep the explicit `null` lines, proving FR-005. Add a comment saying so, so a future cleanup does not "helpfully" slim them.
- [X] T015 [US3] Verify `tests/FS.GG.SDD.Artifacts.Tests/MalformedReferenceTests.fs` (outside this feature's touch-set — read-only check) still passes unchanged: it feeds a verbose fixture to the reader, which this feature does not touch. If it fails, the reader was changed and the plan is violated.

**Checkpoint**: All three stories independently verified.

---

## Phase 6: Polish & cross-cutting

- [X] T016 Run the full suite: `dotnet build FS.GG.SDD.sln && dotnet test FS.GG.SDD.sln`. Zero failures; pass count ≥ the T001 baseline plus the new tests.
- [X] T017 [P] Confirm no `.fsi` file changed and no public-surface baseline moved: `git diff --name-only origin/main -- '*.fsi'` prints nothing (SC-005, Constitution III).
- [X] T018 [P] Confirm no `schemaVersion` value changed: `git diff origin/main -- src/FS.GG.SDD.Artifacts/SchemaVersion.fs` prints nothing (SC-005, research.md R1).
- [X] T019 [P] Confirm the `fs-gg-sdd-evidence` skill body is untouched, so its pinned `sha256` in `FS-GG/.github` `registry/skills.yml` stays valid: `git diff --name-only origin/main -- '*/skills/fs-gg-sdd-evidence/SKILL.md'` prints nothing (plan.md §Agent-facing behavior, ADR-0017).
- [X] T020 Measure SC-002 on a real fixture: render an evidence file with N obligations and assert the line count dropped by exactly `5 × N` versus `git show origin/main`'s rendering of the same input. Record the number in the PR body.
- [X] T021 Walk `quickstart.md` scenarios 1–7 against a real workspace and confirm each expected output.
- [X] T022 **(unplanned; added during implementation)** Regenerate the committed readiness goldens.
      `evidence.yml`'s bytes are hashed into `readiness/<id>/work-model.json`'s source digests, whose
      digest is embedded in `verify.json`/`ship.json`/`summary.md` — so slimming the file moves five
      `sha256` values. Widen the `Paths:` touch-set on FS.GG.SDD#165 to include
      `tests/FS.GG.SDD.Commands.Tests/goldens/**`, re-run `scripts/fsgg-coord overlap` against every
      live item **before** touching them (ADR-0021), then
      `FSGG_UPDATE_BASELINE=1 dotnet test tests/FS.GG.SDD.Commands.Tests --filter ReadinessViewGoldenTests`.
      Assert the diff contains **zero non-digest lines** (SC-006) — that is what proves no readiness
      semantics moved.

---

## Dependencies

- **T001** → everything (baseline).
- **Phase 2 (T002–T003)** → **blocks all stories**. If the absent≡null equivalence does not hold, stop: the feature needs a schema bump and the plan is wrong.
- **T004, T005** (tests) → **T006, T007, T008** (implementation). Tests fail first.
- **T006, T007** → **T008** (the splice consumes the two `string option` renderers).
- **Phase 3** → **Phase 4** → **Phase 5** by story priority; T009–T012 and T013–T015 need the new writer.
- **Phase 6** after all stories.

## Parallel execution

- T002 and T003 touch the same file (`EvidenceArtifactTests.fs`) — **sequential**.
- T009, T010, T011 are marked `[P]` as independent test cases, but all land in `EvidenceCommandTests.fs`; run them as one edit pass rather than concurrently.
- T017, T018, T019 are genuinely independent read-only checks and may run concurrently.

## Requirement coverage

- FR-001: T004, T006
- FR-002: T004, T006
- FR-003: T009, T010
- FR-004: T005, T008
- FR-005: T002, T014, T015
- FR-006: T003, T011, T013
- FR-007: T013
- FR-008: T016 (existing suite unchanged)
- FR-009: **no new test** — pre-existing `evidence blocks undisclosed synthetic evidence without mutation` (see T012)
- FR-010: T012a
- SC-001: T004
- SC-002: T004 (zero lines between `synthetic:` and `notes:` on every declaration ⇒ the 5 × N delta) + T020 (measured 170 → 140 on a real 6-obligation work item)
- SC-003: T002
- SC-004: T013
- SC-005: T017, T018
- SC-006: T022 — golden diff is digest-only (zero non-digest lines changed)

## Which tests are red-before-green, and which are not

Constitution VI demands red-then-green for behaviour-changing code. Stated honestly:

| Test | Red on `origin/main`? | Why |
|---|---|---|
| FR-001/002 slim scaffold (T004) | ✅ red | the old writer emits the five keys |
| FR-003 populated-field retention (T009, T010) | ✅ red | duplicate keys ⇒ parse failure pre-change |
| FR-006 quoted-`"null"` (T011) | ✅ red | same |
| FR-007 normalize-then-settle (T013) | ✅ red | old writer never omits |
| FR-010 deferral round-trip / block (T012a) | ✅ red | same duplicate-key mechanism |
| **T002/T003 reader characterization** | ❌ **green by design** | they *must* pass pre-change — that is the proof that omission is not a schema change |
| **FR-004 blank-line guard (T005)** | ❌ green pre-change | the old writer emits `<key>: null`, not a blank line. This guards the rejected `\| None -> ""` variant (research.md R4), not the shipped code |
| **FR-009 (T012)** | ❌ no new test | already covered by an existing test that is green on both sides |

## Implementation notes (post-hoc)

- **T002 held.** The absent≡null equivalence passed against the unmodified reader on the first run,
  so no schema bump. Had it failed, the whole feature was invalid — that is why it gated Phase 3.
- **`Evidence.fsi` was never touched;** `Evidence.fs` only had a stale comment corrected (it claimed
  the writer emits bare `null`, which stopped being true here). The reader logic needed no change,
  exactly as research.md R1 predicted.
- **T022 was not foreseen.** The plan's Verification Plan missed the digest cascade from
  `evidence.yml` into the readiness goldens. Caught by the full-suite run (T016), not by design.
- **T012a was not foreseen either.** The plan never asked what happens to the *deferral* shape, whose
  four gate-required fields are among the five being omitted. Caught by an adversarial cross-artifact
  review after implementation, not by the plan. The answer turned out to be safe (the gate blocks
  before the write) — but the plan got there by luck, and research.md R3a now records the reasoning.
