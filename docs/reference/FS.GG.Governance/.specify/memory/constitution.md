<!--
Sync Impact Report
==================
Adapted from: FS.GG.Rendering Constitution v1.1.0 (sibling repo, structure/provenance
only), which itself descends from the FS-Skia-UI constitution and the
`fsharp-opinionated` Spec Kit preset.
This file: FS.GG.Governance Constitution v1.0.0

Version change: (template) → 1.0.0
Bump rationale: Initial ratification. The repository shipped with the unfilled stock
Spec Kit constitution template; this is the first real constitution.

Principles (mirrored from FS.GG.Rendering, renumbered/kept):
- I.   Spec → FSI → Semantic Tests → Implementation
- II.  Visibility Lives in `.fsi`, Not in `.fs`
- III. Idiomatic Simplicity Is the Default
- IV.  Elmish/MVU Is the Boundary for Stateful or I/O Workflows
- V.   Test Evidence Is Mandatory (synthetic-evidence disclosure folded in)
- VI.  Observability and Safe Failure

Stripped from the `fsharp-opinionated` preset (mirrors Rendering's decision; NOT
appropriate for a standard Spec Kit tool repo, even one whose *product* is governance
tooling):
- Standalone principle "Synthetic Evidence Requires Loud, Repeated Disclosure" — the
  heavy machinery ([S]/[S*]/[SEH] task markers, the EvidenceAudit after_implement
  merge gate, the --accept-synthetic override, readiness/synthetic-evidence.json). The
  durable lesson (prefer real evidence; disclose synthetic loudly) is folded into
  Principle V.
- The "Workflow & Quality Gates" gate ladder (specification/planning/task/
  implementation/evidence gates). Replaced by a lightweight "Development Workflow"
  mapped to standard Spec Kit.
- Mandatory skill gates. Replaced by an advisory "Local Skills" section.

Evidence extension removed: the `evidence` extension
(`.specify/extensions/evidence/`) and its `speckit-evidence-audit` /
`speckit-evidence-graph` skills have been deleted from this repository, along with
their `installed:` entry, `.registry` block, and the mandatory `before_implement` /
`after_implement` hooks in `.specify/extensions.yml`. This completes the strip-down:
there is no DAG validation or merge-gate audit machinery left in the repo, matching
the sibling FS.GG.Rendering's on-disk shape.

Domain retarget: rendering → governance tooling. There is no rendering backend
(Skia/OpenGL), no controls/themes/design-system, and no UI package family. Engineering
Constraints are rewritten for a deterministic rule/evidence/route-explanation tool
product and encode the cross-repo operating rule.

Sections:
- Added: Local Skills (advisory)
- Changed: Engineering Constraints (governance-tool domain; operating rule;
  dependency-minimalism for the first useful product; `FS.GG.Governance.*` identity)
- Changed: Development Workflow (standard Spec Kit; gate ladder removed)
- Kept:    Change Classification (Tier 1 / Tier 2)

Templates / artifacts reviewed:
- .specify/templates/plan-template.md      ✅ generic "Constitution Check" gate, no edits needed
- .specify/templates/spec-template.md      ✅ no governance terms, no edits needed
- .specify/templates/tasks-template.md     ✅ no skillist/evidence terms, no edits needed
- .specify/templates/checklist-template.md ✅ no edits needed
- README.md                                ✅ Workflow section: evidence extension no longer referenced
- .specify/extensions.yml                  ✅ `evidence` install entry and implement hooks removed
- .specify/extensions/.registry            ✅ `evidence` registry block removed
- .specify/presets/fsharp-opinionated/     ✅ neutralized to the stripped shape: the constitution
                                             template is now six principles with Local Skills +
                                             lightweight Development Workflow (no gate ladder); the
                                             tasks/implement commands and the materialized
                                             .claude/skills/* drop [S]/[S*]/tasks.deps.yml/DAG/audit;
                                             tasks-deps-template.yml deleted; preset.yml descriptions
                                             updated. The `.claude/skills/speckit-*` skills are the
                                             materialized copies and were edited in lockstep.

Deferred TODOs:
- TODO(STRUCTURED_LOGGING): logging library not yet selected; record in an ADR when chosen.
- TODO(PACKAGE_IDENTITY): `FS.GG.Governance.*` is the working namespace; ratify in a
  decision record when the first package is published.
-->

# FS.GG.Governance Constitution

The governance repository owns the optional rule, evidence, and route-explanation
tooling for the FS-GG projects as a normal tool product: deterministic fact and rule
evaluation, explanation and diagnostics primitives, evidence-freshness helpers,
route-analysis helpers, package/docs/template drift analyzers, support-bundle tooling,
and optional Spec Kit extensions. It MUST be buildable, testable, documentable,
packable, and releasable with normal repository tooling and standard Spec Kit.

**Operating rule.** Governance tooling MAY *inspect* rendering; rendering MUST NEVER
*require* governance tooling to build, test, document, package, or release. Generic
code here MUST NOT assume rendering's package IDs, template names, target names, or
directory layout. Rendering is treated as one external customer, not as this tool's
internal shape. Critically, this repository governs itself with the same standard Spec
Kit it offers others — it does not depend on its own (or any external) governance
platform to ship.

## Core Principles

### I. Spec → FSI → Semantic Tests → Implementation

Every non-trivial change MUST follow this order:

1. **Specify.** The feature spec names the user-visible outcome, scope
   boundaries, change classification (Tier 1 / Tier 2), public API impact, and
   verification approach.
2. **Sketch in FSI.** The intended public surface is drafted as a `.fsi`
   signature and exercised interactively in F# Interactive before any `.fs`
   implementation exists. API shape is validated by use.
3. **Semantic tests for FSI.** Tests MUST exercise the API through the same FSI
   surface a human or script would use: load the packed library (or a prelude
   script) and call the public functions. Tests assert behavior, not internals.
4. **Implement.** Write the `.fs` body against the now-stable signature and
   passing tests.

Rationale: FSI is the honest audience. If the shape is awkward in FSI, it is
awkward in production. Designing through FSI catches API mistakes before `.fs`
code exists to defend them.

### II. Visibility Lives in `.fsi`, Not in `.fs`

Every public F# module MUST have a corresponding `.fsi` signature file. The
`.fsi` is the sole declaration of the module's public surface; symbols omitted
from the `.fsi` are private — the F# compiler enforces this.

Therefore `.fs` files MUST NOT carry `private`, `internal`, or `public` access
modifiers on top-level bindings. Visibility is determined by presence or absence
in the `.fsi`, not by keywords scattered across `.fs`. Surface-area baselines
MUST be maintained per public module and validated by an automated test (an API
surface-drift check).

Rationale: Two sources of truth for visibility is one too many. The `.fsi`
already gives the compiler the full picture; access modifiers in `.fs` only
invite drift.

### III. Idiomatic Simplicity Is the Default

Code SHOULD prefer the plainest F# that solves the problem: functions over
classes, records over hierarchies, pipelines over mutation, the standard library
over clever abstractions. A reader should not need a textbook to follow ordinary
code.

Complex features MAY be used, but their use MUST be justified in the feature's
spec or plan. The following require explicit justification:

- Custom operators beyond the F# standard set
- Statically-resolved type parameters (SRTP) and inline tricks that force it
- Reflection and dynamic dispatch
- Non-trivial computation expressions (beyond `async`, `task`, `option`, `result`, `seq`)
- Type providers
- Active patterns beyond single-case or simple discriminants

If such a feature appears without matching justification, the reviewer treats it
as a spec defect, not a code defect.

**Mutation is allowed when it is the simpler or faster code.** `mutable`
bindings, `for` / `while` loops, and `ref` cells MAY be used when they are
demonstrably plainer than the immutable alternative or are needed on a measured
hot path — a single unaliased accumulator, an inner loop over a buffer, a
fixed-point rule-evaluation pass. Disclose the reason at the use site with a
one-line comment (e.g. `// mutable: fixed-point iteration to convergence`) so a
reader doesn't waste effort "fixing" it.

**Recursion is for branching structure, not for hiding state.** `let rec` fits
genuinely recursive problems — state-machine transitions, tree / graph walks,
branching evaluators, parser combinators. It is the wrong tool when its only
purpose is to thread an accumulator through self-calls to avoid a `mutable`;
there the `mutable` is clearer — prefer it.

Rationale: Complexity compounds in F# because the language rewards expressive
tricks, so a simplicity bias keeps code legible. Dogmatic immutability is itself
the cleverness this principle discourages.

### IV. Elmish/MVU Is the Boundary for Stateful or I/O Workflows

Any feature with multi-step state, external I/O, retries, user interaction,
background work, or operational recovery MUST model its behavior through an
Elmish-style Model-View-Update boundary before implementation. Simple pure
functions — a fact store, a single rule evaluation, an explanation formatter —
do not need Elmish ceremony, but once behavior includes stateful workflow or I/O
(scanning a repository, generating a support bundle, polling evidence freshness
over time), the public `.fsi` surface MUST expose or clearly wrap:

- `Model` — the durable state the workflow owns
- `Msg` — the events, user actions, external responses, and internal transitions
  the workflow accepts
- `Effect` or `Cmd<Msg>` — the I/O the workflow requests but does not execute
  inside `update`
- `init` — initial state plus requested startup effects
- `update` — a pure transition from `Msg` and `Model` to next `Model` plus effects
- an interpreter at the edge that executes effects and turns results back into `Msg`

The Elmish package is the preferred runtime when a host benefits from its
`Program`, `Cmd`, or subscription model. For libraries, CLIs, and small tools — the
common shape in this repository — a local MVU/effect algebra is acceptable when it
preserves the same separation: `update` is pure, I/O is represented as data or
`Cmd<Msg>`, and interpretation happens only at the edge.

Semantic tests MUST cover both sides of the boundary:

- pure transition tests: given `Model` + `Msg`, assert the next `Model` and
  emitted effects
- interpreter tests: execute effects against a real filesystem, process, or
  network where safe
- FSI transcripts: exercise `init` and representative `update` paths through the
  packed library or prelude, not private helpers

Rationale: Elmish makes the hard part observable. State transitions become plain
values that can be tested exhaustively, and I/O becomes an explicit contract that
can be audited, interpreted, and exercised with real evidence — which is exactly
the property a governance tool is supposed to provide for others.

### V. Test Evidence Is Mandatory

Behavior-changing code MUST include automated tests that fail before the change
and pass after. Prefer tests that run against real dependencies (real
filesystem, real process, real network where safe). For a deterministic
rule/evidence engine, prefer real evaluation over end-to-end fixtures: feed real
facts and rules and assert the derived facts, provenance, and explanation output.

Tests blocked by out-of-scope issues MUST be marked skipped (the test
framework's skip mechanism) with written rationale. Never mark a failing test as
passed. Never weaken an assertion to green a build — narrow the scope instead,
and document it.

**Synthetic evidence** — mocks, stubs, fakes, hardcoded fixtures, in-memory
substitutes, canned responses — MAY be used when real evidence is unavailable or
prohibitively expensive AND a real-evidence path is planned or documented as
infeasible. Every synthetic use MUST be disclosed at the use site with a comment
naming the fact and reason (e.g. `// SYNTHETIC: no sample rendering repo in CI;
real path tracked in <issue>`), MUST carry the token `Synthetic` in the test
name, and MUST be listed in the PR description. Prefer explicit, ugly literals
over clever factories that make synthetic data feel real.

Rationale: Synthetic evidence is the quiet failure mode of "passing" tests.
Visible disclosure keeps it honest without requiring a governance platform — a
property this repository values doubly, since it builds the very tooling others
might reach for instead.

### VI. Observability and Safe Failure

Operationally significant events (startup, store load/save, rule-evaluation
divergence or non-convergence, evidence-freshness expiry, scan/analyzer failure,
recovery paths) MUST emit structured diagnostics with actionable context. Errors
MUST fail fast or degrade explicitly; silent failure and swallowed exceptions are
forbidden in critical paths.

Diagnostics MUST distinguish a genuine tool defect from missing or malformed
input (an absent file, an unparseable rule, an external customer whose layout
this tool does not recognize), rather than reporting one as the other.

## Change Classification

Every feature declares a tier in its spec:

- **Tier 1 (contracted change)** — adds, removes, or modifies public API
  surface; introduces new dependencies; changes inter-project or package
  contracts; or alters observable behavior covered by existing specs. Requires
  the full artifact chain: spec, plan, `.fsi` updates, surface-area baseline
  updates, test evidence, and documentation updates.
- **Tier 2 (internal change)** — refactors, performance, or internal cleanup
  with no behavioral change. Requires spec and tests; `.fsi` and baselines remain
  untouched.

A Tier 1 change that fails to update `.fsi` or baselines is a defect, regardless
of whether tests pass.

## Engineering Constraints

- F# on .NET is the exclusive stack. Cross-language integration, if ever needed,
  uses gRPC or OpenAPI over separate projects.
- Target framework is .NET `net10.0` unless a plan justifies a narrower target.
- Every public `.fs` module requires a curated `.fsi`.
- Stateful or I/O-bearing features use an Elmish/MVU boundary (`Model`, `Msg`,
  `Effect` or `Cmd<Msg>`, pure `update`, edge interpreter).
- Surface-area baseline files are required for each public module.
- Public API changes document compatibility impact and migration guidance.
- Dependencies are minimized; each new dependency states need, version-pinning
  strategy, and maintenance owner. The first useful product — the rule/evidence
  helper library — MUST NOT depend on FAKE, git, filesystem scanning, Skia, NuGet
  publishing, template profiles, or rendering project paths. Heavier capabilities
  (drift analyzers, support-bundle tooling, Spec Kit extensions) layer on top in
  separate projects, not into the core.
- **Genericity (operating rule).** Generic code MUST NOT assume rendering's
  package IDs, template names, target names, or directory layout. Anything
  rendering-specific is supplied as configuration by the caller; rendering is one
  external customer, not this tool's internal shape. This repository does not own
  rendering product identity, package IDs, docs URLs, template profiles,
  design-system choices, controls, themes, or release decisions.
- Package identity is `FS.GG.Governance.*` as the working namespace; see
  TODO(PACKAGE_IDENTITY). Any rebrand is a separate, explicit release decision,
  not part of ordinary work.
- Pack output location: `~/.local/share/nuget-local/`.
- Structured-logging library: TODO(STRUCTURED_LOGGING) — not yet selected; record
  the choice in an ADR.

## Local Skills

Repo-local skills under `.claude/skills/` — the `/speckit-*` workflow skills — are
**advisory aids**. When a task matches a skill's description, contributors SHOULD
consult it and prefer it over generic guidance; when several apply, use the minimal
set that covers the work.

Skills are not gates. There is no mandatory skill-loading step, no `skillist` task
metadata, and skill usage never blocks task completion or merge readiness. A
contributor can clone this repository, read the standard Spec Kit artifacts, run the
documented build/test commands, and ship a routine governance change without loading
any skill. (The repository ships no evidence-audit or DAG-validation skill; that
machinery was removed in favor of the standard Spec Kit flow described below.)

## Development Workflow

Use standard Spec Kit for feature work: specify → plan → tasks → implement.
`spec.md`, `plan.md`, and `tasks.md` are authored artifacts, not a generated
graph; no custom feature/product/project graph is the source of truth for
ordinary work in this repository.

Repo-owned checks are kept only when they are narrow and pay for themselves —
for example API surface-drift checks, package-skew checks, docs build checks, and
release packaging checks. Each active check SHOULD have a short justification:
what product contract it protects, when it runs, who owns it, and what it costs.
No check requires an external governance repository — including the tooling this
repository itself produces.

Any intentional deferral MUST be explicit in the spec or plan and scoped as a
bounded follow-up.

## Governance

This constitution overrides conflicting local habits, informal preferences, and
agent prompts for work in this repository. Compliance review SHOULD occur at
specification, planning, implementation review, and merge readiness review.

**Amendment procedure:** PR with rationale and migration impact; maintainer
review required. Amendments MUST update dependent templates and guidance files in
the same change. When the constitution and a template disagree, the constitution
is correct and the template is defective until synchronized.

**Versioning policy:**

- MAJOR — backward-incompatible governance changes or principle removals
- MINOR — new principles, new mandatory constraints, or materially expanded
  obligations
- PATCH — clarifications that do not change the meaning of the rules

**Version**: 1.0.0 | **Ratified**: 2026-06-18 | **Last Amended**: 2026-06-18
