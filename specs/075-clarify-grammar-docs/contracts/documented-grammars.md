# Contract: the grammars the documentation must match

This is the authoritative statement of *what the docs must say*, expressed so that each claim is
verifiable against the live parser. The contract is enforced by parser-validated example blocks
in `docs/reference/authoring-contracts.md` (run by an extended `AuthoringDocsContractTests`) and
by the skill drift/manifest guards. If any claim here diverges from the parser, the parser wins
and the docs are the defect.

## C1 — Decision-tag resolution (clarify)

The docs MUST assert, and a parser-validated example MUST demonstrate:

- A `clarifications.md` where `AMB-001` is resolved by
  `- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-001] [AC-001]: …` under `## Decisions`, with
  `## Remaining Ambiguity` carrying a `None.` disclaimer → parses with **zero blocking
  ambiguities**. (label: `clarify-decision:resolved`)
- The same AMB deferred via `- **DEC-002** [CQ-002] [AMB:AMB-002]: …` under
  `## Accepted Deferrals` → **zero blocking** (accepted deferral, not dropped).
  (label: `clarify-decision:deferred`)
- A `clarifications.md` that answers the question under `## Answers` but carries **no** decision
  line with the AMB id, and lists the AMB as a blocking bullet under `## Remaining Ambiguity` →
  **blocking ambiguity remains** (proves answers don't resolve). (label:
  `clarify-decision:answer-does-not-resolve`)
- A `clarifications.md` declaring the same `DEC-002` under both `## Decisions` and
  `## Accepted Deferrals` → **`duplicateClarificationId`**. (label: `clarify-dup:rejected`)

## C2 — Per-stage front matter (gating vs defaulted)

The docs MUST present a per-stage table matching `research.md` R1, and parser-validated example
blocks MUST demonstrate at least:

- A `clarify` front matter with only `schemaVersion, workId, stage, sourceSpec` (omitting
  `title/changeTier/status`) → **accepted** (defaults applied, no incomplete error). (label:
  `front-matter:clarify-minimal`)
- A `clarify` front matter missing `sourceSpec` → **`malformedClarificationFrontMatter`**
  (incomplete). (label: `front-matter:clarify-missing-required`)
- (Optional, if cheaply expressible) a `charter` front matter missing `status` →
  **`malformedCharterFrontMatter`**, demonstrating charter's stricter set.

The docs MUST state the closed `stage` vocabulary and that `changeTier`/`status` are free
strings (no example asserts a rejected `tier`/`status` value, because the parser enforces none —
the docs must not imply otherwise).

## C3 — Source Snapshot / sha256 correction

The docs MUST state: clarifications has no Source Snapshot/sha256; on checklist/plan the
`sha256:` is optional, format-checked (64 hex) only when present, used for staleness detection,
never required to author. No new example block is required (this is a correction of stage
attribution, and the existing checklist/plan examples already exercise the snapshot line); a
one-line factual statement suffices.

## C4 — Skill-body contract invariants (unchanged mechanism, must stay green)

- `.claude` body == embedded resource == `.codex` body (byte-identical).
- `.agents/skills/skill-manifest.json` per-skill `sha256` == canonical body digest, and the
  committed manifest == a fresh `registry skill-manifest --write`.

## Test-label additions (for `AuthoringDocsContractTests.fs`)

New info-string labels the extended test dispatches (final names may be adjusted to match the
existing dispatch style during implementation, but each MUST run its block through the live
parser and assert the stated outcome):

| Label | Parser entry | Asserted outcome |
|---|---|---|
| `clarify-decision:resolved` | `Clarification.parseClarificationFacts` | 0 blocking ambiguities |
| `clarify-decision:deferred` | `Clarification.parseClarificationFacts` | 0 blocking ambiguities (accepted deferral) |
| `clarify-decision:answer-does-not-resolve` | `Clarification.parseClarificationFacts` | ≥1 blocking ambiguity |
| `clarify-dup:rejected` | `Clarification.parseClarificationFacts` | `duplicateClarificationId` present |
| `front-matter:clarify-minimal` | `Clarification.parseClarificationFrontMatter` | Ok / no incomplete diagnostic |
| `front-matter:clarify-missing-required` | `Clarification.parseClarificationFrontMatter` | `malformedClarificationFrontMatter` |

> Verify the exact public parser entry points and diagnostic ids during implementation; the
> table names the behavior, and the parser is the authority.
