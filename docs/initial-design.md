# FS.GG.SDD consumer product design

**Timestamp:** 2026-06-18T23:37:18+02:00
**Revision:** 2026-06-19T12:18:24+02:00
**Author:** Codex
**Status:** Consolidated SDD product design with Governance integration context
**Scope:** Define how consumers use FS.GG.SDD to develop projects through a typed spec-driven development lifecycle, and how optional Governance, Rendering, generated-product, and release capabilities integrate with that lifecycle.

## Executive summary

FS.GG.SDD is a consumer-facing spec-driven development product. A consumer uses
it to initialize a project, capture intent, clarify ambiguity, plan the technical
work, generate a typed task graph, record implementation evidence, produce
readiness views, and keep human, CLI, CI, and agent workflows on the same
contract.

The consumer should be able to start with:

```text
fsgg-sdd init ./MyProduct
fsgg-sdd specify create-shell "Build the first useful product slice..."
fsgg-sdd clarify create-shell
fsgg-sdd plan create-shell
fsgg-sdd tasks create-shell
fsgg-sdd analyze create-shell
fsgg-sdd verify create-shell --json
fsgg-sdd ship create-shell --json
```

The advantage is not ceremony. The advantage is that project intent becomes a
typed lifecycle model that tools and agents can trust: requirements have stable
ids, plans and tasks reference those ids, evidence is declared explicitly,
generated views are currency-checked, and optional Governance gates can inspect
the same artifacts without taking over the authoring workflow.

The SDD mechanisms are Governance-powered in a specific sense: SDD defines the
lifecycle rule pack and artifact contracts, while reusable Governance machinery
evaluates checks, explains findings, manages freshness, and enforces gates where
configured. SDD must not create a competing policy engine for questions such as
"what must a spec contain?", "what must a plan justify?", "which skills must be
loaded?", or "which tests are required?". Those questions are SDD lifecycle
rules expressed in a Governance-compatible check model.

FS.GG.Governance remains the rule and gate product. It provides route
selection, evidence freshness, policy profiles, CI enforcement, and release
gates. FS.GG.SDD provides the lifecycle artifacts that consumers author and that
Governance may optionally consume.

The design has these owners:

| Owner | Responsibility |
|---|---|
| `FS.GG.SDD` | Consumer-facing SDD lifecycle product: project init, charter/spec/clarify/checklist/plan/tasks/evidence artifacts, normalized work model, generated SDD views, lifecycle CLI, and agent command/skill generation. |
| `FS.GG.Governance` | Generic rule system, route/evidence/audit outputs, adapters, CLI, profiles, and CI gate contracts. |
| `FS.GG.Rendering` | Rendering packages, templates, samples, product skills, design artifacts, and generated-product assets. |
| Generated products | Project policy, work items, evidence declarations, product-specific capability catalog entries, and readiness artifacts. |

This document is informed by source analysis of FS-Skia-UI, the current
FS.GG.Governance repository, FS.GG.Rendering tooling, and a local generated
product checkout. The design below is the forward path for FS.GG; the source
systems are only inputs.

The primary SDD outcome is:

```text
fsgg-sdd ship <id> --json
```

That command produces deterministic SDD readiness for a work item. It is useful
without the Governance gate runtime installed and can also be consumed by
Governance.

The primary optional Governance outcome is:

```text
fsgg ship --mode gate --profile standard --json
```

That command becomes the protected-branch gate. It recomputes applicable rules
from the base/head change, reports deterministic JSON, blocks only on
profile-adjusted blocking findings, and emits enough evidence to explain every
selected gate.

This revision incorporates the design review constraints that must shape the
next implementation slice:

| Constraint | Design decision |
|---|---|
| Preserve light-by-default routing | Unknown paths are classified findings only inside governed roots or protected boundaries; there is no global default-deny fallthrough. |
| Prove the protected-boundary value early | Build a `ship` walking skeleton before the full lifecycle command suite. |
| Avoid Markdown/schema drift | Gates evaluate a normalized work model with explicit conflict rules. |
| Treat capability scope as foundational | Define a minimal `.fsgg/capabilities.yml` schema in the first slice. |
| Keep policy dials testable | Generate enforcement truth-table fixtures for modes, profiles, maturity, and severities. |
| Do not over-trust agent judgement | Agent-reviewed checks stay advisory until cache, prompt-isolation, confidence, and calibration rules are in place. |

## Design goals

FS.GG.SDD is successful when consumers can:

| Goal | Design consequence |
|---|---|
| Start a project without hidden FS.GG repository knowledge | `fsgg-sdd init` creates the SDD skeleton, policy pointers, work root, and agent guidance targets. |
| Turn intent into executable work | Charter, spec, clarify, checklist, plan, tasks, evidence, verify, and ship all share one typed lifecycle model. |
| Use agents without creating a second source of truth | Claude, Codex, and future agent guidance is generated from the same lifecycle contract used by the CLI. |
| Know what specs, plans, tasks, and evidence must contain | SDD lifecycle rules define required sections, typed ids, references, loaded skills, evidence obligations, and test expectations. |
| Keep Markdown useful but bounded | Markdown stays the authoring surface; schema-versioned structured artifacts are the machine contract. |
| See what is stale, missing, or inconsistent | Generated views include source digests, generator versions, and diagnostics for conflicts or stale outputs. |
| Use SDD without the Governance gate runtime | SDD commands, schemas, generated views, and agent guidance work locally while sharing the same lifecycle rule/check contracts. |
| Opt into stronger gates later | SDD emits normalized readiness JSON that Governance can inspect for routing, freshness, profiles, and enforcement. |
| Build generated products without coupling SDD to one renderer | Rendering/template vocabulary stays in providers, adapters, and capability catalogs, not generic SDD code. |
| Keep local authoring cheap | The normal SDD loop validates the lifecycle model first; expensive route, release, and product checks remain optional or boundary-specific. |
| Keep automation stable | Deterministic JSON is the contract for CLI users, CI, agents, generated views, and optional Governance consumers. |

## Consumer consumption model

Consumers consume FS.GG.SDD in four ways:

| Consumption surface | Consumer action | Product responsibility |
|---|---|---|
| CLI | Run `fsgg-sdd init`, lifecycle authoring commands, `verify`, and `ship`. | Provide stable command contracts, deterministic JSON, useful plain text, and safe failure diagnostics. |
| Repository artifacts | Author `.fsgg/*.yml`, `work/<id>/*.md`, `tasks.yml`, and `evidence.yml`. | Define schema-versioned contracts, migration posture, generated views, and stale-view behavior. |
| Agent guidance | Use generated Claude/Codex commands or skills to author and update lifecycle artifacts. | Generate equivalent agent guidance from the lifecycle model and keep it from becoming a second source of truth. |
| Governance-compatible checks | Let SDD commands evaluate lifecycle rules for artifact shape, loaded skills, evidence obligations, and required tests. | Define the lifecycle rule pack without implementing a separate rule engine. |
| Optional Governance integration | Feed SDD readiness JSON to Governance for routing, profiles, freshness, and gates. | Emit explicit, versioned contracts while remaining useful when the Governance gate runtime is absent. |

The consumer gets these practical advantages:

- fewer ambiguous handoffs between product intent, plan, implementation tasks,
  evidence, agents, and CI;
- stable ids and typed references that make large work items inspectable;
- generated readiness artifacts that explain what is current, stale, missing,
  or inconsistent;
- one project lifecycle that works for humans, agents, local scripts, and CI;
- lifecycle diagnostics that tell the consumer what to fix before planning,
  tasking, implementation, verification, or ship;
- optional protected-boundary rigor without making every local edit expensive;
- a migration path from ordinary Spec Kit projects into FS.GG lifecycle
  artifacts.

## Lifecycle rule contracts

SDD lifecycle mechanics should be expressed as rules/checks over typed
artifacts, not as informal prompt text. The consumer-facing CLI and generated
agent guidance both use these contracts.

| Lifecycle question | SDD contract |
|---|---|
| How can a spec look? | Markdown authoring is allowed, but the spec must expose typed requirement ids, stories, acceptance criteria, scope, non-goals, ambiguity state, and public or tool-facing impact in the normalized model. |
| How must a spec look before planning? | Required sections and typed ids must parse; material ambiguity must be resolved or recorded as an accepted deferral; acceptance criteria must be measurable enough for planning and evidence. |
| How can a plan look? | The plan may be prose-first, but it must declare architecture decisions, contracts, public API impact, dependencies, risks, migration posture, and verification strategy with stable references back to spec requirements. |
| How must a plan look before tasks? | Each requirement that drives work must map to decisions, contracts, risks, verification expectations, and task-generation inputs; conflicts with structured data produce diagnostics. |
| What happens in the task phase? | `tasks.yml` becomes the typed implementation graph: task ids, requirement/decision references, dependencies, owner/agent assumptions, required skills, expected evidence, and status transitions. |
| How do skill checks work? | Tasks declare required skill ids or capability tags. SDD checks whether generated or installed Claude/Codex guidance covers those skills before an agent is asked to perform the work. |
| Which tests should run? | The lifecycle rule pack derives test obligations from change type, artifact impact, public contracts, generated views, and evidence declarations. Governance may route broader product checks from capabilities. |

Initial test-obligation rules should include:

| Trigger | Expected test or evidence |
|---|---|
| Public F# API or `.fsi` contract impact | `.fsi` update, FSI/semantic usage evidence, public-surface tests, compatibility or migration note when needed. |
| Schema or generated-view contract impact | Schema validation fixtures, malformed-input fixtures, deterministic JSON snapshots, stale-view diagnostics, and migration notes. |
| Lifecycle command output impact | JSON contract fixtures, exit-code behavior, plain text projection checks, and filesystem fixture coverage. |
| Agent command or skill generation impact | Generated guidance snapshots, stale-guidance diagnostics, and Claude/Codex behavioral-equivalence checks. |
| Task graph or evidence impact | Dependency/id/reference validation, missing/stale/synthetic/deferred evidence diagnostics, and readiness JSON fixtures. |
| Optional Governance route or gate impact | Route/audit JSON snapshots, profile/mode/maturity truth tables, freshness-key tests, and protected-boundary exit-code checks. |

This is where Governance machinery matters most: the SDD-specific rules say what
is required, and the reusable check machinery evaluates those rules, explains
findings, records evidence, and allows optional profiles or gates to decide what
blocks.

## Non-goals

FS.GG.SDD does not implement renderers, controls, window hosts, product
templates, or sample applications. Template providers such as FS.GG.Rendering
own runtime code and generated product assets.

FS.GG.SDD also does not implement a competing Governance rule engine. SDD owns
the lifecycle rule pack for spec, plan, task, skill, evidence, and generated-view
contracts. Governance owns the reusable evaluator machinery, evidence freshness,
route selection, profile enforcement, protected-branch gates, release gates, and
provenance hardening.

FS.GG.SDD is not a planner or optimizer. Planners, agents, and generators may
propose work. SDD records the lifecycle contract and generated views; Governance
may check those artifacts at defined boundaries.

## Current foundation

The FS.GG.SDD repository is intentionally scaffold-first:

| Capability | Current position |
|---|---|
| Spec Kit scaffold | `.specify/`, constitution, and agent context are present. |
| Product boundary | SDD owns lifecycle artifacts and generated SDD views; Governance owns rules and gates. |
| Design material | This design and `initial-implementation-plan.md` define the initial product shape. |
| Source code | No source projects exist yet; the first feature defines the artifact model before commands. |
| Reference context | Governance and org reference docs are copied under `docs/reference/` as source material, not SDD-owned behavior. |

FS.GG.Governance already has reusable rule, evidence, adapter, host, and CLI
concepts. SDD may integrate with those concepts through explicit versioned
contracts, but the SDD product starts with its own lifecycle artifact model.

## Architecture

The SDD architecture is a lifecycle product architecture. Product knowledge
lives in consumer-authored artifacts, generated views, agent guidance, optional
template providers, and optional Governance contracts.

```text
FS.GG.SDD.Artifacts
  ids, schema versions, artifact references, diagnostics, source digests

FS.GG.SDD.WorkModel
  normalized lifecycle model, conflict rules, deterministic JSON

FS.GG.SDD.LifecycleRules
  spec/plan/task/evidence contracts, skill requirements,
  test obligations, governance-compatible checks

FS.GG.SDD.Commands
  init, charter, specify, clarify, checklist, plan, tasks,
  analyze, evidence/update, verify, ship

FS.GG.SDD.Generators
  work-model, analysis, verify, ship, summary, agent commands/skills

FS.GG.SDD.Cli
  fsgg-sdd command surface, JSON contract, plain text projection

Optional FS.GG.Governance integration
  reusable check evaluation, routes, profiles, freshness,
  gates, audit, release, provenance

Optional template providers and generated products
  runtime code, templates, samples, captures, package surfaces,
  capability catalogs, product-specific source artifacts
```

Dependency rules:

| Layer | Allowed dependencies |
|---|---|
| Artifact and work-model libraries | BCL and FSharp.Core first; YAML/JSON dependencies only where schema parsing requires them. |
| Lifecycle rule pack | SDD-owned contracts expressed in a Governance-compatible check model. |
| Generators and validators | Artifact/work-model libraries, hashing, deterministic serialization, fixture-friendly filesystem abstractions. |
| Commands | MVU-style state/effect boundaries, process and filesystem interpreters at the edge. |
| CLI | Command parsing, JSON, plain text, and optional presentation dependencies. |
| Agent generation | SDD lifecycle model and templates; no independent workflow truth. |
| Governance integration | Optional, versioned contracts only; SDD remains usable without the Governance gate runtime installed. |
| Template providers | Provider-owned runtime and generated-product dependencies; no product-specific knowledge in generic SDD code. |

## Spec-driven development model

The consumer-facing workflow is spec-driven development. A new project starts
from a charter and a specification, not from code. The specification is the
source of truth for the plan, task graph, implementation evidence, generated
views, and ship audit.

FS.GG adopts the same core progression used by GitHub Spec Kit: initialize the
project, define governing principles, specify what and why, clarify ambiguity,
validate requirements quality, create a technical plan, break that plan into
tasks, analyze cross-artifact consistency, and then implement. FS.GG extends the
flow with typed evidence, capability-aware routing, generated-view currency,
product verification, and protected-branch ship/release gates.

The native source model is `.fsgg` plus `work/<id>`. Markdown remains useful for
authoring, but structured files are authoritative for tools and generated views.
This repository's direct command family is `fsgg-sdd`. The broader `fsgg`
command family in examples is an FS.GG umbrella shape that may delegate to SDD
and Governance commands.

| Stage | Purpose | Primary command | Gate posture |
|---|---|---|---|
| `ProjectInit` | Create the product root, SDD policy files, initial work root, and agent guidance targets. | `fsgg-sdd init <path>` | Advisory. |
| `Charter` | Establish project identity, governing principles, lifecycle boundaries, and optional policy pointers. | `fsgg-sdd charter` | Advisory. |
| `Specify` | Capture user value, scope, non-goals, user stories, acceptance criteria, and measurable requirements. | `fsgg-sdd specify <id>` | Advisory. |
| `Clarify` | Resolve ambiguities before planning and record explicit answers. | `fsgg-sdd clarify <id>` | Advisory. |
| `Checklist` | Validate requirements quality before technical planning. | `fsgg-sdd checklist <id>` | Advisory or early fence. |
| `Plan` | Record architecture, public contracts, dependencies, technical choices, migration posture, and evidence plan. | `fsgg-sdd plan <id>` | Advisory with optional route preview. |
| `Tasks` | Define typed work items, dependencies, owners, skills, and required evidence. | `fsgg-sdd tasks <id>` | Advisory or early fence. |
| `Analyze` | Check spec/plan/task consistency before implementation starts. | `fsgg-sdd analyze <id>` | Advisory by default. |
| `Implement` | Complete work and declare task/evidence state. | `fsgg-sdd evidence <id>` or `fsgg-sdd update <id>` | Local SDD checks. |
| `Verify` | Run selected SDD-owned checks and emit readiness facts. | `fsgg-sdd verify <id> --json` | Blocking only where the consumer configures it. |
| `Ship` | Produce merge-boundary SDD readiness for CI or optional Governance. | `fsgg-sdd ship <id> --json` | Readiness output; Governance may enforce. |
| `Release` | Validate SDD package/release metadata for SDD itself or delegate product release gates to Governance. | `fsgg release <id>` or SDD release workflow | Blocking for publication when configured. |

The CLI and agent-facing skills should expose the same stages. Agent prompts may
write the Markdown authoring files; the SDD CLI owns lifecycle schema
validation, normalized work-model generation, SDD readiness views, and
diagnostics. Governance owns route selection, evidence freshness, profile
enforcement, and protected-boundary verdicts when it is installed.

## Greenfield project bootstrap

A consumer must be able to start a new project with the FS.GG SDD flow:

```text
fsgg-sdd init ./MyProduct
fsgg-sdd charter
fsgg-sdd specify create-shell "Build the first useful product slice..."
fsgg-sdd clarify create-shell
fsgg-sdd checklist create-shell
fsgg-sdd plan create-shell --stack "F#, SQLite"
fsgg-sdd tasks create-shell
fsgg-sdd analyze create-shell
fsgg-sdd evidence create-shell
fsgg-sdd verify create-shell --json
fsgg-sdd ship create-shell --json
```

`fsgg-sdd init` creates the SDD skeleton and may ask a product template provider
to generate starter code. SDD owns the lifecycle artifact layout, work
artifacts, agent guidance targets, normalized work model, evidence declaration
schema, and generated SDD readiness views. Governance may later add policy,
capability catalogs, route contracts, freshness checks, and gate enforcement.
The product template provider owns runtime code and generated product assets.

The bootstrap output must be enough for a coding agent to continue the SDD loop
without hidden repository knowledge:

| Output | Purpose |
|---|---|
| `.fsgg/project.yml` | Project identity, default work root, SDD schema version, and optional pointers to Governance policy or capability catalogs. |
| `.fsgg/sdd.yml` | SDD lifecycle policy, artifact layout, generated-view policy, and schema migration posture. |
| `.fsgg/agents.yml` | Agent command and skill generation targets for Claude, Codex, and future agents. |
| Optional `.fsgg/policy.yml` | Governance profile, stage policy, and branch gate expectations when Governance is adopted. |
| Optional `.fsgg/capabilities.yml` | Template profile, package surfaces, tests, docs, skills, samples, and release surfaces for Governance routing. |
| Optional `.fsgg/tooling.yml` | Tool policy, allowed commands, environment classes, and timeouts for Governance/process checks. |
| `work/` | Native SDD artifacts for specifications, plans, tasks, and evidence. |
| Agent commands or skills | Stage-specific prompts for charter, specify, clarify, checklist, plan, tasks, analyze, implement, verify, and ship. |
| Initial readiness directory | Generated SDD work-model, analysis, verify, ship, summary, and optional Governance-facing outputs as they become available. |

## Capability catalog MVP

The capability catalog is foundational for optional Governance integration, not
a late product-adapter detail. The Governance track needs a small, versioned
schema that can classify paths and name the surfaces a generated product expects
to protect. The core SDD workflow remains useful before this file exists.

Minimum viable `.fsgg/capabilities.yml`:

```yaml
schemaVersion: 1
project:
  id: my-product
  workRoot: work

domains:
  - id: workflow
  - id: package-api

pathMap:
  - glob: "src/**"
    capability: package-api
  - glob: "work/**"
    capability: workflow

surfaces:
  - id: public-api
    kind: fsi
    paths: ["src/**/*.fsi"]
    maturity: block-on-ship

checks:
  - id: build
    command: dotnet build
    cost: medium
    environment: local-or-ci
```

Additional package, docs, skills, generated-product, design, and release fields
can grow from that base, but the MVP must already answer three questions:

| Question | Required answer |
|---|---|
| What changed? | A path-to-capability map with deterministic glob precedence. |
| Why did this gate run? | A capability, surface, route rule, cost, and owner. |
| What is unknown? | Paths under governed roots that no catalog entry claims. |

## Source artifacts

The project-level source files define SDD lifecycle scope first, with optional
Governance policy and capability scope layered on top:

| Artifact | Primary owner | Purpose |
|---|---|---|
| `.fsgg/project.yml` | Shared, with SDD-owned lifecycle fields | Project id, default work root, SDD schema version, and optional pointers to Governance policy or capability catalogs. |
| `.fsgg/sdd.yml` | SDD | SDD lifecycle policy, artifact layout, generated-view policy, and schema migration posture. |
| `.fsgg/agents.yml` | SDD | Agent command and skill generation targets for Claude, Codex, and future agents. |
| `.fsgg/policy.yml` | Governance | Optional Governance profiles, default profile, enforcement mapping, branch policy, and review budgets. |
| `.fsgg/capabilities.yml` | Governance with generated-product input | Optional packages, projects, `.fsi` contracts, tests, skills, docs, samples, baselines, template profiles, evidence tags, and release surfaces. |
| `.fsgg/tooling.yml` | Governance | Optional command allow-list, timeouts, environment classes, external tool policy, and tool version expectations. |
| `work/<id>/charter.md` | SDD | Project or work-item principles, boundaries, and lifecycle policy notes. |
| `work/<id>/spec.md` | SDD | Human-authored value, scope, non-goals, user stories, requirements, and acceptance criteria. |
| `work/<id>/clarifications.md` | SDD | Resolved questions and explicit decisions made before planning. |
| `work/<id>/checklist.md` | SDD | Requirements-quality checks and open issues before planning. |
| `work/<id>/plan.md` | SDD | Architecture decisions, public contract impact, dependencies, technical choices, migration posture, and evidence plan. |
| `work/<id>/contracts/` | SDD and product owners | Source contracts or links to canonical contracts such as `.fsi`, OpenAPI, or gRPC definitions. |
| `work/<id>/tasks.yml` | SDD | Typed work items, dependencies, owners, expected skills, and required evidence. |
| `work/<id>/evidence.yml` | SDD for declarations; Governance for effective state | Authored implementation, verification, synthetic, and deferral evidence declarations. |

### Canonical work model

Gates evaluate a normalized work model, not loose prose. Markdown remains the
authoring surface for humans and agents, but only typed identifiers and parsed
sections enter the machine contract. Structured files such as `tasks.yml` and
`evidence.yml` carry graph-shaped data directly.

The SDD CLI should assemble `WorkModel` from the source artifacts, validate it,
and emit `readiness/<id>/work-model.json` as a generated view with model
version, source digests, and parse diagnostics. Conflict rules are explicit:

| Conflict | Gate behavior |
|---|---|
| Markdown requirement id is missing from the normalized model | Emit `requirementNotTyped`; advisory before planning, blocking at ship when cited by changed work. |
| Structured task references an unknown requirement or decision | Emit `workModelInconsistent`; block at verify/ship under standard and stricter profiles. |
| Markdown and structured data disagree on status, dependency, owner, or required evidence | Prefer the structured graph for execution, keep the prose visible, and emit a consistency finding. |
| Generated `work-model.json` is stale relative to any source artifact | Emit generated-view drift and handle it through the profile. |

Generated views are outputs and must be currency-checked:

| Artifact | Primary owner | Purpose |
|---|---|---|
| `readiness/<id>/work-model.json` | SDD | Deterministic normalized work model with source digests and diagnostics. |
| `readiness/<id>/analysis.json` | SDD | Cross-artifact consistency diagnostics. |
| `readiness/<id>/verify.json` | SDD | SDD-owned verification results and readiness facts. |
| `readiness/<id>/ship.json` | SDD | Merge-boundary SDD readiness for CI and optional Governance consumers. |
| `readiness/<id>/summary.md` | Shared | Human summary rendered from structured readiness data. |
| `readiness/<id>/agent-commands/` | SDD | Generated agent guidance derived from the same lifecycle model. |
| `.fsgg/gates.json` | Governance | Generated gate registry with ids, metadata, prerequisites, cost, timeout, and owner. |
| `.fsgg/rules.md` | Governance | Rendered rule catalog from reified checks. |
| `readiness/<id>/route.json` | Governance | Matched rules, changed paths, selected gates, unknown-path findings, cost, cache eligibility, and profile-adjusted enforcement. |
| `readiness/<id>/contract.json` | Governance | Rule contracts, required inputs, and source reads. |
| `readiness/<id>/explain.json` | Governance | Proof trees and explanation traces for applicable rules. |
| `readiness/<id>/evidence.json` | Governance | Effective evidence states, taint propagation, freshness, and graph failures. |
| `readiness/<id>/audit.json` | Governance | Ship verdict, blockers, warnings, provenance references, and exit-code basis. |
| `readiness/<id>/attestations/` | Governance | Optional SLSA/in-toto-shaped provenance summaries. |

## Rule packs and adapters

The kernel stays generic; adapters own domain facts and rule packs.

| Adapter | Facts it owns | Example rules |
|---|---|---|
| Workflow | Stage, work artifacts, graph nodes, dependencies, evidence declarations, work policy. | `workGraphWellFormed`, `designSatisfiesIntent`, `evidenceNotSynthetic`, `shipFence`. |
| Git/CI | Base/head, branch, changed paths, dirty paths, untracked paths, PR labels, status checks. | `changedPathsRouted`, `unknownPathDefaultsSafe`, `requiredStatusPresent`. |
| Policy/profile | Default profile, command override, maturity, base severity, effective severity, enforcement reason. | `profileKnown`, `profileAllowedForMode`, `effectiveSeverityExplained`. |
| Cost/cache | Rule cost, historical runtime, freshness keys, artifact hashes, command versions, cache entries. | `evidenceFresh`, `ruleWithinBudget`, `expensiveGateJustified`. |
| Tooling/process | Command specs, environment class, exit code, timeout, output digests, captured output URI. | `commandAllowed`, `commandCompleted`, `outputDigestRecorded`. |
| Package/API | Package projects, public `.fsi`, baselines, compatibility notes, FSI transcripts. | `publicSurfaceHasSignature`, `surfaceBaselineCurrent`, `breakingChangeHasMigration`. |
| Generated product | Template profile, generated root, package pins, product tests, generated guidance. | `templateInstantiates`, `generatedProductBuilds`, `generatedGuidanceCurrent`. |
| Skills | Skill ids, paths, references, capability mappings, product skill lists, optional mirrors. | `skillExists`, `skillContractPathValid`, `declaredSkillLoadedBeforeWork`. |
| Docs/examples | FsDocs pages, examples, reference docs, API docs, links, literate scripts. | `examplesRun`, `publicApiDocumented`, `generatedDocsCurrent`. |
| Design/rendering | Token sources, generated tokens, captures, contrast facts, control catalog, interaction states. | `tokensCurrent`, `contrastPasses`, `interactionStatesCovered`. |
| Build/package | Build and test commands, pack outputs, versions, package metadata, local or staging feeds. | `testsPassed`, `packableProjectsPacked`, `versionBumpedWhenPacked`. |
| Provenance | Source commit, base/head, builder identity, command records, artifact digests, generator versions. | `readinessHasProvenance`, `artifactDigestMatches`, `attestationCurrent`. |

## Gate identities

Product gates need stable identities. A typed `GateId` or generated registry
entry should include:

| Field | Meaning |
|---|---|
| `id` | Stable machine id used in route, evidence, and audit JSON. |
| `domain` | Owning adapter or capability domain. |
| `description` | Human-readable purpose. |
| `prerequisites` | Gates or facts required before this gate runs. |
| `cost` | Cheap, medium, high, or exhaustive. |
| `timeout` | Expected timeout class or explicit duration. |
| `owner` | Failure owner or responsible domain. |
| `maturity` | Observe, warn, block-on-pr, block-on-ship, or block-on-release. |
| `productCheck` | Whether the gate validates generated consumers. |
| `freshnessKey` | Inputs used to decide whether prior evidence can be reused. |

Routes should explain every selected gate in terms of changed path, affected
capability, matching rule, expected evidence, cost, and cheaper local
alternative when one exists.

## Routing safety policy

The system keeps light-by-default as a kernel invariant. An unknown path is not a
global default-deny fallthrough. It is a classified finding emitted only when a
path is inside a governed root or protected surface declared by
`.fsgg/capabilities.yml` but no capability rule claims it, or when the protected
boundary policy explicitly requires all paths in an area to be classified.

| Change class | Route posture |
|---|---|
| Unmanaged notes, reports, drafts, scratch, or experiments | Routine unless a declared fence matches. No blocking gate solely because the path is unclassified. |
| Declared capability path | Route to the matching capability gates. Enforcement depends on mode, profile, severity, and maturity. |
| Unknown path under a governed root | Emit `unknownGovernedPath`; profiles decide whether it warns, blocks at gate, or blocks always. |
| Protected release, package, public API, generated view, or provenance surface | Require explicit capability classification before ship or release. |

This preserves the existing routing contract: heavy checks require a positive
match against declared stakes. At the same time, generated products cannot hide
new package, API, readiness, or release surfaces by placing them under a governed
root without adding capability metadata.

## Modes, profiles, and maturity

Run mode answers where the command is running and what boundary is being
protected. Profile answers how strict the project wants to be at that boundary.
Rule maturity answers whether a rule is trusted enough to block.

| Lever | Examples | Changes truth? |
|---|---|---|
| Run mode | `sandbox`, `inner`, `focused`, `verify`, `gate`, `release` | No. |
| Governance profile | `light`, `standard`, `strict`, `release` | No. |
| Rule maturity | `observe`, `warn`, `block-on-pr`, `block-on-ship`, `block-on-release` | No. |

Every finding reports both base and effective severity:

```text
rule: generated-token-current
verdict: fail
baseSeverity: blocking
mode: inner
profile: light
maturity: block-on-ship
effectiveSeverity: advisory
reason: light profile does not block generated-view drift outside the ship gate
```

Profiles belong in `.fsgg/policy.yml`:

```yaml
governance:
  defaultProfile: standard

profiles:
  light:
    unknownPaths: warn
    staleEvidence: warn
    syntheticEvidence: warn
    uncertainVerdict: warn
    generatedViewDrift: warn
    requireProvenance: false
    maxCost: cheap

  standard:
    unknownPaths: block
    staleEvidence: blockAtGate
    syntheticEvidence: blockAtGate
    uncertainVerdict: warn
    generatedViewDrift: blockAtGate
    requireProvenance: false
    maxCost: medium

  strict:
    unknownPaths: block
    staleEvidence: block
    syntheticEvidence: block
    uncertainVerdict: blockAtGate
    generatedViewDrift: block
    requireProvenance: true
    maxCost: high

  release:
    unknownPaths: block
    staleEvidence: block
    syntheticEvidence: block
    uncertainVerdict: block
    generatedViewDrift: block
    requireProvenance: true
    requirePackEvidence: true
    requirePublishPlan: true
    maxCost: exhaustive
```

`unknownPaths` applies to `unknownGovernedPath` findings from the routing safety
policy, not to every unclassified file in the repository.

The profile can change effective enforcement. It must never hide the underlying
verdict, alter rule hashes, or remove findings from JSON.

Every combination that can alter enforcement must have a golden fixture. The
fixture set should cover at least: routine versus fenced route, advisory versus
blocking base severity, deterministic versus agent-reviewed versus human-only
tier, all run modes, all profiles, all maturity levels, and unknown governed
paths. A profile change is not complete until the truth table and representative
JSON snapshots change with it.

## Cost model

Cost control is part of correctness. SDD keeps the authoring loop cheap by
validating lifecycle structure, generated-view currency, and task/evidence
state first. Optional Governance may run broader route, package, generated
product, and release checks at configured boundaries.

| Requirement | Mechanism |
|---|---|
| Keep SDD authoring fast | `fsgg-sdd analyze`, `fsgg-sdd verify`, and `fsgg-sdd ship --json` operate over lifecycle artifacts before broad product gates. |
| Keep optional routing targeted | Governance route commands such as `fsgg route --paths ...`, `fsgg check --paths ...`, and `fsgg check --since <rev>` route from declared capability metadata. |
| Route from impact facts | Governance maps paths to capabilities, packages, public surfaces, generated views, docs pages, controls, tests, and evidence. |
| Avoid needless reruns | Cache evidence by rule hash, artifact hash, command version, generator version, base/head, environment class, and output digest. |
| Split large gates | Separate structural scans, restore/build, focused product tests, visual captures, full generated-product verify, and release checks. |
| Explain broad routes | Include matched rule, changed path, affected capability, selected gate, cost, and cheaper local alternative. |
| Promote carefully | Start new or heuristic rules as observe/warn before they block PR, ship, or release. |
| Run exhaustive checks at the right boundary | Use ship, release, nightly, or explicit verify for broad matrices. |

Examples:

| Change | Expected route |
|---|---|
| Prose-only docs edit | Docs links, generated-docs currency, and affected examples. |
| Private implementation edit | Affected project build/tests and relevant local rules. |
| Public `.fsi` edit | Surface baseline, FSI transcript, compatibility note, and focused semantic tests. |
| Token-source edit | Token generation, contrast checks for affected roles, and selected visual captures. |
| Template or capability catalog edit | Broad generated-product and package-pin checks, because generated consumers are affected. |

## Command surface

The SDD CLI should expose one lifecycle model through stable machine output and
human projections:

| Surface | Purpose | Contract |
|---|---|---|
| JSON | CI, agents, scripts, readiness artifacts, and optional Governance consumers. | Stable schema, deterministic order, no ANSI, no terminal wrapping, no implicit clock. |
| Plain text | Simple logs and redirected output. | Human-readable, not the automation contract. |
| Optional richer presentation | Interactive or formatted lifecycle views when introduced. | Presentation only over the same report objects. |

Initial SDD commands:

| Command | Purpose |
|---|---|
| `fsgg-sdd init` | Scaffold an SDD-governed project skeleton and optional template-provider hook. |
| `fsgg-sdd charter` | Establish lifecycle principles and boundaries. |
| `fsgg-sdd specify` | Create or update the spec as the source of truth for a work item. |
| `fsgg-sdd clarify` | Resolve ambiguities before planning. |
| `fsgg-sdd checklist` | Validate requirements quality before planning. |
| `fsgg-sdd plan` | Create the technical plan and evidence plan from the spec. |
| `fsgg-sdd tasks` | Generate or update the typed task graph from the plan. |
| `fsgg-sdd analyze` | Check consistency across spec, plan, tasks, contracts, and SDD policy. |
| `fsgg-sdd evidence` / `fsgg-sdd update` | Record task status and authored evidence declarations. |
| `fsgg-sdd refresh` | Regenerate declared SDD views. |
| `fsgg-sdd verify` | Run selected SDD-owned checks and emit `verify.json`. |
| `fsgg-sdd ship` | Emit merge-boundary SDD readiness in `ship.json`. |

Optional Governance commands:

| Command | Purpose |
|---|---|
| `fsgg route` | Show selected gates and route trace for paths, since-rev, or base/head. |
| `fsgg check` | Run selected cheap/focused Governance checks for local authoring. |
| `fsgg evidence` | Compute effective evidence state and freshness. |
| `fsgg verify` | Run profile-appropriate product verification before PR or explicit local validation. |
| `fsgg ship --mode gate --profile standard --json` | Recompute merge policy from base/head and emit the protected-branch verdict. |
| `fsgg release` | Validate pack, publish, version, metadata, and provenance requirements. |
| `fsgg tui` / `fsgg watch` | Optional Governance presentation commands. |

`fsgg-sdd ship <id> --json` is the stable SDD readiness entry point. When
Governance is adopted, `fsgg ship --mode gate --profile standard --json` owns
the protected-branch verdict.

## Readiness and provenance

Generated readiness is structured first and Markdown second. SDD readiness
captures lifecycle sources, generated-view currency, task/evidence declarations,
and command records. Optional Governance can extend that record with route,
freshness, build, test, pack, package inspection, visual capture, and release
evidence.

```text
CommandRun =
  executable
  arguments
  workingDirectory
  environmentDelta
  timeout
  exitCode
  stdoutDigest
  stderrDigest
  capturedOutputPath
  duration
```

Wall-clock timestamps and durations are useful evidence, but deterministic JSON
should only include them when supplied as sensed input or explicitly marked as
non-deterministic metadata.

Provenance records should include:

| Field | Purpose |
|---|---|
| Source commit and base/head | Tie readiness to the diff that was checked. |
| Schema, rule, and generator versions | Detect stale checks after schema, rule, or generator changes. |
| Artifact digests | Detect drift in generated files, lifecycle artifacts, packages, baselines, captures, and docs. |
| Command records | Audit external process results without embedding shell policy. |
| Environment class | Separate local, CI, release, and generated-product environments. |
| Builder identity | Support future SLSA/in-toto-shaped attestations without overclaiming compliance. |

## Optional agent-reviewed constraints

Agent-reviewed rules are useful for judgement-heavy checks, but they are not
deterministic proof. They remain advisory by default until the review system has
operational guardrails and calibration evidence.

| Constraint | Required design response |
|---|---|
| Judge identity drift | Cache keys include model id, model version, reviewer prompt hash, relevant model configuration, check hash, artifact hashes, and question text. A judge or prompt change invalidates prior cached verdicts for that rule. |
| Single-sample noise | Blocking promotion requires either deterministic backing evidence, repeated-review confidence thresholds, or explicit human sign-off. |
| Prompt injection | Governed artifact content is always treated as data, separated from reviewer instructions, and captured through bounded excerpts or digests in the review record. |
| Calibration debt | Agent-reviewed rule packs need periodic judge-vs-human comparison before they can move beyond advisory maturity. |
| Auditability | Review requests, response digests, model identity, prompt identity, artifact digests, and final recorded verdict are part of readiness provenance. |

The initial Governance integration should report agent-reviewed findings and
missing reviews, but protected-branch blocking should come from deterministic
checks, human-only escalations, stale evidence, generated-view drift, or explicit
policy violations until calibration exists. SDD agent guidance may help author
artifacts, but it must not become independent proof.

## Tooling strategy

Durable SDD behavior belongs in compiled F# libraries and CLI commands. Shell
and `.fsx` are allowed for bootstrap, sketches, and documentation examples, but
they should not own stable lifecycle contracts, generated-view truth, or command
output schemas.

| Form | Use |
|---|---|
| Compiled F# libraries | Artifact models, schema parsers, work-model assembly, diagnostics, generation manifests, deterministic JSON. |
| Compiled CLI commands | Stable user/CI contract, exit codes, JSON schemas, and plain text projections. |
| `.fsx` scripts | FSI sketches, exploratory reports, and literate docs. |
| Shell scripts | Bootstrap, tool install, or temporary wrappers around `fsgg-sdd ...`. |

Graduation rule: if a script needs tests, stable JSON, stable exit codes,
readiness artifacts, generated-view currency, CI usage, or required user
documentation, it graduates into compiled F#.

Recommended dependencies stay at the edge:

| Need | Tooling |
|---|---|
| File IO, hashing, JSON | BCL and `System.Text.Json`. |
| External processes | In-house process-runner facade over `System.Diagnostics.Process`, with `CliWrap` considered only if needed. |
| CLI parsing | `System.CommandLine` when command complexity exceeds the current parser. |
| Human terminal UI | `Spectre.Console` in the CLI only. |
| Globs | `Microsoft.Extensions.FileSystemGlobbing`. |
| YAML | `YamlDotNet` with strict FS.GG-owned schemas. |
| MSBuild inspection | MSBuild API and `Microsoft.Build.Locator`; actual builds still use `dotnet build/test/pack`. |
| NuGet inspection | `NuGet.Protocol` and `NuGet.Packaging`; actual publishing still uses official tooling. |
| Git facts | Start with the git CLI through the process runner; consider `LibGit2Sharp` only for read-only cases where the native dependency is acceptable. |

## Implementation roadmap

The active implementation plan is
[initial-implementation-plan.md](initial-implementation-plan.md). That document
owns phase tracking, repo ownership, exit criteria, and feature sequencing. The
design-level sequence below explains the consumer-facing product spine and where
optional Governance work attaches.

### Track 1: consumer-facing SDD spine

- Define the SDD artifact model first: `.fsgg/project.yml`, `.fsgg/sdd.yml`,
  `.fsgg/agents.yml`, `work/<id>` metadata, `tasks.yml`, `evidence.yml`, ids,
  schema versions, source digests, generator versions, and diagnostics.
- Assemble authored SDD sources into a deterministic `WorkModel`; emit
  `readiness/<id>/work-model.json` with source digests and stale-view
  diagnostics.
- Add the lifecycle command spine: `fsgg-sdd init`, `charter`, `specify`,
  `clarify`, `checklist`, `plan`, `tasks`, and `analyze`.
- Add task/evidence declarations plus `fsgg-sdd verify` and `fsgg-sdd ship`
  readiness JSON, while leaving effective freshness and protected-boundary
  enforcement to Governance.
- Generate Claude and Codex guidance from the same lifecycle model so consumers
  can use agents without creating a second source of truth.
- Provide bootstrap, quickstart, and migration paths so new and existing Spec
  Kit projects can adopt FS.GG.SDD without Governance, Rendering, or monorepo
  assumptions.

### Track 2: optional Governance boundary

- Define optional Governance schemas for `.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` without making them required
  for the core SDD workflow.
- Add path-to-capability routing, unknown governed path findings, route traces,
  typed gate metadata, profile/mode/maturity computation, and enforcement
  truth-table fixtures in FS.GG.Governance.
- Add `fsgg route --paths ...`, `fsgg route --since <rev>`, and
  `fsgg ship --mode gate --profile standard --json` as the optional protected
  boundary contract.
- Consume SDD readiness JSON as one input to Governance route, evidence,
  freshness, audit, and release decisions.

### Track 3: product and release expansion

- Expand capability catalogs for generated products, package surfaces, docs,
  skills, samples, design artifacts, release surfaces, baselines, template
  profiles, and evidence tags.
- Add generated-product checks in cost tiers so consumers can keep local SDD
  authoring cheap while still getting broad verification at ship or release.
- Add package/API, docs/examples, skills, design/rendering, provenance, and
  release checks through adapters and provider-owned facts.
- Keep product vocabulary in providers, generated-product capability catalogs,
  and Governance adapters, not in generic SDD lifecycle code.

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| SDD becomes a workflow wrapper instead of a consumer product | Keep the consumer loop centered on `fsgg-sdd init`, lifecycle authoring, normalized work models, generated views, agent guidance, and readiness JSON. |
| SDD becomes document ceremony instead of executable project control | Make spec, plan, tasks, evidence, generated views, and ship readiness machine-checkable from one native artifact model. |
| Making local development oppressive | Enforce cost budgets, scoped routing, freshness caching, maturity levels, and advisory-first promotion. |
| Allowing profiles to hide failures | Always emit underlying verdict, base severity, effective severity, mode, profile, maturity, and reason. |
| Letting terminal UI diverge from automation | Render Spectre.Console views from the same immutable report objects used for JSON. |
| Moving shell policy into untested `.fsx` | Graduate required behavior into compiled libraries and commands. |
| Leaking product dependencies into the kernel | Keep product facts in adapters and capability catalogs. |
| Letting stale readiness pass by presence | Key evidence by source hash, rule hash, command version, artifact digest, base/head, and environment class. |
| Overclaiming provenance | Emit compatible metadata first; claim formal compliance only after explicit verification. |
| New-project bootstrap depends on one template provider | Keep `fsgg-sdd init` template-provider-neutral and require generated products to declare optional capabilities through `.fsgg/capabilities.yml`. |
| Reintroducing oppressive default-deny routing | Treat unknown governed paths as explicit findings under declared roots, not as a global heavy-route fallback. |
| Policy dials become hard to reason about | Maintain golden enforcement truth tables and JSON snapshots for every mode/profile/maturity/severity combination that can change blocking. |
| Markdown and structured work artifacts drift | Gate only on the normalized work model, emit parse/conflict diagnostics, and currency-check `work-model.json`. |
| Agent-reviewed checks become uncalibrated blockers | Keep them advisory until judge identity, prompt isolation, confidence thresholds, and judge-vs-human calibration are implemented. |

## Acceptance bar

The SDD consumer product is implemented when a consumer can:

1. Start as a greenfield project through `fsgg-sdd init`.
2. Spec-drive work through charter, specify, clarify, checklist, plan, tasks,
   analyze, implement, verify, and ship.
3. Declare SDD lifecycle policy, work, and evidence in `.fsgg` and `work/<id>`.
4. Produce a deterministic normalized work model.
5. Generate Claude and Codex guidance from the same lifecycle contract.
6. Run lifecycle commands without the Governance gate runtime installed.
7. Refresh generated views from declared sources and detect drift.
8. Emit deterministic `analysis.json`, `verify.json`, `ship.json`, and
   `summary.md` for SDD readiness.
9. Record task and evidence state in structured artifacts.
10. Evolve schemas with explicit migration notes.

The optional Governance integration is implemented when a generated product can:

1. Add Governance policy, capability, and tooling files after SDD initialization.
2. Route a local scoped change cheaply and explain selected gates.
3. Distinguish routine unclassified files from unknown governed paths and explain
   why either does or does not block.
4. Run `fsgg ship --mode gate --profile standard --json` as a protected
   boundary.
5. Validate public package surfaces, docs, examples, skills, design artifacts,
   generated consumers, and release metadata through adapters.
6. Cache fresh expensive evidence and rerun only when relevant inputs change.
7. Emit deterministic route, contract, explain, evidence, and audit JSON.
8. Cover enforcement dials with truth-table fixtures and golden JSON snapshots.
9. Support release checks with package, publish, and provenance evidence.

The central constraint remains simple: useful to consumers before Governance is
installed, strict at protected boundaries when Governance is adopted, cheap in
the authoring loop, and explainable everywhere.
