# FS.GG.SDD Agent Context

FS.GG.SDD is the spec-driven development lifecycle component for FS.GG. It is not
the governance rule engine.

Use standard Spec Kit for work in this repo. Product source and tests now exist;
add or change them only through feature specs and plans that define the
artifact contract and verification plan.

Core boundary:

- FS.GG.SDD owns lifecycle artifacts, including declared `evidence.yml`
  evidence, normalized work models, generated SDD views, lifecycle CLI
  contracts, and agent command/skill generation.
- `fsgg-sdd init` seeds the SDD skeleton, which includes an authored
  `.fsgg/constitution.md` lifecycle constitution and an authored
  `.fsgg/early-stage-guidance.md` early-stage authoring guide — both generic,
  deterministic, and no-clobber on re-run (same policy as `CLAUDE.md`/`AGENTS.md`).
  The skeleton also seeds the 16 consumer-relevant `fs-gg-sdd-*` process skills (the
  10 stage skills plus the 6 cross-cutting skills `lifecycle`/`getting-started`/
  `authoring-contracts`/`refresh-agents`/`validate`/`troubleshooting`; the
  product-internal `fs-gg-sdd-project` is excluded) into **all three** agent-skill roots
  (`.claude/skills/<name>/SKILL.md`, `.codex/skills/<name>/SKILL.md`, and the 056 neutral
  `.agents/skills/<name>/SKILL.md`), byte-identically, so a scaffolded workspace's agent —
  Claude, Codex, or a neutral `.agents` runtime — can discover the lifecycle without
  hand-copying skills (`claude ≡ codex ≡ agents`).
  They are authored, SDD-owned skeleton (the same `AgentGuidanceTarget` no-clobber
  class as the constitution/early-stage guidance), seeded deterministically and
  equivalently across all three roots, and pinned to the on-disk authored set by a
  drift guard.
  Scaffold delivers all of these via the reused `init` effects; they are never app-only
  `generatedProduct` provenance and `refresh` never regenerates them (though `refresh`
  does re-mirror the union to currency). The seeded skill subtrees are SDD-owned: a
  provider that writes into them is rejected, and they are excluded from provider routing
  and provenance. `fsgg-sdd` is the **sole mirror authority**: a provider writes its own
  `fs-gg-*` skills only into the neutral `.agents/skills/` root (never `.claude`/`.codex`,
  and never the reserved `fs-gg-sdd-*` namespace even in `.agents`), and `scaffold` fans
  the byte-identical **union** (seeded ∪ provider) out into all three roots, recording the
  `.claude`/`.codex` mirror copies under `mirroredPaths` (owner `mirrored`, schema stays
  v1) in `.fsgg/scaffold-provenance.json`.
  As the producer of the `fs-gg-sdd-*` process skills, `fsgg-sdd` also emits their
  `skill-manifest` (schema v1) — the committed, process-only
  `.agents/skills/skill-manifest.json` enumerating every seeded process skill with
  `scope: process`, a canonical-body `sha256` (`sha256sum SKILL.md`-equivalent), and the
  ADR-0017 canonical `materializes-when: always` — regenerated/checked by `fsgg-sdd
  registry skill-manifest [--write|--check]` and pinned to the seeded set by a drift
  guard; the org registry (`.github` `registry/skills.yml`) reconciles its process rows
  from it (ADR-0017).
  `.fsgg/early-stage-guidance.md` covers the pre-work-model stages (`charter`,
  `specify`, `clarify`, `checklist`) — per-stage command, required section
  headings, stable-id formats, and the §1.1/§1.2 authoring contracts — and is a
  read-only mirror of the live contract, pinned by a drift-guard test.
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
  and never a second source of truth. When `work-model.json` is **absent** (the
  pre-work-model early stage), `agents` and `refresh` do not dead-end: they emit a
  non-blocking advisory (`agents.earlyStageGuidance` / `refresh.earlyStageGuidance`,
  exit 0) with best-effort facts from the artifacts that exist and a `NextAction`
  pointing to `.fsgg/early-stage-guidance.md` — writing no digest-stamped view.
  Only the *missing* case is reclassified; malformed/stale/blocked work models still
  block. `fsgg-sdd refresh` is a cross-cutting
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
  an empty directory to a buildable, runnable, SDD-managed workspace in one
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
  A starter selection is just a provider-declared scaffold parameter; scaffold
  records the **effective forwarded parameters** — provider-declared
  `parameters[].default`s overlaid by author `--param` overrides (the author value
  always wins) — as the additive `effectiveParameters` field on
  `.fsgg/scaffold-provenance.json` (schema stays v1) and the scaffold report
  (json/text/rich), sorted by key and verbatim, so the chosen default starter is
  auditable and reproducible — value-agnostically, with no provider-specific starter
  value embedded in generic SDD (FR-003 / FR-004).
  Scaffold requires `--provider`; with none it blocks with `scaffold.providerMissing`
  pointing to `fsgg-sdd init` for the skeleton only. User-input failures resolve at
  exit 1; provider defects (`providerFailed`/`providerUnavailable`/
  `providerWroteSddTree`) at exit 2; an incomplete scaffold is never reported as
  complete (FR-009). The reference provider (a full runnable UI app) ships in the
  FS.GG.Rendering repo, demonstrated against the contract without placing any
  Rendering knowledge in generic SDD. After a successful instantiation, scaffold
  itself owns two generic post-instantiation steps — initializing a git repository
  at the workspace root (skipped, non-fatally, inside an existing work tree or when
  git is absent) and setting the executable bit on each produced `.sh` script —
  reported in all three projections and never delegated to the provider. The
  real-provider composition acceptance (`tests/FS.GG.SDD.Acceptance.Tests`) is opt-in
  and network-gated — it drives the real published provider only when
  `FSGG_SDD_ACCEPTANCE_REGISTRY` is set, stays out of the default offline inner loop,
  and emits a deterministic `composition-acceptance-result` v1 verdict (a declared
  release-catalog exception, not a lifecycle artifact).
- `fsgg-sdd doctor` and `fsgg-sdd upgrade` are the cross-cutting remediation verbs
  (not lifecycle stages; `nextLifecycleCommand Doctor = None` / `Upgrade = None`) that
  reconcile a scaffolded workspace's drift from its coherent set — the remediation half
  of ADR-0009. `doctor` is a **strictly read-only** drift report: installed CLI vs the
  feature-052 declarative required minimum (live descriptor wins over the recorded
  value), which seeded `fs-gg-sdd-*` skills / `.fsgg/early-stage-guidance.md` are
  present vs expected, and a dry-run preview of what `upgrade` would change; it makes
  **zero** writes and exits 0 whenever it reports. `upgrade` is the **only** command
  permitted to mutate the CLI installation or consumer artifacts for remediation,
  across up to three steps — CLI self-update (`dotnet tool update` at the `RunProcess`
  edge, effective on the next invocation), consumer-only template re-pin
  (`.fsgg/providers.yml`; value-agnostic and usually inert, R6), and no-clobber re-seed
  of the **missing** seeded artifacts via `init`'s `AgentGuidanceTarget` effects — where
  each actionable step is shown as a diff and applied only after its own `Confirm` (a new
  constitutionally-clean MVU effect resolved by a stdin read at the edge; the pure
  `update` re-derives staging from the interpreted-effect log) or all at once under an
  explicit `--yes`. A non-interactive run without `--yes` refuses
  (`upgrade.nonInteractiveNoYes`, exit 1, zero writes, no prompt-hang); a confirmed step
  that fails to apply exits 2 (`upgrade.selfUpdateFailed`/`upgrade.stepFailed`); a declined
  step is skipped with residual drift surfaced (exit 0); an incomplete reconciliation is
  never reported as complete (FR-013). Both add additive `doctor`/`upgrade` `CommandReport`
  blocks projected json/text/rich, plus an additive `CommandEffectResult.Confirmed` field;
  no persisted schema changes (provenance stays v1, read-only). See
  `docs/reference/doctor-upgrade.md`.
- `fsgg-sdd surface` is a cross-cutting API-surface baseline verb (not a lifecycle stage;
  `nextLifecycleCommand Surface = None`, feature 086) that enforces the workspace surface
  convention: every authored `src/**/*.fsi` signature has a byte-identical committed baseline
  under `docs/api-surface/` at the mirrored `<Pkg>/<Name>.fsi` path. `--check` (default) is
  **read-only** — it enumerates the source `.fsi`, compares each against its baseline byte-for-byte,
  and blocks with a `surface.drift` `DiagnosticError` (exit 1) on any missing or differing baseline,
  exiting 0 when coherent. `--update` refreshes the baselines from the authored signatures (writing
  only what changed, exit 0). Orphan baselines (a committed baseline with no source) are advisory
  (`surface.orphanBaseline` warning, never auto-removed). The source of truth is the `.fsi` **text**
  (copy/diff, no assembly reflection); the `<Pkg>` segment is derived structurally, so generic SDD
  embeds no provider/package literal, and the roots are convention-default (`src/`,
  `docs/api-surface/`) with `--param sourceRoot=…`/`--param baselineRoot=…` overrides. It adds an
  additive `surface` `CommandReport` block (json/text/rich) and no persisted schema change. This is
  a **workspace** gate — the SDD component repo itself uses hand-authored `.fsi` + the internal
  reflection `PublicSurface.baseline` test, a separate mechanism `surface` does not replace.
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
at specs/084-lifecycle-status-footer/plan.md
<!-- SPECKIT END -->
