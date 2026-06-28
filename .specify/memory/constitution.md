<!--
Sync Impact Report
==================
This file: FS.GG.SDD Constitution v1.1.0

Version change: 1.0.0 -> 1.1.0
Bump rationale: Engineering Constraint "package namespace is FS.GG.SDD.*"
materially expanded with an explicit carve-out for org-shared contract packages
owned by SDD (e.g. FS.GG.Contracts), which intentionally use a cross-repo shared
namespace so Governance, Templates, and Rendering can re-type onto one source of
truth. MINOR: relaxes/expands an existing obligation without breaking any
in-scope package.

Prior rationale (1.0.0): Initial ratification for a separate FS.GG spec-driven
development product.

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

Required lifecycle artifacts are:

- project charter and policy;
- specification and acceptance criteria;
- clarification answers;
- requirements checklist;
- technical plan;
- typed task graph;
- evidence declarations;
- normalized work model;
- generated readiness views.

Rationale: SDD must avoid replacing Markdown drift with schema drift. Humans can
write prose, but tools need stable typed contracts.

### III. Visibility Lives in `.fsi`, Not in `.fs`

Every public F# module MUST have a corresponding `.fsi` signature file. The
`.fsi` is the sole declaration of public surface. Top-level `private`,
`internal`, and `public` modifiers in `.fs` files are not used as visibility
policy.

Surface-area baselines MUST be maintained for public modules once code exists.
A Tier 1 API change that does not update signatures, baselines, tests, and docs
is incomplete.

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
and pass after. Prefer real filesystem/process/schema fixtures over mocks. When
synthetic data is unavoidable, disclose it in the test name or nearby comment
and explain what real path it stands in for.

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

## Change Classification

Every feature declares a tier in its spec:

- **Tier 1 (contracted change):** public API, schema, generated-view, command,
  artifact layout, agent-skill contract, or cross-repo integration change.
  Requires spec, plan, tasks, `.fsi` where code exists, tests, docs, and
  migration notes when applicable.
- **Tier 2 (internal change):** implementation cleanup with no user-visible or
  tool-visible contract change. Requires spec and tests; signatures and
  baselines remain unchanged.

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

Use standard Spec Kit: specify -> clarify as needed -> plan -> tasks ->
implement -> analyze before merge.

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

**Version**: 1.1.0 | **Ratified**: 2026-06-19 | **Last Amended**: 2026-06-28
