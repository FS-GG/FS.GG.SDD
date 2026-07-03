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
  `.fsgg/constitution.md` lifecycle constitution and an authored
  `.fsgg/early-stage-guidance.md` early-stage authoring guide — both generic,
  deterministic, and no-clobber on re-run (same policy as `CLAUDE.md`/`AGENTS.md`).
  Scaffold delivers them via the reused `init` effects; they are never app-only
  `generatedProduct` provenance and `refresh` never regenerates them.
  `.fsgg/early-stage-guidance.md` covers the pre-work-model stages (`charter`,
  `specify`, `clarify`, `checklist`) — per-stage command, required section headings,
  stable-id formats, and the §1.1/§1.2 authoring contracts — as a read-only mirror
  of the live contract, pinned by a drift-guard test.
- `fsgg-sdd evidence` owns declared authored evidence and SDD readiness
  summaries; `fsgg-sdd verify` evaluates SDD-owned verification readiness over
  task/evidence/test/skill obligations, emits `readiness/<id>/verify.json`, and
  points to ship; `fsgg-sdd ship` aggregates SDD-owned merge-boundary readiness,
  emits `readiness/<id>/ship.json`, and points ship-ready work to the
  Governance-owned protected-boundary handoff. `fsgg-sdd agents` is a
  cross-cutting generator (not a lifecycle stage) that derives per-target
  Claude/Codex command and skill guidance from `readiness/<id>/work-model.json`
  into `readiness/<id>/agent-commands/<target>/`, marked generated with source
  digests and never a second source of truth. When `work-model.json` is absent (the
  pre-work-model early stage), `agents` and `refresh` do not dead-end: they emit a
  non-blocking advisory (`agents.earlyStageGuidance` / `refresh.earlyStageGuidance`,
  exit 0) with best-effort facts from existing artifacts and a `NextAction` to
  `.fsgg/early-stage-guidance.md`, writing no digest-stamped view; only the *missing*
  case is reclassified (malformed/stale/blocked still block). `fsgg-sdd refresh` is a
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
  URL (FR-002 / SC-005). A starter selection is just a provider-declared scaffold
  parameter; scaffold records the **effective forwarded parameters** —
  provider-declared `parameters[].default`s overlaid by author `--param` overrides
  (the author value always wins) — as the additive `effectiveParameters` field on
  `.fsgg/scaffold-provenance.json` (schema stays v1) and the scaffold report
  (json/text/rich), sorted by key and verbatim, so the chosen default starter is
  auditable and reproducible — value-agnostically, no provider-specific starter value
  in generic SDD (FR-003 / FR-004). Scaffold requires `--provider`; with none it blocks with
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
- `fsgg-sdd doctor` and `fsgg-sdd upgrade` are the cross-cutting remediation verbs
  (not lifecycle stages; `nextLifecycleCommand = None`) that reconcile a scaffolded
  product's drift from its coherent set. `doctor` is a strictly read-only drift
  report (installed CLI vs the feature-052 declarative minimum, seeded artifacts
  present vs expected, a dry-run preview of what `upgrade` would change); it makes
  zero writes and exits 0 whenever it reports. `upgrade` is the **only** command that
  mutates the CLI installation or consumer artifacts for remediation, across up to
  three steps — CLI self-update (`RunProcess` edge), consumer-only template re-pin
  (`.fsgg/providers.yml`; value-agnostic and usually inert, R6), and no-clobber
  re-seed of the **missing** seeded artifacts via `init`'s `AgentGuidanceTarget`
  effects — each shown as a diff and applied only after its own `Confirm` (a new MVU
  effect resolved at the edge) or all at once under `--yes`. A non-interactive run
  without `--yes` refuses (`upgrade.nonInteractiveNoYes`, exit 1, zero writes); a
  confirmed step that fails to apply exits 2; an incomplete reconciliation is never
  reported as complete (residual drift + step ids surfaced, FR-013). Both add additive
  `doctor`/`upgrade` `CommandReport` blocks (json/text/rich); no persisted schema
  changes. See `docs/reference/doctor-upgrade.md`.
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
at specs/064-build-ci-hygiene/plan.md
<!-- SPECKIT END -->

<!-- SKILL:spectre-console START -->
### Spectre.Console

For working with Spectre.Console output in this project — the capability/profile
model, the widget tour, the rich/plain/JSON projection conventions, deterministic
test rendering, and the headless-fidelity pitfall (renders correctly locally but
differs/fails in CI) — see the single source:
`.claude/skills/spectre-console/SKILL.md`. Advisory only — it gates nothing.
<!-- SKILL:spectre-console END -->
