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
- `fsgg-sdd init` seeds the SDD skeleton, which includes an authored
  `.fsgg/constitution.md` lifecycle constitution — generic, deterministic, and
  no-clobber on re-run (same policy as `CLAUDE.md`/`AGENTS.md`). Scaffold delivers
  it via the reused `init` effects; it is never app-only `generatedProduct`
  provenance and `refresh` never regenerates it.
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
- `fsgg-sdd scaffold` is a cross-cutting command (not a lifecycle stage;
  `nextLifecycleCommand Scaffold = None`, FR-015) that takes a product author from
  an empty directory to a buildable, runnable, SDD-managed product in one
  invocation. It establishes the SDD skeleton (reusing `init`'s effects, unchanged,
  so `init` stays byte-identical), then invokes an **external template provider**
  selected by `--provider <name>` and resolved from an author-/provider-owned
  `.fsgg/providers.yml` registry through a generic, schema-versioned provider
  contract (v1). The provider is realized via a generic `dotnet new` wrapper at the
  MVU `RunProcess` edge; SDD owns only the contract, the invocation protocol, the
  `.fsgg/scaffold-provenance.json` record (schema v1, marking produced paths
  `generatedProduct` — externally owned, which `refresh` excludes, FR-007), the
  `scaffold.*` diagnostics, and the three report projections — **never** any
  provider-specific package id, template id, path, or docs URL (FR-002 / SC-005).
  Scaffold requires `--provider`; with none it blocks with `scaffold.providerMissing`
  pointing to `fsgg-sdd init` for the skeleton only. User-input failures resolve at
  exit 1; provider defects (`providerFailed`/`providerUnavailable`/
  `providerWroteSddTree`) at exit 2; an incomplete scaffold is never reported as
  complete (FR-009). The reference provider (a full runnable UI app) ships in the
  FS.GG.Rendering repo, demonstrated against the contract without placing any
  Rendering knowledge in generic SDD. After a successful instantiation, scaffold
  itself owns two generic post-instantiation steps — initializing a git repository
  at the product root (skipped, non-fatally, inside an existing work tree or when
  git is absent) and setting the executable bit on each produced `.sh` script —
  reported in all three projections and never delegated to the provider. The
  real-provider composition acceptance (`tests/FS.GG.SDD.Acceptance.Tests`) is opt-in
  and network-gated — it drives the real published provider only when
  `FSGG_SDD_ACCEPTANCE_REGISTRY` is set, stays out of the default offline inner loop,
  and emits a deterministic `composition-acceptance-result` v1 verdict (a declared
  release-catalog exception, not a lifecycle artifact).
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
at specs/044-publish-cli-tool/plan.md
<!-- SPECKIT END -->
