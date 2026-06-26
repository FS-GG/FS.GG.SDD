# FS.GG.SDD Agent Context

FS.GG.SDD is the spec-driven development lifecycle product for FS.GG. It is not
the governance rule engine.

Use standard Spec Kit for work in this repo. Product source and tests now exist;
add or change them only through feature specs and plans that define the
artifact contract and verification plan.

Core boundary:

- FS.GG.SDD owns lifecycle artifacts, including declared `evidence.yml`
  evidence, normalized work models, generated SDD views, lifecycle CLI
  contracts, and agent command/skill generation.
- `fsgg-sdd evidence` owns declared authored evidence and SDD readiness
  summaries; `fsgg-sdd verify` evaluates SDD-owned verification readiness over
  task/evidence/test/skill obligations, emits `readiness/<id>/verify.json`, and
  points verification-ready work to ship; `fsgg-sdd ship` aggregates SDD-owned
  merge-boundary readiness, emits `readiness/<id>/ship.json`, and points
  ship-ready work to the Governance-owned protected-boundary handoff.
  `fsgg-sdd agents` is a cross-cutting generator (not a lifecycle stage;
  `nextLifecycleCommand Agents = None`) that derives per-target Claude/Codex
  command and skill guidance from `readiness/<id>/work-model.json` into
  `readiness/<id>/agent-commands/<target>/` (a `guidance.json` manifest plus
  `commands.md`/`skills.md` projections), marked generated with source digests
  and never a second source of truth. `fsgg-sdd refresh` is a cross-cutting
  generator (not a lifecycle stage; `nextLifecycleCommand Refresh = None`) that
  brings a work item's SDD-owned generated views back to currency: it regenerates
  the work model and agent guidance and renders the human-readable
  `readiness/<id>/summary.md` projection, while reporting the currency of
  `analysis.json`/`verify.json`/`ship.json` (re-running those generators out of
  lifecycle order corrupts evidence freshness). Governance-owned effective
  evidence freshness and gate enforcement remain optional downstream concerns.
- `fsgg-sdd validate` is a cross-cutting validation harness (not a lifecycle
  stage; reachable only via the CLI, never from a lifecycle command path) that
  exhaustively exercises SDD's broad matrices — every command × output projection
  × representative state, determinism/degradation, release baseline-conformance,
  and Governance-handoff compatibility — on demand and on a schedule, separate
  from the cheap inner loop. It emits one deterministic `validation-report` JSON
  (`--json` default, `--text` projection; `--rich` renders the report richly via
  Spectre.Console, degrading to plain text when non-interactive or color-disabled),
  requires no Governance runtime, and computes no Governance verdict. The report is **not**
  added to the `release-readiness.json` catalog (it carries sensed metadata and is
  harness output, not a produced lifecycle artifact) — a declared exception in
  `docs/release/schema-reference.md`.
- FS.GG.Governance owns rule evaluation, evidence freshness, routing, profiles,
  and gate enforcement.
- Integrations between them must be explicit, versioned, and optional until
  adopted by a feature spec.

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

When working here:

- Follow the constitution at `.specify/memory/constitution.md`.
- Treat Markdown as authoring surface and structured artifacts as machine
  contracts.
- Keep Claude and Codex behavior aligned; update both agent surfaces when the
  workflow changes.
- Do not add rendering-specific package names, templates, paths, or docs URLs to
  generic SDD behavior.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
at specs/024-prerequisite-combinator/plan.md
<!-- SPECKIT END -->
