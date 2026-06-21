# FS.GG.SDD Agent Context

FS.GG.SDD is the spec-driven development lifecycle product for FS.GG. It is
separate from FS.GG.Governance.

Use standard Spec Kit for all non-trivial work. Product source and tests now
exist; add or change them only through a Spec Kit feature that defines the
artifact contract and verification plan.

Read before acting:

- `.specify/memory/constitution.md`
- `docs/initial-implementation-plan.md`
- `.codex/skills/fs-gg-sdd-project/SKILL.md`

Boundary rules:

- SDD owns charter/spec/clarify/checklist/plan/tasks/evidence/verify/ship
  lifecycle artifacts, normalized work models, generated views, and agent
  command/skill generation.
- `fsgg-sdd evidence` owns declared authored evidence and SDD readiness
  summaries; `fsgg-sdd verify` evaluates SDD-owned verification readiness over
  task/evidence/test/skill obligations, emits `readiness/<id>/verify.json`, and
  points to ship; `fsgg-sdd ship` aggregates SDD-owned merge-boundary readiness,
  emits `readiness/<id>/ship.json`, and points ship-ready work to the
  Governance-owned protected-boundary handoff. `fsgg-sdd agents` is a
  cross-cutting generator (not a lifecycle stage) that derives per-target
  Claude/Codex command and skill guidance from `readiness/<id>/work-model.json`
  into `readiness/<id>/agent-commands/<target>/`, marked generated with source
  digests and never a second source of truth. `fsgg-sdd refresh` is a
  cross-cutting generator (not a lifecycle stage) that brings a work item's
  SDD-owned generated views back to currency: it regenerates the work model and
  agent guidance and renders `readiness/<id>/summary.md`, and reports the
  currency of `analysis.json`/`verify.json`/`ship.json`. Governance-owned
  effective evidence freshness and gate enforcement remain optional downstream
  concerns.
- `fsgg-sdd validate` is a cross-cutting validation harness (not a lifecycle
  stage; reachable only via the CLI, never from a lifecycle command path) that
  exhaustively exercises SDD's broad matrices — command × projection × state,
  determinism/degradation, release baseline-conformance, and Governance-handoff
  compatibility — on demand and on a schedule, separate from the cheap inner loop.
  It emits one deterministic `validation-report` JSON (`--json` default, `--text`
  projection; `--rich` deferred), requires no Governance runtime, and computes no
  Governance verdict. The report is not catalogued in `release-readiness.json` (a
  declared exception in `docs/release/schema-reference.md`).
- Governance owns rule evaluation, evidence freshness, routing, profiles, and
  gate enforcement.
- Markdown is an authoring surface; schema-versioned structured artifacts are
  the machine contract.
- Keep Claude and Codex guidance synchronized when workflow behavior changes.

CLI output formats:

- `fsgg-sdd` projects the same `CommandReport` three ways, selected by flag with
  precedence `--rich` > `--text` > `--json` > default.
- default / `--json` — the deterministic JSON automation contract (unchanged; the
  default for every command, including the unknown-command and no-args paths).
- `--text` — portable plain-text summary.
- `--rich` — human-oriented Spectre.Console rendering (panels, tables, color). It
  is a pure projection over the same report: it adds and drops no facts and
  changes no JSON byte, stream routing, or exit code. `--rich` degrades to plain
  text with zero ANSI whenever output is non-interactive/redirected or color is
  disabled (`NO_COLOR` present, or `TERM=dumb`). Rich output is presentation only
  and is excluded from deterministic/golden contracts.

Do not add product source, package projects, or tests without a Spec Kit feature
that defines the artifact contract and verification plan.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
at specs/013-ship-command/plan.md
<!-- SPECKIT END -->
