# Phase 1 Data Model: Early-Stage Authoring Seeds

**Feature**: 089-early-stage-authoring-seeds

No persisted schema changes. This document records the two authored-artifact shapes the feature
produces and the one internal type change that carries them.

---

## 1. Specification Seed (authored surface, `work/<id>/spec.md`)

Rendered by `specificationTemplate` when the invocation supplies no story and/or no acceptance
scenario. Not a type — a derivation over two facts.

| Field | Source | Always present? | Notes |
|---|---|---|---|
| `userValue` | `intent.UserValue` | **Yes** | Required intent fact; `specify` blocks without it. The load-bearing input. |
| `title` | `requestTitle request workId` | Yes | This invocation's `--title`, else the humanized work id (**not** the charter title — research D1). Weak secondary. |

**Derivation** (deterministic, total):

```
capability(userValue) = userValue |> trimEnd '.' |> decapitalizeFirst |> neutralizeIds
displayTitle(title)   = title |> neutralizeIds
```

- `decapitalizeFirst s` lowercases `s[0]` **iff** `s.Length > 1 && isUpper s[0] && isLower s[1]` —
  so `"Let a player…"` → `"let a player…"` while `"MP4 export…"` is left alone.
- `neutralizeIds s` rewrites any `[A-Z]{2,3}-\d{3,}` token to the same characters with the hyphen
  replaced by a space (`FR-002` → `FR 002`), so interpolated author text cannot manufacture a
  spurious cross-reference into a seeded id line (FR-017). Applied **only** to the seeded lines,
  never to author-supplied scope/requirement/non-goal text (whose current handling is unchanged).

**Invariants**
- The `US-001`/`AC-001`/`FR-001` ids and every cross-reference between them are unchanged from the
  previous seed (FR-004). Only the prose after the `:` moves.
- The seeded prose contains no SDD process vocabulary (FR-002/FR-003).
- Substituted only when the corresponding intent list is empty (FR-005).

See `contracts/specification-seed.md` for the exact line grammar.

---

## 2. Blocked Clarification Skeleton (authored surface, `work/<id>/clarifications.md`)

Rendered by `clarificationTemplate` and written by the seed effect when `clarify` blocks and no
`clarifications.md` exists.

### Derived state

Let `A` = `specFacts.AmbiguityIds` (ordered, ids only — the ambiguity prose is not parsed; research
D5) and `answers` = the planned answers for this run.

```
resolved(a)   = ∃ answer ∈ answers. answer.AmbiguityId = a ∧ answer.Kind ∈ { decision, acceptedDeferral }
unresolved    = [ a ∈ A | ¬resolved(a) ]                       -- order preserved
status        = if unresolved ≠ []  then "needsAnswers" else "clarified"
questionId(a) = knownQuestionIdForAmbiguity index existingQuestions a   -- CQ-00(index+1) when fresh
```

### Section bodies

| Section | Body when the skeleton is seeded (zero answers) |
|---|---|
| `Clarification Questions` | one `renderQuestionLine (CQ-00n) (AMB-00n)` per `a ∈ A` (unchanged) |
| `Answers` | `No clarification answers recorded.` |
| `Decisions` | `No concrete decisions recorded.` |
| `Accepted Deferrals` | `No accepted deferrals recorded.` |
| `Remaining Ambiguity` | one blocking line per `a ∈ unresolved` (**new**) |

**Remaining Ambiguity line** for `a ∈ unresolved`:
- if a `stillOpen` answer exists for `a` → today's `renderRemainingLine` text (**unchanged**, so no
  existing golden moves);
- else → `- {a} [{questionId(a)}] blocking: Unanswered. Resolve source ambiguity {a} before
  checklist.` (**new**, generic by necessity — research D5).

When `unresolved = []`, the body is the `No blocking ambiguity remains.` sentinel (unchanged).

> ⚠️ **The explanation prose is machine input.** `parseRemainingAmbiguity` classifies a line by
> scanning it for `accepted deferral`/`defer` (⇒ `acceptedDeferral`) and `non-blocking`
> (⇒ `nonBlocking`). An explanation that *names* those resolutions as options parses as one of them,
> zeroing `BlockingAmbiguityCount` and letting `checklist` pass with the ambiguity unanswered
> (FR-021, research D9 — found by running it). Keep the text free of `defer` and `non-blocking`.

### State transitions

```
(absent)
   │  clarify blocks on unanswered ambiguities          FR-006  ──► seeded skeleton
   │                                                              status: needsAnswers
   │                                                              |unresolved| lines, all blocking
   │
   ├─ clarify --input answering some ambiguities        FR-018  ──► decisions appended,
   │                                                              answered lines RETIRED,
   │                                                              placeholders RETIRED (FR-019),
   │                                                              still blocked on the rest
   │
   ├─ clarify --input answering all ambiguities         FR-018  ──► Remaining Ambiguity = sentinel,
   │                                                              blockingAmbiguityCount = 0,
   │                                                              next stage: checklist
   │
   ├─ clarify re-run, no new answers                    FR-013  ──► no duplicate CQ ids, still blocked
   │
   └─ operator edits the file                           FR-014  ──► content preserved; never re-seeded
                                                        FR-011     (seedText is None once the file exists)
```

**Invariants**
- The skeleton never asserts `status: clarified` while any declared ambiguity is unresolved (FR-007).
- The skeleton never asserts `No blocking ambiguity remains.` while the command blocks (FR-008).
- `parseClarificationFacts` succeeds on the skeleton, and `BlockingAmbiguityCount = |unresolved|`
  (FR-009). Guaranteed by `parseRemainingAmbiguity`: a line naming an `AMB`/`CQ` id, not matching
  `isNoOutstandingSentinel`, and containing neither `non-blocking` nor `deferred`, classifies as
  `blocking`.
- Seeded only when the file is absent, so no existing artifact can be clobbered (FR-011/FR-014).

See `contracts/clarification-skeleton.md` for the exact document shape and the retirement rules.

---

## 3. `blockedSeedEffects` — the H-4 carve-out channel (internal)

`runHandler`'s body continuation currently returns a 4-tuple. It gains a fifth element.

```fsharp
// before
'summaries * GeneratedViewState list * CommandEffect list * CommandEffect list
//            views                     writeEffects        generatedEffects

// after
'summaries * GeneratedViewState list * CommandEffect list * CommandEffect list * CommandEffect list
//            views                     writeEffects        generatedEffects     blockedSeedEffects
```

Consumed at exactly one place (`Prerequisites.fs`, the H-4 gate):

```fsharp
let effects =
    if hasBlocking then blockedSeedEffects   // H-4 carve-out (feature 089): seed-on-blocked
    else writeEffects @ generatedEffects
```

| Property | Value |
|---|---|
| Populated by | `computeClarifyPlan` only. Every other handler returns `[]` via the `runHandler` wrapper. |
| Contents | exactly `[ CreateDirectory "work/<id>"; WriteFile("work/<id>/clarifications.md", skeleton, AuthoredSource) ]` |
| Excludes | `generatedEffects` — a blocked `clarify` still writes **no** `readiness/<id>/work-model.json` |
| Write kind | `AuthoredSource`, the same kind `clarify` already uses. No `changedArtifacts` vocabulary change (FR-016; research D7). |

**Report impact**: `ReportAssembly.outcome` is diagnostics-first, so a blocking diagnostic still
yields `CommandOutcome.Blocked` regardless of the new change entry. The only report delta is
`changedArtifacts: 0 → 1` (FR-010; research D8).

---

## 4. `clarificationDiagnosticsTextAndSummary` result (internal)

Gains a fourth result so the skeleton reaches the effect channel without disturbing the text that
feeds `generatedViewPlan` (research D8).

```fsharp
// before: Diagnostic list * string option * ClarificationSummary option
// after:  Diagnostic list * string option * ClarificationSummary option * string option
//                           ^ text (unchanged, feeds generatedViewPlan)   ^ seedText
```

| Situation | `text` | `seedText` |
|---|---|---|
| file absent, answers incomplete (the §WD5 case) | `None` *(unchanged)* | `Some skeleton` |
| file absent, answers complete but an ambiguity stays `stillOpen` | `Some text` *(unchanged)* | `Some text` |
| file absent, all ambiguities resolved (happy path) | `Some text` | `None` — normal write path |
| file absent, rendered text fails to parse | `Some text` *(unchanged)* | `None` — never seed unparseable text |
| file present (any state, including unsafe-overwrite / malformed / id mismatch) | *(unchanged)* | `None` — FR-011 |
| source specification fails to parse | `None` | `None` — FR-012, no questions to derive |
