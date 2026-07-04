# Implementation Plan: Troubleshooting skill, advertised `--text`, shipped examples

**Branch**: `071-troubleshooting-and-examples` | **Date**: 2026-07-04 | **Spec**: [spec.md](./spec.md)

## Summary

Discoverability/docs feature — **no CLI behavior, `outcome`, counter, or JSON-shape
change**. Add a seeded `fs-gg-sdd-troubleshooting` skill (US1) and ship
live-parser-validated worked examples of the four authored artifacts (US2), fixing
the dead `tests/fixtures/…` reference. Confirmed scope: dedicated seeded skill +
docs examples made consumer-reachable.

## Technical Context

- **Language**: F# on .NET 10; warnings-as-errors on (baseline 0/0). The only F#
  touched is `SeededSkills.skillNames` (+ its comment) and new/updated **tests**.
- **Seeded-set growth 15 → 16** — the pinned surfaces that must move together, or
  a drift guard fails:
  1. `src/FS.GG.SDD.Commands/CommandWorkflow/SeededSkills.fs` — add
     `"fs-gg-sdd-troubleshooting"` to `skillNames`; update the `// 15 …` comment to
     16 (10 stage + 6 cross-cutting).
  2. `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj` — add the
     `<EmbeddedResource … LogicalName="SeededSkill.fs-gg-sdd-troubleshooting" />`.
  3. `.claude/skills/fs-gg-sdd-troubleshooting/SKILL.md` + `.codex/…` (byte-identical).
  4. `tests/FS.GG.SDD.Commands.Tests/SeededSkillsTests.fs` — `15 × 3 = 45` → `16 × 3
     = 48` (comment + `Assert.Equal(48, …)`).
  5. `tests/FS.GG.SDD.Acceptance.Tests/CompositionAcceptanceTests.fs` — add the name
     to the expected seeded-skill list.
  6. `CLAUDE.md` + `AGENTS.md` — "15 … skills (10 stage + 5 cross-cutting)" → "16 …
     (… + 6 cross-cutting)", naming troubleshooting; kept **byte-identical**
     (`AgentSurfaceDriftTests`).
  7. `fs-gg-sdd-lifecycle` skill — the family enumeration + the `--text`/
     troubleshooting one-liner (FR-003).
- **Examples**: `docs/examples/lifecycle-artifacts/` — one coherent work item's
  `clarifications.md`, `checklist.md`, `tasks.yml`, `evidence.yml`, based on the
  **known-valid** fixture content (`tests/fixtures/…/valid-work-item` +
  the `ClarificationArtifactTests`/`ChecklistArtifactTests` fixtures) so they parse
  clean. Drift-guarded by a new test parsing each through the live public parser
  (`Clarification.parseClarificationFacts`, `Checklist.parseChecklistFacts`,
  `Task.parseTaskFacts`, `Evidence.parseEvidence`).

## Approach

1. **Author the examples first** (they anchor the tasks/troubleshooting skills).
   Place under `docs/examples/lifecycle-artifacts/`, adapt ids/paths to a clean
   `001-example` work item, verify each parses via a throwaway harness, then add
   the drift-guard test (`ExampleArtifactsContractTests`) asserting Ok + no blocking
   diagnostics for each.
2. **Fix the dead reference** in `fs-gg-sdd-tasks` (Sources): point at
   `docs/examples/lifecycle-artifacts/tasks.yml` and add a compact complete inline
   `tasks.yml` example in the skill body so a consumer with only the seeded skill is
   unblocked. Sweep all seeded skills for any other `tests/fixtures/…` citation
   (AC-4) and repoint.
3. **Author `fs-gg-sdd-troubleshooting`** (`.claude` + `.codex`): the `--text`
   recipe, a counter→meaning→next-action table, and links to
   [[fs-gg-sdd-authoring-contracts]] / the per-stage skills.
4. **Grow the seeded set** across surfaces 1–6 above; add the lifecycle one-liner
   (FR-003) and family enumeration entry.
5. **Verify**: full `dotnet test`, fantomas, and an end-to-end `init` into a scratch
   dir asserting `.claude/skills/fs-gg-sdd-troubleshooting/SKILL.md` seeds and the
   tasks example resolves.

## Constitution Check

- **Structured artifacts are the machine contract** (II): the shipped examples are
  authoring-surface docs; their machine-truth is enforced by the live-parser drift
  guard, so a doc example can never contradict the parser. PASS.
- **Claude/Codex parity**: new skill mirrored; `CLAUDE.md`/`AGENTS.md` kept
  byte-identical. PASS.
- **Real evidence**: verification is a real seed into a scratch dir + real parser
  runs over the shipped examples, not synthetic. PASS.
- **No behavior drift**: no CLI/JSON change; `validate`/golden contracts untouched.
  PASS.

## Risks

- **Missed enumeration surface** → a drift/count/parity guard fails. Mitigated by
  the explicit surface list above and running the seeded-skill + acceptance +
  agent-surface guards before the full suite.
- **Example rot** → mitigated by the live-parser drift guard (the same discipline
  that protects `authoring-contracts.md`).
- **Consumer-reachability**: `docs/` isn't shipped, so the *inline* skill example is
  the actual consumer fix; the `docs/` copy is the canonical drift-guarded source.
