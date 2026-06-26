# Phase 1 Data Model: prerequisite combinator + handler shell

All types below are **internal** to `module CommandWorkflow` (not in
`CommandWorkflow.fsi`). They model the refactor's shared structure; no
artifact-schema or public type changes. Field/type names are indicative — the
implementer may rename, but the shape and semantics are the contract.

## Entity: `PrerequisiteResolution`

The single value produced by `resolvePrerequisites`, holding the result of the
monotonic lifecycle cascade (specification → clarification → checklist → plan →
tasks) computed once with short-circuiting. Each per-stage field group mirrors
the tuple the corresponding `*PrerequisiteDiagnosticsTextSummaryAndFacts` helper
already returns today.

| Field | Type | Meaning |
|---|---|---|
| `SpecificationDiagnostics` | `Diagnostic list` | diagnostics from the spec prereq check |
| `SpecificationText` | `string option` | authored spec text (None ⇒ absent/blocked) |
| `Specification` | `SpecificationSummary option` | parsed spec summary |
| `SpecificationFacts` | `SpecificationFacts option` | parsed spec facts (threaded forward) |
| `ClarificationDiagnostics` / `…Text` / `Clarification` / `…Facts` | (as above for clarification) | |
| `ChecklistDiagnostics` / `…Text` / `Checklist` / `…Facts` | (as above for checklist) | |
| `PlanDiagnostics` / `…Text` / `Plan` / `…Facts` | (as above for plan) | |
| `TaskDiagnostics` / `…Text` / `Tasks` / `…Facts` | (as above for tasks) | |

**Invariants (preserved from today's cascade).**
- **Monotonic short-circuit:** if stage *N*'s `…Facts` is `None`, every stage
  *>N* has empty diagnostics, `None` text/summary/facts. (Mirrors the
  `match …Facts with Some… -> compute | _ -> [], None, None, None` arms.)
- **Fact threading:** stage *N*'s computation receives exactly the prior
  `…Facts` it consumes today (checklist ⇐ spec+clarification; plan ⇐ +checklist;
  tasks ⇐ +plan). No fact is recomputed.
- **Clarification is unconditional:** like today, the clarification step runs
  regardless of spec facts (it is called directly, not behind a `match`).

**Diagnostic concatenation order.** When a handler assembles its
`commandDiagnostics`, it concatenates the resolver's per-stage diagnostic lists
in the **same order** the handler does today
(`projectDiagnostics @ duplicateDiagnostics @ specificationDiagnostics @
clarificationDiagnostics @ …`) before `DiagnosticsModule.sort`. The resolver does
**not** pre-sort or pre-concatenate across stages — ordering stays at the call
site so sort input is byte-identical (SC-004).

## Entity: handler shell (`runHandler`)

A higher-order function owning the parts identical across all twelve handlers.
Not a data type — a function; its signature is the contract (see
`contracts/run-handler.md`). Conceptual inputs/outputs:

| Element | Role |
|---|---|
| `model : CommandModel` | the planning model (carries `Request.WorkId`) |
| `empty : 'summaries` | the value returned for the missing-`WorkId` guard arm |
| body `workId -> 'summaries * Diagnostic list * (GeneratedView list * CommandEffect list) * (bool -> CommandEffect list)` | per-stage logic: summaries, pre-sort command+generated diagnostics, the generated view(s)+their effects, and a `hasBlocking -> write-effects` thunk |
| **output** | `'summaries * Diagnostic list * GeneratedView list * CommandEffect list` (sorted diagnostics; effects gated) |

**Behavior (single-sourced, replacing the per-handler copies).**
1. **Guard:** `match model.Request.WorkId with | None -> (empty, model.Diagnostics, [], []) | Some workId -> …`.
   (The `None` arm reproduces each handler's current default, e.g.
   `model.Diagnostics, None, [], []` for charter — `empty` carries the
   summaries portion.)
2. **Sort:** `let diagnostics = commandDiagnostics |> DiagnosticsModule.sort`
   over the body-supplied list (which already concatenated command+generated in
   call-site order).
3. **Blocking:** `let hasBlocking = diagnostics |> List.exists (fun d -> d.Severity = DiagnosticSeverity.DiagnosticError)` — defined **once**.
4. **Gate:** `let effects = if hasBlocking then [] else (writeEffects hasBlocking) @ generatedEffects`.

`'summaries` is the stage-specific middle of each handler's public tuple
(`None` for charter; `(specification)` for specify; … the 8-summary group for
verify). Each handler maps the shell's output into its existing flat public tuple
so the `plan` dispatch (CommandWorkflow.fs:6701-6732) is unchanged (research R-3,
encoding (a)).

## Relationship to existing types (all unchanged)

- `Diagnostic`, `DiagnosticSeverity`, `GeneratedView`, `CommandEffect`,
  `CommandModel`, `CommandRequest` — from `CommandTypes`; untouched.
- `SpecificationFacts`, `ClarificationFacts`, `ChecklistFacts`, `PlanFacts`,
  `TaskFacts`, `*Summary` — from `FS.GG.SDD.Artifacts`; untouched.
- The `*PrerequisiteDiagnosticsTextSummaryAndFacts` helpers — **reused as-is** by
  `resolvePrerequisites`; their signatures do not change.

## State / transitions

None. Both helpers are pure functions over an immutable `CommandModel`; there is
no state machine and no I/O. The MVU `init`/`update`/effect-interpreter boundary
is unchanged (Constitution Principle V).
