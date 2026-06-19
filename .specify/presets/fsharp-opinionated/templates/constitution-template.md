<!-- REQUIRED: fill during /speckit.constitution -->
# [PROJECT_NAME] Constitution

<!-- LOCKED: do not modify during /speckit.constitution without user override.
     These six principles are the shared doctrine of the fsharp-opinionated
     preset. Per-project amendment requires explicit user direction and SHOULD
     be followed by a PR to the preset itself so the change propagates. -->

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
performance-critical routine. Disclose the reason at the use site with a
one-line comment (e.g. `// mutable: hot path`) so a reader doesn't waste effort
"fixing" it.

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
functions do not need Elmish ceremony, but once behavior includes stateful
workflow or I/O, the public `.fsi` surface MUST expose or clearly wrap:

- `Model` — the durable state the workflow owns
- `Msg` — the events, user actions, external responses, and internal transitions
  the workflow accepts
- `Effect` or `Cmd<Msg>` — the I/O the workflow requests but does not execute
  inside `update`
- `init` — initial state plus requested startup effects
- `update` — a pure transition from `Msg` and `Model` to next `Model` plus effects
- an interpreter at the edge that executes effects and turns results back into `Msg`

The Elmish package is the preferred runtime when a host benefits from its
`Program`, `Cmd`, or subscription model. For libraries, CLIs, services, and small
hosts, a local MVU/effect algebra is acceptable when it preserves the same
separation: `update` is pure, I/O is represented as data or `Cmd<Msg>`, and
interpretation happens only at the edge.

Semantic tests MUST cover both sides of the boundary:

- pure transition tests: given `Model` + `Msg`, assert the next `Model` and
  emitted effects
- interpreter tests: execute effects against real filesystem, process, network,
  database, or other real dependencies where safe
- FSI transcripts: exercise `init` and representative `update` paths through the
  packed library or prelude, not private helpers

Rationale: Elmish makes the hard part observable. State transitions become plain
values that can be tested exhaustively, and I/O becomes an explicit contract that
can be audited, interpreted, and exercised with real evidence.

### V. Test Evidence Is Mandatory

Behavior-changing code MUST include automated tests that fail before the change
and pass after. Prefer tests that run against real dependencies (real
filesystem, real process, real network, real database where safe).

Tests blocked by out-of-scope issues MUST be marked skipped (the test
framework's skip mechanism, or task status `[-]`) with written rationale. Never
mark a failing test as passed. Never weaken an assertion to green a build —
narrow the scope instead, and document it.

**Synthetic evidence** — mocks, stubs, fakes, hardcoded fixtures, in-memory
substitutes, canned responses — MAY be used when real evidence is unavailable or
prohibitively expensive AND a real-evidence path is planned or documented as
infeasible. Every synthetic use MUST be disclosed at the use site with a comment
naming the fact and reason (e.g. `// SYNTHETIC: no staging DB yet; real path
tracked in <issue>`), MUST carry the token `Synthetic` in the test name, and MUST
be listed in the PR description. Prefer explicit, ugly literals over clever
factories that make synthetic data feel real.

Rationale: Synthetic evidence is the quiet failure mode of "passing" tests.
Visible disclosure keeps it honest without requiring a governance platform.

### VI. Observability and Safe Failure

Operationally significant events (startup, subsystem initialization, asset/IO
failure, recovery paths) MUST emit structured diagnostics with actionable
context. Errors MUST fail fast or degrade explicitly; silent failure and
swallowed exceptions are forbidden in critical paths.

<!-- LOCKED -->
## Change Classification

Every feature declares a tier in its spec:

- **Tier 1 (contracted change)** — adds, removes, or modifies public API
  surface; introduces new dependencies; changes inter-project contracts
  (`.proto`, OpenAPI); alters observable behavior covered by existing specs.
  Requires the full artifact chain: spec, plan, `.fsi` updates, surface-area
  baseline updates, test evidence, and documentation updates.
- **Tier 2 (internal change)** — refactors, performance, internal cleanup
  with no behavioral change. Requires spec and tests; `.fsi` and baselines
  remain untouched.

A Tier 1 change that fails to update `.fsi` or baselines is a defect,
regardless of whether tests pass.

<!-- TAILORABLE: tune per project. Keep the stack-exclusivity rule unless the
     project is intentionally polyglot. Pack output path, logging library,
     and dependency policy are expected to differ per project. -->

## Engineering Constraints

- F# on .NET is the exclusive stack. Cross-language integration uses gRPC or
  OpenAPI over separate projects.
- Every public `.fs` module requires a curated `.fsi`.
- Stateful or I/O-bearing features use an Elmish/MVU boundary (`Model`,
  `Msg`, `Effect` or `Cmd<Msg>`, pure `update`, edge interpreter).
- Surface-area baseline files are required for each public module.
- Public API changes document compatibility impact and migration guidance.
- Dependencies are minimized; each new dependency states need, version
  pinning strategy, and maintenance owner.
- Pack output location: [PACK_OUTPUT_PATH]
- Structured-logging library: [LOGGING_LIBRARY]
- Project-specific constraints: [PROJECT_CONSTRAINTS]

<!-- LOCKED -->
## Local Skills

Repo-local skills under `.claude/skills/` are **advisory aids**. When a task
matches a skill's description, contributors SHOULD consult it and prefer it over
generic guidance; when several apply, use the minimal set that covers the work.

Skills are not gates. There is no mandatory skill-loading step, no `skillist`
task metadata, and skill usage never blocks task completion or merge readiness.
A contributor can clone the repository, read the standard Spec Kit artifacts,
run the documented build/test commands, and ship a routine change without
loading any skill.

<!-- LOCKED -->
## Development Workflow

Use standard Spec Kit for feature work: specify → plan → tasks → implement.
`spec.md`, `plan.md`, and `tasks.md` are authored artifacts, not a generated
graph; no custom feature/product/project graph is the source of truth for
ordinary work.

Repo-owned checks are kept only when they are narrow and pay for themselves —
for example API surface-drift checks, package-skew checks, docs build checks,
and release packaging checks. Each active check SHOULD have a short
justification: what product contract it protects, when it runs, who owns it, and
what it costs. No check requires an external governance repository.

Any intentional deferral MUST be explicit in the spec or plan and scoped as a
bounded follow-up.

<!-- LOCKED -->
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

<!-- REQUIRED -->
**Version**: [CONSTITUTION_VERSION] | **Ratified**: [RATIFICATION_DATE] | **Last Amended**: [LAST_AMENDED_DATE]
