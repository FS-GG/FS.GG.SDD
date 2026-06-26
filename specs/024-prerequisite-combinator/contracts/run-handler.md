# Internal contract: `runHandler` (handler shell)

**Scope:** internal to `module CommandWorkflow` (no `.fsi` entry, no public
surface). Behavioral contract verified by the existing 438-test suite.

## Signature (indicative, encoding (a) from research R-3)

```fsharp
val runHandler :
    model:CommandModel ->
    empty:'summaries ->
    body:(WorkId ->
            'summaries                       // stage-specific summaries
          * Diagnostic list                  // command+generated diagnostics, pre-sort, call-site order
          * GeneratedView list               // generated views to surface
          * CommandEffect list               // generated-view effects
          * (bool -> CommandEffect list)) -> // hasBlocking -> write effects
    'summaries * Diagnostic list * GeneratedView list * CommandEffect list
```

The handler then maps `('summaries * diagnostics * views * effects)` into its
existing flat public return tuple, so the `plan` dispatch
(`CommandWorkflow.fs:6701-6732`) and `CommandWorkflow.fsi` are unchanged.

## Obligations

- **H-1 (guard once).** The `match model.Request.WorkId with | None -> … | Some
  workId -> …` guard is implemented **once**. On `None`, return
  `(empty, model.Diagnostics, [], [])`, reproducing each handler's current
  default arm (e.g. charter's `model.Diagnostics, None, [], []`). (FR-004)
- **H-2 (blocking once).** `hasBlocking = diagnostics |> List.exists (fun d ->
  d.Severity = DiagnosticSeverity.DiagnosticError)` appears **exactly once** in
  the file. A source search finds it a single time, down from ~10+. (SC-003)
- **H-3 (sort once).** The final `DiagnosticsModule.sort` over the combined
  command+generated diagnostics is applied in the shell, over the body-supplied
  list, preserving today's input ordering. (FR-004, SC-004)
- **H-4 (gate parity).** Effects are emitted iff not blocking:
  `if hasBlocking then [] else (writeEffects hasBlocking) @ generatedEffects`.
  The `hasBlocking` flag is passed to `writeEffects` so per-artifact writes (e.g.
  analyze's `WriteFile(analysisPath …)`) gate through the same flag instead of
  recomputing it. Output effects are identical to today for every input. (FR-006)
- **H-5 (universal use).** All twelve handlers route guard/sort/blocking/gate
  through `runHandler`; none keeps a private copy of that boilerplate — including
  the cross-cutting `computeAgentsPlan`/`computeRefreshPlan`, whose `'summaries`
  is their respective non-lifecycle payload and whose body supplies their own
  (non-cascade) prerequisite diagnostics. (FR-005)
- **H-6 (no surface change).** Each handler's *public* return arity is unchanged;
  `'summaries` is only the middle of that tuple. `CommandWorkflow.fsi`
  (`init`/`update`) and the surface-area baseline are byte-identical. (FR-007)

## Non-obligations

- The shell does **not** own prerequisite resolution (that is
  `resolvePrerequisites`) nor artifact/view construction (stage-specific, supplied
  by `body`). It owns only the four invariant steps H-1…H-4.
- Encoding (b) (shell returns only `Diagnostic list * GeneratedView list *
  CommandEffect list`, handler threads `'summaries` itself) is an acceptable
  substitute if generic value-generalization over `'summaries` proves awkward;
  obligations H-1…H-6 bind either encoding.

## Verification

- All 438 tests pass unchanged (SC-001); the blocked-effects-suppressed paths
  (e.g. analyze with a blocking diagnostic emits no write/generated effects) keep
  their current behavior (FR-006).
- Source inspection: one `hasBlocking` definition, one guard, one sort, one gate
  (SC-003); handler section LOC net-shrinks (SC-006).
