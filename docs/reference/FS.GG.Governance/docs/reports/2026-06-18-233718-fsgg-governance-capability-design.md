# FS.GG governance capability design

**Timestamp:** 2026-06-18T23:37:18+02:00
**Revision:** 2026-06-19T12:18:24+02:00
**Author:** Codex
**Status:** Consolidated design update, implementation plan incorporated
**Scope:** Define the FS.GG governance capability envelope for generated products, package surfaces, workflow evidence, design artifacts, documentation, and release gates.

## Executive summary

FS.GG needs a product-neutral governance platform, not a workflow wrapper. The
core system should provide a reusable rule algebra, typed facts, route
selection, evidence propagation, generated readiness, and CI enforcement. Product
domains should plug into that core through adapters and capability catalogs.
It must also provide a native spec-driven development path for new projects:
project charter, specification, clarification, requirements checklist, technical
plan, task graph, analysis, implementation, verification, and ship audit.

The design has three owners:

| Owner | Responsibility |
|---|---|
| `FS.GG.Governance` | Generic rule system, route/evidence/audit outputs, adapters, CLI, profiles, and CI gate contracts. |
| `FS.GG.Rendering` | Rendering packages, templates, samples, product skills, design artifacts, and generated-product assets. |
| Generated products | Project policy, work items, evidence declarations, product-specific capability catalog entries, and readiness artifacts. |

This document is informed by source analysis of FS-Skia-UI, the current
FS.GG.Governance repository, FS.GG.Rendering tooling, and a local generated
product checkout. The design below is the forward path for FS.GG; the source
systems are only inputs.

The primary outcome is:

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

FS.GG governance is successful when it can:

| Goal | Design consequence |
|---|---|
| Govern generated products without coupling to one renderer or template | Put product vocabulary in adapters and `.fsgg/capabilities.yml`, not in the kernel. |
| Let consumers spec-drive new projects | Make the specification lifecycle a native FS.GG command and artifact model, not a compatibility layer. |
| Keep small safe changes cheap | Route from precise impact facts, support scoped authoring, cache fresh evidence, and budget rule cost. |
| Make gates explainable | Emit route traces, rule contracts, proof trees, base severity, effective severity, and profile reasons. |
| Treat generated files as views | Declare source/view/generator relationships and block stale views at the appropriate boundary. |
| Separate truth from enforcement | Rules always report the same verdict; mode and profile only adjust whether a finding blocks. |
| Keep automation stable | Use deterministic JSON for CI, agents, branch protection, and readiness artifacts. |
| Keep humans oriented | Render the same reports through plain text and optional Spectre.Console views. |
| Move durable behavior into typed code | Compile stable governance commands and leave shell or `.fsx` for bootstrap and experiments. |

## Non-goals

FS.GG.Governance does not implement renderers, controls, window hosts, product
templates, or sample applications. It governs those surfaces by sensing facts,
checking contracts, and recording evidence.

FS.GG.Governance is also not a planner or optimizer. Planners, agents, and
generators may propose work. Governance checks the resulting artifacts and
evidence at defined boundaries.

## Current foundation

The repository already has the reusable core:

| Capability | Current position |
|---|---|
| Rule algebra | Reified `Check` values evaluate, render, hash, explain, and report reads. |
| Rule bridge | `CheckRule` separates deterministic, agent-reviewed, and human-only checks. |
| Evidence model | Authored and effective evidence states support synthetic and auto-synthetic propagation. |
| Adapter SPI | Domain adapters can keep their own fact vocabulary while reusing kernel behavior. |
| Existing adapters | Current workflow and design-system adapters prove the model is not tied to one domain. |
| Host and CLI | Route, contract, explain, and evidence commands already exist with text and JSON output. |

The next work is not another kernel rewrite. It is product capability work:
workflow facts, git/CI facts, product gate identities, package/API checks,
generated-product checks, skill checks, docs checks, release checks, cost control,
freshness caching, and profile-aware enforcement.

## Architecture

The kernel remains small and dependency-light. Product knowledge lives outside
it.

```text
FS.GG.Governance.Kernel
  facts, rules, verdicts, checks, explanation, evidence, routes

FS.GG.Governance.Adapters.Spi
  adapter contracts, fact lifting, rule-pack composition

FS.GG.Governance.Adapters.*
  workflow, git/CI, package/API, generated product, skills,
  docs/examples, design/rendering, distribution, provenance

FS.GG.Governance.Host
  read-only sensing, effect boundaries, command execution records,
  report assembly

FS.GG.Governance.Cli
  fsgg route, check, refresh, verify, ship, release, tui, watch

FS.GG.Rendering and generated products
  templates, runtime packages, samples, captures, skills, product policy,
  capability catalogs, source artifacts, and generated readiness
```

Dependency rules:

| Layer | Allowed dependencies |
|---|---|
| Kernel | BCL and FSharp.Core only. |
| SPI | Kernel and light adapter contracts only. |
| Adapters | Domain-specific parsers and inspectors where they own that domain. |
| Host/tooling | File system, process runner, git, MSBuild, NuGet, hashing, generated reports. |
| CLI | Command parsing, JSON, plain text, Spectre.Console presentation. |
| Product runtime packages | No governance dependency unless the package explicitly owns governance behavior. |

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
authoring, but structured files are authoritative for gates.

| Stage | Purpose | Primary command | Gate posture |
|---|---|---|---|
| `ProjectInit` | Create the product root, `.fsgg` policy files, initial work root, agent commands, and optional product template output. | `fsgg new <path>` or `fsgg init` | Advisory. |
| `Charter` | Establish project identity, governing principles, package surfaces, domains, branch policy, and default profile. | `fsgg charter` | Advisory until branch protection is configured. |
| `Specify` | Capture user value, scope, non-goals, user stories, acceptance criteria, and measurable requirements. | `fsgg work specify <id>` | Advisory. |
| `Clarify` | Resolve ambiguities before planning and record explicit answers. | `fsgg work clarify <id>` | Advisory. |
| `Checklist` | Validate requirements quality before technical planning. | `fsgg work checklist <id>` | Advisory or early fence. |
| `Plan` | Record architecture, public contracts, dependencies, technical choices, and evidence plan. | `fsgg work plan <id>` | Advisory with route preview. |
| `Tasks` | Define typed work items, dependencies, owners, skills, and required evidence. | `fsgg work tasks <id>` | Advisory or early fence. |
| `Analyze` | Check spec/plan/task consistency before implementation starts. | `fsgg analyze <id>` | Advisory by default. |
| `Implement` | Complete work and declare evidence. | `fsgg work update <id>` | Local checks, usually light or standard profile. |
| `Verify` | Run selected tests, surface checks, docs checks, generated-view checks, and evidence checks. | `fsgg verify <id>` | Blocking in selected CI contexts. |
| `Ship` | Recompute from base/head and enforce merge policy. | `fsgg ship <id> --mode gate --json` | Blocking. |
| `Release` | Pack, publish, and attest artifacts. | `fsgg release <id>` | Blocking for publication. |

The CLI and agent-facing skills should expose the same stages. Agent prompts may
write the Markdown authoring files; the CLI owns schema validation, route
selection, evidence computation, generated views, and gate verdicts.

## Greenfield project bootstrap

A consumer must be able to start a new project with the FS.GG governance flow:

```text
fsgg new ./MyProduct --template rendering-app --profile standard
fsgg charter
fsgg work specify create-shell "Build the first useful product slice..."
fsgg work clarify create-shell
fsgg work checklist create-shell
fsgg work plan create-shell --stack "F#, FS.GG.Rendering, SQLite"
fsgg work tasks create-shell
fsgg analyze create-shell
fsgg work update create-shell
fsgg verify create-shell
fsgg ship create-shell --mode gate --profile standard --json
```

`fsgg new` creates the governance skeleton and may ask a product template
provider to generate starter code. Governance owns the project policy, work
artifacts, agent commands, route contract, evidence schema, and readiness output.
The product template provider owns the runtime code and generated product assets.

The bootstrap output must be enough for a coding agent to continue the SDD loop
without hidden repository knowledge:

| Output | Purpose |
|---|---|
| `.fsgg/project.yml` | Project identity, domains, default work root, and capability catalog pointer. |
| `.fsgg/policy.yml` | Governance profile, stage policy, and branch gate expectations. |
| `.fsgg/capabilities.yml` | Template profile, package surfaces, tests, docs, skills, samples, and release surfaces. |
| `.fsgg/tooling.yml` | Tool policy, allowed commands, environment classes, and timeouts. |
| `work/` | Native SDD artifacts for specifications, plans, tasks, and evidence. |
| Agent commands or skills | Stage-specific prompts for charter, specify, clarify, checklist, plan, tasks, analyze, implement, verify, and ship. |
| Initial readiness directory | Generated route/contract/explain/evidence/audit outputs as they become available. |

## Capability catalog MVP

The capability catalog is foundational, not a late product-adapter detail. The
first implementation slice needs a small, versioned schema that can classify
paths and name the surfaces a generated product expects to protect.

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

The project-level source files define policy and capability scope:

| Artifact | Purpose |
|---|---|
| `.fsgg/project.yml` | Project id, domain list, package surfaces, capability catalog pointer, default work root. |
| `.fsgg/policy.yml` | Governance profiles, default profile, enforcement mapping, branch policy, review budgets. |
| `.fsgg/capabilities.yml` | Packages, projects, `.fsi` contracts, tests, skills, docs, samples, baselines, template profiles, evidence tags. |
| `.fsgg/tooling.yml` | Command allow-list, timeouts, environment classes, external tool policy, tool version expectations. |
| `work/<id>/spec.md` | Human-authored value, scope, non-goals, user stories, requirements, and acceptance criteria. |
| `work/<id>/clarifications.md` | Resolved questions and explicit decisions made before planning. |
| `work/<id>/checklist.md` | Requirements-quality checks and open issues before planning. |
| `work/<id>/plan.md` | Architecture decisions, public contract impact, dependencies, technical choices, and evidence plan. |
| `work/<id>/contracts/` | Source contracts or links to canonical contracts such as `.fsi`, OpenAPI, or gRPC definitions. |
| `work/<id>/tasks.yml` | Typed work items, dependencies, owners, expected skills, and required evidence. |
| `work/<id>/evidence.yml` | Authored evidence declarations and artifact URIs. |

### Canonical work model

Gates evaluate a normalized work model, not loose prose. Markdown remains the
authoring surface for humans and agents, but only typed identifiers and parsed
sections enter the machine contract. Structured files such as `tasks.yml` and
`evidence.yml` carry graph-shaped data directly.

The CLI should assemble `WorkModel` from the source artifacts, validate it, and
emit `readiness/<id>/work-model.json` as a generated view with model version,
source digests, and parse diagnostics. Conflict rules are explicit:

| Conflict | Gate behavior |
|---|---|
| Markdown requirement id is missing from the normalized model | Emit `requirementNotTyped`; advisory before planning, blocking at ship when cited by changed work. |
| Structured task references an unknown requirement or decision | Emit `workModelInconsistent`; block at verify/ship under standard and stricter profiles. |
| Markdown and structured data disagree on status, dependency, owner, or required evidence | Prefer the structured graph for execution, keep the prose visible, and emit a consistency finding. |
| Generated `work-model.json` is stale relative to any source artifact | Emit generated-view drift and handle it through the profile. |

Generated views are outputs and must be currency-checked:

| Artifact | Purpose |
|---|---|
| `.fsgg/gates.json` | Generated gate registry with ids, metadata, prerequisites, cost, timeout, and owner. |
| `.fsgg/rules.md` | Rendered rule catalog from reified checks. |
| `readiness/<id>/route.json` | Matched rules, changed paths, selected gates, unknown-path findings, cost, cache eligibility, and profile-adjusted enforcement. |
| `readiness/<id>/contract.json` | Rule contracts, required inputs, and source reads. |
| `readiness/<id>/explain.json` | Proof trees and explanation traces for applicable rules. |
| `readiness/<id>/evidence.json` | Effective evidence states, taint propagation, freshness, and graph failures. |
| `readiness/<id>/audit.json` | Ship verdict, blockers, warnings, provenance references, and exit-code basis. |
| `readiness/<id>/summary.md` | Human PR summary rendered from JSON. |
| `readiness/<id>/attestations/` | Optional SLSA/in-toto-shaped provenance summaries. |

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

Cost control is part of correctness. If a one-line low-risk change selects a
high-cost route, the explanation must make the risk obvious. If it cannot, the
route rule is too broad or the gate belongs in advisory or scheduled validation.

| Requirement | Mechanism |
|---|---|
| Keep authoring fast | `fsgg route --paths ...`, `fsgg check --paths ...`, and `fsgg check --since <rev>`. |
| Route from impact facts | Map paths to capabilities, packages, public surfaces, generated views, docs pages, controls, tests, and evidence. |
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

The CLI should expose one data model through three renderers:

| Surface | Purpose | Contract |
|---|---|---|
| JSON | CI, agents, scripts, cached evidence, readiness, branch protection. | Stable schema, deterministic order, no ANSI, no terminal wrapping, no implicit clock. |
| Plain text | Simple logs and redirected output. | Human-readable, not the automation contract. |
| Spectre.Console | Interactive route, evidence, verify, ship, and watch views. | Presentation only over the same report objects. |

Initial commands:

| Command | Purpose |
|---|---|
| `fsgg route` | Show selected gates and route trace for paths, since-rev, or base/head. |
| `fsgg new` | Scaffold a governed greenfield product and optional product template output. |
| `fsgg charter` | Establish project principles, policy, domains, and branch gate expectations. |
| `fsgg work specify` | Create or update the spec as the source of truth for a work item. |
| `fsgg work clarify` | Resolve ambiguities before planning. |
| `fsgg work checklist` | Validate requirements quality before planning. |
| `fsgg work plan` | Create the technical plan and evidence plan from the spec. |
| `fsgg work tasks` | Generate or update the typed task graph from the plan. |
| `fsgg analyze` | Check consistency across spec, plan, tasks, contracts, and policy. |
| `fsgg check` | Run selected cheap/focused checks for local authoring. |
| `fsgg refresh` | Regenerate declared views and baselines. |
| `fsgg evidence` | Compute effective evidence state and freshness. |
| `fsgg verify` | Run profile-appropriate verification before PR or explicit local validation. |
| `fsgg ship` | Recompute merge policy from base/head and emit blocking audit. |
| `fsgg release` | Validate pack, publish, version, metadata, and provenance requirements. |
| `fsgg tui` | Optional interactive command center. |
| `fsgg watch` | Optional local watch projection over route/evidence/check reports. |

`fsgg ship --mode gate --profile standard --json` is the stable CI entry point.
Other jobs may produce evidence, but this command owns the protected-branch
verdict.

## Readiness and provenance

Generated readiness is structured first and Markdown second. Each command record
should capture enough information to make builds, tests, packs, template
instantiation, git diffs, package inspection, and visual capture auditable.

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
| Rule hash and generator version | Detect stale checks after rule or generator changes. |
| Artifact digests | Detect drift in generated files, packages, baselines, captures, and docs. |
| Command records | Audit external process results without embedding shell policy. |
| Environment class | Separate local, CI, release, and generated-product environments. |
| Builder identity | Support future SLSA/in-toto-shaped attestations without overclaiming compliance. |

## Agent-reviewed constraints

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

The initial implementation should report agent-reviewed findings and missing
reviews, but protected-branch blocking should come from deterministic checks,
human-only escalations, stale evidence, generated-view drift, or explicit policy
violations until calibration exists.

## Tooling strategy

Durable governance behavior belongs in compiled F# libraries and CLI commands.
Shell and `.fsx` are allowed, but they should not own policy or stable gate
truth.

| Form | Use |
|---|---|
| Compiled F# libraries | Route sensors, git snapshots, process runner facade, package inspection, generation manifests, freshness keys. |
| Compiled CLI commands | Stable user/CI contract, exit codes, JSON schemas, Spectre projections. |
| `.fsx` scripts | FSI sketches, exploratory reports, and literate docs. |
| Shell scripts | Bootstrap, tool install, or temporary wrapper around `dotnet fsgg ...`. |

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

This roadmap is incorporated into the
[Spec Kit implementation plan](../2026-06-18-governance-kernel-speckit-implementation-plan.md)
as planned features F14-F27 with progress checkboxes, dependencies, surfaces,
test focus, and exit criteria. The phase bullets below remain the design-level
source for that checklist.

### Phase 1: ship walking skeleton and catalog MVP

- Define versioned `.fsgg/project.yml`, `.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` MVP schemas.
- Add git/CI snapshot facts for base ref, head ref, changed paths, dirty paths,
  untracked paths, and CI context.
- Add path-to-capability routing, unknown governed path findings, typed gate
  metadata, and route traces for one concrete adapter.
- Add `fsgg route --paths ...`, `fsgg route --since <rev>`, and
  `fsgg ship --mode gate --profile standard --json` as the first protected
  boundary contract.
- Emit route and audit JSON with deterministic ordering, selected gates, matched
  rules, unmatched governed paths, expected artifacts, cost, cache eligibility,
  profile-adjusted enforcement, and exit-code basis.
- Publish the first GitHub Actions guidance for branch protection.

### Phase 2: native SDD bootstrap

- Add `fsgg new`, `fsgg charter`, `fsgg work specify`, `fsgg work clarify`,
  `fsgg work checklist`, `fsgg work plan`, `fsgg work tasks`, and
  `fsgg analyze` command contracts.
- Add `work/<id>` schemas for specs, clarifications, checklists, plans,
  contracts, typed tasks, and evidence.
- Generate agent commands or skills that drive the same native stages.
- Assemble and validate the normalized `WorkModel`; emit
  `readiness/<id>/work-model.json` as a generated view.
- Make the spec the source of truth for plan, tasks, generated views, route
  expectations, and acceptance evidence through explicit typed ids.

### Phase 3: route parity and enforcement fixtures

- Add profile parsing, maturity, and base/effective severity computation.
- Add scoped `--paths` authoring and complete base/head route parity with CI.
- Generate golden enforcement truth-table fixtures covering modes, profiles,
  maturity, severity, tier, fenced/routine routing, and unknown governed paths.
- Emit route JSON with selected gates, matched rules, unmatched governed paths,
  expected artifacts, cost, cache eligibility, and explanation.

### Phase 4: workflow and evidence

- Implement task graph validation, synthetic taint propagation, accepted
  deferrals, stale evidence, spec-to-plan consistency, plan-to-task
  consistency, and ship audit blockers.
- Produce `contract.json`, `explain.json`, `evidence.json`, `audit.json`, and
  `summary.md`.
- Keep agent-reviewed rules advisory until cache, prompt-isolation, confidence,
  and calibration constraints are implemented.

### Phase 5: generated views

- Add `fsgg refresh` as the single regeneration entry point.
- Define a generation manifest for source, generated view, renderer, and
  currency gate.
- Generate gate metadata, rule catalogs, capability docs, skill references,
  API-surface docs, work-model projections, and baselines.
- Block stale generated views at the configured boundary.

### Phase 6: product and capability catalog expansion

- Expand `.fsgg/capabilities.yml` for generated products, package surfaces,
  docs, skills, samples, design artifacts, release surfaces, and evidence tags.
- Add generated-product checks in cost tiers: structural scan, restore/build,
  focused tests, full verify, and release validation.
- Ensure generated products can run governance locally without monorepo access.
- Replace durable product shell behavior with compiled commands.

### Phase 7: package, design, docs, and skills

- Add `.fsi` surface baseline generation and drift checks.
- Add FSI transcript checks for public examples and package contracts.
- Connect design-system facts to real token, capture, contrast, and control
  catalog sources.
- Add docs/examples checks for FsDocs, literate scripts, public API docs, and
  link/reference currency.
- Add skill-quality checks for product skills, task skill lists, path contracts,
  and optional mirrors.

### Phase 8: release and provenance hardening

- Define `fsgg verify` and `fsgg release` schemas and exit codes.
- Add Spectre.Console projections backed by the same report objects.
- Add command-run records for builds, tests, packs, git facts, and package
  inspections.
- Add scheduled exhaustive validation for broad matrices.
- Add release rules for version bumps, package metadata, template pins,
  publish plans, trusted publishing, and provenance.

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Rebuilding a workflow wrapper instead of a governance platform | Keep `.fsgg` schemas, adapters, capability catalogs, and CI audits as the primary scope. |
| SDD becomes document ceremony instead of executable project control | Make spec, plan, tasks, evidence, route, generated views, and ship audit machine-checkable from one native artifact model. |
| Making local development oppressive | Enforce cost budgets, scoped routing, freshness caching, maturity levels, and advisory-first promotion. |
| Allowing profiles to hide failures | Always emit underlying verdict, base severity, effective severity, mode, profile, maturity, and reason. |
| Letting terminal UI diverge from automation | Render Spectre.Console views from the same immutable report objects used for JSON. |
| Moving shell policy into untested `.fsx` | Graduate required behavior into compiled libraries and commands. |
| Leaking product dependencies into the kernel | Keep product facts in adapters and capability catalogs. |
| Letting stale readiness pass by presence | Key evidence by source hash, rule hash, command version, artifact digest, base/head, and environment class. |
| Overclaiming provenance | Emit compatible metadata first; claim formal compliance only after explicit verification. |
| New-project bootstrap depends on one template provider | Keep `fsgg new` template-provider-neutral and require generated products to declare capabilities through `.fsgg/capabilities.yml`. |
| Reintroducing oppressive default-deny routing | Treat unknown governed paths as explicit findings under declared roots, not as a global heavy-route fallback. |
| Policy dials become hard to reason about | Maintain golden enforcement truth tables and JSON snapshots for every mode/profile/maturity/severity combination that can change blocking. |
| Markdown and structured work artifacts drift | Gate only on the normalized work model, emit parse/conflict diagnostics, and currency-check `work-model.json`. |
| Agent-reviewed checks become uncalibrated blockers | Keep them advisory until judge identity, prompt isolation, confidence thresholds, and judge-vs-human calibration are implemented. |

## Acceptance bar

The design is implemented when a generated product can:

1. Start as a greenfield project through `fsgg new`.
2. Spec-drive work through charter, specify, clarify, checklist, plan, tasks,
   analyze, implement, verify, and ship.
3. Declare project policy, capabilities, work, and evidence in `.fsgg` and
   `work/<id>`.
4. Route a local scoped change cheaply and explain selected gates.
5. Distinguish routine unclassified files from unknown governed paths and explain
   why either does or does not block.
6. Run `fsgg ship --mode gate --profile standard --json` as a minimal protected
   boundary before the full lifecycle command suite is complete.
7. Refresh generated views from declared sources and detect drift.
8. Validate public package surfaces, docs, examples, skills, design artifacts,
   generated consumers, and release metadata through adapters.
9. Cache fresh expensive evidence and rerun only when relevant inputs change.
10. Emit deterministic route, contract, explain, evidence, and audit JSON.
11. Render useful human CLI output without changing automation truth.
12. Cover enforcement dials with truth-table fixtures and golden JSON snapshots.
13. Support release checks with package, publish, and provenance evidence.

The central constraint remains simple: strict at protected boundaries, cheap in
the authoring loop, and explainable everywhere.
