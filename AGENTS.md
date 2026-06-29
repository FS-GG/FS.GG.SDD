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
- `fsgg-sdd init` seeds the SDD skeleton, which includes an authored
  `.fsgg/constitution.md` lifecycle constitution — generic, deterministic, and
  no-clobber on re-run (same policy as `CLAUDE.md`/`AGENTS.md`). Scaffold delivers
  it via the reused `init` effects; it is never app-only `generatedProduct`
  provenance and `refresh` never regenerates it.
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
  projection; `--rich` renders the report richly via Spectre.Console, degrading to
  plain text when non-interactive or color-disabled), requires no Governance
  runtime, and computes no Governance verdict. The report is not catalogued in `release-readiness.json` (a
  declared exception in `docs/release/schema-reference.md`).
- `fsgg-sdd scaffold` is a cross-cutting command (not a lifecycle stage;
  `nextLifecycleCommand Scaffold = None`, FR-015) that takes a product author from
  an empty directory to a buildable, runnable, SDD-managed product in one
  invocation. It establishes the SDD skeleton (reusing `init`'s effects unchanged,
  so `init` stays byte-identical), then invokes an external template provider
  selected by `--provider <name>` from an author-/provider-owned `.fsgg/providers.yml`
  registry through a generic, schema-versioned provider contract (v1), realized via a
  generic `dotnet new` wrapper at the MVU `RunProcess` edge. SDD owns only the
  contract, the invocation protocol, the `.fsgg/scaffold-provenance.json` record
  (schema v1; produced paths marked `generatedProduct` — externally owned, excluded
  by `refresh`, FR-007), the `scaffold.*` diagnostics, and the three report
  projections — never any provider-specific package id, template id, path, or docs
  URL (FR-002 / SC-005). Scaffold requires `--provider`; with none it blocks with
  `scaffold.providerMissing` pointing to `fsgg-sdd init`. User-input failures exit 1;
  provider defects (`providerFailed`/`providerUnavailable`/`providerWroteSddTree`)
  exit 2; an incomplete scaffold is never reported as complete (FR-009). The
  reference provider ships in the FS.GG.Rendering repo, not in generic SDD. After a
  successful instantiation, scaffold itself owns two generic post-instantiation
  steps — initializing a git repository at the product root (skipped non-fatally
  inside an existing work tree or when git is absent) and setting the executable bit
  on each produced `.sh` script — reported in all three projections, never delegated
  to the provider. The real-provider composition acceptance
  (`tests/FS.GG.SDD.Acceptance.Tests`) is opt-in and network-gated — it drives the
  real published provider only when `FSGG_SDD_ACCEPTANCE_REGISTRY` is set, stays out
  of the default offline inner loop, and emits a deterministic
  `composition-acceptance-result` v1 verdict (a declared release-catalog exception,
  not a lifecycle artifact).
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

<!-- SKILL:spectre-console START -->
### Spectre.Console

For working with Spectre.Console output in this project — the capability/profile
model, the widget tour, the rich/plain/JSON projection conventions, deterministic
test rendering, and the headless-fidelity pitfall (renders correctly locally but
differs/fails in CI) — see the single source:
`.claude/skills/spectre-console/SKILL.md`. Advisory only — it gates nothing.
<!-- SKILL:spectre-console END -->
