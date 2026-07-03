# Phase 0 Research: Test-infrastructure hardening

All six defect clusters from roadmap #75 were traced to concrete code before
planning. This file records the decisions; the plan and tasks build on them.

## Scope correction discovered during research (feeds the spec + Complexity Tracking)

The spec's first draft said the feature "touches only test projects." Tracing the
citations showed two clusters (temp-leak cleanup for `validate`, and the
validation-harness environment cells) live in **product code**
`src/FS.GG.SDD.Validation/ValidationRunner.fs`, not tests. Both are still
**Tier 2 / contract-neutral**:

- The affected functions (`copyDirectory`, `tempDirectory`, `withPerturbedHost`,
  `renderProjection`, the cell evaluators) are **all internal** — none appears in
  `ValidationRunner.fsi`, which exposes only `RunnerOptions` / `defaultOptions` /
  `run`. No public surface / baseline change.
- The `validation-report` is **not byte-golden-pinned**: `ValidateCommandTests`
  asserts structural facts (`schemaVersion` present, no ANSI, exit codes) and its
  sensed metadata (`startedAtUtc` / `durationMs` / `host`) is null. Making the
  env cells genuine keeps every verdict `Pass` (the product *is* deterministic and
  *does* degrade), so the report's schema and verdict structure are unchanged.

→ Spec updated: Tier stays 2 but the scope statement now names
`src/FS.GG.SDD.Validation` internal changes; FR-009 narrowed (see Decision 5);
FR-012 clarified to "no public `.fsi` surface, no `validation-report` schema or
verdict change" rather than "only test projects."

## Decision 1 — Env-mutation race isolation (US1 / FR-001..003)

**Facts.** Two sites mutate process-global env and race parallel siblings:
- `ScaffoldCommandTests.fs` sets global `PATH` (git-free) and
  `FSGG_SDD_PROCESS_TIMEOUT_MS`; it is in `[<Collection("Scaffold")>]` (shared
  only with `ScaffoldCliCoherenceTests`), so it serializes against *that one*
  class but not the ~8 other Commands.Tests classes that spawn `dotnet`/`git`
  (CLI smokes via `runCliRaw`, the lifecycle-command process tests, the `Console`
  collection). The product resolves `git`/`dotnet` by name off the ambient `PATH`
  and exposes **no** per-call environment-injection seam, so a test-only fix
  cannot avoid touching process-global env without a Tier-1 product change.
- `CompositionAcceptanceTests.fs` nulls `FSGG_SDD_ACCEPTANCE_REGISTRY` and empties
  `PATH`; `FS.GG.SDD.Acceptance.Tests` has **no** `DisableTestParallelization`, so
  when the registry is set (scheduled CI) these race the other acceptance classes.

**Decision.**
- **Commands.Tests:** introduce one assembly-shared collection `ProcessGlobalEnv`
  and place every class that mutates env **or** spawns a `PATH`-resolved process
  into it (folding the existing `Scaffold` and `Console` collections into it). The
  ~27 pure in-memory classes keep running in parallel. Guard the invariant with a
  **meta-test** that scans the assembly's own source for
  `Process.Start` / `runCliRaw` / `SetEnvironmentVariable` / bare `git`/`dotnet`
  spawns and asserts each owning class carries `[<Collection("ProcessGlobalEnv")>]`
  — so a future process-spawning class can't silently re-open the race.
- **Acceptance.Tests:** assembly-wide `[<assembly: CollectionBehavior(DisableTestParallelization = true)>]`,
  mirroring the existing `Validation.Tests` precedent. The assembly is small (5
  files, all process/registry-driven) and only runs its heavy path when the
  registry is set, so full serialization costs nothing on the offline inner loop.

**Alternatives rejected.**
- *Assembly-wide disable for Commands.Tests* — needlessly serializes 27 pure
  classes; the shared collection keeps their parallelism.
- *Per-child environment injection* (pass env to the spawned process instead of
  mutating the parent) — the product has no such seam; adding one is a Tier-1
  public change, out of scope.
- *A shared collection but no meta-guard* — the race would silently return the
  first time someone adds a process-spawning class and forgets the attribute.

## Decision 2 — Orphaned fixture manifests (US2 / FR-004)

**Facts.** `tests/fixtures/lifecycle-commands/` holds 107 `manifest.yml`; only
`deterministic-report` is consumed (`CommandReportJsonTests.fs`). The other 106
read like a lifecycle-command contract but nothing executes them.

**Decision.** Phase-2 begins with a scripted inventory (manifest → is any test
path referencing this directory name?). For each unconsumed manifest, apply the
spec rule: **wire it in if it encodes a distinct, currently-unguarded scenario;
delete it otherwise.** The default is *delete* — the lifecycle command matrix is
already exhaustively exercised by the Validation harness and the per-command
`*CommandTests`, so most manifests are redundant documentation. Close with a
**guard test**: every remaining `lifecycle-commands/*/manifest.yml` is referenced
by ≥1 executing test (fail if an orphan reappears).

**Open item for tasks:** the per-manifest wire/delete list is produced from the
inventory in Phase 2 (not guessable up front). The guard test makes the outcome
self-enforcing regardless of the split.

**Alternatives rejected.** *Wire all 106 in* — manufactures redundant tests and
slows the suite for no coverage gain. *Delete all 106* — would drop any manifest
that happens to encode a genuinely unique scenario; the inventory decides.

## Decision 3 — Unified baseline-regeneration switch (US3 / FR-005/006)

**Facts.** Five surface captures exist: `FS.GG.Contracts.Tests/PublicSurfaceTests`
(has the `FSGG_UPDATE_BASELINE=1` regenerate-or-assert switch) and four
`SurfaceBaselineTests.fs` (Artifacts / Cli / Commands / Validation) that only
*assert*. Each captures a different surface (types+modules+records vs. module
static methods), so the *capture* logic is genuinely per-assembly; only the
**update-or-assert wrapper** is common.

**Decision.** Extract one shared helper
`SurfaceBaseline.verify (baselinePath: string) (capture: unit -> string[])` that
implements exactly today's Contracts behavior: if `FSGG_UPDATE_BASELINE=1`,
`File.WriteAllLines(baselinePath, capture())`; then assert `capture()` equals the
committed baseline (whitespace-filtered, sorted). Rewire all five tests onto it,
each passing its own `capture`. Baselines stay byte-identical (the assert path is
unchanged). Home: the shared linked file from Decision 6.

**Alternatives rejected.** *Duplicate the switch into each test* — re-introduces
the drift the item is removing. *A single mega-test capturing all assemblies* —
couples the five projects and breaks per-assembly baseline locality.

## Decision 4 — Deterministic temp cleanup (US4 / FR-007/008)

**Facts.** Test side: `TestSupport.tempDirectory ()` mints
`GetTempPath()/fsgg-sdd-<guid>` per fact (~800/run), never deleted. Product side:
`ValidationRunner` `copyDirectory` clones the project per cell (~350/run) under
`tempDirectory ()`, never deleted.

**Decision.**
- **Both sides — nest, then delete the root once.** Route every `tempDirectory ()`
  allocation under a single per-run root
  (`GetTempPath()/fsgg-sdd-<tests|validate>-<runId>/…`). Delete that root once at
  the end of the run inside a `finally`, so cleanup is **failure-safe** (an
  aborted fact/cell still gets swept) and cheap (one recursive delete, not 800).
- **Test side** anchors the per-run root in an xUnit **assembly fixture**
  (`ICollectionFixture` on a global collection, or module teardown) whose
  `Dispose` deletes the tree. Individual facts keep calling `tempDirectory ()`
  unchanged in signature.
- **Product side:** `run` creates its run root, passes it down, and deletes it in
  a `try/finally` around the matrix evaluation — internal to `run`, no `.fsi`
  change, report unchanged.

**Alternatives rejected.** *Per-fact `IDisposable` deleting each dir* — 800
individual deletes, and any test that returns the path for post-assertion
inspection would fight the disposal ordering. *Rely on OS temp reaping* — leaves
the leak on developer machines and long-lived runners (the actual complaint).

## Decision 5 — Genuinely distinct validation-harness cells (US5 / FR-009)

**Facts.** `renderProjection` maps `Rich -> CR.renderText` **by deliberate design**
(inline comment: "no Spectre dependency; research Decision 6 — identical to the
CLI's non-interactive degradation"). `withPerturbedHost` varies culture + `TZ` but
**not** cwd despite its comment. The degradation matrix checks "no ANSI" but the
color-disabling env (`NO_COLOR` / `TERM`) is never actually set.

**Decision.**
- Make the degradation cells **actually apply** their color-disabling condition
  (set `NO_COLOR` / `TERM=dumb` for those cells, scoped and restored) so the
  no-ANSI check is exercised under the real condition rather than vacuously.
- Add **cwd variation** to `withPerturbedHost` (alongside culture/TZ), fulfilling
  its documented contract. If this surfaces a latent cwd-dependence in a producer,
  that is a real determinism bug the harness *should* catch (INV-3a).
- **Preserve** the library `Rich -> renderText` degradation (research Decision 6).
  Forcing library Rich to emit true Spectre ANSI would add a Spectre dependency to
  the validation library and reverse a prior ADR-level decision — a Tier-1
  architectural change, explicitly **out of scope**. The Rich ANSI-degradation
  guarantee remains covered where it belongs: the CLI-process test
  `ValidateCommandTests` already asserts `--rich` emits no ANSI when
  non-interactive.

→ FR-009 narrowed accordingly: the "rich output must not be identical to plain
text" clause is dropped in favor of "the color-disabling cells apply the real
condition and the perturbed-host cell varies cwd; the library's intentional
Rich→text degradation is preserved and the Rich ANSI guarantee stays covered by
the CLI-process tests."

**Alternatives rejected.** *Add Spectre to the validation library so Rich differs*
— Tier-1, reverses Decision 6, and duplicates a guarantee the CLI tests already
hold.

## Decision 6 — De-duplicated shared test helpers (US6 / FR-010/011)

**Facts.** `findRepoRoot` is copy-pasted in 4 test modules (Commands [already
linked into Cli + Validation via `<Compile Include>`], Contracts, Artifacts,
Acceptance). `writeRelative` in 2 test modules (Commands, Acceptance). The
evidence ladder `T001`–`T006` is a hardcoded literal in Commands.Tests
`TestSupport` (`passingTaskEvidence`), already shared via the link.

**Decision.** **Follow the existing linked-file precedent.** Add one small
shared file `tests/Shared/TestShared.fs` (neutral namespace `FS.GG.SDD.TestShared`)
holding the single definitions of `findRepoRoot`/`repoRoot`, `writeRelative`,
`tempDirectory` (+ the Decision-4 cleanup root), and `SurfaceBaseline.verify`
(Decision 3). Link it via `<Compile Include="../Shared/TestShared.fs" />` into all
six test projects. Each project's existing `TestSupport` keeps its public names but
**delegates** to the shared module (`let repoRoot = TestShared.repoRoot`), so the
many `TestSupport.repoRoot` / `TestSupport.writeRelative` call sites are untouched.
Remove the duplicated bodies in Contracts / Artifacts / Acceptance. Derive the
evidence ladder from a `T001..T006` range helper so the magic ids appear once.
Leave the product-side `ValidationRunner.writeRelative` as-is (it is product code,
not a test helper).

**Alternatives rejected.** *A new `FS.GG.SDD.TestSupport` project* — heavier (new
`.fsproj` + sln entry) than the linked-file pattern the repo already uses. *Keep
Commands.Tests/TestSupport.fs as the shared home* — it is a 486-line
lifecycle-helper module; linking all of it into Contracts/Artifacts just for
`findRepoRoot` drags in irrelevant dependencies. The small focused shared file is
cleaner.

## Cross-cutting invariant (guardrail for every decision)

No change to any `fsgg-sdd` CLI output, JSON automation contract, persisted
schema, generated view, agent-skill contract, public `.fsi` surface, committed
baseline, or golden fixture. Enforced by: the five surface-baseline tests
(unchanged assertions), the existing golden/deterministic fixtures, and a
pre/post `git diff` over `**/*.baseline` and the golden fixture dirs showing empty
(SC-003 / SC-007 / FR-012).
