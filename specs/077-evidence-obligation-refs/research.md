# Phase 0 Research: Preserve refs on auto-generated evidence obligations

No open `NEEDS CLARIFICATION` remained after grounding the spec in the code. This records the
decisions that shaped the plan.

## Decision 1 — Where the lost lineage actually lives

- **Decision**: Recover an obligation's origin from the originating task's `SourceIds`, routed by
  id grammar — not from `task.Requirements`/`task.Decisions`.
- **Rationale**: The task-graph author (`TaskGraphAuthoring.plannedTasks`) builds a plan-decision
  task with `maybeTask (decision.DecisionId.Value :: decision.SourceIds) title [] [] …` — the
  `PD-###` id and the plan decision's own source ids go into `SourceIds`, while `Requirements` and
  `Decisions` are passed `[]`. So `EvidenceObligation.LinkedRequirementIds`
  (`= task.Requirements`) and `LinkedDecisionIds` (`= task.Decisions`) are both empty for a
  plan-decision task; `skeletonEvidenceDeclaration` then hardcodes `PlanDecisionRefs = []`. The
  only surviving lineage is `task.SourceIds` (and the human-readable title). Recovering the PD id
  *and* the FR it traces to therefore requires `SourceIds`.
- **Alternatives considered**:
  - *Populate `task.Decisions` with the PD id in the task-graph author instead.* Rejected:
    `Decisions` is typed `DecisionId` (clarification `DEC-`), not `PlanDecisionId`; overloading it
    would corrupt the tasks contract and every downstream consumer, a far larger blast radius than
    the evidence-scaffold fix the issue asks for.
  - *Re-derive lineage by re-parsing the plan.* Rejected: the task graph already recorded it in
    `SourceIds`; re-deriving duplicates truth and risks drift (Principle II).

## Decision 2 — Carry lineage as `LinkedSourceIds` on the obligation

- **Decision**: Add an additive `LinkedSourceIds: string list` to `EvidenceObligation` holding
  `task.SourceIds` verbatim; classify it in `skeletonEvidenceDeclaration`.
- **Rationale**: Keeps the obligation an honest carrier of raw lineage and puts the typed
  routing at the one place that builds the declaration. `EvidenceObligation` is an in-memory
  intermediate (only `Evidence.fs`/`.fsi` and `HandlersEvidence.fs` reference it — verified by
  grep); it is **not** serialized to the work-model JSON, so adding a field does not touch
  work-model/readiness goldens.
- **Alternatives considered**:
  - *Pre-classify into `LinkedRequirementIds`/`LinkedDecisionIds` at obligation-build time.*
    Rejected: `LinkedDecisionIds` is a flat `string list` mixing PD and DEC, so the split has to
    happen at declaration-build time anyway; carrying raw `SourceIds` avoids a lossy intermediate
    and keeps one classification site.

## Decision 3 — Grammar-based routing, limited to requirement + plan-decision buckets

- **Decision**: Route each source id with `Identifiers.createRequirementId` /
  `createPlanDecisionId` (regex-scoped `FR-` / `PD-`) into `requirementRefs` / `planDecisionRefs`.
  Every other id (`AC-`/`DEC-`/`CR-`/`PC-`/`VO-`/`PM-`/`GV-`/…) is left unrouted, so the
  acceptance/clarification/checklist buckets stay `[]` on scaffolds exactly as before.
- **Rationale**: Reuses the single source of id grammar (Principle IV, no duplicate regex); total
  and safe (Principle VIII) — unmatched ids degrade rather than crash. Union with any refs the
  obligation already carries, then sort + `List.distinct` for determinism/idempotency (FR-005,
  SC-005). Scope is exactly the two buckets issue #124 names.
- **Why not route all buckets**: An earlier revision routed `AC-`/`DEC-`/`CR-` too. A pre-merge
  review flagged that this makes routing *total over `task.SourceIds`*, so the evidence stage's
  blocking `unknownEvidenceReference` check — which exists to catch **authored** ref typos — could
  now fire on **scaffolded** refs when `tasks.yml` lineage contains a grammar-valid id absent from
  the current spec/clarify/checklist facts (a stale/inconsistent state). Limiting routing to
  requirement/plan-decision keeps the feature's full value (FR + PD recovery, no title-join) while
  not widening that validation surface with the speculative buckets. The normal (consistent) case
  was clean either way; the narrower scope removes the edge-case blocking surface entirely for
  those buckets and matches the issue's exact ask.
- **Alternatives considered**: (a) keep total routing + exclude scaffolded skeletons from the
  unknown-reference check — rejected as a riskier change to validation semantics; (b) bespoke
  prefix `StartsWith` checks — rejected as a second grammar that can drift from `Identifiers`.

## Decision 4 — `--from-tests` shape and validation

- **Decision**: `evidence --from-tests <path>` seeds each *newly scaffolded* obligation with one
  verification-kind evidence source pointing at `<path>`. A single path applies to the whole run.
  Threaded through the existing command-request Model and parsed in `Program.fs` with the existing
  `optionValue "--from-tests" rest` convention. A missing/unreadable path resolves as an
  actionable input error (FR-009), not a silent empty source.
- **Rationale**: Matches the additive, inert-when-absent requirement (FR-007/FR-008) and the
  existing MVU request-record pattern (`--provider`, `--yes`). Per-obligation test mapping and
  bulk authoring stay out of scope (deferred to epic #127 / #126).
- **Alternatives considered**: per-obligation `--from-tests obligation=path` mapping — rejected as
  scope creep beyond the issue's "optional" ask.

## Decision 5 — No schema version bump

- **Decision**: Do not bump the evidence schema version.
- **Rationale**: `requirementRefs`/`planDecisionRefs`/`acceptanceScenarioRefs`/
  `clarificationDecisionRefs` already exist on `EvidenceDeclaration` and are already rendered by
  `renderEvidenceDeclaration`. The change only populates values previously empty; no field is
  added, removed, or retyped on the persisted artifact. Existing consumers read the same shape.
