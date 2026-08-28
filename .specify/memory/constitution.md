<!--
Sync Impact Report
==================
This file: FS.GG.SDD Constitution v2.0.0

Version change: 1.2.0 -> 2.0.0
Bump rationale: lifecycle artifacts, evidence confidence, and CI obligations now
scale with product risk. Exact-candidate execution and independent review replace
artifact volume and synthetic classification as the default confidence path.
MAJOR: removes compulsory process obligations while preserving compatibility.

Prior rationale (1.1.0): Engineering Constraint "package namespace is
FS.GG.SDD.*" gained an explicit carve-out for SDD-owned org-shared contract
packages such as FS.GG.Contracts.

Source: adapted from the fsharp-opinionated Spec Kit preset and the sibling
FS.GG.Governance constitution, with governance-kernel-specific language removed.

Primary retargeting:
- The repository owns the SDD lifecycle product, not the governance rule engine.
- Markdown is an authoring surface; schema-versioned structured artifacts are
  the machine contract.
- FS.GG.Governance may be integrated as optional rule/gate tooling, but SDD must
  remain independently buildable, testable, and usable with standard Spec Kit.

Templates/artifacts reviewed:
- .specify/templates/*: generic Spec Kit templates retained.
- .specify/presets/fsharp-opinionated/*: F# preset retained.
- CLAUDE.md and AGENTS.md: created for Claude and Codex context.
- .claude/skills/fs-gg-sdd-project/SKILL.md and
  .codex/skills/fs-gg-sdd-project/SKILL.md: created as matching agent guidance.
-->

# FS.GG.SDD Constitution

FS.GG.SDD owns the FS.GG spec-driven development lifecycle product. It defines
the project charter, specification, clarification, checklist, plan, task,
evidence, generated-view, and agent-command model used to start and evolve
FS.GG products.

FS.GG.SDD is separate from FS.GG.Governance. Governance owns rule evaluation,
evidence freshness, routing, profiles, and gate enforcement. SDD may integrate
with Governance through explicit contracts, but SDD does not implement the rule
engine and Governance does not own the SDD lifecycle.

## Core Principles

### I. Spec -> FSI -> Semantic Tests -> Implementation

Every non-trivial F# change MUST follow this order:

1. Specify the user-visible outcome, scope boundaries, change tier, public API
   impact, and verification approach.
2. Sketch the public surface as `.fsi` before implementation.
3. Exercise the public API in F# Interactive or through a prelude before `.fs`
   implementation hardens the shape.
4. Write semantic tests through the public surface.
5. Implement the `.fs` body against the now-stable signature.

Rationale: SDD is a workflow product. Its APIs must be usable by humans,
scripts, and agents before implementation details make them expensive to change.

### II. Structured Artifacts Are the Machine Contract

Markdown is an authoring surface. Schema-versioned structured artifacts are the
machine contract.

Every lifecycle stage MUST define which data is authoritative for tools and
gates. If prose and structured data disagree, the feature plan MUST say which
source wins, how the conflict is reported, and which generated view records the
diagnostic.

Lifecycle artifacts are required only when they carry a decision needed for the
selected risk profile. A small change may use one concise decision package; a
normal change needs a specification, focused verification, candidate binding,
and independent review; a high-risk change retains the relevant full controls.
Generators MUST NOT require repeated source snapshots or stage-to-stage views
that merely restate facts already bound to the candidate.

Rationale: SDD must avoid replacing Markdown drift with schema drift. Humans can
write prose, but tools need stable typed contracts.

### III. Visibility Lives in `.fsi`, Not in `.fs`

Every public F# module MUST have a corresponding `.fsi` signature file. The
`.fsi` is the sole declaration of public surface. Top-level `private`,
`internal`, and `public` modifiers in `.fs` files are not used as visibility
policy.

Surface-area baselines MUST be maintained for public modules once code exists.
A public-API change is high risk and is incomplete without updated signatures,
baselines, tests, and docs.

### IV. Idiomatic Simplicity Is the Default

Prefer plain F#: functions over classes, records and discriminated unions over
hierarchies, simple modules over frameworks, and the standard library over
clever abstractions.

Complex F# features require justification in the feature plan, including custom
operators, SRTP-heavy code, reflection, dynamic dispatch, type providers,
non-trivial computation expressions, and broad active-pattern machinery.

Mutation and loops are allowed when they are clearer than recursion or needed on
a measured hot path. Document the reason with a short comment.

### V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows

Lifecycle commands, generators, validators, agent-command writers, and any
feature with multi-step state or external I/O MUST expose or clearly wrap an
Elmish-style boundary:

- `Model` for durable state;
- `Msg` for events and transitions;
- `Effect` or `Cmd<Msg>` for requested I/O;
- `init` for startup state and effects;
- `update` as a pure transition;
- an edge interpreter for real I/O.

Simple pure parsers, data models, and validators do not need MVU ceremony.

### VI. Test Evidence Is Mandatory

Behavior-changing code MUST include automated tests that fail before the change
and pass after. Confidence comes primarily from execution against the exact
candidate, independently authored negative controls, and independent critic
review. Fixture or synthetic provenance SHOULD remain inspectable metadata, but
MUST NOT override an observed outcome by itself. A claimed pass with no coherent,
current execution or durable-record receipt MUST remain unsatisfied at a
protected boundary.

Generated views, schema migrations, and command output contracts need snapshot
or golden-fixture coverage once they become public or tool-facing.

### VII. Agent And Human Workflows Must Share One Contract

Claude, Codex, CLI users, and CI must operate over the same lifecycle artifacts.
Agent prompts and skills may help author files, but they are not a second source
of truth.

If an agent skill writes Markdown, the corresponding structured model and
generated views must either be refreshed by the workflow or report a stale-view
diagnostic.

### VIII. Observability And Safe Failure

Operationally significant events MUST produce actionable diagnostics: schema
parse failures, missing artifacts, stale generated views, task graph conflicts,
agent-command generation errors, and governance-integration failures.

Failures must distinguish malformed user input from tool defects. Critical
paths fail fast; optional integrations degrade explicitly.

### IX. Comments Explain Reasoning, Not History

Comments MUST describe the code as it exists today and explain non-obvious
purpose, invariants, constraints, trade-offs, and why the implementation has its
shape. They MUST NOT narrate what the code plainly states or preserve edit
history.

Public documentation describes the caller contract. Implementation comments
explain non-obvious reasoning. An issue reference MAY add context, but the comment
MUST stand alone.

Semantic comment quality requires human judgment and cannot be completely
enforced by automatic linting. Automated checks MAY catch structural omissions,
but they MUST NOT claim semantic completeness.

## Change Classification

Every change selects the highest applicable risk profile:

- **Small:** prose, metadata, or localized maintenance that cannot change
  runtime behavior or protected policy. Requires concise intent, relevant cheap
  checks, exact-candidate identity, and review.
- **Normal:** ordinary product behavior. Requires a specification, focused
  tests, exact-candidate execution, and independent critic review.
- **High:** authority, release, migration, destructive, security, public API or
  schema, formal-model, build-policy, or CI-policy change. Requires the full
  relevant fail-closed controls, compatibility analysis, and migration notes.

Unknown or indeterminate impact is high. Profiles promote but never demote when
multiple impacts apply.

## Engineering Constraints

- F# on .NET is the default implementation stack.
- Target framework is `net10.0` unless a feature plan justifies otherwise.
- The package namespace is `FS.GG.SDD.*`, with one exception: an org-shared
  contract package owned by SDD but consumed by every FS-GG repo (Governance,
  Templates, Rendering) MAY use a deliberately cross-repo namespace
  (`FS.GG.Contracts`, F# namespace `Fsgg`) so the shared contract is not falsely
  scoped as SDD-internal. Such a package MUST still be SDD-owned, MUST justify
  the name in its feature plan, and MUST embed no provider-/rendering-/Governance-
  specific identity.
- The CLI command family is `fsgg-sdd` unless an explicit release decision
  chooses a different name.
- Spec Kit is the repository workflow baseline.
- The repository starts source-empty; code is added only through feature specs.
- SDD may depend on stable FS.GG.Governance packages only through explicit,
  versioned integration contracts.
- SDD must remain useful without Governance installed.
- No repo-specific knowledge of FS.GG.Rendering package IDs, templates, or docs
  URLs belongs in generic SDD code.

## Development Workflow

Use the smallest decision-bearing workflow allowed by the risk profile. Standard
Spec Kit remains available for normal and high-risk work; small changes do not
need empty clarification, checklist, plan, task, evidence, analysis, verify, and
ship hand-offs. Before merge, bind executed checks and independent critic review
to the exact candidate.

For lifecycle features, plans must identify:

- authored artifacts;
- structured machine contracts;
- generated views;
- schema version and migration posture;
- agent-facing behavior for Claude and Codex;
- optional Governance integration points;
- tests and fixtures for stale or conflicting artifacts.

## Governance

This constitution overrides conflicting local habits, prompts, or generated
plans. Amendments require a PR or commit with rationale and migration impact.
When the constitution and templates disagree, the constitution is authoritative
and the templates are defective until synchronized.

Versioning policy:

- MAJOR: backward-incompatible principle or governance changes.
- MINOR: new principles or materially expanded obligations.
- PATCH: clarifications that do not change obligations.

**Version**: 2.0.0 | **Ratified**: 2026-06-19 | **Last Amended**: 2026-08-28
