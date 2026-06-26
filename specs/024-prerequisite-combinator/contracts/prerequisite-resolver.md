# Internal contract: `resolvePrerequisites`

**Scope:** internal to `module CommandWorkflow` (no `.fsi` entry, no public
surface). This is a *behavioral* contract verified by the existing 438-test
suite, not a published API.

## Signature (indicative)

```fsharp
val resolvePrerequisites : workId:WorkId -> model:CommandModel -> PrerequisiteResolution
```

`PrerequisiteResolution` is the internal record in `data-model.md` (one
diagnostics/text/summary/facts group per lifecycle stage: specification,
clarification, checklist, plan, tasks).

## Obligations

- **C-1 (single definition).** The spec→clarification→checklist→plan→tasks
  cascade — the ordered sequence of `*PrerequisiteDiagnosticsTextSummaryAndFacts`
  calls with their `match …Facts with Some… | _ -> empty` short-circuit — is
  defined **exactly once**, here. No `compute*Plan` handler may contain a
  hand-rolled nested prerequisite `match`. (FR-001, SC-002)
- **C-2 (short-circuit parity).** For every input, the set, identity, severity,
  and per-stage grouping of prerequisite diagnostics equals what the handler
  produced before: when stage *N*'s facts are `None`, stages *>N* contribute
  empty diagnostics and `None` results. (FR-002)
- **C-3 (fact threading parity).** Each stage receives exactly the prior facts it
  consumes today (checklist ⇐ spec+clarification; plan ⇐ +checklist; tasks ⇐
  +plan); the threaded facts are identical objects to today's. (FR-003)
- **C-4 (no extra work).** The resolver parses no artifact a handler did not
  already parse for the prefix it requests. A handler needing only spec+clarif
  (e.g. clarify) must not cause checklist/plan/tasks parsing with observable
  diagnostics. *Implementation note:* either compute lazily per field or expose
  per-prefix entry points; the binding requirement is that no **observable**
  diagnostic or effect differs from today, not a particular laziness strategy.
- **C-5 (ordering neutrality).** The resolver returns per-stage diagnostic lists
  **unsorted and unconcatenated across stages**; concatenation order and the
  single `DiagnosticsModule.sort` stay at the handler call site, byte-identical to
  today. (SC-004)

## Consumers

| Handler | Prefix consumed |
|---|---|
| `computeClarifyPlan` | specification, clarification |
| `computeChecklistPlan` | + checklist |
| `computePlanPlan` | + plan |
| `computeTasksPlan` | + tasks |
| `computeAnalyzePlan` | full chain (spec…tasks) |
| `computeEvidencePlan` | full chain |
| `computeVerifyPlan` | full chain |
| `computeShipPlan` | full chain |
| `computeCharterPlan` | none (charter prereq, not cascade) — does not call resolver |
| `computeSpecifyPlan` | specification only (+ its own charter prereq) |
| `computeAgentsPlan` / `computeRefreshPlan` | none (cross-cutting; not lifecycle) — do not call resolver |

## Verification

- The per-command handler test suites (charter…ship) pass unchanged (SC-001).
- Source inspection: zero nested prerequisite `match` blocks remain in
  `compute*Plan`; the 5-/6-deep cascades at the old `computeAnalyzePlan` /
  `computeEvidencePlan` sites are gone (SC-002).
