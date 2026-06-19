---
title: Spec-driven development in the system
category: Governance design
categoryindex: 7
index: 7
description: How FS.GG realizes spec-driven development as a native project lifecycle: charter, specify, clarify, checklist, plan, tasks, analyze, implement, verify, ship.
---

# Spec-driven development in the system

GitHub Spec Kit is the reference prior art for the workflow shape: initialize a
project, establish governing principles, write the specification first, clarify
ambiguity, validate requirements, create a technical plan, derive tasks, analyze
consistency, and then implement.

FS.GG realizes that workflow as native governance capability. It is not only an
observer over externally authored files. A consumer of this project must be able
to start a new product and drive it through spec-driven development using FS.GG
commands, artifacts, agent prompts, route explanations, evidence, and ship gates.

## Native lifecycle

The native lifecycle is:

```text
ProjectInit -> Charter -> Specify -> Clarify -> Checklist -> Plan -> Tasks
  -> Analyze -> Implement -> Verify -> Ship -> Release
```

The source model is `.fsgg` plus `work/<id>`. Markdown remains the authoring
surface for humans and agents; YAML and JSON schemas provide the machine contract
for gates.

| Stage | Purpose | Primary command |
|---|---|---|
| `ProjectInit` | Create the product root, `.fsgg` policy files, work root, agent commands, and optional template output. | `fsgg new <path>` or `fsgg init` |
| `Charter` | Establish project identity, governing principles, domains, package surfaces, and branch policy. | `fsgg charter` |
| `Specify` | Capture what and why: user value, stories, requirements, scope, non-goals, and acceptance criteria. | `fsgg work specify <id>` |
| `Clarify` | Resolve ambiguities before planning. | `fsgg work clarify <id>` |
| `Checklist` | Check requirements quality before technical planning. | `fsgg work checklist <id>` |
| `Plan` | Decide architecture, dependencies, contracts, stack choices, and evidence plan. | `fsgg work plan <id>` |
| `Tasks` | Derive typed work items, dependencies, owners, skills, and required evidence. | `fsgg work tasks <id>` |
| `Analyze` | Check consistency across spec, plan, tasks, contracts, and policy. | `fsgg analyze <id>` |
| `Implement` | Execute tasks and declare evidence. | `fsgg work update <id>` |
| `Verify` | Run selected checks and validate generated views/evidence. | `fsgg verify <id>` |
| `Ship` | Recompute from base/head and enforce merge policy. | `fsgg ship <id> --mode gate --json` |
| `Release` | Validate package, publish, and provenance requirements. | `fsgg release <id>` |

Everything before `Ship` may be advisory depending on the profile. `Ship` is the
protected boundary: it recomputes from base/head and applies blocking rules.

## Native artifacts

The specification is the source of truth. Plan, tasks, evidence, route
expectations, generated views, and acceptance checks must derive from or cite it.

| Artifact | Role |
|---|---|
| `.fsgg/project.yml` | Project identity, domains, work root, and capability catalog pointer. |
| `.fsgg/policy.yml` | Profiles, stage policy, branch gates, and review budgets. |
| `.fsgg/capabilities.yml` | Packages, tests, docs, skills, samples, generated products, baselines, and release surfaces. |
| `.fsgg/tooling.yml` | Allowed commands, tool expectations, timeouts, and environment classes. |
| `work/<id>/spec.md` | Human-authored specification: value, scope, user stories, requirements, and acceptance criteria. |
| `work/<id>/clarifications.md` | Resolved questions and explicit decisions before planning. |
| `work/<id>/checklist.md` | Requirements-quality checks and open issues. |
| `work/<id>/plan.md` | Architecture, contracts, dependencies, technical choices, and evidence plan. |
| `work/<id>/contracts/` | Public contracts such as `.fsi`, OpenAPI, gRPC, or links to canonical contracts. |
| `work/<id>/tasks.yml` | Typed task graph with dependencies, owners, skills, and required evidence. |
| `work/<id>/evidence.yml` | Authored evidence declarations and artifact URIs. |
| `readiness/<id>/*.json` | Generated route, contract, explanation, evidence, and audit reports. |

The Markdown files are not informal notes. They are governed source artifacts
with schemas, cross-artifact checks, and generated views.

## Greenfield flow

A consumer starts a new governed product like this:

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

`fsgg new` owns the governance skeleton. A product template provider may produce
starter runtime code, but the provider does not own governance truth. The
generated product declares its capabilities in `.fsgg/capabilities.yml`; FS.GG
uses that catalog to route checks and evaluate evidence.

## Rule model

The workflow adapter turns stages and artifacts into facts. Rules then check
quality and consistency.

```fsharp
type WorkflowStage =
    | ProjectInit | Charter | Specify | Clarify | Checklist | Plan
    | Tasks | Analyze | Implement | Verify | Ship | Release

type WorkflowArtifact =
    | ProjectPolicy | CapabilityCatalog | ToolingPolicy
    | Spec | Clarifications | Checklist | Plan | Contracts
    | Tasks | Evidence | Readiness

type WorkflowFact =
    | StageReached of WorkflowStage
    | ArtifactPresent of WorkflowArtifact
    | RequirementDeclared of id: string
    | AcceptanceCriterionDeclared of id: string
    | DecisionRecorded of id: string
    | TaskDeclared of id: string
    | TaskDependsOn of taskId: string * dependencyId: string
    | EvidenceDeclared of taskId: string * evidenceId: string
```

Example rule families:

| Rule family | Examples |
|---|---|
| Specification quality | Requirements are testable; acceptance criteria exist; non-goals are explicit; ambiguity is resolved before planning. |
| Plan consistency | Plan addresses every requirement; architecture decisions cite constraints; public contract impacts are named. |
| Task graph | Tasks cover the plan; dependencies resolve; graph is acyclic; owners and required skills are declared. |
| Evidence | Each task has required evidence; synthetic evidence is disclosed; stale evidence blocks at configured boundaries. |
| Generated views | Route, contract, explain, evidence, summaries, baselines, and docs are current. |
| Ship | Base/head route is complete; unknown paths are handled; blocking evidence and generated views are fresh. |

Agent-reviewed and human-only checks remain explicit `CheckTier`s. They can help
evaluate specification quality, plan completeness, and product judgement, but
the gate still reports who made the decision and what evidence was recorded.

## Run modes

The authoring loop stays cheap:

| Stage group | Default mode | Default enforcement |
|---|---|---|
| `ProjectInit`, `Charter`, `Specify`, `Clarify` | `sandbox` or `inner` | Advisory. |
| `Checklist`, `Plan`, `Tasks`, `Analyze` | `inner` or `focused` | Advisory by default, early fence optional by policy. |
| `Implement`, `Verify` | `focused` or `verify` | Profile-dependent. |
| `Ship` | `gate` | Blocking for protected branches. |
| `Release` | `release` | Blocking for publication. |

This keeps GitHub Spec Kit's useful discipline, but moves the contract into
FS.GG's own command surface, schemas, rule algebra, evidence graph, route
explainer, and CI gate.

## Status

Design update. The existing implementation still has earlier workflow pieces;
the native SDD capability described here is the target for the next workflow
adapter and CLI work.
