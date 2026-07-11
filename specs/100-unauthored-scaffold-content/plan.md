# Implementation Plan: An Unauthored Decision Is a Missing Decision

**Spec**: `specs/100-unauthored-scaffold-content/spec.md`

**Tracks**: FS.GG.SDD#351 (child of `.github` epic #417, *the SDD lifecycle fails open*)

**Tier**: **Tier 1** — new blocking diagnostic id (`unauthoredScaffoldContent`), new blocking
behaviour in the `analyze` command contract. No schema change, no new public type.

## Summary

`plan` writes a decision per requirement carrying that requirement's own refs by construction, so the
traceability chain closes with zero human authorship. Make `analyze` refuse a plan that still holds
the prose the scaffold wrote.

The whole design is one sentence: **the detector is the generator.** `plannedPlanEntries` is pure in
the four facts `analyze` already holds, so re-derive exactly what `plan` would have written and ask
whether the authored plan still contains it. Nothing else is needed — and specifically, no marker.

## Technical Context

- F# / net10.0, Elmish-MVU command workflow, pure `update` + edge interpreter.
- `analyze` already resolves every authored artifact through `resolvePrerequisites`, so the detector
  needs no new read, no new effect, and no new I/O.

## Constitution Check

| Principle | How this feature satisfies it |
|---|---|
| I. Spec → `.fsi` → tests → impl | This spec; the failure-leg tests (`UnauthoredScaffoldTests`); then the rule. No `.fsi` surface changes — the rule is `internal`. |
| II. Structured artifacts are the contract | No schema change. The gate reads `plan.md` text that already exists and adds no field to it. |
| III. Visibility lives in `.fsi` | Nothing new is exported. `unauthoredPlanLines` lives beside the generator it re-derives, inside `module internal ChecklistPlanAuthoring`. |
| IV. Idiomatic simplicity | No marker, no `status: unauthored`, no schema field, no new effect case. One pure function and one diagnostic. |
| V. MVU is the boundary for I/O | The detector is pure in `(workId, specFacts, clarificationFacts, checklistFacts, planText)`. No `System.IO` anywhere near it. |
| VI. Test evidence is mandatory | Per epic #266 — *an untested failure leg is how this class survives* — the refusal is asserted on the **diagnostic id**, not an exit code, and the full "cannot reach ship" leg is committed. |
| VIII. Observability / safe failure | The diagnostic names every unauthored entry by the id the plan carries, so the author knows what to go and write. |

## Design Detail

### Why no marker

A `TODO` or `status: unauthored` field is a thing an author can delete without authoring anything —
it moves the gate from "did you think?" to "did you remove the sticker?". Re-deriving the scaffold
needs no cooperation from the author at all, and the reference **is** the scaffold's own output, so
the rule cannot drift from the thing it is detecting.

### Why `analyze`, and only `analyze`

It is the last gate before implementation and it already holds every authored artifact. One
`DiagnosticError` there means no `analysis.json` is written (H-4: no handler writes on a blocking
run), so `evidence` refuses on its missing prerequisite (`evidence.analysisNotReady`) and `verify`
and `ship` never get a verdict to aggregate. The acceptance criterion — *a lifecycle on untouched
scaffold cannot reach `shipReady`* — is therefore met at **one** point rather than re-litigated at
four.

### Why the comparison ignores the entry id

This is the subtle half, and getting it wrong fails open silently.

We re-derive from a blank slate (`existingFacts = None`), so our ids always count from `PD-001`.
`plan` assigns its ids **incrementally** — `nextScopedIndex` over the plan that already exists, and
it appends entries only for sources it has not already covered. The two agree only while the plan was
written in a single pass. Insert a requirement *above* an existing one and re-run `plan`, and the
plan holds `PD-002 [FR-001] …` where a fresh derivation produces `PD-001 [FR-001] …`.

Compare whole lines and **nothing** matches: no diagnostic, and a plan that is scaffold from top to
bottom ships green. So the gate compares the line **minus its id** — the refs and the prose, which
are what the tool authored — and reports the id the **plan** carries, so the diagnostic names an
entry the author can actually find.

### Conservative by construction

A line the author has touched at all no longer matches, so the gate fires only on prose the tool
wrote and a human never read. A partially-authored plan blocks on exactly the entries not yet
reached. False negatives (an author who retypes the scaffold's sentence verbatim) are accepted; false
positives are not.

## Fixture Consequence

The gate is a real behaviour change, so every fixture that drove `analyze` over scaffold output must
now author its plan. `authorPlanProse` does it the way a human would: keep the id, the refs, and the
kind token — the machine contract the later stages resolve against — and replace only the prose,
which is the part that needs judgement.

`ValidationRunner` needs the same treatment, and it is the sharp edge: without it the harness **does
not fail — it *skips*** the six cells that need an analyzed-or-later project. Coverage silently going
missing rather than going red.
