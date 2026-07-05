# Contract: Re-run Outcomes for `checklist` and `tasks`

Deterministic outcome each command produces on a re-run, before vs after this feature.
`--json` is the automation contract; `--text`/`--rich` are pure projections. Exit codes
unchanged. These are the observable acceptance points for SC-001..SC-006.

## `fsgg-sdd checklist`

| Scenario | Before | After (this feature) |
|---|---|---|
| Not-stale re-run; file has a `CHK-###` blocking row for an FR now covered in `spec.md` | row preserved, re-counted **blocking** (#146) | row **re-derived away**; not blocking; no file deletion |
| Re-run; FR still uncovered | blocking (possibly a preserved prior row) | blocking, as a **freshly re-derived** verdict (no duplicate) |
| Re-run; unchanged sources, authored notes present | `noChange`, byte-identical | `noChange`, byte-identical; authored sections untouched |
| First re-run after upgrade of an old-format file | n/a | may rewrite once (canonical re-derive), then `noChange` |
| Artifact carries `unsafe-overwrite` sentinel | `unsafeOverwrite` block, names file+cmd | **unchanged** |

## `fsgg-sdd tasks`

| Scenario | Before | After (this feature) |
|---|---|---|
| `plan.md` edited to add a `DEC-002` disposition; re-run | `status: stale`, `TF-001`, `staleTask`, `StaleCount>0`, **graph unchanged** (#147) | graph **re-derived**; a task referencing `DEC-002` appears; downstream `analyze` disposition clears |
| Re-run; task previously marked `Done`/`Skipped`/`owner` set, source still present | preserved (carried verbatim) | **preserved** (status/owner carried onto the re-derived task; `T###` id kept) |
| Re-run; a task's source removed from plan | relabeled `Stale`, kept | **dropped** (no longer derived) |
| Re-run; unchanged sources | `noChange`, byte-identical | `noChange`, byte-identical |
| First re-run after upgrade of an old-format file | n/a | may rewrite once (canonical re-derive + merge), then `noChange` |
| Artifact carries `unsafe-overwrite` sentinel | `unsafeOverwrite` block, names file+cmd | **unchanged** |

## Retired signals (task source-change path)

`staleTask`, `TF-001`, `status: stale`, `StaleCount>0`, and the `tasks.correctStaleTasks`
next-action are **no longer emitted** on an upstream source change. The symbols remain in the
public surface (a legacy/authored `Stale` value still parses); they are simply not minted by
this path. No downstream stage reads task `Stale` (verified), so no verdict changes.

## Invariant across both commands

Every re-run yields exactly one of: **regenerated in place** (default) or **blocked by the
`unsafe-overwrite` opt-out** (names file + command). Never "stale and unchanged with no
actionable next step." (FR-004/FR-006.)
