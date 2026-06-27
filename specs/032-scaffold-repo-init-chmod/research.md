# Phase 0 Research: Scaffold repo-init & script-executable post-instantiation

All NEEDS CLARIFICATION from the spec/Technical Context are resolved below. Each decision is
grounded in the current tree (verified 2026-06-27 @ `3ed0c77`) and the spec's Assumptions.

## Decision 1 — Sense git repo state by **exit code only** (no stdout)

**Decision**: Detect the three repo-init cases with a single
`git rev-parse --is-inside-work-tree` probe, interpreted through the existing `RunProcess`
edge, and branch **only on `ProcessRunResult`**:

| Sensed | Meaning | Repo-init action | Reported outcome |
|---|---|---|---|
| `Process = Some { Started = false }` | git not launchable (absent / not on PATH) | skip, non-fatal | `skippedGitUnavailable` |
| `ExitCode = 0` | inside a git work tree | skip (no nesting) | `skippedExistingRepository` |
| `ExitCode ≠ 0` (git prints 128) | not inside a repo | run `git init` at product root | `initialized` |

**Rationale**: `ProcessRunResult` deliberately captures only `Started` + `ExitCode` and
**excludes stdout** from the deterministic contract (`CommandTypes.fsi:391-396`;
`CommandEffects.fs:94-95` drains and discards stdout/stderr). `git rev-parse
--is-inside-work-tree` is the exact check named in the contract's S2 and the spec's
Assumptions, and its exit code already encodes the full trichotomy — so no new
process-result field, and no contract change, is required. The same edge already used for
`dotnet new` is reused verbatim.

**Alternatives considered**:
- *Read the probe's stdout ("true"/"false")* — rejected: would force adding stdout to
  `ProcessRunResult`, breaking the deterministic-contract boundary for a fact the exit code
  already gives.
- *`Directory.Exists(".git")` probe instead of git* — rejected: misses the "inside a parent
  work tree" case (a subdirectory of a repo has no local `.git`), which FR-002/US3-AC1
  explicitly requires; and would still need git for the actual init.

**Edge note**: `--is-inside-work-tree` prints `false`/exit 0 inside a bare repo or a `.git`
dir; we treat exit 0 uniformly as "a repository already governs this path → skip", which is
the safe choice (never nest).

## Decision 2 — Make-executable via a new `SetExecutable` **permission edge**, not `chmod`

**Decision**: Add `SetExecutable of path: string` to the `CommandEffect` DU, interpreted at
the edge with `System.IO.File.SetUnixFileMode` (OR-ing the three execute bits onto the
existing mode). Failures (read-only FS, non-Unix host, missing file) are caught and reported
as a **skip/partial**, never as a scaffold failure.

**Rationale**: The spec names "the existing MVU process/**permission** edges". SDD has a
process edge (`RunProcess`) but **no** permission edge, so one is introduced as a
first-class effect — keeping permission mutation inside the MVU edge interpreter
(constitution V) rather than shelling out. `File.SetUnixFileMode` is the cross-platform BCL
primitive (no new dependency), is a pure filesystem side-effect (no JSON bytes → FR-012
holds), and degrades cleanly: on non-Unix it throws `PlatformNotSupportedException`, on
read-only FS it throws `IOException`/`UnauthorizedAccessException` — both caught into the
"skipped/partial" outcome (FR-005, US2-AC3).

**Alternatives considered**:
- *`RunProcess("chmod", ["+x"; path], …)`* — rejected: Unix-only (silent no-launch on
  Windows surfaces as `Started=false`, indistinguishable from a real failure), spawns one
  child process per script, and treats a permission change as a process rather than the
  permission edge the spec calls for.
- *Set the bit inside the provenance `WriteFile` interpreter* — rejected: conflates two
  effects and would not cover provider-produced scripts (only SDD writes).

**Determinism**: the executable bit is a filesystem attribute; it changes no byte of
`scaffold-provenance.json` or the report JSON. The report records only the **count** of
scripts made executable (and a skip/partial flag), which is a deterministic function of the
produced-path set in a given environment.

## Decision 3 — Stateless recomputation; three new staged ticks (no new model field)

**Decision**: Keep the scaffold driver's existing pattern — recompute every decision from
`InterpretedEffects` each tick (`HandlersScaffold.fs:294-324`) — and add a post-instantiation
phase that runs only after a **successful** create, as three ticks:

1. **Plan (tick A)** — after the create is interpreted with a success outcome
   (`ProviderSucceeded` / `ProviderSucceededEmpty`): plan the provenance `WriteFile` (as
   today) **plus** the `git rev-parse --is-inside-work-tree` probe **plus** one
   `SetExecutable` effect per produced `.sh` path. (The exec batch needs no probe, so it is
   planned here.)
2. **Decide repo-init (tick B)** — once the probe result is in `InterpretedEffects`: if
   `Started` and `ExitCode ≠ 0`, plan `RunProcess("git", ["init"], "")`; otherwise plan
   nothing (skip).
3. **Finalize (tick C)** — once `git init` is interpreted (or skipped in tick B): compute
   `RepoInitOutcome`, `ExecutableScriptCount`, the skip/partial flag, the advisory
   diagnostics, and the final `NextActionHint`, then set `Scaffold = Some summary`.

Each tick distinguishes its phase by scanning `InterpretedEffects`/`PendingEffects` for the
marker effects (the `git rev-parse` probe, the `git init`, the `SetExecutable` batch) —
exactly as `isCreateProcess` already gates the create tick. The MVU loop
(`CommandWorkflow.fs:129-133`, test loop `ScaffoldCommandTests.fs:35-54`) already re-ticks
while new effects are returned, so no driver change is needed.

**Rationale**: `finalizeScaffold` currently sets `Scaffold = Some` in one tick. Repo-init
has a genuine probe→decide→act dependency that cannot be collapsed into one tick (the
init decision depends on the probe's exit code, which only exists after interpretation).
Recomputing from the interpreted-effect log (rather than threading a staging field through
`CommandModel`) matches the established design and avoids a `CommandModel` surface change.

**Alternatives considered**:
- *Add a `ScaffoldStaging` field to `CommandModel`* — rejected: enlarges the public model
  surface and duplicates state already recoverable from `InterpretedEffects`.
- *Plan probe + init together and ignore the probe* — impossible: `git init` must be
  conditional on the probe to satisfy FR-002 (no nesting).
- *Run the probe before the create* — rejected: the product root only exists after the
  skeleton+create; probing the final product root post-create is correct and matches FR-001
  ("after the provider succeeds").

**Failure/empty interaction**: on `ProviderFailed` (intrusion, non-zero exit, or
`Started=false`) the finalize stays terminal in one tick as today — **no** probe, init, or
exec is planned (FR-009). On `ProviderSucceededEmpty` the post-instantiation phase **does**
run: repo-init initializes over the non-empty skeleton+provenance tree (FR-004 edge), and
the exec batch is an empty no-op (count 0).

## Decision 4 — Identify shell scripts by `.sh` shape over the produced set

**Decision**: The make-executable target set is `produced |> filter (path ends with ".sh")`,
where `produced` is the already-computed app-only, non-SDD diff
(`HandlersScaffold.fs:268-273`). The product root for repo-init is the scaffold project root
(`.`), the same working directory the create ran in.

**Rationale**: FR-006 requires generic, scaffolded-tree-derived discovery with **no**
provider-specific script name. A file-*shape* rule (`.sh` suffix) carries no provider
identity and reuses the existing diff as the single source of truth. Operating only on
`produced` (not the skeleton) keeps SDD-owned files out of scope and is automatically
provider-agnostic.

**Alternatives considered**:
- *Shebang sniffing (`#!`-first-line)* — rejected for v1: requires reading file contents at
  the handler (more I/O, another effect), and `.sh` shape matches the contract's S1 phrasing
  and the spec's Assumption. (Recorded as a possible future refinement if non-`.sh` scripts
  appear.)
- *A provider-declared script manifest* — rejected: would put provider-specific knowledge on
  the scaffold path, violating FR-006/SC-005.

## Decision 5 — Repo-init outcome is **sensed metadata**; goldens pin structure

**Decision**: Treat `RepoInitOutcome` (and the exec count) like the validation report's
sensed fields. Golden/determinism tests run in environment-controlled temp dirs (a fresh
directory outside any git work tree, with git available → `initialized`); the FR-012
determinism assertion is "two identical runs **in the same environment** yield byte-identical
provenance and report JSON", **not** a host-independent verdict. Tests that must assert a
specific outcome control the environment (e.g. create a parent `.git` to force
`skippedExistingRepository`; point `PATH` away from git, or assert structurally, for
`skippedGitUnavailable`).

**Rationale**: The outcome legitimately depends on the host (git present? already in a
tree?), so a single golden cannot pin it across all environments. This mirrors the
`validation-report`'s declared "sensed metadata" exception
(`docs/release/schema-reference.md`) and keeps the deterministic contract honest: provenance
bytes are invariant (FR-012), and JSON determinism is asserted within a fixed environment.

**Alternatives considered**:
- *Pin a single golden outcome for all hosts* — rejected: would fail on a host without git
  or inside a work tree, contradicting FR-003/FR-012's environment-sensitivity.
- *Omit the outcome from the deterministic JSON* — rejected: FR-011/SC-006 require the
  outcome in every projection including JSON.

## Decision 6 — Leak invariant: new code lands in the already-scanned scaffold-source union

**Decision**: All new production code lives in files already covered by the
`ScaffoldGuardTests.fs:54-60` curated scan (`HandlersScaffold.fs`, `CommandSerialization.fs`,
`CommandRendering.fs`, `CommandReports.fs`, `Cli/Rendering.fs`) plus `CommandEffects.fs`
(add to the scanned set) and `Diagnostics.fs`. The guard re-asserts SC-005: no
`fs-gg-ui` / `FS.GG.Rendering` identifier, and no provider-specific package/template/path/
script-name/docs-URL. A US4 behavioral test additionally runs a **non-rendering** fixture
and asserts identical repo-init/exec behavior.

**Rationale**: The deny-list scan already exists; extending coverage to the one new source
file (`CommandEffects.fs`) and re-running it is the cheapest enforceable guarantee. The
`.sh` discovery rule and `git`/`SetExecutable` orchestration are intrinsically generic, so
no value-branching guard is needed beyond the identifier deny-list and the behavioral
non-rendering test.

## Resolved unknowns summary

| Unknown | Resolution |
|---|---|
| How to detect "inside a work tree" / "git absent" without stdout | Exit code of `git rev-parse --is-inside-work-tree` (Decision 1) |
| How to make scripts executable cross-platform within MVU | New `SetExecutable` edge → `File.SetUnixFileMode`, try/skip (Decision 2) |
| How to stage probe→init without a new model field | Stateless recompute from `InterpretedEffects`, three ticks (Decision 3) |
| What counts as a "shell script" generically | Produced (app-only) path with `.sh` suffix (Decision 4) |
| How goldens cope with an environment-sensed outcome | Sensed metadata; goldens fix the environment; provenance bytes invariant (Decision 5) |
| How the leak invariant stays enforced | New code in the existing scanned union + non-rendering behavioral test (Decision 6) |
</content>
