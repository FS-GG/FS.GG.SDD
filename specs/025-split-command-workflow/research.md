# Phase 0 Research: Splitting CommandWorkflow

All decisions resolve the single open question in the spec assumptions: *"the
exact module/file layout is an implementation decision deferred to
`/speckit-plan`."* The R3 split of `LifecycleArtifacts.fs` is the reference
precedent throughout.

## Decision 1 — Internal modules live in a child namespace

**Decision**: Place the internal concern modules in a new namespace
`FS.GG.SDD.Commands.Internal` (folder `CommandWorkflow/`), not in the parent
`FS.GG.SDD.Commands`.

**Rationale**: The split modules must be `[<AutoOpen>]` so the facade and the
modules themselves see each others' ~260 bindings without rewriting call sites
(Decision 2). But `[<AutoOpen>]` in the *parent* namespace would auto-open those
bindings into every sibling file in `FS.GG.SDD.Commands` compiled afterward —
`CommandEffects.fs`, `CommandSerialization.fs`, `CommandRendering.fs` — risking
silent shadowing of names they resolve from `CommandTypes`/`CommandReports`.
Scoping the modules to the **child** namespace `FS.GG.SDD.Commands.Internal`
means AutoOpen only takes effect where the child namespace is in scope: inside
the child namespace itself (all internal files see each other) and in the facade,
which opts in with a single `open FS.GG.SDD.Commands.Internal`. Sibling files do
not open it and are provably untouched.

**Alternatives considered**:
- *Parent-namespace AutoOpen (literal R3 mirror).* R3 did this safely within
  Artifacts, but CommandWorkflow has generically-named bindings (`plan`,
  `snapshot`, `relationship`) and three sibling files compiled after it. Rejected
  to remove any silent-shadowing risk by construction rather than relying on the
  build to catch it.
- *Non-AutoOpen modules with explicit `open` per file.* Eliminates pollution but
  forces qualification/opens at every cross-module reference, enlarging the diff
  and the surface for a transcription error in a byte-stable refactor. Rejected.

## Decision 2 — `[<AutoOpen>] module internal` for each internal file

**Decision**: Each internal file is `[<AutoOpen>] module internal <Name>` (no
`.fsi`), mirroring `LifecycleArtifacts/Internal.fs`.

**Rationale**: Within the child namespace, AutoOpen makes every prior internal
module's bindings available unqualified to later ones, so the moved bodies keep
their original references verbatim — the strongest guarantee of byte-stable
behavior and the smallest possible diff. `module internal` keeps the bindings
assembly-scoped (never part of the package's public API), and the absence of a
`.fsi` is exactly the precedent of `Internal.fs`. Constitution III is satisfied
because the package's sole public contract remains `CommandWorkflow.fsi`
(`init`/`update`); these are not public modules.

**Alternatives considered**: per-module `.fsi` files (R3's Core/family pattern).
Rejected — that pattern exists in R3 because those modules *are* public Artifacts
surface needing baselines; here nothing but `init`/`update` is public, so adding
`.fsi` files would invent public surface the constitution says to keep minimal.

## Decision 3 — Redeclare the seven artifact-namespace aliases per file

**Decision**: Each internal file that uses the aliases redeclares the needed
`module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics` (and the other six)
in its header.

**Rationale**: In F#, module *abbreviations* (`module X = A.B`) are always
file-local — they are not exported and cannot be shared via AutoOpen. The body
code references them as `WorkModelModule.foo`, `DiagnosticsModule.sort`, etc.; to
keep those references byte-stable the abbreviation must be in scope in each file,
which means a small fixed header (≤7 lines) repeated per file. This is the spec's
flagged edge case ("the seven artifact-namespace aliases … must remain available
to every internal module that used them") and the only sanctioned repetition.

**Alternatives considered**: rewrite `WorkModelModule.foo` → `WorkModel.foo` with
`open FS.GG.SDD.Artifacts.WorkModel`. Rejected — touches many call sites for no
benefit and risks a transcription error.

## Decision 4 — Compile order preserves original top-to-bottom sequence

**Decision**: List the internal files in `.fsproj` in the same order their
sections appear in today's monolith; the facade (`CommandWorkflow.fsi` +
`CommandWorkflow.fs`) compiles immediately after the last internal file and
before `CommandEffects`.

**Rationale**: F# requires definition-before-use across files. The monolith
already compiles top-to-bottom, so preserving that section order across the split
files is sufficient and provably acyclic. Resulting order:
`Foundation → ParsingEarly → ParsingMid → ParsingTasks → ViewGeneration →
Prerequisites → HandlersEarly → HandlersAnalyze → HandlersEvidence →
HandlersVerify → HandlersShip → HandlersAgents → HandlersRefresh →
CommandWorkflow.fsi → CommandWorkflow.fs`. `CommandEffects`/`Serialization`/
`Rendering` keep their existing relative position after the facade. The
`Artifacts → Commands` layering is untouched (no new project references), so
FR-008 holds trivially.

**Edge case — mutual references across concern boundaries**: handlers depend on
parsing, view generation, and prerequisites; all of those precede the handlers in
the order above, so no forward reference arises. `runHandler` and
`resolvePrerequisites` (Prerequisites.fs) sit immediately before the handler
files that consume them.

## Decision 5 — Collision / shadowing safety

**Decision**: Rely on child-namespace scoping (Decision 1) plus the Release build
(FR-007) and full suite (FR-009) as the equivalence guard; add no defensive
renames.

**Rationale**: Splitting one flat module cannot introduce *intra-workflow*
collisions — the names were already unique in a single scope. Cross-file leakage
into siblings is eliminated by the child namespace. Any residual ambiguity would
be a hard compile error, not silent drift, and is caught before merge.

## Decision 6 — Incremental, always-green landing

**Decision**: The split may land in one commit or several; every intermediate
commit must build clean, keep the suite green, and keep `CommandWorkflow.fsi` and
all JSON byte-stable.

**Rationale**: Spec edge case. Because the internal modules are AutoOpen within
the child namespace, a partially-extracted state (some sections still in the
facade, some moved) still compiles as long as compile order is maintained,
enabling safe incremental commits. The recommended path: extract `Foundation`
first, build+test, then proceed section by section in compile order, verifying
the byte-stable gates (quickstart) at each step.

## Validation strategy (feeds quickstart)

The behavioral equivalence proof is mechanical and fully scripted (diff against
the immutable pre-refactor baseline `BASE=$(git merge-base main HEAD)`, not the
moving `main` ref):
1. `git diff --exit-code "$BASE" -- src/FS.GG.SDD.Commands/CommandWorkflow.fsi` → no output.
2. `dotnet build -c Release` → clean; capture FS3261 unique-site count and
   compare to the ~290 `src` baseline (must not increase from this change).
3. `dotnet test` → 438 passing, **zero** golden/baseline/surface/release-readiness
   files regenerated (`git status` clean for fixtures).
4. `wc -l` over the new files → none > ~1,500.

No new behavioral tests are required (FR-009 / spec assumption); an optional
structural test asserting the file-size cap and facade surface may be added.
