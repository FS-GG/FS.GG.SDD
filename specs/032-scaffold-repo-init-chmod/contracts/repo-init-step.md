# Contract: Repository-initialization post-instantiation step

This is a **behavior contract** over the existing `fsgg-sdd scaffold` surface, not a new
external interface. It pins what repo-init does, senses, and reports.

## Trigger

Runs **after** a provider instantiation whose outcome is `ProviderSucceeded` or
`ProviderSucceededEmpty`, on the **real** execution path only (not dry-run). Never runs on
`ProviderFailed` / `ProviderNotRun` (FR-009).

## Sensing (exit-code only)

A single probe `RunProcess("git", ["rev-parse"; "--is-inside-work-tree"], "")` at the product
root. Branch on `ProcessRunResult` (stdout is not read — Decision 1):

| `ProcessRunResult` | Action | `RepoInitOutcome` | Diagnostic |
|---|---|---|---|
| `Some { Started = false }` | none | `skippedGitUnavailable` | `scaffold.repoInitSkippedGitUnavailable` (advisory) |
| `Some { Started = true; ExitCode = 0 }` | none (no nesting) | `skippedExistingRepository` | `scaffold.repoInitSkippedExistingRepository` (advisory) |
| `Some { Started = true; ExitCode ≠ 0 }` | `RunProcess("git",["init"],"")` | `initialized` | none |

## Guarantees

- **G1 (FR-001/SC-001)**: target not in a work tree + git available ⇒ a git repository exists
  at the product root after scaffold, and the report records `initialized`.
- **G2 (FR-002/SC-002)**: target already inside a work tree ⇒ **no** nested repo; report
  records `skippedExistingRepository`.
- **G3 (FR-003/SC-004)**: git unavailable ⇒ repo-init skipped, scaffold reaches its normal
  success outcome and exit code; report records `skippedGitUnavailable`.
- **G4 (FR-004)**: when initialized, the work tree spans the SDD skeleton + provider product
  files + `scaffold-provenance.json` (repo-init runs after the provenance write, Decision 3).
- **G5 (FR-013)**: re-running into an already-scaffolded dir is the existing-repo case (G2);
  idempotent, no nesting.
- **G6 (FR-009)**: on any provider-failure path, repo-init does not run; the existing
  failure outcome/diagnostic/exit code are preserved.
- **G7 (FR-012)**: creating `.git` writes no byte of `scaffold-provenance.json` or the report
  JSON; only the additive sensed `RepoInitOutcome` field reflects it.

## Non-goals

- No initial **commit** is made (Assumption: "initialized repository, not initial commit").
- No `initGit`/`allow-scripts` flag is passed to the provider (FR-007).
</content>
