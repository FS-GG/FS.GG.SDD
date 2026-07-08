# Contract: Blocked-clarify skeleton and ambiguity retirement

**Feature**: 089 · **Artifact**: `work/<id>/clarifications.md` (authored surface) · **Stage**: `clarify`

## When the skeleton is written

Exactly when **all** of the following hold (FR-006, FR-011, FR-012):

1. the source `spec.md` parsed successfully (so `AMB-###` ids and `CQ-###` ids are derivable);
2. no `work/<id>/clarifications.md` exists;
3. the run is **blocked** — at least one declared ambiguity has no answer, or an answer is `stillOpen`;
4. the rendered skeleton itself parses under `parseClarificationFacts`.

The write is `WriteFile("work/<id>/clarifications.md", skeleton, AuthoredSource)`, carried on the
`blockedSeedEffects` channel so it survives the H-4 "blocked ⇒ zero writes" gate. Nothing else rides
that channel: a blocked `clarify` still writes **no** `readiness/<id>/work-model.json`.

## What does *not* change

| Fact | Before | After |
|---|---|---|
| `outcome` | `blocked` | `blocked` |
| exit code | unchanged | unchanged |
| diagnostics | `missingClarificationAnswer` / `unresolvedBlockingAmbiguity` | identical |
| `changedArtifacts` | `0` | **`1`** ← the only report delta |
| `work-model.json` | not written | not written |

## Skeleton shape (zero answers, two declared ambiguities)

```markdown
---
schemaVersion: 1
workId: demo
title: Demo
stage: clarify
changeTier: tier1
status: needsAnswers
sourceSpec: work/demo/spec.md
publicOrToolFacingImpact: true
---

# Demo Clarifications

## Source Specification
- work/demo/spec.md

## Clarification Questions
- CQ-001 [AMB:AMB-001] blocking open: Resolve source ambiguity AMB-001 before checklist.
- CQ-002 [AMB:AMB-002] blocking open: Resolve source ambiguity AMB-002 before checklist.

## Answers
No clarification answers recorded.

## Decisions
No concrete decisions recorded.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
- AMB-001 [CQ-001] blocking: Unanswered. Resolve source ambiguity AMB-001 before checklist.
- AMB-002 [CQ-002] blocking: Unanswered. Resolve source ambiguity AMB-002 before checklist.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work demo`.
```

## Rules

| # | Rule |
|---|---|
| K1 | `status` is `needsAnswers` iff any declared ambiguity carries no concrete decision and no accepted deferral; otherwise `clarified` (FR-007). A `stillOpen` answer is *not* a resolution. |
| K2 | Remaining Ambiguity holds one `blocking` line per unresolved declared ambiguity, in declaration order (FR-008). It holds the `No blocking ambiguity remains.` sentinel iff none is unresolved. |
| K3 | The skeleton never carries both `status: clarified` and a blocking Remaining Ambiguity line, and never carries `status: needsAnswers` with the sentinel. |
| K4 | A `stillOpen` answer's Remaining Ambiguity line keeps today's `renderRemainingLine` text. Only an ambiguity with **no** answer gets the generic `Unanswered. Resolve source ambiguity … before checklist.` explanation. The new rendering is a strict superset of the old (research D5). |
| K5 | `parseClarificationFacts skeleton` succeeds, and `BlockingAmbiguityCount` equals the number of unresolved declared ambiguities (FR-009). |
| K6 | One `CQ-00n` per declared ambiguity `AMB-00n`, tagged `[AMB:AMB-00n]`, matching the ids the command derived (FR-009). |
| K7 | Written only when the file is absent, so no existing artifact is clobbered — including files with the unsafe-overwrite marker, malformed front matter, or a mismatched work id (FR-011, FR-014). |
| K8 | Byte-identical across repeated blocked runs on unchanged inputs (FR-015). |
| K9 | **The explanation prose is machine input.** `parseRemainingAmbiguity` classifies a line by scanning it for `accepted deferral`/`defer` (⇒ `acceptedDeferral`) and `non-blocking` (⇒ `nonBlocking`). Any generated sentence placed under Remaining Ambiguity MUST contain neither `defer` nor `non-blocking`, or it parses as a resolution and the ambiguity silently stops blocking (FR-021, research D9). |
| K10 | `checklist` run against a raw skeleton **blocks** (rc=1, `unresolvedBlockingAmbiguity`). This is the observable consequence of K5 + K9 and is the sharpest regression test for both. |

## Retirement rules (applied on the existing-file path)

Both run **after** answers are appended, on the proposed text.

### R1 — Retire a resolved ambiguity's line (FR-018)

> Remove each Remaining Ambiguity line whose `AMB-###` id now carries a concrete decision **or** an
> accepted deferral. If the section is left with no content line, insert the sentinel
> `No blocking ambiguity remains.`

- Lines for still-unresolved ambiguities are left untouched, including operator-authored prose (FR-014).
- The `No blocking ambiguity remains.` sentinel is never treated as a removable line.
- `isNoOutstandingSentinel` already exempts the sentinel from `BlockingAmbiguityCount`.

**Without R1 the skeleton is unresolvable.** Verified against the pre-change CLI: a skeleton plus
`clarify --input` answering every ambiguity yields

```
outcome: succeeded        blockingAmbiguities: 2
```

and then `checklist` → `outcome: blocked`, `why: Blocking ambiguity remains unresolved…`, rc=1.

### R2 — Retire an empty-state placeholder (FR-019)

> When a section holds at least one real content line, remove its empty-state placeholder.

Applies to exactly these placeholder strings:

- `No clarification questions recorded.`
- `No clarification answers recorded.`
- `No concrete decisions recorded.`
- `No accepted deferrals recorded.`

`No blocking ambiguity remains.` is **not** a placeholder — it is a meaningful sentinel and is
governed by R1.

Without R2, the first `clarify --input` over a skeleton leaves:

```markdown
## Decisions
No concrete decisions recorded.

- DEC-001 [CQ-001] [AMB:AMB-001]: Use the MP4 container
```

### R3 — Retire a stale `status` (FR-020)

> When nothing remains blocking, rewrite `status: needsAnswers` to `status: clarified`.

Scoped so the command corrects only its **own** bookkeeping:

- only the literal value the tool writes (`needsAnswers`) is rewritten — an operator-chosen status is
  preserved (FR-014);
- only inside the leading `---` … `---` front-matter block; a `status:` line in the document body is
  never touched.

Without R3 a fully answered artifact reads `status: needsAnswers` beside
`No blocking ambiguity remains.` — the same self-contradiction K3 forbids in the skeleton. No
consumer reads clarification `status`, so this is a truthfulness rule, not a gate.

## Behavior this contract does NOT change

A **partially** answered `clarify` (some declared ambiguities answered, others not) blocks and
persists **nothing** — `changedArtifacts: 0`, artifact byte-identical. Pre-existing, correct (it never
half-writes an operator's file), and unchanged here. Answer every declared ambiguity in one
invocation, or edit the skeleton directly (research D10).

## End-to-end flow this contract guarantees

```
fsgg-sdd clarify --work demo                    → blocked, writes skeleton, changedArtifacts: 1
fsgg-sdd checklist --work demo                  → blocked (K10): the skeleton's entries really block
  (operator reads the skeleton, answers via --input or by editing)
fsgg-sdd clarify --work demo --input "AMB-001: … / AMB-002: …"
                                                → decisions recorded, lines retired,
                                                  status: clarified, blockingAmbiguities: 0
fsgg-sdd checklist --work demo                  → succeeded, next: fsgg-sdd plan
```
