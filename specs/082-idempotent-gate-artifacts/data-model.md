# Phase 1 Data Model: Idempotent Generated Gate Artifacts

This feature adds no new persisted schema. It formalizes the **authored-vs-generated
boundary** inside two existing artifacts and the **merge model** that preserves authored
state across re-derivation. The types below already exist (`Checklist.fs(i)`, `Task.fs(i)`);
this documents the roles the fix relies on.

## checklist.md — regions

| Region (section) | Ownership | Re-run behavior |
|---|---|---|
| Source Specification | authored (rendered mirror) | preserved untouched |
| Source Clarifications | authored (rendered mirror) | preserved untouched |
| Source Snapshot | tool (provenance) | **refreshed** each run (current spec/clarify digests) |
| Checklist Items (`CHK-###`) | tool-derived | **re-derived** from current spec/clarify facts |
| Review Results (`CR-###`) | tool-derived | **re-derived** |
| Accepted Deferrals | tool-derived | **re-derived** |
| Blocking Findings | tool-derived | **re-derived** |
| Advisory Notes | authored | preserved untouched |
| Lifecycle Notes | authored | preserved untouched |

**Invariant**: no `CHK-###`/`CR-###` row is ever read back as authored input. A row exists
in the output iff the current sources justify it. (Kills #146.)

**Coverage input**: the `- FR-###: … (covers AC-###)` declaration is parsed from **`spec.md`**
requirement references, never from `checklist.md`. Adding coverage there makes the next
`checklist` run derive a pass and omit the missing-coverage blocking row.

## tasks.yml — task record and merge model

`WorkTask` (existing, `Task.fs:49-61` / `Task.fsi:45-57`) — relevant fields:

| Field | Ownership | Re-run behavior |
|---|---|---|
| `id` (`T###`) | tool (stable identity) | **preserved** for a matched task; newly allocated for a new one |
| `SourceIds` (`FR-###`/`DEC-###`/…) | tool-derived | re-derived; **leading id is the merge key** |
| `Title`, `Dependencies`, `Requirements`, `Decisions`, `RequiredSkills`, `RequiredEvidence` | tool-derived | **re-derived** from current sources |
| `status` (`Pending`/`InProgress`/`Done`/`Skipped of string`/`Stale`) | author-mutable | **carried forward** on a matched task |
| `owner` | author-mutable | **carried forward** on a matched task |

File-level `advisoryNotes` / `lifecycleNotes`: authored, preserved untouched.

### Merge (re-derive + four-way, keyed on task Title)

`tasks.yml` mixes tool-derived and hand-authored tasks (the corpus proves it — see
research Decision 3), so the merge is four-way. `derivedCoverage` = the set of disposition
ids the re-derived graph covers; `liveIds` = the ids the current sources can dispose
(mirrors analyze's `required` set).

```text
derived = re-derive full graph from current sources           # fresh ids, Pending
for each d in derived:                                         # keyed on task Title
    match = prior with title == d.title
    if match:  d.id ← match.id; d.status ← match.status; d.owner ← match.owner
               d.requirements/decisions/sourceIds ← derived ∪ (match's, filtered to liveIds)
    else:      d.id ← next T### (above every prior id); deps remapped fresh→final
keptAuthored = prior with no title match AND that uniquely cover a live disposition
               NOT in derivedCoverage        # hand-authored tasks; refs filtered to liveIds
result = mergedDerived ++ keptAuthored        # everything else (dead/redundant orphan) dropped
```

**Invariants**:
- Unchanged sources ⇒ `derived` reproduces the prior graph, the merge is identity, and
  `keptAuthored` is empty ⇒ byte-identical output ⇒ `noChange` (FR-008).
- A new source (e.g. `DEC-002`) ⇒ a new `Pending` task appears ⇒ `analyze` disposition
  clears (fixes #147).
- A hand-authored task or ref covering a live disposition the derivation misses (e.g.
  `decisions: [DEC-001]`) is **preserved**; one whose source is gone, or already covered by
  derivation, is **dropped** — no stale content re-ingested (FR-002).
- `TaskStatus.Stale` is retained in the type (legacy/authored parse) but is **not produced**
  by this path (Decision 4). `StaleCount` from this path is therefore `0`.

## State transitions (task status across a re-run)

| Prior status | Source still present | After re-run |
|---|---|---|
| `Pending` | yes | `Pending` (re-derived) |
| `InProgress` / `Done` / `Skipped` | yes | **same** (carried forward on matched id) |
| any | no (source removed) | **dropped** |
| — | new source added | `Pending` (new `T###`) |
| `Stale` (legacy/authored in file) | yes | replaced by the re-derived status per row (no longer minted) |

## Diagnostics / outcomes touched (no schema change)

| Symbol | Before | After |
|---|---|---|
| `staleTask`, `TF-001`, `tasks.correctStaleTasks`, `StaleCount` | emitted on upstream change | **not emitted** by the re-run path; symbols retained |
| checklist orphaned `CHK-###` blocking row | preserved, re-counted as blocking | **re-derived away** when sources no longer justify it |
| `unsafeOverwrite` (`<!-- fsgg-sdd: unsafe-overwrite -->`) | blocks overwrite, names file+command | **unchanged** (the sole re-run block, FR-006/FR-009) |

## Ownership boundary (unchanged)

All artifacts remain SDD-owned lifecycle artifacts. No Governance runtime, verdict, freshness
evaluation, or gate enforcement is introduced (FR-012). `analyze`/`verify`/`ship`/`refresh`
read the re-derived graph exactly as before; none reads task `Stale`.
