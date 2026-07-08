# Phase 0 Research: `refresh` Reports True Facts About the Committed Ship Verdict

**Feature**: 095 · **Issue**: FS.GG.SDD#188 · **Date**: 2026-07-08 · **Baseline**: `39fa3e5`

All findings below were confirmed by reading the source at the cited lines, not inferred.

---

## R1 — The ship-view oracle already exists and takes exactly the value we hold

**Decision**: Adopt `ShipModule.parseShipView` as the ship-view validator. Do not write a new one.

**Rationale**: `Ship.fsi:55` exports
`val parseShipView: snapshot: FileSnapshot -> Result<ShipView, Diagnostic list>`. Inside
`downstreamClass` (`HandlersRefresh.fs:442`) we already hold `snapshot path model : FileSnapshot option`,
so the `Some snap` case can pass `snap` **verbatim** — no re-read, no string round-trip, no new
`FileSnapshot` construction. The validator is strictly stronger than `parsesAsJson` (`:350`): a
non-JSON body fails `parseJsonView` before any field is read, so `parseShipView` subsumes it and
US1-AS4 (invalid JSON still reports `malformed`) is satisfied by construction rather than by a
second check.

**Alternatives considered**:
- *A bespoke "looks like a ship view" predicate in `HandlersRefresh`.* Rejected: a second, weaker
  oracle drifts from the real one the moment `Ship.fs` gains a field. The bug being fixed **is** a
  weaker oracle standing in for the real one.
- *Calling `shipVerdictEmission` and inspecting its `jsonOpt`.* Rejected: that conflates "is the
  source valid" with "can the verdict be projected", which is precisely the conflation that
  mis-attributes `malformed` to the verdict today (`:527`).

**Discarded output**: `parseShipView`'s `Diagnostic list` on the error path is dropped. The currency
word plus `refresh.malformedGeneratedView` already carry the operator-facing message, and this
matches how `parsesAsJson`'s `false` is handled today. Threading the inner diagnostics up is a
strictly larger report change and is not required by any FR.

---

## R2 — `downstreamClass` is local; the fix cannot leak out of the declared touch-set

**Decision**: Thread a per-artifact validator into `downstreamClass` as a parameter.

**Rationale**: `downstreamClass` is a `let`-bound closure at `HandlersRefresh.fs:438`, inside the
handler body. `grep -rn "downstreamClass" src/ tests/` returns exactly four hits — its definition
and its three call sites (`:451`, `:452`, `:453`), all in the same file. It is not exported (no
`HandlersRefresh.fsi` exists; the module is internal to `FS.GG.SDD.Commands`). Therefore:

- No `.fsi` changes. Constitution III's "visibility lives in `.fsi`" obligation is not triggered.
- No `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` change.
- The edit is confined to `HandlersRefresh.fs`, inside the `Paths:` line declared on FS.GG.SDD#188.
  **No ADR-0021 overlap re-check is required.**

**Shape**: `downstreamClass (isValid: FileSnapshot -> bool) path`, called as
`downstreamClass parsesAsJsonSnap (analysisPath workId)`,
`downstreamClass parsesAsJsonSnap (verifyPath workId)`, and
`downstreamClass parsesAsShipView (shipPath workId)`. Passing the validator (rather than branching on
the path) keeps `downstreamClass` honest about *what varies* and satisfies FR-002 by construction:
analysis and verify keep the identical predicate they use today.

**Alternatives considered**:
- *Branch inside `downstreamClass` on `path = shipPath workId`.* Rejected: couples a generic currency
  helper to one artifact's identity and makes FR-002 a runtime accident rather than a type-level fact.
- *Validate `ship.json` at the verdict site only (`:516`).* Rejected in spec AMB-001: it silences the
  wrong word on the verdict but leaves `ship: current` lying (FR-003 unmet).

---

## R3 — Exit codes are invariant, and this is provable from the source (not assumed)

**Decision**: Assert exit-code invariance across the whole state matrix; do not special-case it.

**Rationale**: `verdictClass` is a member of `structuredClasses` (`:541-547`). `structuredAllClean`
(`:552`) is `List.forall isClean` over that list, `summaryRenderable = structuredAllClean` (`:620`),
and a non-renderable summary emits `refreshUnrenderableSummary` (`:631`), an **error**
(`DiagnosticConstructors.fs:968`). So:

| State | Today | After |
|---|---|---|
| valid-JSON/invalid-view ship | `sh=AlreadyCurrent` (clean), `verdict=Malformed` (not clean) | `sh=Malformed` (not clean), `verdict=Blocked` (not clean) |
| ⇒ `structuredAllClean` | `false` | `false` |
| ⇒ `refresh.unrenderableSummary` | emitted (error) | emitted (error) |
| ⇒ exit code | non-zero | non-zero — **unchanged** |

The same argument covers B: `Stale` and `Missing` are both non-clean, before and after, so the
severity correction on the *verdict's own* diagnostic row cannot move the exit code either.

This is the single most important finding: **A and B are re-attributions of words, not changes of
behavior.** FR-007 and SC-004 pin it with a table test so the reasoning is enforced rather than
trusted.

**Risk this retires**: the obvious fear — "making `ship.json` validation stricter will start failing
runs that used to pass" — is false. The run *already* failed; it just blamed the wrong file.

---

## R3a — A second-order report change: `ship` leaves `AlreadyCurrentViewIds`

**Finding** (not anticipated by #188): `classifyToBucket` (`:740-747`) buckets by currency class —
`AlreadyCurrent` → `alreadyCurrentViewIds`, `Refreshed` → `refreshedViewIds`, `NotApplicable` → its
own bucket, and **everything else → `blockedViewIds`**. Those buckets surface on the report as
`refresh.AlreadyCurrentViewIds` / `RefreshedViewIds` / `BlockedViewIds` (`:780`).

So in the valid-JSON/invalid-view state, correcting `shClass` from `AlreadyCurrent` to `Malformed`
also moves `"ship"` out of `AlreadyCurrentViewIds` and into `BlockedViewIds`.

**This is correct and is part of the fix, not a side effect to be suppressed.** `ship` sitting in
`AlreadyCurrentViewIds` while its own `generatedViews[].currency` said `current` was the *same lie*
told through a second field; a consumer reading either field was misled. FR-003 is therefore
tightened by an acceptance scenario asserting the bucket move (US1-AS7), so the correction is pinned
in both projections rather than only the one #188 happened to name.

`"ship-verdict"` was already in `BlockedViewIds` (`Malformed` falls into the `| _ ->` catch-all at
`:747`) and stays there as `Blocked` — which is why `RefreshCommandTests.fs:111` passes both before
and after, and why this second-order change went unnoticed in review.

---

## R4 — The severity asymmetry is in the diagnostic, not the currency word

**Decision**: Fix B by changing which diagnostic the `(shClass = Stale, verdict = None)` state emits.
Leave the currency word `missing`.

**Rationale**: The verdict genuinely is absent. `generatedViews[].currency` is a fact about the
artifact; reporting `stale` for a nonexistent file trades one false word for another (spec AMB-003).
The falsehood is in `verdictDiags` (`:607-618`), whose `Missing` arm (`:616`) emits
`refresh.blockedUpstreamView` — *"cannot be refreshed until upstream view is current"* — for a state
whose remediation is the plain `re-run ship`, identical to the `Stale` case at `:617`.

Severity table, confirmed at `DiagnosticConstructors.fs`:

| Constructor | Line | Severity |
|---|---|---|
| `refreshStaleView` | `:931` | warning |
| `refreshMalformedGeneratedView` | `:939` | warning |
| `refreshBlockedUpstreamView` | `:946` | **error** |
| `refreshUnrenderableSummary` | `:968` | **error** |

So `verdictDiags`'s `Missing` arm must split on the *source's* class: `Stale` source →
`refreshStaleView` (warning, FR-009); any other source class → `refreshBlockedUpstreamView` (error,
FR-011). This keeps the error for the states that really are blocked (malformed/blocked/missing
source) and matches the present-verdict case for the state that is merely stale.

**Alternatives considered**:
- *Downgrade `refreshBlockedUpstreamView` to a warning.* Rejected: it is correctly an error for the
  malformed/blocked-source states, and it is emitted from `downstreamDiags` too (`:593`, `:599`),
  where changing severity would silently touch analysis/verify.
- *Report the verdict `stale` when absent.* Rejected: false fact; violates FR-010 and the whole
  premise of the feature.

---

## R5 — One existing test asserts the bug; it is a characterization test, not a contract

**Decision**: Update `RefreshCommandTests.fs:88-113` in place. Do not add a parallel test.

**Rationale**: The test
``a ship json that is valid json but not a ship view blocks the verdict with a diagnostic``
(`:88`) writes `{ "schemaVersion": 99 }` to `ship.json` and asserts:

```fsharp
Assert.Equal("current",   TestSupport.refreshViewState report "ship")          // :105
Assert.Equal("malformed", TestSupport.refreshViewState report "ship-verdict")  // :106
```

Its own comment (`:90-93`) *names the hole* — "`shClass` is computed from `parsesAsJson`, which is
weaker than `parseShipView`" — and treats it as a known compromise, guarding only that a diagnostic
is emitted at all. Feature 092 shipped the compromise knowingly. This feature closes it, so `:105`
becomes `"malformed"` and `:106` becomes `"blocked"`. The test name stays accurate (the verdict is
still blocked, still with a diagnostic); the comment is rewritten to state the invariant now held.

`Assert.Contains("ship-verdict", summary.BlockedViewIds)` (`:111`) already passes today and continues
to pass — `BlockedViewIds` collects non-clean views regardless of which non-clean word they carry.

**Tests that must NOT change** (regression evidence for FR-008, verified by reading each):

| Test | Line | State | Why unchanged |
|---|---|---|---|
| ``refresh does not rewrite the verdict from a malformed ship json`` | `:74` | `ship.json` = `"{ not json"` | already `sh=Malformed`; stronger oracle subsumes |
| ``a fresh clone …`` | `:116` | `ship.json` absent, verdict present | `sh=Missing` → verdict `Blocked`; validator never runs |
| ``an edited source makes the committed verdict stale, not blocked`` | `:129` | stale source, verdict present | FR-012; `Stale` decided after the validator passes |
| ``refresh does not write a verdict when both … missing`` | `:149` | both absent | `sh=Missing`, verdict `Missing`; FR-011 keeps the error |
| ``refresh regenerates the structured … views`` | `:162` | fully valid | FR-008 byte-identity |

**Gap**: no test exists for B's state (stale source, verdict **absent**). That is a new test, not an
edit — which is itself the reason the asymmetry survived review.

---

## R6 — Determinism and goldens are untouched

**Decision**: No golden-fixture regeneration; no `docs/release/` change.

**Rationale**:
- No new diagnostic id is introduced (R4 reuses `refresh.staleView`), so the release baseline's
  diagnostic-id inventory is unchanged. `docs/release/release-readiness.json` is not touched — and
  notably it is **not** in #188's declared `Paths:`, which independently corroborates the scope.
- The committed goldens under `tests/FS.GG.SDD.Commands.Tests/goldens/readiness` are produced from
  *valid* work items. FR-008 pins that path byte-identical, so no golden moves. (This also keeps the
  touch-set disjoint from FS.GG.SDD#164, which widened mid-flight onto exactly that goldens tree.)
- The added validation runs once per `refresh` over a single already-in-memory `FileSnapshot`. No I/O
  is added; SC-007's determinism claim is about output bytes, which are a pure function of the same
  inputs as before.

---

## R7 — `Ship.fs` is not touched, though #188 lists it

**Decision**: Leave `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Ship.fs` unmodified.

**Rationale**: #188's `Paths:` line includes `Ship.fs` only for the redundant `|> List.sort` at
`Ship.fs:120` (`jsonStringList` already sorts), which the issue itself files under "**Also noted, not
defects**". Spec §Out of Scope excludes it: removing it is a behavior-preserving no-op that would
touch a parsing module for no reason and add a diff to review. `parseShipView` is *consumed* from its
existing `.fsi` export; nothing in `Ship.fs` needs to change to consume it.

The realized touch-set is therefore **narrower** than declared — `HandlersRefresh.fs` and
`RefreshCommandTests.fs` — which is always safe under ADR-0021 (a narrower set cannot introduce an
overlap that a wider one did not already have).

---

## Resolved unknowns

Every `NEEDS CLARIFICATION` from Technical Context is closed above:

| Unknown | Closed by |
|---|---|
| Which ship-view oracle? | R1 — `ShipModule.parseShipView`, taking the `FileSnapshot` we already hold |
| Where does the fix live? | R2 — a validator parameter on the local `downstreamClass` |
| Does the exit code change? | R3 — no; proven from `structuredClasses` membership, pinned by SC-004 |
| Currency word vs. severity for B? | R4 — word stays `missing`; the diagnostic changes |
| Do goldens/baselines move? | R6 — no |
| Is `Ship.fs` in scope? | R7 — no |
