---
title: Initial implementation plan
category: SDD
categoryindex: 6
index: 12
description: Implementation plan for the FS.GG.SDD consumer product design, with explicit SDD, Governance, Rendering, and generated-product ownership.
---

# Initial implementation plan

This is the implementation plan for the FS.GG.SDD consumer product design in
[initial-design.md](initial-design.md). It is maintained in this repository
because FS.GG.SDD owns the native spec-driven development lifecycle. The plan
also names optional Governance, Rendering, and generated-product work where the
consumer workflow crosses repository boundaries.

The design is implemented by coordinated work across:

| Owner | Implementation responsibility |
|---|---|
| `FS.GG.SDD` | Lifecycle artifact model, normalized work model, generated SDD views, lifecycle authoring commands, agent command/skill generation, and optional Governance-facing contracts. |
| `FS.GG.Governance` | Rule algebra use, route/evidence/audit reports, capability catalogs, profiles, freshness, cost/cache, protected-boundary gates, policy enforcement, and release/provenance gates. |
| `FS.GG.Rendering` | Rendering templates, generated-product assets, design-system facts, samples, captures, and product-specific documentation surfaces. |
| Generated products | Project policy, declared capabilities, work items, evidence declarations, local readiness artifacts, and product-specific configuration. |

FS.GG.SDD remains independently buildable and usable without Governance
installed. Governance integration is optional and versioned.

Implementation is driven by the consumer experience:

| Consumer need | Product response |
|---|---|
| Start a project without hidden FS.GG repository knowledge. | `fsgg-sdd init` creates the SDD skeleton, work root, policy pointers, and agent guidance targets. |
| Turn intent into executable work. | Charter, spec, clarify, checklist, plan, tasks, evidence, verify, and ship share one typed lifecycle model. |
| Know what artifacts must contain. | SDD lifecycle rules define valid spec/plan/task/evidence shape, loaded-skill expectations, and test obligations. |
| Use agents safely. | Claude and Codex guidance is generated from the lifecycle model and cannot become a second source of truth. |
| Keep local authoring cheap. | SDD validates lifecycle artifacts and generated-view currency before optional broad product gates. |
| Add protected-boundary rigor later. | SDD emits versioned readiness JSON that Governance can inspect for routing, freshness, profiles, and enforcement. |

## Development Workflow

Standard Spec Kit remains the workflow used to develop this repository:

```text
charter -> specify -> clarify -> checklist -> plan -> tasks -> analyze -> evidence -> verify -> ship
```

Product code, packages, and tests are added to this repository only through a
Spec Kit feature that defines artifact contracts and verification. The first
FS.GG.SDD feature remains `001-sdd-artifact-model`; it defines the lifecycle
artifact contract before commands or generators are implemented.

For the native SDD product lifecycle, `fsgg-sdd analyze` is the tasks-ready
readiness step that emits `readiness/<id>/analysis.json`; `fsgg-sdd evidence`
records authored evidence declarations and refreshes the SDD work model; and
`fsgg-sdd verify` (complete — see `specs/012-verify-command/readiness/`) evaluates
SDD-owned verification readiness, emits `readiness/<id>/verify.json`, refreshes
the work model, and points verification-ready work to ship; and `fsgg-sdd ship`
(complete — see `specs/013-ship-command/readiness/`) aggregates SDD-owned
merge-boundary readiness, emits `readiness/<id>/ship.json`, and points ship-ready
work to the Governance-owned protected-boundary handoff.

## Scope Boundary

FS.GG.SDD owns:

- project charter and policy workflow as SDD lifecycle artifacts;
- specify, clarify, checklist, plan, tasks, analyze, implement, verify, and
  ship readiness artifacts;
- schema-versioned structured contracts for SDD-authored sources;
- normalized work model generation;
- generated SDD readiness views;
- agent command and skill generation for Claude and Codex;
- optional contracts consumed by FS.GG.Governance.

FS.GG.SDD does not own:

- rule evaluation;
- evidence freshness policy;
- route/profile selection;
- protected branch gate enforcement;
- package release enforcement for non-SDD products;
- rendering templates, controls, captures, or generated-product runtime
  behavior.

Those concerns are planned here so the design is complete, but their
implementation happens in FS.GG.Governance, FS.GG.Rendering, or generated
products.

## Design Coverage

| Design area | Primary owner | Implementation track |
|---|---|---|
| Consumer SDD experience | `FS.GG.SDD` | `fsgg-sdd init`, lifecycle commands, readable diagnostics, quickstart, migration, and no-Governance workflow. |
| Native SDD lifecycle | `FS.GG.SDD` | Artifact model, work model, lifecycle commands, task/evidence state, agent guidance. |
| Lifecycle rule pack | `FS.GG.SDD` with Governance machinery | Spec/plan/task/evidence contracts, skill requirements, test obligations, and Governance-compatible checks. |
| Normalized work model | `FS.GG.SDD` | `WorkModel` assembly, source digests, conflict diagnostics, deterministic JSON. |
| Capability catalog MVP | `FS.GG.Governance` | `.fsgg/capabilities.yml`, path map, surfaces, checks, governed-root classification. |
| Project policy and tooling | `FS.GG.Governance` | `.fsgg/policy.yml`, `.fsgg/tooling.yml`, profile, command, timeout, and environment schemas. |
| Route and ship walking skeleton | `FS.GG.Governance` | Git/CI facts, route traces, selected gates, audit JSON, protected-branch guidance. |
| Profiles, modes, and maturity | `FS.GG.Governance` | Base/effective severity, truth-table fixtures, enforcement JSON snapshots. |
| Cost and cache | `FS.GG.Governance` | Rule cost, freshness keys, evidence reuse, broad-route explanation. |
| Generated readiness views | Shared | SDD emits lifecycle readiness; Governance emits route/contract/explain/evidence/audit. |
| Agent guidance | `FS.GG.SDD` with Governance contracts | Generated Claude/Codex instructions from the lifecycle model; agent-reviewed rule outputs stay advisory until calibrated. |
| Package, docs, skills, design adapters | `FS.GG.Governance`, `FS.GG.Rendering` | Adapter rule packs, product facts, generated-product checks, docs/examples checks, design facts. |
| Release and provenance | `FS.GG.Governance`; SDD for its own packages | Command records, artifact digests, package/release rules, attestations, compatibility docs. |

## Ground Rules

- Markdown is an authoring surface. Schema-versioned structured artifacts are
  the machine contract.
- If prose and structured data disagree, the feature plan must say which source
  wins, how the conflict is reported, and which generated view records the
  diagnostic.
- Public F# modules get `.fsi` signatures first, semantic tests through the
  public surface, then `.fs` implementation.
- Stateful commands, generators, validators, and agent writers expose or wrap an
  Elmish-style `Model`, `Msg`, `Effect`, `init`, and `update` boundary.
- JSON is the automation contract. Plain text and Spectre.Console output are
  projections over the same report objects.
- Deterministic JSON must not depend on implicit clocks, terminal wrapping, or
  ANSI output.
- Generated views are outputs. Their presence is not proof of currency.
- Agent-reviewed findings are advisory until cache, prompt-isolation,
  confidence, and calibration constraints are implemented.
- SDD must remain useful without the Governance gate runtime installed.

## Command Naming

`initial-design.md` uses `fsgg ...` for the broad FS.GG command family. This
repository's constitution reserves the SDD command family as `fsgg-sdd` unless
a later release decision chooses otherwise.

| Design command | Owner | Planned command |
|---|---|---|
| `fsgg new` / `fsgg init` | SDD plus optional template providers | `fsgg-sdd init` for SDD skeletons; optional FS.GG umbrella command may delegate. |
| `fsgg charter` | SDD | `fsgg-sdd charter` |
| `fsgg work specify` | SDD | `fsgg-sdd specify` |
| `fsgg work clarify` | SDD | `fsgg-sdd clarify` |
| `fsgg work checklist` | SDD | `fsgg-sdd checklist` |
| `fsgg work plan` | SDD | `fsgg-sdd plan` |
| `fsgg work tasks` | SDD | `fsgg-sdd tasks` |
| `fsgg analyze` | SDD | `fsgg-sdd analyze` |
| `fsgg work update` / `fsgg evidence` | SDD for declarations; Governance for freshness | `fsgg-sdd evidence` or `fsgg-sdd update`; Governance computes effective evidence. |
| `fsgg route` | Governance | Governance CLI command |
| `fsgg check` | Governance | Governance CLI command |
| `fsgg refresh` | Shared | SDD refreshes SDD views; Governance refreshes gate/rule/capability views. |
| `fsgg verify` | Shared | `fsgg-sdd verify` emits SDD readiness; Governance owns profile-aware verification gates. |
| `fsgg ship` | Shared contract; Governance enforcement | `fsgg-sdd ship` emits SDD readiness; Governance owns protected-branch verdict. |
| `fsgg release` | Governance | Governance release gate; SDD uses it for its own packages once packaged. |
| `fsgg tui` / `fsgg watch` | Governance | Optional Governance presentation commands. |

Exact subcommand names are locked by the feature specs that introduce each CLI
surface.

## Source Model

The full design source model is `.fsgg` plus `work/<id>`. SDD owns the lifecycle
contract. Governance owns capability, route, profile, freshness, and gate
policy.

Project-level authored sources:

| Artifact | Primary owner | Purpose |
|---|---|---|
| `.fsgg/project.yml` | Shared, with SDD-owned lifecycle fields | Project id, domain list, default work root, package surfaces, and pointers to SDD and Governance policy. |
| `.fsgg/sdd.yml` | SDD | SDD lifecycle policy, artifact layout, generated-view policy, and schema migration posture. |
| `.fsgg/agents.yml` | SDD | Agent command and skill generation targets for Claude, Codex, and future agents. |
| `.fsgg/policy.yml` | Governance | Governance profiles, default profile, enforcement mapping, branch policy, and review budgets. |
| `.fsgg/capabilities.yml` | Governance with generated-product input | Capability domains, path map, protected surfaces, checks, owners, cost, maturity, and release surfaces. |
| `.fsgg/tooling.yml` | Governance | Command allow-list, timeouts, environment classes, external tool policy, and tool version expectations. |

Work-item authored sources:

| Artifact | Primary owner | Purpose |
|---|---|---|
| `work/<id>/charter.md` | SDD | Project or work charter when a lifecycle slice needs local principles and boundaries. |
| `work/<id>/spec.md` | SDD | User value, scope, stories, requirements, non-goals, and acceptance criteria. |
| `work/<id>/clarifications.md` | SDD | Material ambiguity and explicit answers. |
| `work/<id>/checklist.md` | SDD | Requirements-quality checks before planning. |
| `work/<id>/plan.md` | SDD | Technical plan, contracts, risks, verification, and migration posture. |
| `work/<id>/contracts/` | SDD and product owners | Public or tool-facing contracts described by the plan. |
| `work/<id>/tasks.yml` | SDD | Typed implementation task graph. |
| `work/<id>/evidence.yml` | SDD for declarations; Governance for effective state | Declared implementation, verification, synthetic, and deferral evidence. |

Generated views:

| Artifact | Primary owner | Purpose |
|---|---|---|
| `readiness/<id>/work-model.json` | SDD | Deterministic normalized work model with source digests and diagnostics. |
| `readiness/<id>/analysis.json` | SDD | Cross-artifact consistency diagnostics. |
| `readiness/<id>/verify.json` | SDD | SDD-owned verification results and readiness facts. |
| `readiness/<id>/ship.json` | SDD | Merge-boundary SDD readiness for CI and optional Governance consumers. |
| `readiness/<id>/summary.md` | Shared | Human-readable summary rendered from structured readiness data. |
| `readiness/<id>/agent-commands/` | SDD | Generated agent guidance derived from the same lifecycle model. |
| `.fsgg/gates.json` | Governance | Generated gate registry with ids, prerequisites, cost, timeout, owner, maturity, and freshness keys. |
| `.fsgg/rules.md` | Governance | Rendered rule catalog from reified checks. |
| `readiness/<id>/route.json` | Governance | Matched rules, changed paths, selected gates, unknown-path findings, cost, cache eligibility, and profile-adjusted enforcement. |
| `readiness/<id>/contract.json` | Governance | Rule contracts, required inputs, and source reads. |
| `readiness/<id>/explain.json` | Governance | Proof trees and explanation traces for applicable rules. |
| `readiness/<id>/evidence.json` | Governance | Effective evidence states, taint propagation, freshness, and graph failures. |
| `readiness/<id>/audit.json` | Governance | Ship verdict, blockers, warnings, provenance references, and exit-code basis. |
| `readiness/<id>/attestations/` | Governance | Optional SLSA/in-toto-shaped provenance summaries. |

Every generated view must identify sources, source digests, generator version,
and stale-view diagnostics.

## Roadmap

Progress markers (status legend): 🟢 / ✅ complete · 🟡 partial (core landed;
emission/wiring deferred) · 🔴 not started · ⬜ optional/out of scope. As of
2026-06-22 every `FS.GG.SDD`-owned feature is complete (🟢); the remaining 🟡/🔴
rows are Governance- or Rendering-owned follow-ons. The cache-eligibility **host
wiring** landed 2026-06-22 (Governance F044 `FS.GG.Governance.CacheEligibilityCommand`),
the verdict **embed** into `route.json`/`audit.json` landed 2026-06-22 (F045
`FS.GG.Governance.RouteJson`/`AuditJson`), and **real** cache-eligibility verdicts now
flow from `fsgg route`/`fsgg ship` themselves (F046 `FS.GG.Governance.FreshnessSensing`/
`RouteCommand`/`ShipCommand`) — so the cache-eligibility thread is end-to-end. The
evidence-reuse store **write half** (serialise + bounded retention + superseded-world
pruning, as a pure core) landed 2026-06-22 (F047 `FS.GG.Governance.EvidenceReuseStore`);
the one remaining cache-eligibility piece is the **impure on-disk store write** (atomic
temp+rename persistence wired into `fsgg route`/`fsgg ship`, so the cache actually warms
between runs) plus real evidence-reference capture from gate execution. Other open rows
are capability-catalog expansion, `refresh`/stale-view blocking, and the `verify`/`release`
gates.

- 🟢 [x] Scaffold empty repository with Spec Kit metadata, constitution, docs, and
  Claude/Codex guidance.
- 🟢 [x] Create GitHub repository under `FS-GG`.
- 🟢 [x] Update FS-GG org profile/site to list SDD as a separate product.
- 🟢 [x] Copy development-relevant Governance and org reference docs into
  `docs/reference/`.
- 🟢 [x] Replace copied Governance-only roadmap with an SDD-scoped plan.
- 🟢 [x] Expand this plan to cover the full `initial-design.md` design with
  explicit owner boundaries.
- 🟢 [x] Implement `001-sdd-artifact-model` as the first packable SDD artifact
  model library with fixtures, diagnostics, deterministic JSON, optional
  Governance boundary contracts, and readiness evidence.
- 🟢 [x] Implement `002-normalized-work-model` by extending the artifact model
  library with pure normalized work-model generation, generated-view currency
  checks, schema migration posture, diagnostics, fixtures, deterministic JSON,
  and readiness evidence.
- 🟢 [x] Complete `003-native-sdd-lifecycle-commands`; the command library, CLI
  host, public MVU/report surface, and `fsgg-sdd init` MVP are implemented
  with readiness evidence.
- 🟢 [x] Implement `004-charter-command` by adding `fsgg-sdd charter`, safe
  authored charter create/rerun behavior, generated work-model state reporting
  and refresh where source data is valid, deterministic reports, text
  projection, optional Governance compatibility facts, CLI smoke evidence, FSI
  evidence, and full-suite verification.
- 🟢 [x] Implement `005-specify-command` by adding `fsgg-sdd specify`, typed
  specification ids and parser contracts, safe specification create/rerun and
  refusal behavior, specification summaries in command reports, generated-view
  currency reporting and refresh where source data is valid, deterministic
  JSON, text projection, dry-run behavior, optional Governance compatibility
  facts, CLI smoke evidence, FSI evidence, and full-suite verification.
- 🟢 [x] Implement `006-clarify-command` by adding `fsgg-sdd clarify`, typed
  clarification question ids and parser contracts, safe clarification
  create/rerun behavior, durable decisions and accepted deferrals, missing
  answer and unsafe-change diagnostics, clarification summaries in command
  reports, generated-view currency reporting and refresh where source data is
  valid, deterministic JSON, text projection, dry-run behavior, optional
  Governance compatibility facts, CLI smoke evidence, FSI evidence, and
  full-suite verification.
- 🟢 [x] Implement `007-checklist-command` by adding `fsgg-sdd checklist`, typed
  checklist item/result ids and parser contracts, safe checklist create/rerun
  behavior, durable requirements-quality results, failed-quality and stale
  result diagnostics, checklist summaries in command reports, generated-view
  currency reporting and refresh where source data is valid, deterministic
  JSON, text projection, dry-run behavior, optional Governance compatibility
  facts, CLI smoke evidence, FSI evidence, and full-suite verification.
- 🟢 [x] Implement `008-plan-command` by adding `fsgg-sdd plan`, typed plan
  decision/contract/verification/migration/generated-view ids and parser
  contracts, safe plan create/rerun behavior, durable planning decisions,
  accepted deferral visibility, stale decision and unsafe-change diagnostics,
  plan summaries in command reports, generated-view currency reporting and
  refresh where source data is valid, deterministic JSON, text projection,
  dry-run behavior, optional Governance compatibility facts, CLI smoke
  evidence, FSI evidence, performance evidence, and full-suite verification.
- 🟢 [x] Implement `009-tasks-command` by adding `fsgg-sdd tasks`, typed
  `tasks.yml` facts and parser contracts, task source snapshots, task graph
  derivation, safe task create/rerun behavior, stable task ids, stale task
  visibility, dependency/evidence/status diagnostics, task summaries in command
  reports, generated-view currency reporting and refresh where source data is
  valid, deterministic JSON, text projection, dry-run behavior, optional
  Governance compatibility facts, CLI smoke evidence, FSI evidence,
  performance evidence, and full-suite verification. Evidence is recorded in
  `specs/009-tasks-command/readiness/`.
- 🟢 [x] Implement `010-analyze-command` by adding `fsgg-sdd analyze`, the
  generated `readiness/<id>/analysis.json` contract, analysis summaries in
  command reports, tasks-ready prerequisite diagnostics, authored-source
  preservation, dry-run reporting, deterministic JSON/text projection, optional
  Governance compatibility facts, CLI smoke evidence, FSI evidence,
  performance evidence, and full-suite verification. Evidence is recorded in
  `specs/010-analyze-command/readiness/`.
- 🟢 [x] Implement `011-evidence-command` by adding `fsgg-sdd evidence`, the
  schema-versioned `work/<id>/evidence.yml` contract, evidence summaries in
  command reports, analysis-ready prerequisite diagnostics, safe authored
  evidence writes, dry-run reporting, deterministic JSON/text projection,
  optional Governance compatibility facts, CLI JSON/dry-run/text smoke
  evidence, FSI evidence, performance evidence, and full-suite verification.
  Evidence is recorded in `specs/011-evidence-command/readiness/`.
- 🟢 [x] Implement `012-verify-command` (`fsgg-sdd verify`): SDD-owned verification
  readiness over task/evidence/test/skill obligations, emitting
  `readiness/<id>/verify.json` and pointing verification-ready work to ship.
- 🟢 [x] Implement `013-ship-command` (`fsgg-sdd ship`): SDD-owned merge-boundary
  readiness emitting `readiness/<id>/ship.json` and pointing ship-ready work to
  the Governance-owned protected-boundary handoff.
- 🟢 [x] Implement `014-agent-guidance` (`fsgg-sdd agents`): per-target Claude/Codex
  command and skill guidance generated from `readiness/<id>/work-model.json`.
- 🟢 [x] Implement `015-refresh-command` (`fsgg-sdd refresh`): regenerate SDD-owned
  generated views and report `analysis.json`/`verify.json`/`ship.json` currency.
- 🟢 [x] Implement `016-bootstrap-migration`: end-to-end quickstart, migration
  guidance, Governance-after-init boundary, and a no-Governance lifecycle smoke
  harness (Phase 9 SDD slice).
- 🟢 [x] Implement `017-governance-handoff`: the versioned SDD→Governance handoff
  contract (accepted at v1.0.0, ADR 0002; Governance-side consumer pending).
- 🟢 [x] Implement `018-release-readiness`: frozen/versioned public contracts,
  versioning policy, compatibility matrix, schema reference, and golden baselines.
- 🟢 [x] Implement `019-spectre-rendering`: the rich `--rich` Spectre.Console
  projection over the same `CommandReport`.
- 🟢 [x] Implement `020-exhaustive-validation` (`fsgg-sdd validate`): the scheduled
  exhaustive validation harness emitting one deterministic `validation-report`.
- 🟢 [x] Implement `021-rich-validation-report`: `fsgg-sdd validate --rich` renders
  the `validation-report` via Spectre, closing the last SDD-owned deferral.

The SDD-owned implementation slice (Phases 1, 3, 4, 6, 7, 8, 9, and 13) is
complete as of 2026-06-21. All remaining open roadmap items are **Governance-owned**
(Phases 2, 5, 10, 11, 12, and the Governance rows of Phase 13) and are implemented
in the sibling `FS.GG.Governance` repository, not here.

### Phase 1: SDD Artifact Model

Owner: `FS.GG.SDD`.

Purpose: define the typed lifecycle contract before SDD commands or generators
exist.

- 🟢 [x] Create the first feature spec for `001-sdd-artifact-model`.
- 🟢 [x] Define `WorkId`, `Stage`, `RequirementId`, `DecisionId`, `TaskId`,
  `EvidenceId`, `ArtifactRef`, `SchemaVersion`, source digest, and generator
  version types.
- 🟢 [x] Specify SDD-owned schemas for `.fsgg/project.yml`, `.fsgg/sdd.yml`, and
  `.fsgg/agents.yml`.
- 🟢 [x] Specify schemas for `work/<id>` metadata, structured front matter where
  used, `tasks.yml`, and `evidence.yml`.
- 🟢 [x] Define diagnostic ids for missing artifacts, malformed schema versions,
  duplicate ids, unknown references, stale generated views, and
  prose/structured mismatch.
- 🟢 [x] Define the first SDD lifecycle rule contracts for required spec sections,
  plan obligations, task graph shape, evidence declarations, loaded skills, and
  test obligations.
- 🟢 [x] Express lifecycle rules in a Governance-compatible check model without
  implementing route/profile/freshness/gate semantics in SDD.
- 🟢 [x] Define conflict behavior for requirement ids, task references, decision
  references, status, dependency, owner, and required-evidence disagreement.
- 🟢 [x] Add `.fsi` signatures before implementation.
- 🟢 [x] Add semantic tests for schema validation, id stability, conflict
  diagnostics, stale-view diagnostics, and deterministic ordering.
- 🟢 [x] Record compatibility boundaries for Governance-owned `.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` without implementing
  Governance semantics.

Status: complete on 2026-06-19. The implemented library is
`src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj`; verification evidence is
recorded in `specs/001-sdd-artifact-model/readiness/`.

Exit criteria:

- The repository has a packable SDD artifact-model library.
- Public signatures define the SDD machine contract.
- Fixtures cover valid, malformed, duplicate-id, unknown-reference,
  prose/structured mismatch, and stale-view cases.
- Lifecycle rule fixtures explain what a consumer must fix in specs, plans,
  tasks, evidence, skills, and test declarations.
- The plan for every lifecycle artifact identifies authored source, structured
  model, generated view, stale behavior, and diagnostics.

### Phase 2: Governance Ship Walking Skeleton And Catalog MVP — 🟢 SHIPPED (handoff consumer pending)

Owner: `FS.GG.Governance`; SDD provides optional lifecycle inputs.

Purpose: prove the protected-boundary value early, as required by the design,
without waiting for the full lifecycle command suite.

Status (rechecked 2026-06-21 against the sibling repo
`/home/developer/projects/FS.GG.Governance`, `main` @ `558ebd9`): the walking
skeleton is **shipped**, and the enforcement audit chain has now fully landed.
Governance has merged **F014** (`.fsgg` typed schemas) → **F025** (audit.json
projection, `FS.GG.Governance.AuditJson`, merge `558ebd9`); **~19 src projects**
(38 `.fsproj` incl. tests). Since the prior recheck,
**F023** enforcement/effective-severity (`FS.GG.Governance.Enforcement`), **F024**
ship verdict rollup (`FS.GG.Governance.Ship`), and **F025 audit-json projection**
(`FS.GG.Governance.AuditJson` — the pure, total, deterministic
`AuditJson.ofShipDecision : ShipDecision -> string` plus a `schemaVersion`
constant) are all **merged**. **F026 `fsgg ship` host command**
(`FS.GG.Governance.ShipCommand`) is now **🟢 merged** (Governance `main` @ `8fd9517`,
"Merge F026"), and **F027** (`027-branch-protection-guidance`) — the first GitHub
Actions branch-protection guidance, the **Phase-2 closing row** — is **🟢 landing** as a
docs+template deliverable (no new F# code; `docs/ci/` + `scripts/check-ship-ci-guidance.sh`,
16/16 cross-check assertions green). With it, Phase 2's product surface is complete except
the **cache-eligibility/freshness** verdict's host emission (the one remaining 🟡 below): the
cache-eligibility **core** (F041) and its deterministic **`cache-eligibility.json` projection**
(F042, merged 2026-06-22) have now landed, so the evaluated verdict exists as a projection —
only the CLI host wiring that resolves `FreshnessInputs` and embeds it into the route/audit
JSON remains.
The SDD→Governance handoff contract is accepted at **v1.0.0** (ADR 0002),
but its Governance-side **consumer is still not implemented** (grep over Governance
`src/`+`tests/` finds zero `governance-handoff` references; reader → evidence
adapter → gate decision remain queued) — the seam stays one-directional and
optional.

Legend: 🟢 complete · 🟡 partial (core landed; emission/wiring deferred) ·
🔴 not started.

- 🟢 [x] Define versioned `.fsgg/project.yml`, `.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` MVP schemas in Governance.
  (Governance **F014**.)
- 🟢 [x] Include the minimum capability catalog fields: domains, path map,
  surfaces, checks, cost, owner, environment, and maturity. (F014/**F015**.)
- 🟢 [x] Implement deterministic glob precedence for path-to-capability routing.
  (**F015**.)
- 🟢 [x] Add git/CI snapshot facts for base ref, head ref, changed paths, dirty
  paths, untracked paths, branch, PR labels, status checks, and CI context.
  (**F016**.)
- 🟢 [x] Add unknown governed path findings only inside governed roots or protected
  boundaries. (**F017**.)
- 🟢 [x] Define typed `GateId` metadata with prerequisites, cost, timeout, owner,
  maturity, product-check flag, and freshness key. (**F018** typed gate registry.)
- 🟢 [x] Add `fsgg route --paths ...`, `fsgg route --since <rev>`, and
  `fsgg ship --mode gate --profile standard --json`. (Route selection **F019** +
  `fsgg route` host command **F022** landed; the `fsgg ship --mode gate` verdict
  rollup landed as **F024** `FS.GG.Governance.Ship`.)
- 🟢 [x] Emit deterministic route and audit JSON with selected gates, matched
  rules, unmatched governed paths, expected artifacts, cost, cache eligibility,
  profile-adjusted enforcement, and exit-code basis. (`route.json` **F020** +
  `gates.json` **F021** shipped; profile-adjusted **enforcement** effective
  severity landed as **F023**; the `audit.json` projection **shipped** as **F025**
  `FS.GG.Governance.AuditJson` — verdict/exit-code-basis + blockers/warnings/passing
  with full six-field enforcement carry. The Phase-11 freshness and evidence-reuse
  **cores** landed (F029/F030), and the per-gate **cache-eligibility roll-up core**
  (**F041** `FS.GG.Governance.CacheEligibility`) plus its deterministic, versioned
  **`cache-eligibility.json` projection** (**F042** `FS.GG.Governance.CacheEligibilityJson`
  — pure, total `ofReport : CacheEligibilityReport -> string`, schema
  `fsgg.cache-eligibility/v1`, merged 2026-06-22) landed in Governance. The per-gate
  **freshness-inputs resolution (join) core** landed as **F043**
  (`FS.GG.Governance.FreshnessResolution` — `resolve : Gate list -> SensedFacts ->
  FreshnessResolutionReport`, no-hide `Unresolved`, `candidate` bridge into F041,
  merged 2026-06-22), and the cache-eligibility **host wiring** landed as **F044**
  (`FS.GG.Governance.CacheEligibilityCommand` — the `fsgg cache-eligibility` edge that
  senses each gate's facts from the real repo, calls F043 `resolve`, runs F041
  `evaluate` against a read-only F030 store, and emits a deterministic standalone
  `cache-eligibility.json` (F042 verbatim) + a no-hide `cache-eligibility.unresolved.json`
  sidecar; merged 2026-06-22). The verdict **embed** *into* the route/audit JSON then
  landed as **F045** (`FS.GG.Governance.RouteJson`/`AuditJson` now carry the per-gate
  `cacheEligibility` verdict, merged 2026-06-22), and **real** cache-eligibility verdicts
  now flow from the host commands themselves as **F046** (`FS.GG.Governance.FreshnessSensing`
  shared sensing edge + `RouteCommand`/`ShipCommand` emitting real verdicts from `fsgg
  route`/`fsgg ship`, merged 2026-06-22) — so route/audit JSON carry a real, embedded,
  recompute-safe cache-eligibility verdict end-to-end. The store **write half** (so the
  cache can warm across runs) landed as the pure core **F047**
  (`FS.GG.Governance.EvidenceReuseStore` — `serialise` the byte-stable inverse of the F046
  reader + bounded `retain` + superseded-world `prune`, all pure/total/recompute-safe,
  merged 2026-06-22); the impure on-disk store **write wiring** into `fsgg route`/`fsgg
  ship` and real evidence-reference capture are the remaining deferred host rows.)
- 🟢 [x] Publish the first GitHub Actions guidance for branch protection.
  (Governance **F027** `027-branch-protection-guidance` — a docs+template deliverable, no
  new F# code: the guidance `docs/ci/github-actions-branch-protection.md` + copyable
  workflow `docs/ci/templates/fsgg-ship.yml` wire the `fsgg ship` exit code into a GitHub
  required status check, fail-closed, with `audit.json` surfacing. `scripts/check-ship-ci-guidance.sh`
  is the Principle-V evidence: it lints the template and cross-checks the documented
  exit-code map against the live `Loop.exitCode` (F026) and the audit field set against the
  F025 contract — 16/16 assertions green. `.github/workflows/` stays empty (self-hosting
  deferred); the SC-001 live-GitHub sandbox is the one disclosed manual step.)

Exit criteria:

- A generated product can run a minimal ship gate before the full SDD lifecycle
  exists.
- Routine unclassified files do not trigger global default-deny behavior.
- Unknown paths under declared governed roots produce explicit findings.
- Route and audit JSON explain every selected protected-boundary gate.

### Phase 3: Normalized Work Model

Owner: `FS.GG.SDD`.

Purpose: turn authored lifecycle artifacts into the single machine-readable SDD
contract consumed by humans, agents, CI, and optional Governance tooling.

- 🟢 [x] Parse `.fsgg` and `work/<id>` authored sources into a `WorkModel`.
- 🟢 [x] Emit `readiness/<id>/work-model.json` with model version, source paths,
  source digests, schema versions, generator version, and diagnostics.
- 🟢 [x] Guarantee byte-stable JSON for identical source trees.
- 🟢 [x] Prefer structured graph data for execution when Markdown prose disagrees,
  keep prose visible, and emit a consistency diagnostic.
- 🟢 [x] Emit `requirementNotTyped` when a Markdown requirement id is missing from
  the normalized model.
- 🟢 [x] Emit `workModelInconsistent` when structured tasks reference unknown
  requirements or decisions.
- 🟢 [x] Report stale or missing generated work models.
- 🟢 [x] Document schema migration behavior and compatibility rules.

Status: complete on 2026-06-19. The implementation extends
`src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj`; verification evidence is
recorded in `specs/002-normalized-work-model/readiness/`.

Exit criteria:

- Given the same source tree, work-model JSON is byte-stable.
- Diagnostics explain malformed or conflicting input without replacing user
  intent.
- Stale generated models are detectable from source digests and generator
  metadata.

### Phase 4: Native SDD Lifecycle Commands — 🟢 complete

Owner: `FS.GG.SDD`.

Purpose: expose the native spec-driven development stages as SDD commands over
the same model.

Status: complete as of 2026-06-21 (every checklist item below is shipped with
recorded evidence). The implemented slices add
`src/FS.GG.SDD.Commands`, `src/FS.GG.SDD.Cli`, command tests, lifecycle-command
fixture roots, readiness evidence, deterministic init, charter, specify, and
clarify/checklist/plan/tasks/analyze/evidence command reports, and real
filesystem smoke paths for `fsgg-sdd init`, `fsgg-sdd charter`,
`fsgg-sdd specify`, and
`fsgg-sdd clarify`, `fsgg-sdd checklist`, `fsgg-sdd plan`, and
`fsgg-sdd tasks`, `fsgg-sdd analyze`, and `fsgg-sdd evidence`.

- 🟢 [x] Add `fsgg-sdd init` for SDD skeleton creation.
- 🟢 [x] Add `fsgg-sdd charter`.
- 🟢 [x] Add `fsgg-sdd specify`.
- 🟢 [x] Add `fsgg-sdd clarify`.
- 🟢 [x] Add `fsgg-sdd checklist`.
- 🟢 [x] Add `fsgg-sdd plan`.
- 🟢 [x] Add `fsgg-sdd tasks`.
- 🟢 [x] Add `fsgg-sdd analyze`.
- 🟢 [x] Add `fsgg-sdd evidence`.
- 🟢 [x] Keep stateful or I/O command behavior behind `Model`, `Msg`, `Effect`,
  `init`, and `update` boundaries for the implemented init, charter, specify,
  clarify, checklist, plan, tasks, analyze, and evidence slices.
- 🟢 [x] Ensure command output has deterministic JSON for automation and plain text
  for humans for the implemented init, charter, specify, clarify, checklist,
  plan, tasks, analyze, and evidence slices.
- 🟢 [x] Refresh generated SDD views when possible and report stale-view
  diagnostics when not for the implemented charter, specify, clarify, and
  checklist/plan/tasks/analyze/evidence work-model and analysis views.

Current verification evidence for the implemented slice is recorded in
`specs/003-native-sdd-lifecycle-commands/readiness/`: clean Release build,
focused command workflow/init/report/text/Governance-boundary tests, full suite
with 50 passing tests, FSI public-surface transcript, real init interpreter
transcript, and disposable-directory CLI smoke output. Charter verification
evidence is recorded in `specs/004-charter-command/readiness/`: clean Release
build, focused command workflow/charter/generated-view/report/text/
Governance-boundary tests, full suite with 70 passing tests, FSI
public-surface transcript, disposable-directory CLI smoke output, performance
evidence, SDD/Governance boundary review, and artifact traceability.
Specify verification evidence is recorded in
`specs/005-specify-command/readiness/`: clean Release build, focused specify
create/rerun/diagnostic tests, generated-view tests, deterministic
report/text/Governance-boundary tests, command workflow MVU tests, full suite
with 91 passing tests, FSI public-surface transcript, disposable-directory CLI
smoke output, performance evidence, SDD/Governance boundary review, human
summary review, and artifact traceability.
Clarify verification evidence is recorded in
`specs/006-clarify-command/readiness/`: clean Release build, focused clarify
create/rerun/diagnostic tests, generated-view tests, deterministic
report/text/Governance-boundary tests, command workflow MVU tests, full suite
with 114 passing tests, FSI public-surface transcript, disposable-directory CLI
smoke output, performance evidence, SDD/Governance boundary review, human
summary review, and artifact traceability.
Checklist verification evidence is recorded in
`specs/007-checklist-command/readiness/`: clean Release build, focused
checklist artifact and command create/rerun/diagnostic tests, generated-view
tests, deterministic report/text/Governance-boundary tests, command workflow
MVU tests, full suite with 140 passing tests, FSI public-surface transcript,
disposable-directory CLI smoke output, performance evidence,
SDD/Governance boundary review, human summary review, and artifact
traceability.
Plan verification evidence is recorded in
`specs/008-plan-command/readiness/`: clean Release build, focused plan artifact
and command create/rerun/diagnostic tests, generated-view tests,
deterministic report/text/Governance-boundary tests, command workflow MVU
tests, full suite with 168 passing tests, FSI public-surface transcript,
disposable-directory CLI JSON/dry-run/text smoke output, performance evidence,
SDD/Governance boundary review, human summary review, and artifact
traceability.
Tasks verification evidence is recorded in
`specs/009-tasks-command/readiness/`: clean Release build, focused task artifact
and command create/rerun/diagnostic tests, generated-view tests,
deterministic report/text/Governance-boundary tests, command workflow MVU
tests, full suite with 189 passing tests, FSI public-surface transcript,
disposable-directory CLI JSON/dry-run/text smoke output, performance evidence,
SDD/Governance boundary review, human summary review, and artifact
traceability.
Analyze verification evidence is recorded in
`specs/010-analyze-command/readiness/`: clean Release build, focused analysis
view and command tests, generated-view tests, deterministic report/text/
Governance-boundary tests, command workflow MVU tests, full suite, FSI
public-surface transcript, disposable-directory CLI JSON/dry-run/text smoke
output, performance evidence, SDD/Governance boundary review, human summary
review, and artifact traceability.
Evidence verification evidence is recorded in
`specs/011-evidence-command/readiness/`: clean Release build, focused evidence
artifact and command tests, output/boundary tests, full suite with 63 artifact
tests and 152 command tests, FSI public-surface transcript, disposable-directory
CLI JSON/dry-run/text smoke output, performance evidence, SDD/Governance
boundary review, human summary review, and artifact traceability.

Exit criteria:

- A user can create and advance a work item from charter through evidence
  without product source code.
- Commands write authored sources and refresh or diagnose generated views.
- JSON output is deterministic and plain text is presentation only.

### Phase 5: Route Parity, Profiles, And Enforcement Fixtures — 🟢 core + ship + fixtures shipped (per-finding rule-id pending upstream)

Owner: `FS.GG.Governance`.

Status: the pure enforcement core landed in Governance feature
`023-enforcement-effective-severity` (`FS.GG.Governance.Enforcement`) on
2026-06-21 — the typed enforcement vocabulary (six-value `RunMode`, four-value
`Profile`, base/effective `Severity`, `Recognized<'T>`) and the single total,
deterministic `deriveEffectiveSeverity : EnforcementInput -> EnforcementDecision`
mapping (base severity, maturity, run mode, profile) → (effective severity,
reason), reusing F014 `Maturity`/`ProfileId` verbatim. Evidence:
`specs/023-enforcement-effective-severity/readiness/` (28 green tests: full
240-input truth table, totality, determinism, base-carry, no-drop, recognition,
surface drift). The pure decision is the value the later `fsgg ship`/`audit.json`
rows compose.

The **whole-change verdict rollup** then landed in Governance feature
`024-ship-verdict-rollup` (`FS.GG.Governance.Ship`) on 2026-06-21 — the single
total, deterministic `rollup : RouteResult -> RunMode -> Profile -> ShipDecision`
that rolls an already-routed change up into one ship decision: a closed
`Pass`/`Fail` verdict, the deterministic `Blockers`/`Warnings`/`Passing` partition
of every enforced item (each carrying its full F023 `EnforcementDecision`), and a
typed `ExitCodeBasis` (clean vs blocked). It reuses F023 `deriveEffectiveSeverity`
per item and the F019 `RouteResult`/F018 `Gate`/F017 finding values verbatim.
Evidence: `specs/024-ship-verdict-rollup/readiness/` (24 green tests: rollup
verdict/partition, the design's worked example at change scale, determinism +
shuffle-invariance, totality over the cross-product incl. the empty route, the
partition law `|B|+|W|+|P| = N+M`, base-severity carry / no-hide, and surface
drift). Update (rechecked 2026-06-21): the **`audit.json` emission** has now
**shipped** as **F025** `FS.GG.Governance.AuditJson` — the pure, total,
deterministic `AuditJson.ofShipDecision : ShipDecision -> string` (+ `schemaVersion`
`"fsgg.audit/v1"`) that renders the `ShipDecision` to the versioned `audit.json`
document (verdict + exit-code basis verbatim; blockers/warnings/passing each in F024
composite order; every item tagged `kind` with identity + the six-field enforcement
carry; emit-only, no new dependency). Evidence:
`specs/025-audit-json-projection/readiness/` (21 green tests: projection,
determinism + permutation-invariance + version/field-order + exclusion sweep,
six-field/no-hide carry, totality). Update (rechecked 2026-06-22): the **`fsgg ship` host command** that wires the F024
rollup + F025 `audit.json` to a CLI surface has now **shipped** as **F026
`FS.GG.Governance.ShipCommand`** (merged in Governance @ `8fd9517`; 44 green tests) —
snapshot→load catalog→route→select→`Ship.rollup`→emit `audit.json`→map `ExitCodeBasis`
to a blocking process exit (`Clean → 0`, `Blocked → 1`), the protected-branch sibling
of F022 `fsgg route`. The GitHub Actions branch-protection guidance then shipped as
**F027** (`docs/ci`), and the **golden enforcement truth-table + `audit.json`
fixtures** as **F028** (tests/fixtures). Still out of scope here: per-finding rule-id
annotation (un-modeled upstream), `.fsgg/policy.yml` per-class dial map, and the
cache-eligibility/freshness verdict's **host emission** into the route/audit JSON
(Phase 11 cores landed — F029/F030 — the cache-eligibility roll-up core F041 + its
`cache-eligibility.json` projection F042, the freshness-inputs resolution join F043, the
`fsgg cache-eligibility` host F044, the verdict **embed** into route/audit JSON F045, and
**real** verdicts from `fsgg route`/`fsgg ship` F046 have all now landed — so route/audit
JSON carry a real, embedded, recompute-safe cache-eligibility verdict end-to-end. The
evidence-reuse store **write half** landed as F047; only the impure on-disk store-write
wiring that warms the cache between runs remains).

Legend: 🟢 complete · 🟡 partial (pure core landed; emission/wiring deferred) ·
⬜ not started.

Purpose: make route selection, profile strictness, and blocking behavior
explainable and testable.

- 🟢 [x] Parse run modes: `sandbox`, `inner`, `focused`, `verify`, `gate`, and
  `release`. (F023 `recognizeMode` — total, exact-token, six values; CLI flag
  wiring is a later host-edge row.)
- 🟢 [x] Parse Governance profiles: `light`, `standard`, `strict`, and `release`.
  (F023 `recognizeProfile`/`profileOfProfileId` — total, exact-token, four
  values, round-tripping F014 `ProfileId`.)
- 🟢 [x] Parse rule maturity: `observe`, `warn`, `block-on-pr`, `block-on-ship`,
  and `block-on-release`. (Reused verbatim from F014 `Config.Model.Maturity`;
  consumed by F023's derivation.)
- 🟡 [ ] Emit every finding with rule id, verdict, base severity, mode, profile,
  maturity, effective severity, and reason. (F023's `EnforcementDecision` carries
  base severity, mode, profile, maturity, effective severity, and a lever-naming
  reason; **F024** adds the whole-change `Verdict` + `Blockers`/`Warnings`/
  `Passing` rollup and the typed `ExitCodeBasis`; **F025** `audit.json` now **emits**
  all of it (verdict/basis + each item's `kind`/identity + six-field `enforcement`)
  to the versioned document. Remaining partial: per-finding **rule id** is still
  un-modeled upstream, so it is not yet carried.)
- 🟡 [ ] Ensure profiles never hide underlying verdicts, alter rule hashes, or
  remove findings from JSON. (F023 proves base-severity carry (SC-003) and
  no-drop over a finding list (SC-006) at the decision level; **F024** proves the
  no-hide rule at change scale — a relaxed blocker is always a visible
  self-explaining `Warning`, the partition is disjoint + exhaustive (`|B|+|W|+|P|
  = N+M`, SC-006), and base severity is byte-identical out (SC-003). **F025** now
  carries the no-hide rule to the **JSON level** — every emitted item carries both
  `baseSeverity` and `effectiveSeverity` (a relaxed base-`blocking` warning shows
  both, SC-005), every item is rendered with no section overlap and none dropped
  (SC-001), and the exclusion sweep proves no profile/dial value can suppress a
  field (SC-007). The rule-hash guarantee follows once rule-id is modeled upstream.)
- 🟢 [x] Add scoped `--paths` authoring and complete base/head route parity with
  CI. (F022 `fsgg route` + F026 `fsgg ship` share the `--paths`/`--since`/default
  base-head scope surface.)
- 🟢 [x] Generate golden enforcement truth-table fixtures covering routine versus
  fenced routes, base severity, rule tier, all modes, all profiles, all maturity
  levels, and unknown governed paths. (F028 — golden enforcement truth-table
  fixtures over the enforcement dials, building on the F023 base-severity × maturity
  × mode × profile truth table; this closed Phase 5.)
- 🟢 [x] Add representative JSON snapshots for combinations that alter blocking.
  (F028 — golden `audit.json` snapshots over the blocking/relaxing combinations,
  alongside the F025 snapshot tests.)

Exit criteria:

- Local route previews and CI route decisions agree for the same inputs.
- Every enforcement dial has fixture coverage.
- Profile-adjusted blocking is explained without changing rule truth. (Held by
  F023's reason text + base-carry/no-drop guarantees at the decision level.)

### Phase 6: Tasks, Evidence, Verify, And Ship Readiness — 🟢 SDD slice complete (Governance evidence inputs pending)

Owner: `FS.GG.SDD` for task/evidence declarations and SDD readiness;
`FS.GG.Governance` for effective evidence freshness and enforcement.

Purpose: make implementation work and merge readiness inspectable without
turning SDD into the Governance rule engine.

Status (reconciled 2026-06-21): the **SDD-owned slice is complete**. Task-graph
structure, dependencies, ids, owners, required skills, required evidence, and
status/stale transitions landed in feature `009-tasks-command`; required
skill/capability-tag visibility and required test/evidence obligation
dispositions landed in feature `012-verify-command` (which also emits
`readiness/<id>/verify.json`); merge-boundary readiness
(`readiness/<id>/ship.json`) landed in feature `013-ship-command`. The two
remaining boxes are **Governance-owned** effective-evidence freshness inputs and
protected-branch enforcement.

Legend: 🟢 complete · ⬜ not started (Governance-owned).

- 🟢 [x] Validate task graph structure, dependencies, ids, owners, required skills,
  required evidence, and status transitions. (Feature `009-tasks-command`: typed
  task graph with stable ids, owners, dependency/requirement/decision links,
  required-skill and evidence obligations, status, and stale-task diagnostics.)
- 🟢 [x] Check that required Claude/Codex skills or capability tags are available
  before agent-driven task execution. (Feature `012-verify-command`: required
  skill/capability-tag visibility and disposition as a verification-readiness
  fact.)
- 🟢 [x] Derive required test/evidence obligations from lifecycle rules and changed
  artifact impact. (Features `009-tasks-command` + `012-verify-command`: required
  test/evidence obligation dispositions plus stale-source / changed-plan
  detection.)
- 🟢 [x] Parse and normalize evidence declarations.
- 🟢 [x] Distinguish real evidence, accepted deferrals, missing evidence, and
  synthetic evidence disclosures.
- 🟢 [x] Add `fsgg-sdd evidence` or equivalent update command for authored
  declarations.
- 🟢 [x] Add `fsgg-sdd verify` to run selected SDD-owned checks and emit
  `readiness/<id>/verify.json`. (Feature `012-verify-command`.)
- 🟢 [x] Add `fsgg-sdd ship` to produce SDD merge-boundary readiness in
  `readiness/<id>/ship.json`. (Feature `013-ship-command`.)
- ⬜ [ ] Define Governance effective-evidence inputs for freshness, synthetic taint
  propagation, accepted deferrals, and stale evidence. (Governance-owned.)
- ⬜ [ ] Keep protected-branch enforcement decisions in Governance. (Governance-owned.)

Exit criteria:

- Work items can prove what tasks were completed and what evidence supports
  them.
- Verify and ship outputs are stable enough for CI, agents, and optional
  Governance consumers.
- Missing, stale, synthetic, and deferred evidence produces actionable
  diagnostics.
- Task readiness explains missing skills and missing tests before implementation
  or ship.

### Phase 7: Generated Views And Refresh — ✅ SDD slice complete

Owner: Shared.

Status: SDD-owned `fsgg-sdd refresh` complete on 2026-06-20 — see
`specs/015-refresh-command/readiness/`. Governance-owned `fsgg refresh` and
boundary stale-view blocking remain Governance concerns (out of SDD scope).

Purpose: make generated artifacts explicit, reproducible, and currency-checked.

- 🟢 [x] Define a generation manifest shape for source, generated view, renderer,
  generator version, source digest, output digest, and currency gate.
- 🟢 [x] Add an SDD refresh path for lifecycle views:
  `work-model.json`, `summary.md`, and `agent-commands/` are regenerated;
  `analysis.json`, `verify.json`, `ship.json` are currency-reported (re-running
  their generators out of lifecycle order corrupts evidence freshness — see
  `specs/015-refresh-command/tasks.md` Implementation Notes).
- 🟡 [ ] Add Governance `fsgg refresh` for gate metadata, rule catalogs,
  capability docs, skill references, API-surface docs, route projections, and
  baselines. (Governance-owned; out of SDD scope.)
- 🟢 [x] Emit stale-view diagnostics when generated views are older than their
  declared sources.
- 🟡 [ ] Block stale generated views at the configured Governance boundary.
  (Governance-owned; SDD reports, Governance enforces.)
- 🟢 [x] Add snapshot or golden-fixture coverage once a generated view becomes
  public or tool-facing. (Covered by real-evidence `RefreshCommandTests` over
  disposable shipped project trees, per the 014 precedent.)

Exit criteria:

- Generated files can be traced back to declared sources and generator versions.
- Stale views are detected by source and generator digests, not by presence.
- Markdown summaries are rendered from structured JSON.

### Phase 8: Agent Guidance Generation — ✅ COMPLETE

Owner: `FS.GG.SDD`; Governance contributes optional rule/evidence contracts.

Purpose: keep human, Claude, Codex, and future-agent workflows on one lifecycle
contract.

Delivered by feature `014-agent-guidance` (`fsgg-sdd agents`). Evidence:
`specs/014-agent-guidance/readiness/`.

- 🟢 [x] Generate Claude command and skill guidance from the normalized lifecycle
  model.
- 🟢 [x] Generate Codex skill guidance from the same lifecycle model.
- 🟢 [x] Mark generated agent files as generated and include source digests.
- 🟢 [x] Report stale generated agent guidance.
- 🟢 [x] Keep Claude and Codex behavior equivalent when workflow behavior changes
  (shared `NormalizedGuidanceModel` + `behaviorModelDigest` equivalence
  guardrail).
- 🟢 [x] Ensure agent prompts may author Markdown but do not become a second source
  of truth (generated view only; authored sources preserved byte-identical).
- 🟢 [x] If agent guidance writes Markdown, refresh corresponding structured
  models or report stale-view diagnostics.

Exit criteria:

- 🟢 [x] Agent guidance is generated from structured SDD data.
- 🟢 [x] Stale guidance is detected when lifecycle contracts change.
- 🟢 [x] Agent instructions identify the same authored sources and generated views
  as the CLI.

The cross-cutting `fsgg-sdd agents` command derives per-target guidance under
`readiness/<id>/agent-commands/<target>/` (a `guidance.json` manifest plus
`commands.md`/`skills.md` projections) from `readiness/<id>/work-model.json`,
without altering the `charter -> ship` chain
(`nextLifecycleCommand Agents = None`). See
`specs/014-agent-guidance/readiness/artifact-traceability.md` for the full
requirement-to-evidence map.

### Phase 9: Bootstrap And Migration Experience — 🟢 SDD slice complete (runtime templates optional/out of scope)

Owner: `FS.GG.SDD`, with optional FS.GG.Rendering template providers and
Governance policy setup.

Purpose: make FS.GG.SDD useful for new products and existing Spec Kit projects.

Status (reconciled 2026-06-21): the **SDD-owned slice is complete**. The
`.fsgg`/`work/`/readiness skeleton is created by `fsgg-sdd init` (feature
`003-native-sdd-lifecycle-commands`); the end-to-end quickstart, migration
guidance, Governance-after-init integration boundary, and an automated
no-Governance lifecycle smoke harness landed in feature `016-bootstrap-migration`
(evidence: `specs/016-bootstrap-migration/readiness/`). Runtime product-skeleton
templates and FS.GG.Rendering template-provider delegation are explicitly
**optional and out of scope** for the SDD slice.

Legend: 🟢 complete · ⬜ optional / not started.

- ⬜ [ ] Add project templates for a new SDD-governed product skeleton. (Optional;
  kept out of scope by feature `016-bootstrap-migration` — runtime templates are a
  template-provider concern.)
- 🟢 [x] Create `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`, `work/`,
  and initial readiness directories. (Feature `003-native-sdd-lifecycle-commands`
  `fsgg-sdd init`.)
- ⬜ [ ] Optionally call a template provider for runtime code while keeping runtime
  ownership outside SDD. (Optional; out of scope by feature
  `016-bootstrap-migration`.)
- 🟢 [x] Provide migration guidance from existing Spec Kit projects to native SDD
  artifacts. (Feature `016-bootstrap-migration`.)
- 🟢 [x] Preserve standard Spec Kit as a valid development workflow for the SDD repo
  itself. (Feature `016-bootstrap-migration`; affirmed by `CLAUDE.md`.)
- 🟢 [x] Add quickstart docs for `fsgg-sdd init` through `fsgg-sdd ship`. (Feature
  `016-bootstrap-migration`.)
- 🟢 [x] Add smoke tests that create a temporary SDD project and run the lifecycle
  without the Governance gate runtime installed. (Feature `016-bootstrap-migration`
  no-Governance lifecycle smoke harness.)
- 🟢 [x] Document how Governance can add `.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` after SDD initialization.
  (Feature `016-bootstrap-migration` Governance-after-init integration boundary.)

Exit criteria:

- A new project can initialize the SDD skeleton and continue through the native
  lifecycle.
- Existing Spec Kit users have a documented migration path.
- Bootstrap does not assume FS.GG.Rendering, Governance, or a monorepo checkout.

### Phase 10: Capability Catalog And Product Adapter Expansion

Owner: `FS.GG.Governance`, with product facts from FS.GG.Rendering and generated
products.

Purpose: expand beyond the MVP catalog into the product surfaces named by the
design.

- 🔴 [ ] Expand `.fsgg/capabilities.yml` for generated products, package surfaces,
  docs, skills, samples, design artifacts, release surfaces, baselines,
  template profiles, and evidence tags.
- 🔴 [ ] Add generated-product checks in cost tiers: structural scan,
  restore/build, focused tests, full verify, and release validation.
- 🔴 [ ] Ensure generated products can run Governance locally without monorepo
  access.
- 🔴 [ ] Add package/API facts for package projects, public `.fsi` contracts,
  baselines, compatibility notes, and FSI transcripts.
- 🔴 [ ] Add docs/examples facts for FsDocs pages, literate scripts, public API
  docs, links, and reference currency.
- 🔴 [ ] Add skill facts for skill ids, paths, references, capability mappings,
  task skill lists, and optional mirrors.
- 🔴 [ ] Add design/rendering facts for token sources, generated tokens, captures,
  contrast facts, control catalog, and interaction states.
- 🔴 [ ] Keep product vocabulary in adapters and capability catalogs, not in the
  Governance kernel or generic SDD code.

Exit criteria:

- Product surfaces can be routed and checked through declared capabilities.
- New package, docs, skills, generated-product, design, or release surfaces
  cannot be hidden under a governed root without classification.
- Generated-product checks scale by cost tier and explain broad routes.

### Phase 11: Cost, Cache, And Provenance — 🟢 COMPLETE

Owner: `FS.GG.Governance`; SDD supplies source and generated-view digests for
its lifecycle artifacts.

Purpose: keep local authoring cheap while making protected-boundary evidence
auditable.

Status (2026-06-21): **complete** — all six rows landed in `FS.GG.Governance`:
freshness keys (**F029**), evidence reuse (**F030**), route-cost explanation
(**F031**), command records (**F032**), provenance (**F033**), and the closing
sensed-metadata marking + flagged-rendering core (**F034**
`FS.GG.Governance.SensedMetadata`).

- ✅ Define freshness keys over rule hash, artifact hash, command version,
  generator version, base/head, environment class, and output digest.
  _(F029 — `FS.GG.Governance.FreshnessKey`, merged.)_
- ✅ Cache reusable evidence only when all freshness inputs match.
  _(F030 — `FS.GG.Governance.EvidenceReuse` reuse-decision core, merged; the store
  **write half** — `serialise`/`retain`/`prune`, the byte-stable inverse of the F046
  reader plus bounded retention + superseded-world pruning, all pure/total/recompute-safe
  — landed as F047 `FS.GG.Governance.EvidenceReuseStore`, merged 2026-06-22. The impure
  on-disk write wiring that warms the cache between runs is the remaining deferred host row.)_
- ✅ Explain high-cost routes with matched rule, changed path, affected
  capability, selected gate, cost, and cheaper local alternative.
  _(F031 — `FS.GG.Governance.RouteExplain`, merged.)_
- ✅ Record command runs with executable, arguments, working directory,
  environment delta, timeout, exit code, stdout digest, stderr digest, captured
  output path, and duration.
  _(F032 — `FS.GG.Governance.CommandRecord`, merged.)_
- ✅ Include source commit, base/head, rule hash, generator version, artifact
  digests, command records, environment class, and builder identity in
  provenance.
  _(F033 — `FS.GG.Governance.Provenance`, merged.)_ Pure, total
  `Provenance.build` over the nine already-sensed facts → one flat complete value
  carrying all eight declared facts; `canonicalId` renders only the reproducible
  facts to a byte-stable identity (artifact digests a set, command records ordered
  and folded via F032 `CommandRecord.canonicalId` so the sensed durations are never
  read). First core to reference three sibling cores (FreshnessKey + CommandRecord +
  Config), reusing F029/F032/F014 vocabulary verbatim; no new dependency.
- ✅ Mark wall-clock timestamps and durations as sensed or non-deterministic
  metadata when included in deterministic reports.
  _(F034 — `FS.GG.Governance.SensedMetadata`, merged.)_ The structural foundation
  was already there — F032 carries the run duration as a distinct `SensedDuration`
  apart from the reproducible facts and F033 keeps the embedded durations
  structurally excluded from the provenance identity — and F034 adds the missing
  **presentation half**: pure, total `SensedMetadata.markDuration` / `markTimestamp`
  mark an already-measured duration / timestamp (each with its label) as a
  `SensedMetadatum` whose `Value` is a closed `SensedValue` DU, so **the type is the
  flag** — there is no representation of a marked timestamp or duration that is
  reproducible. `render` surfaces one metadatum behind a reserved `!sensed!` marker
  in the F029/F032/F033 tagged, length-prefixed, injective discipline — a form no
  reproducible field tag ever produces, so it is unmistakably distinguishable from a
  reproducible field and unspoofable by its data — and `renderSection` groups a list
  into one order-preserving, separable `!sensed-section!`. Identity-neutral: it
  computes no reproducible identity and references no identity-computing core. Reuses
  F032's `SensedDuration` verbatim (the only genuinely new fact is `SensedTimestamp`);
  no new dependency. **This closes Phase 11** — its sixth and final row.

Exit criteria:

- Expensive evidence is reused only when freshness is defensible.
- Route reports explain cost and cheaper local alternatives.
- Audit records are sufficient to explain builds, tests, packs, template
  instantiation, git diffs, package inspection, and visual capture.

### Phase 12: Agent-Reviewed Rule Guardrails — 🟢 complete (cache key, invalidation, prompt isolation, review record, advisory promotion, calibration)

Owner: `FS.GG.Governance`; SDD and generated products may provide artifacts
under review.

Purpose: allow judgement-heavy checks without treating uncalibrated agent output
as deterministic proof.

Status (2026-06-22): **all six rows are 🟢 complete** — six pure, total,
deterministic cores shipped in `FS.GG.Governance`, each the analogue of a Phase-11
core specialised to agent-reviewed verdicts and reusing the F029/F032/F035
length-prefixed, injective encoding discipline: (1) the cache-key row — feature
`035-agent-review-cache-key`, `FS.GG.Governance.AgentReviewKey` (analogue of F029
`FreshnessKey`): `compute`/`matches`/`diff`/`value` over the seven judge / prompt /
check / artifact inputs, reusing F029 `RuleHash`/`ArtifactHash` verbatim; (2) the
verdict-store invalidation row — feature `036-agent-verdict-invalidation`,
`FS.GG.Governance.VerdictReuse` (analogue of F030 `EvidenceReuse`): `lookup`/`record`
over a cached-verdict store, Valid iff an entry F035-`matches` on every input, else
`Invalidated` with a located cause (`NoCachedVerdict` or `InputsChanged` naming the
moved inputs), reusing F035 `AgentReviewInputs`/`matches`/`diff` verbatim; (3) the
prompt-isolation row — feature `037-reviewer-prompt-isolation`,
`FS.GG.Governance.PromptIsolation` (structural sibling of F035): `assemble`/`render`
keep trusted reviewer instructions and untrusted governed-artifact content in
separate channels, carry each artifact as a bounded excerpt or a digest, and render
the two channels with an injective, unspoofable data fence, reusing F035
`QuestionText` and F029 `ArtifactHash` verbatim; (4) the review-record row — feature
`038-agent-review-record`, `FS.GG.Governance.ReviewRecord` (the agent-review analogue
of F032 `CommandRecord` / F033 `Provenance`): `build`/`canonicalId`/`identityValue`
assemble one completed review as an immutable record carrying the six audit facts
(F037 `ReviewRequest`, F035 model/prompt identity, F029 `ArtifactHash` digests,
response digest, final verdict) plus F034 `SensedMetadatum` held structurally apart,
and derive a byte-stable, injective `RecordIdentity` over the reproducible facts only
(artifacts as a set; sensed metadata excluded — the F032/F033 honesty boundary),
reusing F037/F035/F029/F034 vocabulary verbatim; and (5) the advisory-promotion row —
feature `039-advisory-promotion-gate`, `FS.GG.Governance.AdvisoryPromotion` (the
agent-review analogue of F023 `deriveEffectiveSeverity` / F030 `decide` / F036
`lookup`): a pure decision core whose `decide : PromotionFacts -> PromotionDecision`
promotes an agent-reviewed finding from advisory to `EligibleToBlock` **iff** one of
the only three permitted bases holds — deterministic backing evidence, a
repeated-review confidence count clearing the threshold at the inclusive
`c >= t && c >= 2` floor, or an explicit human sign-off — naming **every** satisfied
basis, and otherwise **defaults to advisory** with a no-hide reason; the model's own
self-confidence is not a basis and `EligibleToBlock` is necessary-not-sufficient,
reusing F030 `EvidenceRef` verbatim; and (6) the calibration row — feature
`040-calibration-evidence-gate`, `FS.GG.Governance.Calibration` (the agent-review
analogue of F023 `deriveEffectiveSeverity` / F030 `decide` / F036 `lookup` / F039
`decide`): a pure decision core whose `decide : CalibrationThresholds ->
CalibrationEvidence -> CalibrationDecision` is `Calibrated` naming the satisfied
metrics **iff** the judge-vs-human comparison-sample count clears the effective minimum
`max(MinimumSamples, 2)` (a lone sample never calibrates) **and** the observed
agreement clears the threshold at the inclusive `>=` floor, otherwise **defaults to
uncalibrated** with a no-hide reason; the model's own self-confidence is not an input
and a `Calibrated` decision is necessary-not-sufficient (the rule-pack-level
prerequisite for moving beyond advisory maturity), reusing F035 model/prompt identity
and F038 `RecordedVerdict` verbatim. All six are purely additive (no merged core or
`surface/*.surface.txt` baseline changed); F035 30 tests, F036 39 tests, F037 35
tests, F038 28 tests, F039 33 tests, F040 27 tests pass (incl. determinism, purity,
injectivity, advisory-default, uncalibrated-default, the inclusive no-single-sample
comparators, surface-drift). **Phase 12 is closed.**

Legend: 🟢 complete · 🟡 partial (core landed; emission/wiring deferred) ·
🔴 not started.

- 🟢 [x] Cache agent-reviewed verdicts by model id, model version, reviewer prompt
  hash, model configuration, check hash, artifact hashes, and question text.
  (F035 `FS.GG.Governance.AgentReviewKey` — pure key core.)
- 🟢 [x] Invalidate cached verdicts when judge identity or prompt identity changes.
  (F036 `FS.GG.Governance.VerdictReuse` — pure verdict-store + invalidation-decision core.)
- 🟢 [x] Separate governed artifact content from reviewer instructions and pass it
  as bounded data or digests.
  (F037 `FS.GG.Governance.PromptIsolation` — pure channel-separation + bounded-capture + injective-render core.)
- 🟢 [x] Record review requests, response digests, model identity, prompt identity,
  artifact digests, and final verdict.
  (F038 `FS.GG.Governance.ReviewRecord` — pure record-assembly + injective record-identity core.)
- 🟢 [x] Keep agent-reviewed findings advisory until deterministic backing
  evidence, repeated-review confidence thresholds, or explicit human sign-off
  exists. (F039 `FS.GG.Governance.AdvisoryPromotion` — pure advisory-to-blocking
  promotion-gate decision core: `decide` is `EligibleToBlock` naming every satisfied
  basis iff ≥1 of the three permitted bases holds (deterministic backing evidence,
  repeated-review confidence at the inclusive `c >= t && c >= 2` floor, or human
  sign-off), else defaults to advisory with a no-hide reason; self-confidence is not a
  basis, eligibility is necessary-not-sufficient.)
- 🟢 [x] Define judge-vs-human calibration evidence before any agent-reviewed rule
  can block protected boundaries. (F040 `FS.GG.Governance.Calibration` — pure
  calibration-evidence decision core: `decide : CalibrationThresholds ->
  CalibrationEvidence -> CalibrationDecision` is `Calibrated` naming the satisfied
  metrics iff the judge-vs-human comparison-sample count clears the effective minimum
  `max(MinimumSamples, 2)` (a lone sample never calibrates) and the observed agreement
  clears the threshold at the inclusive `>=` floor, else defaults to `Uncalibrated`
  with a no-hide reason (`NoCalibrationEvidence`/`TooFewSamples`/`AgreementBelowThreshold`);
  the model's own self-confidence is not an input, and a `Calibrated` decision is
  necessary-not-sufficient — calibration is the rule-pack-level prerequisite that
  composes with F039 per-finding eligibility and the F023/F024 enforcement machinery
  before a protected boundary can block. Reuses F035 model/prompt identity + F038
  `RecordedVerdict` verbatim.)

Exit criteria:

- Agent-reviewed outputs are auditable and prompt-isolated.
- Missing or stale reviews are visible findings.
- Protected-branch blocking does not depend on uncalibrated agent judgement.

### Phase 13: Release And Distribution Readiness — 🟢 SDD slice complete (Governance release gates pending)

Owner: `FS.GG.Governance` for release gates; `FS.GG.SDD` for SDD package and
CLI distribution once its lifecycle surface is stable.

Purpose: prepare SDD and Governance-managed products for versioned release.

Status (2026-06-21): the **SDD-owned distribution slice is complete** — feature
`018-release-readiness` froze/versioned/documented the public contracts, feature
`019-spectre-rendering` shipped the rich `CommandReport` projections, feature
`020-exhaustive-validation` shipped the scheduled exhaustive validation harness
(`fsgg-sdd validate`), and feature `021-rich-validation-report` closed the last
SDD-owned deferral by making `validate --rich` render the `validation-report`
richly. The remaining rows are **Governance-owned release/provenance gates**.

Legend: 🟢 complete · 🟡 partial · 🔴 not started.

- 🟢 [x] Add package identity and versioning policy for `FS.GG.SDD.*`. (Feature
  018: single `Directory.Build.props <Version>`, `versioning-policy.md`.)
- 🟢 [x] Add SDD release checklist and compatibility matrix for Spec Kit and
  Governance versions. (Feature 018: `compatibility-matrix.md`,
  `release-readiness.json` `compatibility[]`.)
- 🟢 [x] Add CLI installation docs. (Feature 018: `installation.md`, `dotnet tool`
  packaging of `fsgg-sdd`.)
- 🟢 [x] Add generated artifact schema documentation. (Feature 018:
  `schema-reference.md` projected from `release-readiness.json`.)
- 🟢 [x] Add baseline fixtures for public schemas and command output. (Feature 018:
  golden baselines + `ReleaseContract.evaluate` conformance.)
- 🟢 [x] Add migration notes for breaking schema or command changes. (Feature 018:
  `docs/release/migrations/` + additive-only posture.)
- 🟡 [ ] Define Governance `fsgg verify` and `fsgg release` schemas and exit
  codes. (Governance: ship verdict rollup landed as **F024**; the `release` gate
  schema is pending.)
- 🔴 [ ] Add release rules for version bumps, package metadata, template pins,
  publish plans, trusted publishing, and provenance. (Governance-owned; Phase 11.)
- 🟢 [x] Add Spectre.Console projections backed by the same report objects used for
  JSON. (Feature `019-spectre-rendering`: `--rich` projection over the same
  `CommandReport`. Feature `021-rich-validation-report` extends the same edge to the
  `validation-report`: `fsgg-sdd validate --rich` renders verdict + summary counts +
  per-matrix rollup + every non-passing cell via Spectre, degrading to plain text
  when non-interactive/color-disabled. Two additive `Rendering` functions; no
  `validation-report` JSON/schema/matrix change. 437 tests green.)
- 🟢 [x] Add scheduled exhaustive validation for broad matrices. (Feature
  `020-exhaustive-validation`: `fsgg-sdd validate` cross-cutting harness over the
  lifecycle-output / determinism / baseline-conformance / compatibility matrices,
  one deterministic `validation-report`, no Governance runtime required. The
  concrete CI cron wiring is operational config, out of scope.)

Exit criteria:

- SDD packages and CLI can be versioned and released with clear compatibility
  guarantees.
- Public schemas, generated views, and command JSON have documented stability
  rules.
- Breaking changes require explicit migration notes.
- Release gates support package, publish, and provenance evidence.

## First Features

The first SDD implementation feature is:

```text
001-sdd-artifact-model
```

It must not add lifecycle commands yet. It defines the artifact model, schema
versioning posture, id types, diagnostics, and deterministic JSON fixtures that
later commands and generators use.

The first Governance implementation slice from the design is:

```text
ship-walking-skeleton-and-catalog-mvp
```

It belongs in FS.GG.Governance and proves
`fsgg ship --mode gate --profile standard --json` with a minimal capability
catalog before the full lifecycle command suite is complete.

## Design Acceptance Trace

| Design acceptance item | Planned coverage |
|---|---|
| Start as a greenfield project through `fsgg-sdd init`. | SDD bootstrap and migration phase; optional template-provider delegation. |
| Spec-drive work through charter, specify, clarify, checklist, plan, tasks, analyze, implement, verify, and ship. | SDD artifact model, work model, lifecycle commands, task/evidence, verify, and ship phases. |
| Declare project policy, capabilities, work, and evidence in `.fsgg` and `work/<id>`. | SDD source model plus Governance policy, capability, and tooling schemas. |
| Produce deterministic SDD readiness without the Governance gate runtime installed. | SDD work model, lifecycle commands, verify, ship, generated views, and refresh phases. |
| Route a local scoped change cheaply and explain selected gates. | Optional Governance ship skeleton, route parity, cost/cache, and capability expansion phases. |
| Distinguish routine unclassified files from unknown governed paths. | Optional Governance catalog MVP and route parity phases. |
| Run `fsgg ship --mode gate --profile standard --json` as a protected boundary after SDD readiness exists. | Governance ship walking skeleton phase. |
| Refresh generated views from declared sources and detect drift. | Shared generated views and refresh phase. |
| Validate public package surfaces, docs, examples, skills, design artifacts, generated consumers, and release metadata. | Governance capability and product adapter expansion plus release readiness phases. |
| Cache fresh expensive evidence and rerun only when inputs change. | Governance cost, cache, and provenance phase. |
| Emit deterministic route, contract, explain, evidence, and audit JSON. | Governance ship skeleton, route parity, evidence, generated views, and provenance phases. |
| Render useful human CLI output without changing automation truth. | SDD lifecycle commands and Governance release/readiness presentation work. |
| Cover enforcement dials with truth-table fixtures and golden JSON snapshots. | Governance route parity, profiles, and enforcement fixtures phase. |
| Support release checks with package, publish, and provenance evidence. | Governance release and distribution readiness phase. |

## Risks And Mitigations

| Risk | Mitigation |
|---|---|
| The SDD repo accidentally takes ownership of the Governance rule engine. | Keep route, profile, freshness, and enforcement work explicitly assigned to FS.GG.Governance; SDD emits optional contracts only. |
| SDD becomes document ceremony instead of executable project control. | Make spec, plan, tasks, evidence, generated views, and ship readiness machine-checkable from one normalized work model. |
| Markdown and structured artifacts drift. | Prefer structured graph data for execution, keep prose visible, emit conflict diagnostics, and currency-check generated views. |
| Local development becomes oppressive. | Keep SDD usable without Governance; Governance enforces cost budgets, scoped routing, freshness caching, and advisory-first promotion. |
| Profiles hide failures. | Governance always emits underlying verdict, base severity, effective severity, mode, profile, maturity, and reason. |
| Generated views pass because files exist. | Key generated views by source digest, generator version, and output digest. |
| Agent-reviewed checks become uncalibrated blockers. | Keep them advisory until prompt isolation, cache keys, confidence thresholds, and calibration are implemented. |
| Product vocabulary leaks into generic SDD code. | Keep product facts in Governance adapters, Rendering providers, generated-product capability catalogs, and optional SDD contracts. |
| Release/provenance claims overreach. | Emit compatible metadata first; claim formal compliance only after explicit verification. |

## Acceptance Bar

The SDD consumer product is implemented when a consumer can:

1. Start as a greenfield project through `fsgg-sdd init` or an approved FS.GG
   umbrella command that delegates to it.
2. Spec-drive work through charter, specify, clarify, checklist, plan, tasks,
   analyze, implement, verify, and ship.
3. Declare lifecycle policy, work, and evidence in `.fsgg` and `work/<id>`.
4. Produce a deterministic normalized work model.
5. Generate Claude and Codex guidance from the same contract.
6. Run lifecycle commands without the Governance gate runtime installed.
7. Refresh generated views from declared sources and detect drift.
8. Emit deterministic `analysis.json`, `verify.json`, `ship.json`, and
   `summary.md`.
9. Record task and evidence state in structured artifacts.
10. Evolve schemas with explicit migration notes.

The optional Governance integration is implemented when a generated product can:

1. Add Governance policy, capability, and tooling files after SDD initialization.
2. Route a local scoped change cheaply and explain selected gates.
3. Distinguish routine unclassified files from unknown governed paths and
   explain why either does or does not block.
4. Run `fsgg ship --mode gate --profile standard --json` as a protected
   boundary.
5. Validate public package surfaces, docs, examples, skills, design artifacts,
   generated consumers, and release metadata through adapters.
6. Cache fresh expensive evidence and rerun only when relevant inputs change.
7. Emit deterministic route, contract, explain, evidence, and audit JSON.
8. Render useful human CLI output without changing automation truth.
9. Cover enforcement dials with truth-table fixtures and golden JSON snapshots.
10. Support release checks with package, publish, and provenance evidence.

FS.GG.SDD is complete enough for its own first release when it can:

1. Initialize an SDD skeleton.
2. Author lifecycle artifacts in Markdown and structured files.
3. Produce a deterministic normalized work model.
4. Generate Claude and Codex guidance from the same contract.
5. Run lifecycle commands without the Governance gate runtime installed.
6. Optionally expose readiness artifacts that Governance can inspect.
7. Detect stale generated views.
8. Record task and evidence state in structured artifacts.
9. Produce verify and ship readiness JSON.
10. Evolve schemas with explicit migration notes.

The central constraint is unchanged: useful to consumers before Governance is
installed, strict at protected boundaries when Governance is adopted, cheap in
the authoring loop, and explainable everywhere, while SDD remains the lifecycle
product and Governance remains the rule and gate product.
