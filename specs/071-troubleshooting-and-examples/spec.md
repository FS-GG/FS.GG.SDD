# Feature Specification: Troubleshooting skill, advertised `--text` diagnostics, and shipped example artifacts

**Feature ID**: 071-troubleshooting-and-examples
**Branch**: `071-troubleshooting-and-examples`
**Date**: 2026-07-04
**Roadmap**: closes [#106](https://github.com/FS-GG/FS.GG.SDD/issues/106) (cross-repo request from `FS-GG/.github`; epic `.github#165`)
**Source of truth**: Space Invaders consumer feedback §1.3–§1.4 (Friction/Polish).

## Context

Two discoverability gaps, both verified against `HEAD`:

- **§1.3 — `--text` is the best diagnostic surface and is never advertised as
  one.** A stage can block on a state the default JSON `outcome` hides: `clarify`
  reports `outcome: noChange` with `blockingAmbiguities: 1` and `diagnostics: 0`,
  while `--text` surfaces the `blockingAmbiguities`/`unresolvedAmbiguities`/
  `checklistFailedBlocking`/`staleResultCount`/`failedBlockingCount` counters that
  explain *why*. (Confirmed during 070; the trap form is fixed, but a **genuine**
  blocking count still reads as `noChange` at the JSON top level.) There is no
  skill that says "stage blocked unexpectedly → run `--text`, read these
  counters," and `fs-gg-sdd-lifecycle` mentions `--text` only as an output format.
- **§1.4 — no consumer-reachable reference artifacts.** `fs-gg-sdd-tasks`'
  Sources cite `tests/fixtures/sdd-artifact-model/valid-work-item/…/tasks.yml`
  (confirmed at `SKILL.md:85`). That path exists in *this* repo but is **never
  shipped to a scaffolded consumer** — a consumer receives only the seeded
  `fs-gg-sdd-*` skills, `.fsgg/constitution.md`, and `.fsgg/early-stage-guidance.md`
  (not `docs/` or `tests/fixtures/`). So the reference is a dead end for exactly
  the audience that needs it, and there is no complete worked example of the
  authored artifacts (`clarifications.md`, `checklist.md`, `tasks.yml`,
  `evidence.yml`) a consumer can copy-adapt.

Contract note (drives the plan): the seeded skill set is **pinned to exactly the
15 names in `SeededSkills.skillNames`** — `SeededSkillsTests` asserts the on-disk
`.claude`/`.codex` authored set equals it and that the seed emits `15 × 3 = 45`
write effects. A new `fs-gg-sdd-troubleshooting` skill is therefore a deliberate,
pattern-following growth of that pinned set (like 056 grew the roots 30→45), not a
free-floating file — it must flow through every enumeration surface, or the drift
guards fail.

## User Stories

**US1 (P2)** — As an SDD author (or agent) whose stage blocked and whose default
JSON output says only `noChange`/no diagnostics, I want a discoverable
`fs-gg-sdd-troubleshooting` skill that tells me to re-run with `--text` and read
the specific summary counters, plus a one-line pointer from
`fs-gg-sdd-lifecycle`, so I diagnose the block in seconds — and the skill ships to
scaffolded consumers like every other lifecycle skill.

**US2 (P3)** — As an SDD author starting a work item, I want one complete, valid,
worked example of each authored artifact reachable from the skills I actually have
in my scaffolded repo (not a dead `tests/fixtures/…` path), so I copy-adapt a
known-good artifact instead of inferring the grammar.

## Requirements

- **FR-001**: A new `fs-gg-sdd-troubleshooting` skill MUST be authored and MUST be
  part of the **seeded** consumer skill set — added to `SeededSkills.skillNames`,
  embedded as a resource, mirrored byte-identically across the maintained agent
  roots, and reflected in every count/enumeration surface (the `skillNames`
  comment, `SeededSkillsTests` `15×3=45` → `16×3=48`, the composition-acceptance
  expected-skill list, and the `CLAUDE.md`/`AGENTS.md` "15 … skills" description),
  so all seeded-skill drift guards stay green.
- **FR-002**: The troubleshooting skill MUST carry the core recipe — *stage blocks
  or reports `noChange` unexpectedly → re-run `fsgg-sdd <stage> --text` and read
  the summary counters* — with a table mapping each counter
  (`blockingAmbiguities`, `unresolvedAmbiguities`, `checklistFailedBlocking`,
  `staleResultCount`, `failedBlockingCount`, …) to its meaning and the stage/next
  action, and MUST link the load-bearing grammars ([[fs-gg-sdd-authoring-contracts]]).
- **FR-003**: `fs-gg-sdd-lifecycle` MUST gain a one-line pointer advertising
  `--text` as the diagnostic surface and directing a blocked author to
  [[fs-gg-sdd-troubleshooting]].
- **FR-004**: One complete, valid worked example of each authored artifact —
  `clarifications.md`, `checklist.md`, `tasks.yml`, `evidence.yml` (a single
  coherent work item) — MUST be shipped under `docs/` as the canonical reference,
  and MUST be validated by a drift guard that parses each through the **live**
  artifact parser so the examples cannot rot.
- **FR-005**: The dead `tests/fixtures/…` reference in `fs-gg-sdd-tasks` MUST be
  replaced with a consumer-reachable reference (the shipped example and/or a
  complete inline example in the skill body); no seeded skill may point a consumer
  at a path absent from a scaffolded repo.

## Acceptance Criteria

- **AC-1** (FR-001): Given a freshly scaffolded/`init`-ed product, when the seed
  runs, then `.claude/skills/fs-gg-sdd-troubleshooting/SKILL.md` (and the `.codex`
  mirror) is present, and the full seeded set is `16` skills; all seeded-skill
  drift/parity/count tests pass.
- **AC-2** (FR-002/FR-003): Given the troubleshooting skill and the lifecycle
  skill, when inspected, then the troubleshooting skill contains the `--text`
  counter recipe + counter table, and `fs-gg-sdd-lifecycle` links to it.
- **AC-3** (FR-004): Given the shipped `docs/` examples, when the drift guard runs,
  then each of `clarifications.md`/`checklist.md`/`tasks.yml`/`evidence.yml` parses
  cleanly through its live parser (no blocking diagnostics), and a contradiction
  fails the build.
- **AC-4** (FR-005): Given every seeded skill, when its referenced paths are
  checked, then none points at a `tests/fixtures/…` (or other non-shipped) path;
  the `fs-gg-sdd-tasks` reference resolves to shipped content.
- **AC-5**: Given `CLAUDE.md` and `AGENTS.md`, when compared, then they remain
  byte-identical (the `AgentSurfaceDriftTests` parity guard stays green) with the
  updated "16 … skills (10 stage + 6 cross-cutting)" description.

## Success Criteria

- **SC-001**: A scaffolded consumer that hits an unexpected block can self-diagnose
  from the seeded `fs-gg-sdd-troubleshooting` skill alone (no repo/website access).
- **SC-002**: Every authored-artifact type has one shipped, live-parser-validated
  worked example a consumer can copy-adapt; no seeded skill cites a non-shipped
  path. Full suite green, 0 new warnings.

## Scope / Non-goals

- **In scope**: the new seeded troubleshooting skill + all seeded-set enumeration
  updates; the lifecycle one-liner; the `docs/` worked examples + their drift
  guard; the dead-reference fix.
- **Out of scope**: changing any CLI behavior, `outcome` value, counter, or JSON
  shape (this is discoverability/docs only — the `noChange`-with-a-blocking-counter
  behavior is documented, not changed); Governance surfaces.

## Open decision (for confirmation)

The issue asks for a dedicated `fs-gg-sdd-troubleshooting` skill; the alternative
is folding the recipe into the existing `fs-gg-sdd-lifecycle` skill (no growth of
the pinned set). A dedicated **seeded** skill is recommended — it is what the issue
asks for and is more discoverable (agents match skills by description), at the cost
of touching the enumeration surfaces above. This spec assumes the dedicated seeded
skill; see the scope question at the review checkpoint.
