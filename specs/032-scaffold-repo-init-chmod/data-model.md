# Phase 1 Data Model: Scaffold repo-init & script-executable post-instantiation

Entities are the typed contracts touched or added by this feature. Authoritative source for
each is its `.fsi`; this document is the design intent and validation-rule record.

## 1. `CommandEffect.SetExecutable` (new effect case)

**Where**: `CommandTypes.fsi` / `CommandTypes.fs` — `CommandEffect` DU
(currently `CommandTypes.fsi:381-389`).

```fsharp
type CommandEffect =
    | ReadFile of path: string
    | EnumerateDirectory of path: string
    | CreateDirectory of path: string
    | WriteFile of path: string * text: string * kind: ArtifactWriteKind
    | RunProcess of command: string * args: string list * workingDir: string
    | SetExecutable of path: string          // NEW — set the executable bit on a scaffolded file
    | EmitStdout of text: string
    | EmitStderr of text: string
    | SetExitCode of code: int
```

**Fields**: `path` — repo-root-relative path of the produced shell script to mark executable.

**Interpretation** (`CommandEffects.fs`, new `interpret` arm):
- On `dryRun` → success, **no** filesystem change (FR-008).
- Else → `File.SetUnixFileMode(absolute, existing ||| Execute bits)`; wrapped in `try`. On
  exception (read-only FS, non-Unix host, missing file) → a result whose `Succeeded = false`
  / diagnostic marks the script as *not* made executable, feeding the skip/partial count
  (FR-005, US2-AC3). **Never** throws past the interpreter.

**Validation rules**:
- Only ever planned for paths in the produced (app-only, non-SDD) set (FR-006); never for
  SDD-owned or skeleton paths.
- `effectPath (SetExecutable p) = Some p` (extends the existing `effectPath` mapping).
- A `SetExecutable` failure does not flip the scaffold outcome (FR-010).

## 2. `ScaffoldSummary` (extended)

**Where**: `CommandTypes.fsi:328-336`. Existing fields retained; new fields added.

```fsharp
type ScaffoldSummary =
    { ProviderName: string option
      ProviderContractVersion: string option
      Outcome: string
      SkeletonCreated: bool
      ProviderInvoked: bool
      ProducedPathCount: int
      ProducedPaths: string list
      // NEW — post-instantiation step outcomes (FR-011, SC-006)
      RepoInitOutcome: string            // see Repo-init step outcome below
      ExecutableScriptCount: int         // shell scripts made executable (0 when none)
      ExecutableScriptsSkipped: int      // scripts a bit could not be applied to (skip/partial)
      NextActionHint: string }
```

**Validation rules**:
- `RepoInitOutcome` ∈ the closed vocabulary in §3.
- `ExecutableScriptCount + ExecutableScriptsSkipped = ` the number of produced `.sh` scripts
  the run attempted (Decision 4); both `0` when the provider produced no scripts (US2-AC2).
- On `ProviderNotRun` / `ProviderFailed` and on dry-run: `RepoInitOutcome = notApplicable`,
  `ExecutableScriptCount = 0`, `ExecutableScriptsSkipped = 0` (FR-008/009).
- These fields are **additive**; they change no existing field's bytes (FR-012).

## 3. Repo-init step outcome (closed vocabulary)

A single string field `RepoInitOutcome` with exactly these values (Decision 1):

| Value | When | Diagnostic | Exit impact |
|---|---|---|---|
| `initialized` | provider succeeded, not inside a work tree, git available | none | none |
| `skippedExistingRepository` | target already inside a git work tree (probe exit 0) | advisory | none (FR-002) |
| `skippedGitUnavailable` | git not launchable (`Started=false`) | advisory | none (FR-003) |
| `notApplicable` | provider not run / failed, or dry-run | none | none (FR-008/009) |

**State source**: derived purely from the `git rev-parse --is-inside-work-tree`
`ProcessRunResult` and whether the create outcome was a success (Decision 3). It is **sensed
metadata** (Decision 5): host-dependent, not host-invariant.

## 4. Make-executable step outcome

Represented by `ExecutableScriptCount` (made executable) and `ExecutableScriptsSkipped`
(could not apply). Derived from the interpreted `SetExecutable` effects:

- `ExecutableScriptCount` = count of `SetExecutable` results with `Succeeded = true`.
- `ExecutableScriptsSkipped` = count with `Succeeded = false` (read-only/non-Unix).
- Both `0` ⇒ no produced shell scripts (no-op, US2-AC2).

## 5. Post-instantiation staging (state machine, not a stored type)

The driver recomputes its phase from `InterpretedEffects` each tick (Decision 3). Marker
effects partition the phases:

```text
              create interpreted?
                    │ no  ──────────────► (existing) plan install/update/skeleton/create
                    │ yes
          create outcome success?
            │ no ───────────────────────► (existing) terminal: set Scaffold summary,
            │                              provenance, failure diagnostic — NO post-steps (FR-009)
            │ yes
   probe planned/interpreted?
     │ no ─────────► TICK A: plan provenance write
     │               + RunProcess("git",["rev-parse";"--is-inside-work-tree"],"")
     │               + SetExecutable(p) for each produced *.sh
     │ yes (probe interpreted, init not yet decided)
     │   ┌── exit ≠ 0 & Started ─► TICK B: plan RunProcess("git",["init"],"")
     │   └── else ───────────────► TICK B: plan nothing (skip)
     │ git init interpreted OR skipped
     └─────────────► TICK C: compute RepoInitOutcome + exec counts + diagnostics + hint;
                              set Scaffold = Some summary  (terminal)
```

**Invariants**:
- Post-instantiation effects exist in the plan **only** on a success create outcome (FR-009).
- `git init` is planned **only** when the probe says not-in-a-tree and git is available
  (FR-002/003).
- The final summary is set exactly once, after all post-instantiation effects are
  interpreted (no false-complete, FR-010).
- Re-running into an already-scaffolded directory hits `skippedExistingRepository` and
  re-applies executable bits idempotently (FR-013).

## 6. Diagnostics (new advisory facts)

**Where**: `Diagnostics.fsi:49-61` / `Diagnostics.fs:140-250` (scaffold family).

| Diagnostic id | Severity | Raised when | Next action |
|---|---|---|---|
| `scaffold.repoInitSkippedExistingRepository` | advisory | target already in a work tree | "Left the existing repository untouched; no nested repo created." |
| `scaffold.repoInitSkippedGitUnavailable` | advisory | git not launchable | "Install git and re-run, or run `git init` yourself; scaffold otherwise succeeded." |
| `scaffold.scriptsNotMadeExecutable` | advisory | ≥1 `.sh` could not be chmod'd | "Set the executable bit manually (e.g. on a read-only or non-Unix filesystem)." |

**Rules**: all three are **advisory and non-fatal** — they never change the exit code or flip
the scaffold to failed/incomplete (FR-010, constitution VIII). They are user-facing
observability facts (split from provider-defect ids, which remain exit-2).

## 7. Unchanged contracts (explicitly out of scope)

- `ScaffoldProvenanceRecord` / `scaffold-provenance.json` (schema v1) — **byte-unchanged**
  (FR-012); repo-init captures it in the work tree (FR-004) but writes none of its bytes.
- `ProviderDescriptor` / `providers.yml` (v1), the provider invocation protocol, and the
  `dotnet new` create-arg vector — unchanged; SDD passes **no** `initGit`/`allow-scripts`
  to the provider (FR-007).
- `ProcessRunResult` — unchanged; exit-code-only sensing needs no new field (Decision 1).
</content>
