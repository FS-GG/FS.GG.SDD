# Phase 0 Research: 090 — Plan Upstream Snapshot

All findings verified against the source at `origin/main` (`66545c9`). No `NEEDS CLARIFICATION` remains.

## R1 — Why the mutation reaches disk at all

**Decision**: Raise the tool-detected staleness from a `DiagnosticWarning` to a `DiagnosticError` and delete the prose injection. No new suppression logic.

**Rationale**: `runHandler` (`Prerequisites.fs:139`) already implements the effect gate:

```fsharp
let effects = if hasBlocking then [] else writeEffects @ generatedEffects
```

where `hasBlocking` is `List.exists (fun d -> d.Severity = DiagnosticError)`. Today's `stalePlanDecision` (`DiagnosticConstructors.fs:410`) is built with `warningDiagnostic`, so `hasBlocking` is false, the gate opens, and the text produced by `appendStalePlanDecision` (`ChecklistPlanAuthoring.fs:1083`) — which appended a synthesized `PD-###` line to `## Plan Decisions` — is written to the authored `plan.md`. The bug is one severity level.

Consequences that fall out for free:
- FR-003's "writes zero bytes" is delivered by the existing gate.
- `ReportAssembly.outcome` (`:14`) maps any `DiagnosticError` to `Blocked`; `Blocked` routes to stderr and exits 1. FR-002/FR-003 need no exit-code work.
- `HandlersEarly.fs` — owned by the concurrently-running FS.GG.SDD#174 — needs **no change**. The declared touch-set holds.

**Alternatives considered**: adding an explicit `if stale then []` guard in `computePlanPlan`'s `planEffects`. Rejected: duplicates the gate `runHandler` already owns, and would have forced an edit to `HandlersEarly.fs`.

## R2 — Where `plan` and `tasks`/`analyze` each get their plan diagnostics

**Decision**: Emit `stalePlanSnapshot` from **two** call sites in `ChecklistPlanAuthoring.fs` — `planDiagnosticsTextAndSummary` (the `plan` stage) and `planPrerequisiteDiagnosticsTextSummaryAndFacts` (the downstream prerequisite read).

**Rationale**: `resolvePrerequisites` (`Prerequisites.fs:55`) always computes `PlanDiagnostics` via `planPrerequisiteDiagnosticsTextSummaryAndFacts`, but `computePlanPlan` (`HandlersEarly.fs:236`) *does not fold `prereqs.PlanDiagnostics` into its command diagnostics* — it calls `planDiagnosticsTextAndSummary` instead. `computeTasksPlan` (`HandlersEarly.fs:321`) and `HandlersAnalyze.fs:53` **do** read `prereqs.PlanDiagnostics`. So the two functions cleanly partition the stages, and emitting from both yields FR-002 (at `plan`) and FR-008 (at `tasks`/`analyze`) with no double-emit at `plan`.

**Key constraint**: `planPrerequisiteDiagnosticsTextSummaryAndFacts` currently takes only the upstream *facts*, not the upstream *texts*, and digests require text. It already reads `plan.md` itself via `snapshot path model`, so it can read `specPath workId` / `clarificationPath workId` / `checklistPath workId` from the same `model`. **No signature change, therefore no edit to `Prerequisites.fs`** — which #174 owns.

**Alternatives considered**: threading the texts through `PrerequisiteResolution`. Rejected: edits `Prerequisites.fs`, creating a real overlap with #174 for zero benefit.

## R3 — There is no unknown-flag rejection to hook into

**Decision**: `--accept-upstream` is a `plan`-read flag, inert elsewhere. Spec FR-012 amended accordingly.

**Rationale**: `Program.fs` builds one `CommandRequest` for every command from a flat `rest` argument list using `hasFlag`/`optionValue` (`Program.fs:19`, `:235-249`). `SurfaceUpdate = hasFlag "--update" rest` is set on *every* command's request; only `surface` reads it. There is no unknown-flag validator anywhere in `src/` — `unknownCommand` covers the verb, not its flags. Rejecting unknown flags would be a Tier 1 behavior change across all commands.

**Alternatives considered**: adding a per-command allowed-flag set. Rejected as out of scope; worth its own issue.

## R4 — Advisory precedent and the remediation pointer

**Decision**: Model FR-011's advisory on `agents.earlyStageGuidance` (`DiagnosticConstructors.fs:799-806`) — a `DiagnosticInfo`. Wire FR-010's remediation through `RemediationPointers.suffixFor`.

**Rationale**: `ReportAssembly.outcome` inspects only `DiagnosticError` and `DiagnosticWarning`; a `DiagnosticInfo` changes neither outcome nor exit code, satisfying FR-011's "adds a fact, not an outcome". `ViewGeneration.fs:274` and `HandlersAgents.fs:191` already project `DiagnosticInfo` as `"advisory"`. `commandDiagnostic` (`DiagnosticConstructors.fs:23`) funnels *every* constructor through `RemediationPointers.suffixFor id`, so appending the `fsgg-sdd plan --accept-upstream` pointer there is the single wiring point for FR-010, matching feature 078's design.

**Risk logged**: FR-011 adds a `DiagnosticInfo` to *every successful* `plan` report. Command diagnostics flow into `generatedViewPlan`, so a golden fixture that snapshots a plan-stage work model would churn. `tests/FS.GG.SDD.Commands.Tests/goldens/readiness/*` is inside **#174's declared touch-set**. If FR-011 churns any file outside this feature's touch-set, it is deferred to a follow-up issue rather than merged across the overlap — it is the spec's lowest-priority story (P3) and explicitly independently shippable.

## R5 — `sourceDigestsStale` semantics, preserved

**Decision**: Reuse `Foundation.sourceDigestsStale` (`:964`) unchanged; derive the *changed path list* with a parallel, sorted comparison.

**Rationale**: Its signature is `(string * string option) list -> Map<string,string> -> bool`. The `| _ -> false` arm means an **absent recorded digest is not stale** (FR-016) and a **recorded path with no current entry is not stale** (the missing-source edge case, already covered by `missing…Prerequisite`). Preserving it means no pre-existing plan becomes blocked on upgrade. It returns only `bool`, so FR-002's "name the changed sources, deterministically sorted" needs a sibling function returning the changed paths; it must apply the identical predicate so the two can never disagree.

**Alternatives considered**: changing `sourceDigestsStale` to return the path list. Rejected: it is called from the checklist path too (`Foundation.fs` is shared, and outside the touch-set); a new sibling in `ChecklistPlanAuthoring.fs` keeps the blast radius inside the declared paths.

## R6 — `signalsStaleView` is a substring test

**Finding (no action)**: `Diagnostics.signalsStaleView` (`Diagnostics.fs:48`) is `Id.IndexOf("stale", OrdinalIgnoreCase) >= 0`. Both the retired `stalePlanDecision` and the new `stalePlanSnapshot` match it, so the agent-refresh path's classification is unchanged by this feature. Noted so the coincidence is deliberate rather than accidental.

## R7 — Change tier

**Decision**: **Tier 1 (contracted change)** per the constitution's Change Classification.

**Rationale**: adds a command flag, adds a diagnostic id to the JSON contract, and changes `plan`'s exit code and write behavior on the stale path. Requires spec, plan, tasks, `.fsi` updates, tests, and docs. No persisted schema version is bumped and no cross-repo contract moves (FR-015): `plan.md` stays `AuthoredSource`, `CommandReport.schemaVersion` stays `1`. `ReportAssembly.ReportVersion` is a semantic version bumped for *additive report blocks*; this feature adds no block, so it stays `1.3.0`.
