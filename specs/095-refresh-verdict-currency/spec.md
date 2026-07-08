# Feature Specification: `refresh` Reports True Facts About the Committed Ship Verdict

**Feature Branch**: `item/188-sdd-refresh-ship-verdict-currency-report`

**Created**: 2026-07-08

**Status**: Draft

**Input**: User description: "refresh reports true facts about the committed ship verdict — strengthen the ship.json currency check so `ship: current` stops lying, re-attribute `malformed` away from the well-formed verdict, and make the stale-source severity symmetric whether or not the verdict is present (FS.GG.SDD#188)"

## Overview

Feature **092** (ADR-0026, FS.GG.SDD#177) made `readiness/<id>/ship-verdict.json` a **committed**
artifact: unlike `ship.json`, which is gitignored, the verdict survives a fresh clone and is read
straight out of git history. Its entire value is that a reader can trust it as a durable fact.

`fsgg-sdd refresh` reports the currency of that verdict in `generatedViews[].currency`. In three
situations it reports a word that is **not true of the verdict**. In every case the accompanying
diagnostic *message* already names the real cause, and in every case the exit code is already
correct — so nothing behaves wrongly today. What is wrong is the **machine-readable fact**: a
consumer that reads `generatedViews[].currency` (rather than parsing prose) draws a false
conclusion about a committed artifact.

This feature makes each reported currency word true *of the artifact it is attached to*.

The three defects, confirmed against `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersRefresh.fs`
at `39fa3e5`:

**A. `malformed` is attributed to the well-formed verdict.** `downstreamClass` (`:438-449`) gates
on `parsesAsJson` (`:350`), which is strictly weaker than `ShipModule.parseShipView`
(`Ship.fs:78`, exported at `Ship.fsi:55`). A `ship.json` that is valid JSON but not a valid ship
view — a future `schemaVersion`, a bad `workId`, an unparseable `stage` — yields
`shClass = AlreadyCurrent`. The verdict projection then fails, and `:527` stamps
`ViewCurrencyClass.Malformed` on the **verdict**. The same run reports `ship: current`. So the
report says the committed artifact is corrupt and its gitignored source is fine, when the truth is
exactly inverted.

**B. Severity is asymmetric across the presence of the verdict.** With a stale `ship.json`: if the
verdict is **present**, `:533` yields `Stale` → `refresh.staleView` (a **warning**). If the verdict
is **absent**, `:539` yields `Missing` → `refresh.blockedUpstreamView` (an **error**, `:616`). The
underlying state is identical — the source moved, re-run `ship` — and the absent case is the
ordinary fresh-clone-then-edit path. Nothing about it is "blocked".

**C. A dead branch.** In the `(AlreadyCurrent, _)` arm, `:528`'s `| None -> Missing` is unreachable:
`shClass = AlreadyCurrent` is computed from `snapshot (shipPath workId)` returning `Some`, so
`textOf (shipPath workId)` at `:517` cannot be `None`. It is kept only for match totality.

**Exit codes do not change.** `verdictClass` already participates in `structuredClasses` (`:541-547`),
so a non-clean verdict already forces `summaryRenderable = false` (`:620`) and emits the
`refresh.unrenderableSummary` **error** (`:631`). Both A's and B's states are non-clean before and
after this feature. This is a re-attribution of *words*, not a change of *behavior*.

**Change tier: Tier 1** (command output-contract change: the `currency` value reported on two
`generatedViews[]` rows, and the severity of one diagnostic row). No persisted schema version
changes. No new diagnostic id is required by A or C; B needs one severity correction, which FR-009
resolves by reusing the existing `refresh.staleView`.

## Clarifications

### Session 2026-07-08

- **Q (AMB-001): Does A's fix belong in `downstreamClass` (shared by analysis/verify/ship) or only
  at the verdict's projection site?** → A: **In `downstreamClass`, per-artifact.** Patching only the
  verdict site would silence the wrong word on the verdict but leave `ship: current` lying about a
  `ship.json` that does not parse as a ship view. The root cause is that `downstreamClass` validates
  *syntax* (`parsesAsJson`) where the artifact's contract is *schema*. The fix threads a per-artifact
  validator into `downstreamClass` so `ship.json` is validated with `parseShipView` while
  `analysis.json` and `verify.json` keep their current `parsesAsJson` gate. Widening analysis/verify
  to schema-validation is a **separate** decision with its own blast radius and is explicitly out of
  scope (see Out of Scope). `downstreamClass` is a **local** `let` inside the refresh handler, not a
  cross-module export, so this change touches no other file — the touch-set declared on
  FS.GG.SDD#188 stands unchanged.

- **Q (AMB-002): When `ship.json` is valid JSON but not a valid ship view, what should the verdict's
  currency be?** → A: **`blocked`**, via the existing `| _, Some _ -> Blocked` arm (`:536`). Once
  `shClass` is correctly `Malformed`, the `(AlreadyCurrent, _)` arm no longer matches and the verdict
  falls through to the arm whose meaning is exactly right: *"present, but the source cannot be read
  or trusted, so refresh cannot tell whether the committed verdict is current."* The verdict is not
  malformed; it is un-assessable. Its diagnostic becomes `refresh.blockedUpstreamView` pointing at
  `ship.json`, whose own row now carries `refresh.malformedGeneratedView`. Every word is true of the
  artifact it names, and the operator is pointed at the file that actually needs repair.

- **Q (AMB-003): Should B be fixed by changing the currency word (`Missing` → `Stale`) or by changing
  the severity of the `Missing` row?** → A: **Neither — by changing which diagnostic the
  `(Stale, None)` state emits.** The verdict *is* missing; reporting `stale` about a file that does
  not exist would trade one false word for another, and `generatedViews[].currency` must stay true.
  What is wrong is the *diagnostic*: `refresh.blockedUpstreamView` (error) claims the verdict "cannot
  be refreshed until upstream is current", but the remediation is the plain `re-run ship`, identical
  to the present-verdict case. So `currency` stays `missing`, and the `(Stale, None)` state emits
  `refresh.staleView` (warning) against `ship.json`, matching the `(Stale, Some _)` case. The
  `Missing`-with-a-non-`Stale`-source states keep `refresh.blockedUpstreamView`.

- **Q (AMB-004): C is unreachable and harmless. Remove it, or keep it for totality?** → A: **Keep the
  arm, remove the ambiguity.** F# requires the match to be total and the compiler cannot prove
  `snapshot ⇒ textOf`. Deleting the arm breaks the build; making it throw trades a dead branch for a
  live crash risk. The arm stays and gains a comment stating the invariant that makes it unreachable
  and where that invariant is established. After A lands, this arm becomes *doubly* unreachable
  (`shClass = AlreadyCurrent` now additionally implies `parseShipView` succeeded), which the comment
  records. No behavior change; this is a readability obligation, and the FR that carries it is
  verified by inspection, not by a runtime test.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The report never calls a committed artifact corrupt when it is not (Priority: P1)

A maintainer's `ship.json` carries a `schemaVersion` from a newer CLI (or a hand-edited `workId`).
It is well-formed JSON. They run `fsgg-sdd refresh` and read the JSON report, or a CI job reads
`generatedViews[].currency` to decide whether the committed readiness artifacts in git are
trustworthy.

Today the report tells them `ship-verdict: malformed` and `ship: current`. The maintainer inspects
`ship-verdict.json`, finds it perfectly well-formed, and loses confidence in the report. The CI job,
which cannot read prose, concludes the committed artifact is corrupt and fails the wrong thing.

After this change the report says `ship: malformed` and `ship-verdict: blocked`, and points the
operator at `ship.json` — the file that actually needs repair.

**Why this priority**: This is the defect that makes a *committed* artifact — the one thing feature
092 exists to make trustworthy — carry a false machine-readable status. It also fixes `ship: current`
lying, which is a second false fact in the same report. It subsumes the surface symptom in #188's
title.

**Independent Test**: Fully testable by writing a `ship.json` that is valid JSON but fails
`parseShipView`, running `refresh`, and asserting the two `generatedViews[]` rows. Delivers value
alone: the report stops making two false claims.

**Acceptance Scenarios**:

1. **Given** a work item whose `ship.json` is valid JSON but not a valid ship view (unknown future
   `schemaVersion`), and a well-formed committed `ship-verdict.json`, **When** `fsgg-sdd refresh`
   runs, **Then** `generatedViews[]` reports `ship: malformed` and `ship-verdict: blocked`, and no
   row reports `ship-verdict: malformed`.
2. **Given** the same state, **When** `fsgg-sdd refresh` runs, **Then** the diagnostics contain
   `refresh.malformedGeneratedView` whose path is `ship.json`, and `refresh.blockedUpstreamView`
   whose path is `ship-verdict.json` and whose related upstream ref is `ship.json`.
3. **Given** the same state, **When** `fsgg-sdd refresh` runs, **Then** the exit code is unchanged
   from the pre-change behavior (non-zero, via `refresh.unrenderableSummary`), and no verdict file is
   written.
4. **Given** a `ship.json` that is **not** valid JSON at all, **When** `fsgg-sdd refresh` runs,
   **Then** `ship: malformed` as before — the stronger validator subsumes the weaker one and this
   pre-existing behavior is unchanged.
5. **Given** a work item whose `ship.json` is a fully valid ship view, **When** `fsgg-sdd refresh`
   runs, **Then** every currency word, diagnostic, effect, and exit code is byte-identical to the
   pre-change behavior.
6. **Given** a work item whose `analysis.json` or `verify.json` is valid JSON but not a valid
   analysis/verify view, **When** `fsgg-sdd refresh` runs, **Then** its currency is `current`,
   exactly as before — only `ship.json` gains schema validation.
7. **Given** a work item whose `ship.json` is valid JSON but not a valid ship view, **When**
   `fsgg-sdd refresh` runs, **Then** the refresh summary lists `ship` in `blockedViewIds` and **not**
   in `alreadyCurrentViewIds` — the same correction, told through the second field that carried it.

---

### User Story 2 - The same underlying state gets the same severity (Priority: P2)

A maintainer clones the repo (so `ship.json`, being gitignored, is absent), deletes or has never
produced `ship-verdict.json`, edits an authored source, and runs `refresh`. Alternatively the
verdict is present. Both are the ordinary "the source moved, re-run `ship`" state.

Today the absent-verdict path emits an **error** (`refresh.blockedUpstreamView`) and the
present-verdict path emits a **warning** (`refresh.staleView`), for the same underlying fact and the
same remediation. A consumer triaging by severity treats one as a blocker and the other as advice.

**Why this priority**: Real inconsistency in the reported contract, but strictly narrower than US1 —
it misreports *severity*, not the identity or integrity of a committed artifact. Independently
valuable and independently shippable.

**Independent Test**: Fully testable by driving both states (verdict present, verdict absent) against
a stale `ship.json` and asserting the diagnostic ids and severities match.

**Acceptance Scenarios**:

1. **Given** a stale `ship.json` (an authored source changed under it) and **no** committed
   `ship-verdict.json`, **When** `fsgg-sdd refresh` runs, **Then** the verdict's currency is
   `missing` and the emitted diagnostic is `refresh.staleView` (severity `warning`), not
   `refresh.blockedUpstreamView`.
2. **Given** a stale `ship.json` and a **present** committed `ship-verdict.json`, **When**
   `fsgg-sdd refresh` runs, **Then** the verdict's currency is `stale` and the emitted diagnostic is
   `refresh.staleView` (severity `warning`) — unchanged from today.
3. **Given** both states, **When** `fsgg-sdd refresh` runs, **Then** the exit codes are equal to each
   other and unchanged from the pre-change behavior.
4. **Given** an absent `ship-verdict.json` whose `ship.json` is `malformed` or `blocked` (i.e. **not**
   `stale`), **When** `fsgg-sdd refresh` runs, **Then** the verdict's currency is `missing` and the
   diagnostic remains `refresh.blockedUpstreamView` (severity `error`) — the severity correction is
   scoped to the stale-source state alone.

---

### User Story 3 - A reader can tell a dead branch from a live one (Priority: P3)

A maintainer reading the verdict-currency match encounters `| None -> Missing` in the
`(AlreadyCurrent, _)` arm and cannot tell whether it is reachable. They must reconstruct the
invariant relating `snapshot` to `textOf` across ~90 lines to conclude it is dead.

**Why this priority**: Pure readability. No behavior, no report, no test. Bundled because it lives
three lines from US1's edit and would otherwise be re-derived by the next reader.

**Independent Test**: Verified by inspection — the arm carries a comment naming the invariant and its
establishing site. No runtime assertion is possible for unreachable code.

**Acceptance Scenarios**:

1. **Given** the verdict-currency match, **When** a maintainer reads the `(AlreadyCurrent, _)` arm's
   `None` case, **Then** an adjacent comment states that `shClass = AlreadyCurrent` implies the
   snapshot exists, names where that is established, and notes the arm exists solely for match
   totality.

---

### Edge Cases

- **`ship.json` valid JSON, invalid ship view, verdict absent.** `shClass = Malformed`, verdict
  falls to `(_, None) -> Missing`. Source is not `Stale`, so FR-009 does not apply: the diagnostic
  stays `refresh.blockedUpstreamView` (error), correctly pointing at the unreadable source.
- **`ship.json` absent (fresh clone), verdict present.** Unchanged: `shClass = Missing`, verdict
  `(_, Some _) -> Blocked`. This is the fresh-clone invariant feature 092 protects — the verdict must
  never be reported `missing` when it is the one artifact that survived the clone.
- **Work model blocked.** `downstreamClass` short-circuits to `Blocked` before any validator runs
  (`:439`). The stronger `ship.json` validator must not be invoked in this state, and no currency
  word changes.
- **A `ship.json` that parses as a ship view but whose content is stale.** Untouched: schema validity
  and currency are orthogonal. `Stale` is decided by `wmChanged`, after the validator passes.
- **A future `schemaVersion` that `parseShipView` accepts.** Out of this feature's reach: whatever
  `parseShipView` accepts is, by definition, a valid ship view. This feature adopts `parseShipView`
  as the oracle; it does not redefine it.

## Requirements *(mandatory)*

### Functional Requirements

**A — attribute `malformed` to the artifact that is malformed**

- **FR-001**: `refresh` MUST validate `ship.json` against the ship-view schema (not merely as
  well-formed JSON) when computing its currency class.
- **FR-002**: `refresh` MUST continue to validate `analysis.json` and `verify.json` as well-formed
  JSON only; their reported currency MUST be unchanged for every input.
- **FR-003**: When `ship.json` is well-formed JSON but does not parse as a ship view, `refresh` MUST
  report `ship` currency as `malformed`.
- **FR-003a**: In that state, `refresh` MUST list `ship` in the summary's `blockedViewIds` and MUST
  NOT list it in `alreadyCurrentViewIds` — the bucket projection and the `currency` word must agree.
- **FR-004**: In that state, `refresh` MUST report `ship-verdict` currency as `blocked`, and MUST NOT
  report it as `malformed`.
- **FR-005**: In that state, `refresh` MUST emit `refresh.malformedGeneratedView` against `ship.json`
  and `refresh.blockedUpstreamView` against `ship-verdict.json` naming `ship.json` as the upstream.
- **FR-006**: `refresh` MUST NOT write, re-project, or delete `ship-verdict.json` when `ship.json`
  fails ship-view validation.
- **FR-007**: The exit code for every input MUST be identical to the pre-change behavior.
- **FR-008**: For a `ship.json` that parses as a valid ship view, every currency word, diagnostic,
  planned effect, and exit code MUST be byte-identical to the pre-change behavior.

**B — one underlying state, one severity**

- **FR-009**: When `ship.json` is `stale` and `ship-verdict.json` is absent, `refresh` MUST emit
  `refresh.staleView` (severity `warning`), not `refresh.blockedUpstreamView` (severity `error`).
- **FR-010**: In that state, `refresh` MUST continue to report `ship-verdict` currency as `missing` —
  the currency word describes the artifact, and the artifact is absent.
- **FR-011**: When `ship-verdict.json` is absent and `ship.json` is in any state **other than**
  `stale`, `refresh` MUST continue to emit `refresh.blockedUpstreamView` (severity `error`).
- **FR-012**: The stale-source-with-verdict-present path MUST remain `refresh.staleView` with currency
  `stale`, unchanged.

**C — the dead branch is legibly dead**

- **FR-013**: The unreachable `None` arm in the verdict's `(AlreadyCurrent, _)` case MUST be retained
  for match totality and MUST carry a comment naming the invariant that makes it unreachable and the
  line at which that invariant is established.

**Cross-cutting**

- **FR-014**: No persisted artifact schema version changes. `ship-verdict.json`, `ship.json`,
  `work-model.json`, and the release baseline are untouched on disk.
- **FR-015**: The `--json`, `--text`, and `--rich` projections MUST continue to report the same facts
  as each other; `--rich` MUST add and drop no facts and change no JSON byte.

### Key Entities

- **`ship.json`**: The gitignored, generated ship readiness view. Source of the verdict. Its currency
  is what US1 corrects.
- **`ship-verdict.json`**: The committed, durable compact verdict (feature 092 / ADR-0026). Survives a
  fresh clone without its source. Never reported `malformed` on account of its source.
- **`ViewCurrencyClass`**: The refresh-local currency vocabulary (`Current`/`AlreadyCurrent`/
  `Refreshed`/`Stale`/`Malformed`/`Missing`/`Blocked`/`NotApplicable`), projected to
  `GeneratedViewCurrency` on the report. Each word must be true of the artifact it labels.
- **`parseShipView`**: The existing ship-view schema oracle (`Ship.fsi:55`). Adopted, not redefined.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every input state, each `generatedViews[].currency` word is true of the artifact it
  names — verified by an exhaustive matrix over `{ship.json: absent, invalid-JSON, valid-JSON-invalid-view,
  valid-stale, valid-current} × {ship-verdict.json: absent, present}` (10 cells), asserting the
  reported word against the artifact's actual on-disk state.
- **SC-002**: Zero inputs produce a `ship-verdict: malformed` report while `ship-verdict.json` is
  well-formed.
- **SC-003**: Zero inputs produce a `ship: current` report while `ship.json` fails ship-view
  validation.
- **SC-004**: Across the 10-cell matrix, the exit code for each cell is identical before and after the
  change (regression-locked by a table test).
- **SC-005**: Two states that differ only in the presence of `ship-verdict.json`, over a `stale`
  `ship.json`, emit diagnostics of equal severity.
- **SC-006**: A maintainer reading the report for a bad `ship.json` is pointed at `ship.json` — the
  file needing repair — by the path on the sole `malformed` diagnostic, without reading prose.
- **SC-007**: Running `refresh` twice against an unchanged, fully valid work item is byte-identical on
  both runs (determinism is not regressed by the added validation).

## Assumptions

- `ShipModule.parseShipView` is the correct and sole oracle for "is this a valid ship view". This
  feature adopts it; it does not audit or redefine what it accepts.
- Threading a per-artifact validator through `downstreamClass` is confined to
  `HandlersRefresh.fs` — `downstreamClass` is a local binding (`:438`), not a module export. The
  touch-set declared on FS.GG.SDD#188 therefore stands, and no ADR-0021 overlap re-check is needed.
- `refresh.staleView`, `refresh.blockedUpstreamView`, and `refresh.malformedGeneratedView` already
  exist with the required severities (`DiagnosticConstructors.fs:931/939/946`). No new diagnostic id
  is introduced, so `docs/release/` baseline conformance is unaffected.
- `verdictClass` already participates in `structuredClasses` (`:541-547`), so the states this feature
  re-words are already non-clean and already emit `refresh.unrenderableSummary`. Exit codes are
  therefore invariant under this change — an assumption FR-007 and SC-004 lock down with tests rather
  than trust.
- `parseShipView` returns `Result<ShipView, Diagnostic list>`; its diagnostics are discarded here in
  favour of the currency word plus `refresh.malformedGeneratedView`, matching how `parsesAsJson`'s
  failure is handled today.

## Out of Scope

- **Schema-validating `analysis.json` and `verify.json`.** Same latent weakness (`parsesAsJson`), but
  each needs its own oracle, its own state matrix, and its own regression sweep. FR-002 pins their
  behavior unchanged so this feature cannot silently widen. A follow-up item.
- **Redefining what `parseShipView` accepts**, including any policy on future `schemaVersion` values.
- **`Ship.fs`'s redundant `|> List.sort` on `DispositionBlockingFindingIds`** (`Ship.fs:120`);
  `jsonStringList` already sorts. Noted in #188 as "not a defect". Harmless; removing it is a
  no-op cleanup that would touch `Ship.fs` for no behavioral reason. Left alone, and this spec
  records why so the next reader does not re-litigate it.
- **The `specs/*/readiness/*/*` gitignore depth-1 gap** noted in #188. No producer writes at that
  depth (`readinessDirectory workId` always inserts the id segment) and the seeded consumer rule has
  no gap. Not a defect; not fixed here.
- **Any change to `ship`, `verify`, or `analyze`.** This feature touches the `refresh` reporting path
  only.
