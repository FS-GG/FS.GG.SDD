# Phase 1 Data Model: 090 — Plan Upstream Snapshot

No persisted schema changes. Every entity below already exists; this feature changes *who writes them* and *at what severity*.

## Authored artifact: `work/<id>/plan.md`

Classification: `AuthoredSource` (unchanged, FR-015).

| Section | Owner before | Owner after |
|---|---|---|
| front matter | operator | operator |
| `## Source Snapshot` | `plan` at creation only; **never refreshed** | `plan` at creation, and `plan --accept-upstream` thereafter — the plan's sole tool-writable region |
| `## Plan Decisions` | operator **+ `plan` injecting a synthesized `PD-###` stale line** | operator only |
| `## Plan Scope`, `## Contract Impact`, `## Verification Obligations`, `## Migration Posture`, `## Generated View Impact`, `## Accepted Deferrals`, `## Planning Findings`, `## Advisory Notes` | operator (+ `appendPlanEntries` derived rows) | unchanged |

The invariant this feature establishes: **`plan` writes exactly one region of `plan.md` — its own `## Source Snapshot` — and only under an explicit operator gesture.**

## Entity: Source Snapshot entry

Parsed into `PlanFacts.SourceSnapshots : (Path: string; Digest: string option) list`.

- `Path`: one of `work/<id>/spec.md`, `work/<id>/clarifications.md`, `work/<id>/checklist.md`.
- `Digest`: `sha256` of the source text at the moment the plan was created or last accepted. `None` when the recorded line carries no digest.

**Staleness predicate** (unchanged semantics, `Foundation.sourceDigestsStale`):

```
stale(entry) = match entry.Digest, currentDigest(entry.Path) with
               | Some recorded, Some actual -> recorded <> actual   (ordinal-ignore-case)
               | _                          -> false
```

Both `_` arms are load-bearing:
- `Digest = None` → not stale (FR-016: old plans do not become blocked on upgrade).
- no current digest for the path → not stale; the source is *missing*, which `missingSpecificationPrerequisite` and friends already report. `stalePlanSnapshot` must not mask a missing-source error.

**Changed-path projection** (new): `changedPlanSourcePaths` returns the `Path`s for which `stale(entry)` holds, sorted ordinally. It MUST apply the identical predicate, so `changedPlanSourcePaths ≠ [] ⟺ sourceDigestsStale = true`. A property test pins this equivalence.

## Entity: Plan Decision (`PD-###`)

Parsed into `PlanFacts.Decisions : { DecisionId; Status; SourceIds; … } list`. `Status = "stale"` is derived from a literal `stale:` marker in the line's text.

After this feature no `PD-###` line is ever tool-authored, so `Status = "stale"` can only arise from operator prose. The downstream `failedPlanPrerequisite: "Plan contains stale decisions."` check (`ChecklistPlanAuthoring.fs:1325`) is **retained** as the safety net for that authored case (FR-009).

## Entity: `stalePlanSnapshot` diagnostic (new)

| Field | Value |
|---|---|
| `Id` | `stalePlanSnapshot` |
| `Severity` | `DiagnosticError` |
| `Artifact` | `work/<id>/plan.md` |
| `Message` | `Plan snapshot is stale: <n> source(s) changed since the plan was recorded.` |
| `Correction` | `Review the recorded plan decisions against the changed sources, then re-run with --accept-upstream.` + the `RemediationPointers.suffixFor` suffix |
| `RelatedIds` | the changed source paths, ordinally sorted |
| `IsToolDefect` | `false` — a stale upstream is a workspace state, not a tool defect. Keeps the blocked exit at 1, not 2. |

Emitted from `plan` (via `planDiagnosticsTextAndSummary`) and from `tasks`/`analyze` (via `planPrerequisiteDiagnosticsTextSummaryAndFacts`).

Suppressed **only** when `request.AcceptUpstream` is set **and** the command is `plan` — never for the downstream prerequisite read, where accepting the upstream is not the operator's gesture to make.

## Entity: `stalePlanDecision` diagnostic (retired for the tool-detected case)

Remains as a `DiagnosticWarning` constructor and remains reachable for a plan whose *authored* prose carries a `stale:` decision. It is no longer emitted as a consequence of digest drift.

## Entity: `CommandRequest.AcceptUpstream` (new field)

`bool`, parsed in `Program.fs` as `hasFlag "--accept-upstream" rest`, declared in `CommandTypes.fsi`. Additive; `CommandReport.schemaVersion` stays `1` and `reportVersion` stays `1.3.0` (no new report block).

## State transitions — `fsgg-sdd plan`

| Plan exists | Snapshot stale | `--accept-upstream` | Outcome | `plan.md` write | Exit |
|---|---|---|---|---|---|
| no | — | either | `Succeeded` | created (fresh snapshot) | 0 |
| yes | no | no | `NoChange` | none | 0 |
| yes | no | yes | `NoChange` | none (identical bytes) | 0 |
| yes | yes | no | `Blocked` (`stalePlanSnapshot`) | **none** — effect gate | 1 |
| yes | yes | yes | `Succeeded` | `## Source Snapshot` body only | 0 |
| yes | any | yes, **+ unrelated blocking diagnostic** | `Blocked` | **none** — effect gate | 1 |

The last row is FR-006: `--accept-upstream` suppresses one diagnostic, it does not force a write. The effect gate enforces it without special-casing.

## State transitions — `fsgg-sdd tasks` / `analyze`

| Plan snapshot stale | Outcome | Exit |
|---|---|---|
| no | unchanged from today | unchanged |
| yes | `Blocked` (`stalePlanSnapshot`, pointing at `plan --accept-upstream`) | 1 |

`--accept-upstream` is never honored here (FR-008 + the suppression rule above).
