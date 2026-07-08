# Implementation Plan: Committed Compact Ship Verdict

**Branch**: `092-committed-ship-verdict` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/092-committed-ship-verdict/spec.md`

## Summary

`fsgg-sdd ship` additionally emits `readiness/<id>/ship-verdict.json` — a 20-line, byte-stable,
**committed** projection of the 279-line `ship.json`, replacing its `sources[]` inventory with one
aggregate `sourcesDigest`. `refresh` re-projects it under the same gate it already uses for
`governance-handoff.json`. The `init` `.gitignore` seed, and this repository's own dogfood rule,
both move from excluding the readiness **directory** to excluding its **contents** plus a negation
for the verdict. This introduces the artifact class ADR-0018 left implicit — *durable generated* —
marked by an additive `durableGenerated` flag on the release-catalog entry, from which the taxonomy
doc's new durable table is derived.

Phase 0 research turned up four facts that reshape the work relative to the issue text:

1. The byte-exact `.gitignore` drift guard lives in **`ArtifactTaxonomyTests.fs`**, not `Drift.fs`
   (which is presence-only and needs **no change**) — and its `Assert.Contains("readiness/*/", …)`
   companion assert goes **vacuous** under `readiness/*/*`, since the new pattern contains the old
   as a substring. Byte-equality against a constant cannot tell whether the constant *works*. The
   negation must be proved by staging a real git repository (research D1/D2, FR-014/FR-015).
2. The taxonomy drift guard **inverts** on a durable generated view: it asserts the doc's regenerable
   list equals *every* `generatedView` catalog entry. Cataloguing the verdict as `generatedView` — as
   ADR-0026 §4 requires — makes the guard demand the doc contradict the ADR. It must be
   **re-partitioned** by `durableGenerated`, not extended (research D3, FR-013).
3. `ReleaseBoundaryTests`' T024 pins the catalog's view kinds to feature 018's set and rejects
   `GeneratedViewKind.Other "shipVerdict"` too, so the escape hatch escapes nothing. T024 is amended
   deliberately; T019 then forces the catalog entry to exist (research D4, FR-016).
4. `refresh` **never** regenerates `ship.json` — it reports its currency. The one view it re-projects
   is `governance-handoff.json`, gated on `shClass = AlreadyCurrent`, via a **pure function shared
   with `ship`**. The verdict takes that slot and that gate; sharing the pure projection is what makes
   the two producers byte-identical by construction rather than by golden-file coincidence
   (research D5/D6, FR-006/FR-007).

So the plan is: add the artifact module and its aggregate digest (small, pure, Artifacts-layer), wire
one shared emission function into `ship` and `refresh` (surgical, mirrors `governanceHandoffEmission`),
extend the catalog with one additive flag and one entry, re-partition the taxonomy guard, amend T024,
enumerate the view in `validate`, and land **both** `.gitignore` adoptions with a behavioral git test
behind each.

**Change tier**: **Tier 1 (contracted change)**. A new generated view and its schema, a new
`GeneratedViewKind` case, an additive field on `SchemaReferenceEntry`, an additive field on
`ShipView`, and a change to the seeded artifact layout. Requires `.fsi` updates
(`ShipVerdict.fsi` new; `Ship.fsi`, `ReleaseContract.fsi`, `GenerationManifest.fsi` extended), the
`FS.GG.SDD.Artifacts` `PublicSurface.baseline`, tests, and docs. No **cross-repo** contract moves:
the verdict carries no `contractVersion` and is not registered in `registry/dependencies.yml`
(FR-018), so no migration note and no coherent-set version flip.

## Technical Context

**Language/Version**: F# on .NET, `net10.0`

**Primary Dependencies**: FSharp.Core; `System.Text.Json` (`Utf8JsonWriter`); Spectre.Console
(presentation edge only — untouched here)

**Storage**: Filesystem lifecycle artifacts under `work/<id>/` and `readiness/<id>/`

**Testing**: xUnit — `tests/FS.GG.SDD.Artifacts.Tests` (projection, digest, parse, catalog),
`tests/FS.GG.SDD.Commands.Tests` (ship/refresh emission, real-filesystem and **real-git** fixtures,
goldens), `tests/FS.GG.SDD.Validation.Tests` (matrix coverage)

**Target Platform**: Linux/macOS/Windows CLI (`fsgg-sdd`)

**Project Type**: CLI tool over a pure MVU core with an effect interpreter at the edge

**Performance Goals**: N/A — per-invocation projection over an in-memory string; one extra
`WriteFile` effect

**Constraints**: Byte-identical output on unchanged inputs (no clock, path, or ANSI); `ship` and
`refresh` must emit identical bytes; the verdict must never be written when `ship.json` is not; the
seeded `.gitignore` stays whole-file no-clobber; `ship.json` must not change

**Scale/Scope**: One new module (~90 lines + `.fsi`), ~6 edited source files, 2 `.gitignore` files,
2 docs, ~10 test files; one new golden

**Local build note**: this sandbox cannot restore `FSharp.Core` against the committed
`packages.lock.json` (research E1 — an environment artifact; CI's `--locked-mode` gate is green on
`main`). Build and test locally with
`-p:RestorePackagesWithLockFile=false -p:NuGetLockFilePath=<scratch>/nolock.json`. **Never** commit a
regenerated lock file.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| **I. Spec → FSI → Semantic tests → Implementation** | Followed. Spec written first; `ShipVerdict.fsi` (new) and the three extended signature files precede their `.fs` bodies; Phase 2 orders semantic tests before implementation for every FR. |
| **II. Structured artifacts are the machine contract** | Honored and central. `ship-verdict.json` is a machine contract, documented in `contracts/ship-verdict.md`, catalogued in `docs/release/release-readiness.json`, and pinned by golden + drift guards. The taxonomy Markdown stays a **projection** of the catalog (FR-013) — where doc and structure could disagree, the catalog wins. |
| **III. Visibility lives in `.fsi`** | Honored. `ShipVerdict.fsi` declares exactly `ShipVerdict`, `sourcesDigest`, `fromShipView`, `toJson`. `Ship.fsi`/`ReleaseContract.fsi`/`GenerationManifest.fsi` gain their additive members. `HandlersShip`/`HandlersRefresh`/`Foundation`/`ViewGeneration` are `internal` (namespace `…Commands.Internal`) and stay signature-less. The `FS.GG.SDD.Artifacts` `PublicSurface.baseline` moves and is regenerated deliberately. |
| **IV. Idiomatic simplicity** | Plain F#: one record, one fold to a canonical string, one `Utf8JsonWriter` body, one shared emission function mirroring `governanceHandoffEmission`. No new abstraction, no reflection, no CE. The aggregate digest reuses `SchemaVersion.sha256Text` and the `WorkModel.behaviorModelDigest` join idiom rather than inventing a helper. |
| **V. Elmish/MVU is the boundary** | Preserved. The projection is pure and lives in the Artifacts layer; the only new I/O is one additional `WriteFile` effect returned from two handlers and interpreted at the existing edge. `refresh` compares against the existing `snapshot` read plan rather than reading from pure code. No I/O moves into pure code. |
| **VI. Test evidence is mandatory** | Every FR gets a test that fails before and passes after. The load-bearing ones are **behavioral**: FR-014/FR-015 stage a real `git` repository, because the pre-existing byte-exact guard provably cannot distinguish a working negation from an inert one (research D2). The pre-change baseline (the 0018-era rule staging nothing) is asserted as a regression, not merely described. |
| **VII. Agent and human workflows share one contract** | Unchanged. The verdict is one generated view both agents and humans read from git; it is never hand-authored. `docs/reference/artifact-taxonomy.md` remains the single adoption instruction for both, and remains catalog-derived. |
| **VIII. Observability and safe failure** | Honored. No new diagnostic code. The verdict inherits `ship`'s existing `not hasBlocking` write gate (FR-005), so an incomplete ship is never recorded as a verdict; `refresh` inherits ship's currency class rather than projecting from a stale input (FR-006). Non-adopting repos degrade explicitly to ADR-0018 behavior (FR-019). |

**Gate result: PASS.** Two deviations require justification and are recorded in Complexity Tracking:
the new `GeneratedViewKind` case (which amends feature 018's T024 no-scope-creep guard), and the
first-ever committed `readiness/` artifact (which carves a role-based exception into ADR-0018's
"ignore by role, never re-include" rule).

### Re-check after Phase 1 design

Still **PASS**. Design kept the projection pure and Artifacts-local, kept the shared emission function
the single writer for both producers, and confirmed the `durableGenerated` flag is additive
(`AdditiveOptional` by the catalog's own stability rules — the release-readiness `schemaVersion` does
not move). No cross-repo contract, no `contractVersion`, no registry entry, no new diagnostic code
appeared during design. Tier 1 classification holds.

## Project Structure

### Documentation (this feature)

```text
specs/092-committed-ship-verdict/
├── plan.md                       # This file
├── spec.md                       # Feature specification
├── research.md                   # Phase 0 — D1..D11 + E1, verified against git and the running code
├── data-model.md                 # Phase 1 — the verdict, the two additive fields, the new view kind
├── quickstart.md                 # Phase 1 — runnable before/after validation, incl. the git proof
├── contracts/
│   └── ship-verdict.md           # schema v1: fields, sourcesDigest pre-image, determinism, git disposition
├── checklists/
│   └── requirements.md           # Spec quality checklist
└── tasks.md                      # Phase 2 output
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Artifacts/
├── LifecycleArtifacts/ShipVerdict.fs(i)   # NEW — ShipVerdict, sourcesDigest, fromShipView, toJson
├── LifecycleArtifacts/Ship.fs(i)          # + DispositionBlockingFindingIds on ShipView
├── LifecycleArtifacts/Core.fs             # + artifact-inventory row
├── GenerationManifest.fs(i)               # + GeneratedViewKind.ShipVerdict, expectedShipVerdictOutputPath
├── ReleaseContract.fs(i)                  # + SchemaReferenceEntry.DurableGenerated; catalog entry; write/parse
└── FS.GG.SDD.Artifacts.fsproj             # + ShipVerdict.fs(i) after Ship.fs(i)

src/FS.GG.SDD.Commands/CommandWorkflow/
├── HandlersShip.fs        # shipVerdictEmission (pure, shared); WriteFile effect; view state
├── HandlersRefresh.fs     # re-project under shClass = AlreadyCurrent; refreshCanonicalViews; buckets
└── Foundation.fs          # gitignoreSeedText; shipVerdictPath; ship/refresh read plans

src/FS.GG.SDD.Validation/
├── ValidationHarness.fs   # determinismOutputs (+ "ship-verdict.json")
└── ValidationRunner.fs    # classifyOutput basename match

docs/
├── release/release-readiness.json    # regenerated (byte-locked by ReleaseContractTests)
├── release/schema-reference.md       # T016-enforced catalog table + field inventory
└── reference/artifact-taxonomy.md    # new durable-generated table; amended seed fragment

.gitignore                            # this repo's dogfood adoption (FR-010)

tests/
├── FS.GG.SDD.Artifacts.Tests/ShipVerdictTests.fs   # NEW — projection, digest, field set, 20 lines
├── FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline
├── FS.GG.SDD.Artifacts.Tests/baselines/release-readiness.json
├── FS.GG.SDD.Commands.Tests/GitignoreNegationTests.fs  # NEW — the real-git behavioral proof
├── FS.GG.SDD.Commands.Tests/ArtifactTaxonomyTests.fs   # re-partitioned guard; strengthened asserts
├── FS.GG.SDD.Commands.Tests/ReleaseBoundaryTests.fs    # T024 amended
├── FS.GG.SDD.Commands.Tests/goldens/readiness/ship-verdict.json  # NEW golden
└── … (ShipCommandTests, RefreshCommandTests, ReadinessViewGoldenTests, ReleaseConformanceTests,
      ReleaseContractTests, ReleaseReadinessCheckTests, DeterminismMatrixTests)
```

**Structure Decision**: The projection lives in `FS.GG.SDD.Artifacts`, not in the Commands layer,
following the `GovernanceHandoff.fs` precedent: the type, the digest, and `toJson` are pure and belong
beside `ShipView`, which they project from. The Commands layer contributes exactly one thin, pure
`shipVerdictEmission` in `HandlersShip` — shared verbatim by `ship` and `refresh` — which is the
mechanism that makes FR-007 true by construction. No new project, no new module in Commands.

## Phase 1 Design Notes

### The shared emission function (research D5/D6)

Mirrors `governanceHandoffEmission`, minus the work-model gate — the verdict projects from
`ship.json` **alone**:

```fsharp
/// Pure over the ship.json text. Called by `ship` (fresh write) and `refresh` (re-projection),
/// which is what makes the two producers byte-identical by construction (FR-007).
let shipVerdictEmission workId (generator: GeneratorVersion) (shipText: string)
    : GeneratedViewState option * CommandEffect list * string option =
    match ShipModule.parseShipView { Path = shipPath workId; Text = shipText } with
    | Ok view ->
        let json = ShipVerdictModule.toJson (ShipVerdictModule.fromShipView view)
        …
        Some state, [ WriteFile(shipVerdictPath workId, json, GeneratedView) ], Some json
    | Error _ -> None, [], None
```

`ship` appends its effects beside the `ship.json` write, **inside** the existing `not hasBlocking`
gate, so FR-005 holds without a new branch. `refresh` calls it only when `shClass = AlreadyCurrent`
and compares the result against the on-disk snapshot to choose `AlreadyCurrent` vs `Refreshed`,
exactly as it does for the handoff; every other class is inherited from ship.

### The aggregate digest (research D8)

```fsharp
let sourcesDigest (sources: AnalysisSourceRecord list) : SourceDigest =
    sources
    |> List.sortBy (fun s -> s.Path)
    |> List.map (fun s -> $"{s.Path}|{s.Digest.Algorithm}:{s.Digest.Value}")
    |> String.concat "\n"
    |> SchemaVersionModule.sha256Text
```

`sha256Text` already returns `SourceDigest = { Algorithm; Value }` — the exact `{"algorithm","value"}`
shape the field serializes — so no new record and no new hashing helper. Binding `Path` to its digest
is what satisfies the ADR's purpose clause; hashing the digest values alone would not (research D8).
It is exposed in `ShipVerdict.fsi` so the test can recompute it independently (SC-003).

### The taxonomy guard, re-partitioned (research D3)

```
regenerable block      ≡ catalog[] where sourceArtifact.kind = generatedView ∧ ¬durableGenerated
durable-generated table ≡ catalog[] where sourceArtifact.kind = generatedView ∧  durableGenerated
```

Both halves stay set-equal to a doc projection, so a future view can escape neither table. Loosening
the existing assert to `⊇` — the tempting one-line fix — would let exactly that happen, which is the
rot ADR-0018 pinned the guard against.

### The two `.gitignore` adoptions (research D1)

Seed (`Foundation.gitignoreSeedText`, whole-file no-clobber): `readiness/*/` → `readiness/*/*` +
`!readiness/*/ship-verdict.json`, with a one-line ADR-0026 comment. The doc's fenced fragment moves
with it (pinned byte-exactly). This repository's own `.gitignore`: `specs/*/readiness/` →
`specs/*/readiness/*/*` + `!specs/*/readiness/*/ship-verdict.json`. Root `readiness/<id>/` proofs are
matched by no rule and stay committed.

Both are proved by a **behavioral** test that runs `git add -A` in a temporary repository and asserts
the staged set beneath the readiness root is exactly `{ship-verdict.json}` — including a regression
that the 0018-era rule stages **nothing**, so the change cannot be silently reverted to an inert
negation (research D2).

## Complexity Tracking

> Filled because the Constitution Check records two deviations.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| A new `GeneratedViewKind` case, amending feature 018's T024 "no scope creep" guard | ADR-0026 introduces a first-class generated view; T024 asserts that *018* added none, and it must be updated to say which feature did. `ReleaseReadinessCheckTests`' T019 then forces the catalog entry, keeping the two guards mutually reinforcing (FR-016). | *`GeneratedViewKind.Other "shipVerdict"`* — is not in T024's `known` set either, so it evades neither the guard nor the intent, and it turns a checked match into a string comparison. *Reuse `GeneratedViewKind.Ship`* — two catalog entries with one kind makes T019's "covers every enumerable kind" vacuous for the verdict and hides the new view from `viewKindValue` consumers. |
| The first committed artifact under `readiness/`, carving an exception into ADR-0018's "ignore by role, never re-include" rule | The merge-boundary verdict is the one lifecycle fact regeneration cannot reconstruct: re-running `ship` reports *today's* disposition, not the merge's. ADR-0026 §2 bounds the exception — **one** rule keyed on the artifact's *role*, constant in the number of work items, not the per-feature re-inclusion list 0018 prohibited. Cardinality: one ~15-line file per work item vs the ~12 generated files it sits beside. | *Un-ignore `ship.json`* — 279 lines, ~59% pure inventory, per work item; the footprint class 0018 removed (Rendering: 2,053 files / 35.4k lines). *Commit nothing and re-run the tool for audits* — cannot answer the question: it reports today's disposition, and answering historically needs the era's sources *and* an era-compatible CLI. *A hand-kept summary file* — a second source of truth, unpinned by any drift guard, and stale by its first commit. |
