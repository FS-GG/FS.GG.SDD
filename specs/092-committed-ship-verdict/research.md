# Phase 0 Research — Committed Compact Ship Verdict

Feature: `092-committed-ship-verdict` · Issue: FS-GG/FS.GG.SDD#177 · Decision: ADR-0026

Every finding below was verified against the running code or real `git` in this worktree, not
inferred from the issue text. Three of them change the shape of the work.

---

## D1 — The `.gitignore` negation is inert under the 0018-era pattern (verified against git)

ADR-0026 §2 asserts this; it is the single fact the feature stands on, so it was re-verified.

Scratch repository containing `readiness/003-demo/{ship.json, ship-verdict.json, verify.json,
agent-commands/claude/guidance.json}`:

| `.gitignore` | `git add -A -n` stages under `readiness/` |
|---|---|
| `readiness/*/` + `!readiness/*/ship-verdict.json` | **nothing** |
| `readiness/*/*` + `!readiness/*/ship-verdict.json` | `readiness/003-demo/ship-verdict.json` — and nothing else |

Git never descends into an **excluded directory**, so a negation beneath `readiness/*/` can never
fire. Excluding the directory's *contents* (`readiness/*/*`) keeps the parent traversable. Nested
views stay ignored either way: `readiness/*/*` matches the `agent-commands` **directory** itself,
which git then does not descend into.

The dogfood variant was verified the same way. With
`specs/*/readiness/*/*` + `!specs/*/readiness/*/ship-verdict.json`:

```
IGNORED    specs/092-x/readiness/003-demo/ship.json
tracked->  specs/092-x/readiness/003-demo/ship-verdict.json
IGNORED    specs/092-x/readiness/003-demo/agent-commands/claude/guidance.json
tracked->  readiness/019-pinned/proof.json     # root proofs: matched by no rule, unaffected
```

**Consequence**: FR-009/FR-010 are two edits, and FR-014/FR-015 must be *behavioral* tests that run
git. No string assertion can distinguish "negation correct" from "negation inert".

## D2 — The existing drift guard cannot catch an inert negation, and one assert goes vacuous

The byte-exact `.gitignore` guard is **`tests/FS.GG.SDD.Commands.Tests/ArtifactTaxonomyTests.fs`**,
not `Drift.fs` (which carries `.gitignore` presence-only in `expectedArtifactPaths`, for
`doctor`/`upgrade` re-seeding — and therefore needs **no change**).

`ArtifactTaxonomyTests.fs:67` asserts the seeded file equals `Foundation.gitignoreSeedText` — that
binds. `ArtifactTaxonomyTests.fs:68` asserts `Assert.Contains("readiness/*/", seeded)` — a
**substring** check that `readiness/*/*` satisfies trivially. It passes whether the negation is
absent, present-but-inert, or correct. Byte-equality against a constant proves the constant was
copied, never that it *works*.

**Consequence**: strengthen `:68` to assert both the contents rule and the negation line, and add
the D1 behavioral test. The second is the real guard.

## D3 — The taxonomy drift guard *inverts* on a durable generated view

`ArtifactTaxonomyTests.catalogGeneratedViewPaths()` (`:25-37`) collects **every** catalog entry whose
`sourceArtifact.kind` is `generatedView` and asserts set-equality (`:46-58`) with the taxonomy doc's
regenerable `readiness/<id>/…` list.

Catalogue `ship-verdict.json` as `generatedView` — which ADR-0026 §4 requires — and the guard demands
it appear in the **regenerable** block, i.e. demands the doc contradict the ADR.

**Consequence**: the guard is re-partitioned, not extended:

- regenerable block ≡ `generatedView && not durableGenerated`
- durable-generated table ≡ `generatedView && durableGenerated`

Both stay catalog-derived. Extending the doc by hand and loosening the assert to `⊇` would silently
let a future view escape both tables — the exact rot ADR-0018 pinned the guard against.

## D4 — `ReleaseBoundaryTests` T024 blocks any new view kind, including `Other`

```fsharp
// ReleaseBoundaryTests.fs:55-67
let ``T024 the catalog adds no GeneratedViewKind beyond the pre-018 enumerable set`` () =
    let known = Set.ofList [ WorkModel; Analysis; Verify; Ship; Summary; AgentCommands; GovernanceHandoff ]
    for entry in (currentRelease ()).Catalog do
        match entry.Kind with
        | GeneratedViewContract(kind, _) -> Assert.Contains(kind, known)
        | CommandOutputContract -> ()
```

`GeneratedViewKind.Other "shipVerdict"` is not in `known` either, so the escape hatch escapes
nothing. T024 is feature **018**'s FR-013 "no scope creep" guard — it asserts *018* added no view
kind. ADR-0026 deliberately adds one.

**Decision**: add a named `ShipVerdict` case and amend T024's `known` set, renaming the test to say
which feature introduced the kind. `ReleaseReadinessCheckTests`' T019 ("the catalog covers every
enumerable kind") then *forces* the catalog entry to exist. Adding the kind without cataloguing it,
or cataloguing it without admitting the kind, both stay build failures — the guards remain mutually
reinforcing (FR-016).

## D5 — `refresh` never regenerates `ship.json`; the verdict belongs in the handoff slot

`HandlersRefresh.downstreamClass` classifies `analysis`/`verify`/`ship` as
`Blocked | Missing | Malformed | Stale | AlreadyCurrent` and **reports** them. Re-running the
read-only generators out of lifecycle order would corrupt evidence freshness.

The one view `refresh` re-projects is `governance-handoff.json` (`HandlersRefresh.fs:454-490`), and
only when `shClass = AlreadyCurrent`, because it is a pure projection over `ship.json`. It compares
the regenerated JSON against the on-disk snapshot to choose `AlreadyCurrent` vs `Refreshed`;
otherwise it inherits ship's class.

`ship-verdict.json` is the same shape of thing. It goes in that slot, under that gate (FR-006).

One simplification over the handoff: `governanceHandoffEmission` needs the **work model** text (it
folds evidence nodes). The verdict is a projection over `ship.json` **alone**, so
`shipVerdictEmission` takes `workId → generator → shipText` and no work-model gate.

## D6 — Byte-stability across `ship` and `refresh` is structural, not incidental

`governanceHandoffEmission` (`HandlersShip.fs:124-183`) is a **pure** function called by both
`HandlersShip` (`:625-631`) and `HandlersRefresh` (`:473-479`). That, not a golden file, is what
makes the two producers agree.

**Consequence**: `shipVerdictEmission` must be one pure function in `HandlersShip`, called from both
handlers (FR-007). A second, "equivalent" writer in `HandlersRefresh` would satisfy today's golden
and drift tomorrow.

## D7 — `ShipView` discards `disposition.blockingFindingIds`

`Ship.parseShipView` flattens `disposition` to a bare string (`Ship.fs:106-109`):

```fsharp
let disposition =
    tryJsonProperty "disposition" root
    |> Option.bind (fun element -> jsonString "state" element)
    |> Option.defaultValue "blocked"
```

The verdict must carry `disposition.blockingFindingIds` (ADR-0026 §1).

Two candidates were compared. `HandlersShip.parseShipReadinessFacts` (`:102-112`) *does* extract the
blocking ids — but it is a **report** projection: it drops `sources[]` (needed for `sourcesDigest`)
and lives in the Commands layer.

**Decision**: extend `ShipView` with `DispositionBlockingFindingIds: string list` (additive field on
a parse-only record) and project the verdict from `ShipView`. Moves
`tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline`.

## D8 — `sourcesDigest` binds the path→digest **pairing**, not the digest list alone

ADR-0026 words it "one aggregate SHA-256 over the canonical `sources[]` digest list" and fixes the
purpose: *"so a later reader can prove the committed verdict corresponds to the committed sources."*

Hashing the digest **values** alone does not bind which input produced which digest — two sources
exchanging content are indistinguishable from no change if their sort order swaps with them. The
purpose clause requires the pairing.

**Canonical pre-image** (one line per source, `sources[]` already sorted by `path`, joined `\n`):

```
<path>|<algorithm>:<value>
```

hashed with the existing `SchemaVersion.sha256Text`. This reuses the established
`WorkModel.behaviorModelDigest` idiom — canonicalize a list into a separator-joined string, then
hash — rather than inventing one.

Verified against the golden `ship.json` (12 sources):
`sourcesDigest = 78a32b33a4bb370f169ad4a44307d7f4c0fafc7741bea0d5a82f1a1d5ad5b117`.

`sha256Text` already returns a `SourceDigest = { Algorithm; Value }`, which is exactly the
`{"algorithm","value"}` shape the field serializes — no new record, no new helper beyond the
pre-image fold. An empty `sources[]` yields the well-known empty-string SHA-256, a defined stable
value rather than an omitted field.

## D9 — The ≤ 20-line target is exact, and `writeViewPreamble` already emits the first six fields

`ViewGeneration.writeViewPreamble` writes, in order: `schemaVersion`, `viewVersion`, `workId`,
`stage`, `status`, `generator` — the verdict's first six fields, in the verdict's order. The verdict
does **not** use `writeReadinessEnvelope`, which forces a `sources[]` array and the verify/ship
findings tail.

Rendered under `Utf8JsonWriter(Indented = true)`, the ship-ready shape is **exactly 20 lines**
(`blockingFindingIds` renders inline as `[]` when empty). Field order mirrors `ship.json`'s own
top-level order, with `sources` replaced in place by `sourcesDigest`, so the verdict reads as a
projection that preserves order:

```json
{
  "schemaVersion": 1,
  "viewVersion": "1.0",
  "workId": "068-readiness-golden",
  "stage": "ship",
  "status": "shipReady",
  "generator": "FS.GG.SDD.Artifacts/0.8.0",
  "sourcesDigest": { "algorithm": "sha256", "value": "…" },
  "verificationReadiness": { "status": "verificationReady" },
  "disposition": { "state": "shipReady", "blockingFindingIds": [] },
  "readiness": "shipReady"
}
```

A non-empty `blockingFindingIds` does **not** cost one line per id — the writer expands the array
over its own bracket lines, giving `21 + n` lines for `n ≥ 1` (measured: 22/23/24 for one/two/three
ids). A **diagnostically** blocked run is unaffected: `HandlersShip` writes `ship.json` only when `not hasBlocking`, so the verdict inherits
that gate and is never emitted (FR-005).

## D10 — `validate` detects an unenumerated view as a coverage gap (so FR-017 is enforced, not decorative)

`ValidationRunner.reconcileSurface` (`:718+`) walks the **real** `readiness/` tree and compares it to
`Set.ofList plan.DeterminismOutputs`. A `ship-verdict.json` on disk that
`ValidationHarness.determinismOutputs` (`:36-47`, comment: *"the nine generated views"*) does not
enumerate surfaces as a `CoverageGap`. `ValidationRunner.classifyOutput` (`:646-652`) matches by
basename and must gain `"ship-verdict.json"`.

## D11 — Where the verdict's projection code belongs

`GovernanceHandoff.fs` is the precedent: an Artifacts-layer module owning the type, the projection
(`fromWorkModel`), and `toJson` (its own `Utf8JsonWriter(Indented = true)`), with the Commands layer
supplying pre-extracted facts to avoid a circular dependency.

The verdict is simpler — it projects from `ShipView`, which already lives in Artifacts — so
`LifecycleArtifacts/ShipVerdict.fs(i)` owns `ShipVerdict`, `fromShipView`, `sourcesDigest`, and
`toJson`, and `HandlersShip.shipVerdictEmission` is a thin, pure `shipText → (view, effects, json)`
wrapper shared by both handlers (D6).

## E1 — Local build note (environment, not code)

This sandbox cannot restore `FSharp.Core` against the committed `packages.lock.json` (NU1403 hash
mismatch); CI's `--locked-mode` gate is green on `main`. Build and test locally with
`-p:RestorePackagesWithLockFile=false -p:NuGetLockFilePath=<scratch>/nolock.json`. **Never** commit a
regenerated lock file.
