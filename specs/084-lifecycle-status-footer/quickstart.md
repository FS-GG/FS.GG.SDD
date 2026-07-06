# Quickstart / Validation: Lifecycle-Status Footer

Runnable scenarios that prove the feature end-to-end. Details of the fact live in `contracts/lifecycle-status.md` and `data-model.md`.

## Prerequisites

- `dotnet` (net10.0 SDK), repo restored: `dotnet restore`
- Build: `dotnet build src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj`
- A scratch SDD workspace: `fsgg-sdd init` in an empty dir (or reuse an existing `work/<id>` tree).

## Scenario A — footer present & correct on a lifecycle stage (FR-001..FR-004)

1. In a work item that has `charter.md`, `spec.md`, `clarifications.md` on disk, run the clarify stage command with `--json`.
2. **Expect**: output ends with a `lifecycleStatus` object; `stages[0..2]` (charter/specify) = `done`, `clarify` = `current`, `checklist` = `next`, rest `pending`; `currentOrdinal = 3`, `totalStages = 10`, `nextCommand = "checklist"`.
3. Re-run with `--text`. **Expect**: the final block is the 3-line text footer (`lifecycle:`/`stages:`/`next:`) with the same facts.

## Scenario B — projection parity + degradation (FR-009, FR-010, SC-002, SC-003, SC-008)

1. Run the same command three ways: `--json`, `--text`, and `--rich` piped to a file (non-interactive).
2. **Expect**: the piped `--rich` output is **byte-identical** to `--text` and contains zero ANSI/color/box control sequences.
3. Run `--rich` in an interactive terminal. **Expect**: a colored stage rail (done=green, current=cyan, next=yellow, pending=dim), same facts, as the final element.
4. Diff the fact set across all three. **Expect**: no fact present in one and absent in another.

## Scenario C — non-contiguous progress (FR-004, SC-006)

1. In a work item, ensure `readiness/<id>/verify.json` exists but delete/omit `work/<id>/evidence.yml` (a later artifact present, an earlier one absent).
2. Run any stage command.
3. **Expect**: `verify` = `done` while `evidence` = `pending` — the footer reflects true on-disk state, not contiguous completion.

## Scenario D — blocked outcome: explanation + options (FR-013, FR-017, SC-007)

1. Trigger a blocking diagnostic on a stage (e.g. malformed checklist coverage line).
2. Run that stage command.
3. **Expect** (text): footer marks the stage `blocked`, then `why:` = the blocking diagnostic message, `fix:` = its correction (already carrying the remediation pointer), `options:` = `fsgg-sdd <nextAction command>`. Every line traces to an existing `diagnostics`/`nextAction` fact — nothing invented.
4. **Expect** (rich): the same, with the blocked stage and failure block in red.

## Scenario E — cross-cutting & early-stage coherence (FR-011, FR-012)

1. Run a cross-cutting command (`refresh`) in a mid-lifecycle work item. **Expect**: `isLifecycleStage = false`, no stage `current`, rail sensed from disk, footer flags "refresh is not a lifecycle stage."
2. Run an early-stage command in a work item with only `charter.md`. **Expect**: `charter = done` (if present), rest `pending`, no error, coherent footer.
3. Run a command with no resolvable work id (`init`). **Expect**: `workId = null`, all stages `pending`, `next: fsgg-sdd charter`.

## Scenario F — determinism (FR-015)

1. Run the same command twice against an unchanged work item with `--json`.
2. **Expect**: byte-identical `lifecycleStatus` both times.

## Automated coverage (see tasks.md)

- Sensing/derivation over real-fs fixtures (staged/partial/non-contiguous/no-work-id) — `FS.GG.SDD.Commands.Tests`.
- Golden fixture for the deterministic `--text` footer.
- json↔text parity + blocked-outcome projection tests.
- Rich footer content + non-interactive degradation (== text, zero ANSI) — `FS.GG.SDD.Cli.Tests`.
