# Phase 0 Research: prerequisite combinator + handler shell

Three design questions the spec deliberately deferred to planning (spec
Assumptions: "the exact F# mechanism … are planning/implementation details").
Each is resolved below with the alternatives considered, because the naive
"ordered list of `WorkModel -> Diagnostic list` checks the engine folds" framing
in the refactor report (§5.1) does **not** type-check against the real cascade.

---

## R-1 — How to fold a cascade whose steps have *growing, heterogeneous* arity

**Problem.** The report imagines a uniform `WorkModel -> Diagnostic list` fold.
The real prerequisite helpers do not have a uniform signature — each consumes the
*parsed facts* of specific earlier stages and the fact types differ:

```fsharp
specificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model
  // -> Diagnostic list * string option * SpecificationSummary option * SpecificationFacts option
clarificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model
  // -> ... * ClarificationFacts option           (takes no prior facts)
checklistPrerequisiteDiagnosticsTextSummaryAndFacts workId specFacts clarificationFacts model
  // requires SpecificationFacts AND ClarificationFacts
planPrerequisiteDiagnosticsTextSummaryAndFacts workId specFacts clarificationFacts checklistFacts model
  // requires 3 prior facts
tasksPrerequisiteDiagnosticsTextSummaryAndFacts workId specFacts clarificationFacts checklistFacts planFacts model
  // requires 4 prior facts
```

A homogeneous `'a list` fold cannot express this (the element type changes per
step), and a generic short-circuit combinator over heterogeneous tuples would
need GADTs / SRTP that Principle IV forbids.

**Decision.** Extract a single **closed, hand-written resolver** —
`resolvePrerequisites workId model : PrerequisiteResolution` — that performs the
maximal spec→clarification→checklist→plan→tasks cascade exactly once, reproducing
today's `match …Facts with Some … -> compute | _ -> empty` short-circuit at each
step, and returns a record with one field group per stage (diagnostics, text,
summary, facts). The "combinator" is this resolver: the *ordering and
short-circuit policy live in one place*, and every handler reads the prefix it
needs from the record. The fact-threading stays explicit and typed (no generic
heterogeneous machinery), satisfying FR-001/FR-002/FR-003 and Principle IV.

**Why this is still "one combinator, not twelve".** The duplication the report
targets is the *repetition of the cascade across handlers*, not the five distinct
per-stage steps. analyze re-rolls a 5-step nested match
(`computeAnalyzePlan` lines 4008-4034); evidence re-rolls the same prefix **plus**
an analysis step — a 6-deep cascade that additionally threads `analysisText` /
`analysisFacts` (`computeEvidencePlan` lines 4625-4656); clarify/checklist/plan/
tasks re-roll prefixes of it. After this
change that cascade exists once in `resolvePrerequisites`.

**Alternatives considered.**
- *Generic ordered-list fold with existential facts* — rejected: needs
  type-level machinery (SRTP/boxing/`obj` casts) that violates Principle IV and
  re-introduces partiality.
- *Per-pair smart constructors only (no resolver record)* — rejected: removes the
  nested matches but leaves each handler re-sequencing the five calls, so the
  cascade ordering is still duplicated ~7×.
- *Lazy/`Option`-monad threading* — the cascade is already an `Option`-gated
  chain; wrapping it in a CE (Principle IV "non-trivial computation expressions"
  need justification) buys nothing over the explicit record and obscures the
  short-circuit. Rejected.

---

## R-2 — Where charter/specify and the cross-cutting handlers fit

**Problem.** Not every handler uses the spec→tasks cascade. `computeCharterPlan`
has *no* lifecycle prerequisite (it uses `charterDiagnosticsAndText`).
`computeSpecifyPlan` uses spec + a *charter* prerequisite
(`charterPrerequisiteDiagnosticsAndText`), not the cascade tail.
`computeAgentsPlan` / `computeRefreshPlan` are cross-cutting generators
(`nextLifecycleCommand = None`) with a different prerequisite shape and a
different return type and a different read-gating path in `plan` (lines 6752,
6774).

**Decision.**
- `resolvePrerequisites` covers the **monotonic lifecycle cascade** consumed by
  clarify, checklist, plan, tasks, analyze, evidence, verify, ship. Each takes a
  prefix (clarify = spec+clarification; … evidence/verify/ship = full chain).
- charter and specify keep their charter-specific prerequisite calls; they still
  use the **shell** (R-3) for guard/blocking/gating. specify may read `spec` from
  the resolver (its spec step is identical) while keeping its charter prereq
  separate.
- agents and refresh use the **shell** for guard/blocking/gating but **not** the
  lifecycle cascade (spec FR-005 + Edge Case + Assumption explicitly permit this).
  Their prerequisite parsing is left as-is.

This honors FR-005 ("all twelve route guard/blocking/gating through the shell")
while not forcing non-cascade handlers into the linear resolver, which would
change behavior.

**Alternative considered.** Forcing agents/refresh through the lifecycle resolver
— rejected: they have no spec→tasks dependency and doing so would parse artifacts
they do not need, changing their diagnostics (a behavior change forbidden by
FR-006).

---

## R-3 — The handler shell vs. the differing return arities

**Problem.** Handlers return tuples of different arity — `computeCharterPlan`
returns a 4-tuple `(diagnostics, specification, generatedViews, effects)`;
`computeEvidencePlan` returns a 10-tuple; the `plan` dispatch (lines 6701-6732)
destructures each shape and re-pads it to the uniform 12-slot lifecycle tuple. A
shell that returns one fixed tuple cannot serve all twelve.

**Decision.** `runHandler` owns only the parts that are *identical* across
handlers and returns the **common trailing pair plus the blocking flag**, leaving
each handler to assemble its own summaries tuple:

```fsharp
// shape (final signature pinned in data-model.md / contracts/run-handler.md)
runHandler model (fun workId ->
    // per-stage body computes:
    //   commandDiagnostics : Diagnostic list   (already includes prereq diags)
    //   generated          : GeneratedView * CommandEffect list   (view + its effects)
    //   writeEffects       : bool -> CommandEffect list            (hasBlocking -> writes)
    // returns the per-stage artifact summaries it alone knows about
    ... )
```

`runHandler` performs: the `match model.Request.WorkId` guard (returning the
caller-supplied `empty` value when `None`), `DiagnosticsModule.sort`, the single
`hasBlocking = diagnostics |> List.exists (fun d -> d.Severity = DiagnosticError)`, and the
final `if hasBlocking then [] else writeEffects @ generatedEffects` gate. It hands
`hasBlocking` to the `writeEffects` thunk so the per-stage artifact writes (e.g.
analyze's `WriteFile(analysisPath …)`, currently gated at line 4074) gate through
the same flag rather than recomputing it.

**Return-arity handling.** Two viable encodings; the implementer picks during
tasks, both keep `CommandWorkflow.fsi` and the `plan` dispatch semantics intact:
- **(a) Generic payload (preferred):** `runHandler` is generic over `'summaries`
  (the stage-specific middle of the tuple) and returns
  `('summaries * Diagnostic list * GeneratedView list * CommandEffect list)`; each
  handler maps that into its existing flat public tuple before returning, so the
  `plan` dispatch destructuring at lines 6704-6731 is unchanged.
- **(b) Shell returns only `(Diagnostic list * GeneratedView list * CommandEffect
  list)`** and the handler threads its summaries around the call. Slightly more
  per-handler glue, zero generics.

Either way the guard/sort/`hasBlocking`/gate exist **once** (SC-003) and no
handler's *public* return arity changes, so the dispatch and `.fsi` are untouched
(FR-007). Encoding (a) is preferred for least per-handler glue.

**Alternative considered.** A 12-arity uniform shell return — rejected: it would
force every handler onto the full lifecycle tuple and push the padding the
`plan` dispatch already does into the shell, with no readability gain.

---

## Cross-cutting notes

- **Determinism (SC-004).** All diagnostic ordering is funnelled through the same
  `DiagnosticsModule.sort` calls, in the same places, over the same lists
  (resolver concatenation order mirrors the current `@`-chains exactly). No view
  serialization changes, so JSON output is byte-identical by construction.
- **Warnings (FR-010/SC-005).** No `JsonElement`/`GetString` access moves (those
  live in the `Artifacts` parsers, untouched here), so FS3261 counts in this file
  are unaffected except by line relocation; FS0025 is already 0 (R4) and no new
  `match` over a sum type is introduced without a total set of arms.
- **Regression gate.** Behavior-preserving, so the existing **438** tests
  (post-R4 baseline) are the whole gate; no fixtures, no new assertions (R-1/R-2
  preserve each diagnostic and effect verbatim).
