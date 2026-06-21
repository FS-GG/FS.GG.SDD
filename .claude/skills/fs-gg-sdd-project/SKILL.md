---
name: fs-gg-sdd-project
description: Work on FS.GG.SDD, the FS.GG spec-driven development lifecycle product. Use when creating or editing SDD lifecycle specs, plans, schemas, generated views, agent commands, or Governance integration contracts.
---

# FS.GG.SDD Project Skill

Use this skill for work in the FS.GG.SDD repository.

## Boundaries

- FS.GG.SDD owns the lifecycle product: charter, specify, clarify, checklist,
  plan, tasks, analyze, evidence declarations, normalized work model, generated
  SDD views, and agent command/skill generation.
- FS.GG.Governance owns rule evaluation, evidence freshness, routing, profiles,
  and gate enforcement.
- FS.GG.Rendering is a possible customer, not the shape of this repository.

## Required Reading

Before changing behavior, read:

1. `.specify/memory/constitution.md`
2. `docs/initial-implementation-plan.md`
3. `README.md`

## Working Rules

- Use standard Spec Kit: specify -> clarify as needed -> plan -> tasks ->
  implement -> analyze.
- Do not add source projects to the scaffold without a feature spec.
- Treat Markdown as authoring surface and schema-versioned structured artifacts
  as the machine contract.
- For every lifecycle artifact, identify the authored source, structured model,
  generated views, stale-view behavior, and diagnostics.
- Keep Claude and Codex guidance equivalent when workflow behavior changes.
- Integrate Governance only through explicit optional contracts.

## CLI output formats

`fsgg-sdd` projects the same `CommandReport` three ways, selected by flag with
precedence `--rich` > `--text` > `--json` > default:

- default / `--json` — deterministic JSON automation contract (unchanged default).
- `--text` — portable plain-text summary.
- `--rich` — human-oriented Spectre.Console rendering (panels, tables, color),
  a pure projection over the same report. It changes no JSON byte, stream, or exit
  code, and degrades to plain text with zero ANSI when output is
  non-interactive/redirected or color is disabled (`NO_COLOR`, or `TERM=dumb`).
  Rich output is presentation only and excluded from deterministic/golden contracts.

## Validation harness

`fsgg-sdd validate` is a cross-cutting validation harness (not a lifecycle stage;
reachable only via the CLI, never from a lifecycle command path). It exhaustively
exercises SDD's broad matrices — every command × output projection × representative
state, determinism/degradation, release baseline-conformance, and Governance-handoff
compatibility — on demand and on a schedule, separate from the cheap inner loop. It
emits one deterministic `validation-report` JSON (`--json` default, `--text`
projection; `--rich` renders the report richly via Spectre.Console, degrading to
plain text when non-interactive or color-disabled), requires no Governance runtime,
and computes no Governance verdict. The report is not catalogued in `release-readiness.json` (a
declared exception in `docs/release/schema-reference.md`).

## First Feature Bias

The first implementation feature should define the SDD artifact model and
schemas before commands or generators are built.
