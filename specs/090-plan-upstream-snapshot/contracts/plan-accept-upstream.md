# Contract: `fsgg-sdd plan --accept-upstream` and the `stalePlanSnapshot` diagnostic

Tier 1 (contracted change): a new command flag and a new diagnostic id in the JSON automation contract. No persisted schema version moves.

## CLI surface

```
fsgg-sdd plan [--accept-upstream] [--json|--text|--rich] [--force-color]
```

`--accept-upstream` — Re-baseline the plan's `## Source Snapshot` against the current
`spec.md`, `clarifications.md`, and `checklist.md`. Use after reviewing recorded plan
decisions against an upstream edit. Rewrites only the snapshot; never other prose.

Read only by `plan`. Inert on every other command (FR-012); `fsgg-sdd` has no unknown-flag
rejection layer.

## JSON contract additions

`CommandReport.diagnostics[]` may now contain:

```json
{
  "id": "stalePlanSnapshot",
  "severity": "error",
  "artifact": { "path": "work/042/plan.md", "kind": "plan" },
  "message": "Plan snapshot is stale: 2 sources changed since the plan was recorded.",
  "correction": "Review the recorded plan decisions against the changed sources, then re-run with --accept-upstream.",
  "relatedIds": ["work/042/clarifications.md", "work/042/spec.md"]
}
```

- `relatedIds` — exactly the source paths whose recorded digest differs from the current
  content, **ordinally sorted** (FR-002, FR-014).
- `severity` is `error`: the report `outcome` is `blocked`, output routes to stderr, exit is `1`.
- The diagnostic is **not** a tool defect, so the blocked exit stays `1` and never escalates to `2`.

Emitted by `plan`, `tasks`, and `analyze`. Suppressed only on `plan --accept-upstream`.

Unchanged: `schemaVersion: 1`, `reportVersion: "1.3.0"`. No new report block.

## Behavioral contract

| # | Given | When | Then |
|---|---|---|---|
| C1 | plan exists, snapshot current | `plan` | `noChange`, exit 0, no `stalePlanSnapshot` |
| C2 | plan exists, `spec.md` edited | `plan` | `blocked`, exit 1, `stalePlanSnapshot` with `relatedIds = ["work/<id>/spec.md"]`, `changedArtifacts: 0`, `plan.md` byte-identical |
| C3 | as C2 | `plan --accept-upstream` | `succeeded`, exit 0, `changedArtifacts: 1`, only `## Source Snapshot` body differs |
| C4 | plan exists, snapshot current | `plan --accept-upstream` | `noChange`, exit 0, no flag-related diagnostic |
| C5 | no plan | `plan` / `plan --accept-upstream` | identical results; snapshot created fresh |
| C6 | as C2, plus a malformed front matter | `plan --accept-upstream` | `blocked`, exit 1, the front-matter error; **no** snapshot rewrite, zero writes |
| C7 | `spec.md` and `clarifications.md` both edited | `plan` | `relatedIds` names both, sorted |
| C8 | plan exists, snapshot current, `spec.md` edited | `tasks` (no `plan` re-run) | `blocked`, exit 1, `stalePlanSnapshot` |
| C9 | as C8 | `analyze` | `blocked`, exit 1, `stalePlanSnapshot` |
| C10 | as C8 | `tasks --accept-upstream` | `blocked` — the flag is not honored downstream |
| C11 | recorded snapshot entry has no digest | `plan` | not stale; no `stalePlanSnapshot` (FR-016) |
| C12 | recorded snapshot names a now-missing source | `plan` | the existing `missing…Prerequisite` error; **no** `stalePlanSnapshot` |
| C13 | plan whose `## Plan Decisions` carries an operator-authored `stale:` line | `tasks` | the pre-existing `failedPlanPrerequisite: "Plan contains stale decisions."` still blocks (FR-009) |
| C14 | any `plan` run on an existing plan | — | no `PD-###` line is ever appended (FR-001) |

## Invariant (SC-002)

For every `plan` invocation against an existing `plan.md`:

> the only region of the file that may differ after the run is the body of `## Source Snapshot`,
> and it may differ only when `--accept-upstream` was passed.

Pinned by a byte-level test that diffs the pre- and post-run file by section.

## Retired behavior

- `appendStalePlanDecision` — deleted. `plan` no longer writes into `## Plan Decisions`.
- `stalePlanDecision` (`DiagnosticWarning`) — no longer emitted on digest drift. The constructor
  is retained: an operator-authored `stale:` decision line still surfaces it.

## Migration posture

None required. A plan whose snapshot is already current is unaffected. A plan carrying a
previously-injected `PD-### … stale:` line keeps blocking at `tasks` via the retained
`failedPlanPrerequisite` (FR-009) until the operator deletes the line — the same manual step
they face today, and the tool will not add another. A plan with a stale snapshot now blocks at
`plan` with an actionable one-command recovery instead of silently advancing.
