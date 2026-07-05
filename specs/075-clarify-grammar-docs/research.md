# Phase 0 Research: Document the clarify decision-tag grammar and per-stage front-matter

All findings are grounded in the live parser/validation source (read-only). Where the
TD1 feedback's premise is inaccurate, the correction is recorded here and folded back
into the spec FRs. The parser is the source of truth; the documentation must match it.

## R1 — Per-stage front-matter required fields (the "incomplete" gate)

**Decision.** Document, per stage, the fields that *gate* parsing versus fields the template
includes but the parser *defaults*. Do not repeat the feedback's flat 7-field list as "required."

**Findings (source of truth):**

| Stage | Artifact | REQUIRED (absence → incomplete/malformed) | Defaulted (present in template, not gating) | Incomplete diagnostic |
|---|---|---|---|---|
| charter | `charter.md` | `schemaVersion, workId, title, stage, changeTier, status` (all six) | — (strict) | `malformedCharterFrontMatter` |
| specify | `spec.md` | `schemaVersion, workId, stage` | `title`→workId, `changeTier`→`tier1`, `status`→`draft`, `publicOrToolFacingImpact` | `malformedSpecificationFrontMatter` |
| clarify | `clarifications.md` | `schemaVersion, workId, stage, sourceSpec` | `title`, `changeTier`→`tier1`, `status`→`needsAnswers`, `publicOrToolFacingImpact` | `malformedClarificationFrontMatter` |
| checklist | `checklist.md` | `schemaVersion, workId, stage, sourceSpec, sourceClarifications` | `title`, `changeTier`, `status`→`needsReview`, … | `malformedChecklistFrontMatter` |
| plan | `plan.md` | `schemaVersion, workId, stage, sourceSpec, sourceClarifications, sourceChecklist` | `title`, `changeTier`, `status`→`planned`, … | `malformedPlanFrontMatter` |
| tasks | `tasks.yml` (whole-doc, no `---`) | `schemaVersion` (workId derivable) | `work.*`, `stage`→`Tasks`, `status`→`tasksReady`, source paths | `malformedTasksArtifact` |
| evidence | `evidence.yml` (whole-doc) | `schemaVersion`, valid `workId` | `status`→`draft`, source paths | `evidence.malformedEvidenceArtifact` |

- `verify`/`ship` parse generated JSON views, not authored front matter — no field set to document.
- **Value vocabularies:** `stage` is the **only** closed vocabulary — `charter, specify, clarify, checklist, plan, tasks, analyze, implement, evidence, verify, ship`. `changeTier` and `status` are **free strings** with stage-specific defaults; no parser validates `tier1`/`tier2` or any status value. `schemaVersion` major must be `1`.
- **Correction to feedback (§4.1):** the "incomplete" *message* names all seven clarify fields, but only four gate parsing. Documenting the four gating fields (plus noting the template's full set) is the accurate fix; claiming all seven are required would be false.

**Rationale.** Constitution Principle II — the structured parser is the machine contract; docs must
not assert a stricter contract than the tool enforces, or authors chase phantom requirements.

## R2 — The `[AMB:AMB-###]` decision-tag grammar

**Decision.** Teach `[AMB:AMB-###]` as the canonical tool-emitted form, but state the true
load-bearing rule so the mental model is correct.

**Findings.** An ambiguity is associated to a decision when its `AMB-###` id appears on a
`DEC-###` line under `## Decisions` or `## Accepted Deferrals`. Id extraction uses bare-token
regexes (`\bAMB-\d{3,}\b`, `\bDEC-\d{3,}\b`, etc.) over the raw line; brackets are cosmetic
(`cleanDecisionText` only strips a leading `[...]` run for display). So `[AMB:AMB-001]`,
`[AMB-001]`, and bare `AMB-001` are all equivalent to the parser. The tool *writes*
`[AMB:AMB-001]` (`renderDecisionLine`), and the shipped example uses it — so it is the right
canonical form to teach, while being honest that the bracket is a convention.

**Rationale.** Documenting the bracket as a hard rule would repeat the original sin (a
convention mistaken for a contract). The load-bearing fact is "AMB id on a DEC line in one of
the two sections."

## R3 — What actually clears a blocking ambiguity (two diagnostics)

**Decision.** Document both halves; the clarify skill already covers the second half (the
`## Remaining Ambiguity` empty-section rule) — connect them.

**Findings.**
- `missingClarificationAnswer` ("missing answers for blocking ambiguity: …") clears when a
  decision/deferral carries the AMB id (`existingResolutionTextForAmbiguity` scans
  `Decisions @ AcceptedDeferrals`). Keying an answer under `## Answers` does **not** clear it —
  the Answers section produces answer facts only and is never consulted for resolution.
- `unresolvedBlockingAmbiguity` clears only when the AMB is not left as a blocking bullet under
  `## Remaining Ambiguity` (use a `None.`/disclaimer sentinel, or mark the line
  `deferred`/`non-blocking`).

**Correction to feedback (§3.1).** "Keying answers by CQ/AMB still reported missing answers" —
CONFIRMED: resolution is via the decision tag, not the answer.

## R4 — `duplicateClarificationId`

**Decision.** Document declaration-vs-reference precisely and show the safe deferral pattern.

**Findings.** A "declaration" is a line under `## Decisions` **or** `## Accepted Deferrals`
whose first `DEC-###` is that id; each such line is one record, and the two sections are
pooled for duplicate detection. Mentioning a `DEC-###` in `## Answers`,
`## Remaining Ambiguity`, or `## Lifecycle Notes` is a *reference* (safe). The trap is
declaring the same `DEC-###` id in **both** Decisions and Accepted Deferrals. Safe pattern
(matches the shipped example): declare each `DEC-###` exactly once, in exactly one of the two
sections — `DEC-001` under Decisions, `DEC-002` under Accepted Deferrals.

## R5 — `Source Snapshot` / `sha256`

**Decision.** Correct the stage attribution: sha256 is not a clarify concept.

**Findings.** Clarifications has no `## Source Snapshot` section and no `sha256`. `Source
Snapshot` + optional `sha256:` are `checklist`/`plan` sections (and `tasks`/`evidence`
`sources.digest`). The `sha256:` group is optional and captured only when it is exactly 64 hex
chars, and is used solely for staleness detection — a non-conforming placeholder is silently
ignored (never a hard error), and a real digest is never required to author. So the honest
answer: real digests are what the tool writes, but authoring does not require them.

## R6 — Edit path & drift guards (implementation surface)

**Decision.** Edit the canonical Claude body + mirror the Codex copy byte-identically,
rebuild, regenerate the manifest; put parser-validated grammar examples in the drift-guarded
reference doc and extend its contract test to cover the new grammars.

**Findings.**
- Canonical body: `.claude/skills/<name>/SKILL.md`, linked as an `<EmbeddedResource>`
  (`LogicalName SeededSkill.<name>`) in `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj`.
  `SeededSkills.fs` holds only the name list + a resource loader — **no inline body text**.
- `.codex/skills/<name>/SKILL.md` is a separately committed, byte-identical duplicate;
  `SeededSkillsTests` asserts `embedded == claude` and `claude == codex`. There is **no**
  generator/union script in this repo — the Codex copy is hand-mirrored.
- `.agents/skills/*` `fs-gg-sdd-*` bodies are **not** committed here (they only materialize
  into downstream products at seed time). `.agents/skills/skill-manifest.json` (per-skill
  `sha256`) **is** committed and is regenerated by
  `dotnet run --project src/FS.GG.SDD.Cli -- registry skill-manifest --write`
  (`--check` for CI). `ProcessSkillManifestTests` fails if a body changes without a rewrite.
- **Where parser-validated grammar examples go:** `docs/reference/authoring-contracts.md`,
  in labelled fenced blocks that `AuthoringDocsContractTests` runs through the live parsers.
  Existing labels: `coverage:accepted|rejected`, `evidence:satisfied|unsatisfied`,
  `remaining-ambiguity:disclaimer|blocking`, `blocking-findings:disclaimer|finding`. The skill
  *body* prose is **not** parser-validated. The shipped worked example
  `docs/examples/lifecycle-artifacts/clarifications.md` **is** parser-validated
  (`ExampleArtifactsContractTests`) and already demonstrates the correct decision-tag grammar.

**Design implication.** To make the newly documented decision-tag and front-matter grammars
*drift-guarded* (not merely prose), add labelled example blocks to
`docs/reference/authoring-contracts.md` and extend `AuthoringDocsContractTests.fs` with the
matching label handlers, so the examples are run through `Clarification.parseClarificationFacts`
and the stage front-matter parsers on every build. The skill bodies then carry the
human-facing guidance and point to the drift-guarded reference for the authoritative form —
keeping the skill's existing "durable, drift-guarded source is
`docs/reference/authoring-contracts.md`" claim true and preventing the recurrence of "grammar
lived only in an example."

## Guard tests that must stay green

- `tests/FS.GG.SDD.Commands.Tests/SeededSkillsTests.fs` — set membership, embedded==claude, claude==codex, cross-root byte-identity, determinism.
- `tests/FS.GG.SDD.Commands.Tests/ProcessSkillManifestTests.fs` — per-skill sha256 == authored body; committed manifest == fresh generation.
- `tests/FS.GG.SDD.Commands.Tests/AuthoringDocsContractTests.fs` — labelled reference-doc examples through the live parsers (extend for new grammars).
- `tests/FS.GG.SDD.Artifacts.Tests/ExampleArtifactsContractTests.fs` — worked examples parse clean.
- `tests/FS.GG.Contracts.Tests/SkillMirrorTests.fs` — mirror/verify/sha256 algorithm.
