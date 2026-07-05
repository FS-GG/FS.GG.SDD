# Phase 1 Data Model: Preserve refs on auto-generated evidence obligations

## Entities

### EvidenceObligation (in-memory intermediate — `FS.GG.SDD.Artifacts/LifecycleArtifacts/Evidence.fs`)

The per-task obligation the evidence stage scaffolds from. **Additive change**: one new field.

| Field | Type | Change | Notes |
|---|---|---|---|
| `ObligationId` | `string` | — | unchanged |
| `Kind` | `string` | — | unchanged |
| `SourceArtifactPath` | `string` | — | unchanged |
| `SourceId` | `string option` | — | originating task id |
| `LinkedTaskIds` | `TaskId list` | — | unchanged |
| `LinkedRequirementIds` | `RequirementId list` | — | from `task.Requirements` |
| `LinkedDecisionIds` | `string list` | — | from `task.Decisions` |
| **`LinkedSourceIds`** | **`string list`** | **NEW** | verbatim `task.SourceIds` (the origin lineage bag) |
| `ExpectedEvidenceKinds` | `string list` | — | unchanged |
| `RequiredSkillOrCapabilityTags` | `string list` | — | unchanged |
| `Blocking` | `bool` | — | unchanged |
| `Correction` | `string` | — | unchanged |

Not serialized to the work-model JSON (in-memory only) — adding the field touches `Evidence.fsi`
and the `FS.GG.SDD.Artifacts` public-API baseline, but no persisted golden.

### EvidenceDeclaration (machine contract — scaffolded into `work/<id>/evidence.yml`)

**No shape change.** The ref fields already exist; scaffolding now *populates* them instead of
leaving `PlanDecisionRefs` (and, for PD tasks, `RequirementRefs`) empty.

| Field | Type | Behavior change |
|---|---|---|
| `RequirementRefs` | `RequirementId list` | now includes `FR-` ids recovered from the task's source lineage (was: only `task.Requirements`) |
| `PlanDecisionRefs` | `PlanDecisionId list` | now includes `PD-` ids from lineage (was: hardcoded `[]`) |
| `AcceptanceScenarioRefs` | `AcceptanceScenarioId list` | **unchanged** — stays `[]` on scaffolds (routing limited to requirement/plan-decision) |
| `ClarificationDecisionRefs` | `DecisionId list` | **unchanged** — stays `[]` on scaffolds |
| `ChecklistResultRefs` | `ChecklistResultId list` | **unchanged** — stays `[]` on scaffolds |
| `SourceRefs` | `EvidenceSourceReference list` | `--from-tests`: one verification-kind source `<path>` on newly scaffolded skeletons (was: none unless authored) |

### Command request (MVU Model — `FS.GG.SDD.Commands/CommandTypes`)

| Field | Type | Change | Notes |
|---|---|---|---|
| `FromTests` (name TBD in impl) | `string option` | NEW additive | parsed from `--from-tests <path>`; `None` ⇒ inert |

## Validation & routing rules

- **Ref routing** (pure): for each id in `LinkedSourceIds`, classify by `Identifiers` validators —
  `FR-`→requirement, `PD-`→plan-decision (routing limited to these two buckets). Union with
  any refs the obligation already carried; then `List.distinct |> List.sort`. Unrecognized id
  shape ⇒ left unrouted (no crash, no diagnostic).
- **No-clobber (FR-006)**: routing applies only to *skeleton* declarations scaffolded for
  not-yet-authored obligations; an author-authored entry is never rewritten.
- **`--from-tests` validation (FR-009)**: `Some path` that is missing/unreadable ⇒ actionable
  input error at the effect edge; scaffolding does not proceed with an empty source. `None` ⇒ no
  source added, output byte-identical aside from refs (FR-008).
- **Determinism (FR-005, SC-005)**: all ref lists and the seeded source list are sorted; re-runs
  are idempotent.

## State / migration

- No state machine.
- **Schema migration**: none. No evidence schema version bump (fields pre-exist; only values
  change). Older/newer readers see the same declaration shape.
