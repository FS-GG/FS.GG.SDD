# Contract: `fsgg-sdd scaffold` CLI command

**Command**: `scaffold` · cross-cutting (not a lifecycle stage; `nextLifecycleCommand
Scaffold = None`, FR-015) · projects the same `CommandReport` three ways.

## Synopsis

```text
fsgg-sdd scaffold --provider <name> [--param <key>=<value> ...] [--force]
                  [--root <path>] [--dry-run] [--json | --text | --rich]
```

## Options

| Option | Required | Meaning |
|---|---|---|
| `--provider <name>` | **yes** | descriptor name from `.fsgg/providers.yml`. Omitted → `scaffold.providerMissing` (points to `fsgg-sdd init`). |
| `--param <key>=<value>` | no, repeatable | provider parameter; overlays descriptor defaults |
| `--force` | no | opt-in to materialize into a **non-empty** target (FR-010) |
| `--no-update` | no | skip the best-effort `dotnet new update` template refresh (still installs + creates); for create-only / offline runs. Template refresh is **on by default**. |
| `--root <path>` | no (default `.`) | project/target root |
| `--dry-run` | no | plan + report effects (incl. the provider `RunProcess`) without executing real I/O |
| `--json` / `--text` / `--rich` | no | output projection; precedence `--rich` > `--text` > `--json` > default(json) |

Parsed in `Cli/Program.fs` using the existing `optionValue`/`hasFlag` helpers; `--param`
needs a small repeatable-collector (the only new parsing primitive).

## Behavior (happy path)

1. Resolve `--provider` → descriptor (`.fsgg/providers.yml`).
2. Validate descriptor `contractVersion` ∈ supported range; validate required params.
3. **Establish the SDD skeleton** (init effects, unchanged) — FR-004.
4. Snapshot target dir; if non-empty and not `--force` → `scaffold.targetCollision`
   (per-path), stop before invocation.
5. `RunProcess` → `dotnet new <templateId> -o <root> -p:<k>=<v> …` (`--force` iff opted in).
6. Snapshot after; `producedPaths = after − before − skeleton`.
7. Guard: any produced path under `.fsgg/`/`work/`/`readiness/` → `providerWroteSddTree`.
8. Write `.fsgg/scaffold-provenance.json` (produced paths marked `generatedProduct`).
9. Build the report: `ScaffoldSummary` + produced paths as `ChangedArtifacts`
   (`generatedProduct`), `NextAction = None`, info hint to begin at `charter`.

## Output contract

Identical facts across `--json` / `--text` / `--rich` (SC-006); `--rich` is presentation
only and changes no JSON byte; it degrades to plain text when non-interactive / `NO_COLOR`
/ `TERM=dumb`. The `--json`/default form is byte-stable: produced paths sorted, no
clock/abs-path/ANSI, provider stdout/stderr excluded.

Exit codes (via existing `exitCodeForReport`): `0` success (incl. empty), `1`
user-input block (missing/unknown provider, unsupported version, missing param,
collision, malformed provenance), `2` provider defect (failed, unavailable, wrote SDD
tree).

## `--dry-run`

Plans all effects including the provider `RunProcess`, reports what *would* run and what
*would* be produced (best-effort: produced paths unknown until run, so the dry-run report
states the planned command and that produced paths are determined at execution), writes
nothing, runs no child process.

## Examples

```text
# error: skeleton-only belongs to init
fsgg-sdd scaffold
  -> blocked (exit 1): scaffold.providerMissing — use `fsgg-sdd init` for skeleton-only

# scaffold a runnable product from a registered provider
fsgg-sdd scaffold --provider <name> --param productName=Acme --rich

# into an existing project, explicitly allowing overwrite
fsgg-sdd scaffold --provider <name> --force --json
```
