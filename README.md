# FS.GG.SDD

Spec-driven development lifecycle tooling for FS.GG.

FS.GG.SDD is the consumer-facing product a project team uses to develop its own
projects through SDD: initialize a lifecycle skeleton, author specs and plans,
generate typed task/evidence models, produce readiness views, and give humans,
agents, CLI tools, and optional Governance gates the same contract.
Its lifecycle checks are expressed as Governance-compatible rules for artifact
shape, required skills, evidence obligations, and expected tests.

This repository started scaffold-only. Spec Kit features have since added the
packable artifact-model library, normalized work-model generation, the native
command workflow through `fsgg-sdd ship`, the cross-cutting `agents`, `refresh`,
and `validate` generators, and the `fsgg-sdd scaffold` template-provider command
that takes an author from an empty directory to a buildable, SDD-managed product
in one invocation.

## Getting started

The default way to start a new product is `fsgg-sdd scaffold`: it establishes the
SDD lifecycle skeleton and invokes an external **template provider** to materialize
a runnable product, in one command.

```sh
# install the CLI as a global .NET tool (exposes `fsgg-sdd`)
dotnet tool install --global FS.GG.SDD.Cli

# scaffold a runnable, SDD-managed product from a registered provider
fsgg-sdd scaffold --root ./MyApp --provider <name> --param productName=MyApp

cd ./MyApp && dotnet build && dotnet run   # the runnable product
fsgg-sdd charter                           # continue the SDD lifecycle
```

`--provider <name>` is resolved from an author-/provider-owned `.fsgg/providers.yml`
registry through a generic, schema-versioned provider contract (the provider's
`templateId` + `source` are realized via `dotnet new`). The template is refreshed on
each run (`dotnet new install` + `dotnet new update`); pass `--no-update` to skip the
refresh, or `--force` to materialize into a non-empty target. Produced files are
recorded in `.fsgg/scaffold-provenance.json` as externally owned (out of SDD's
refresh/freshness scope), and SDD embeds **no** provider-specific id, path, or URL —
the reference runnable-UI provider ships in
[FS.GG.Rendering](https://github.com/FS-GG/FS.GG.Rendering).

For the skeleton **only** (no runtime template), use `fsgg-sdd init` — `scaffold`
without `--provider` blocks and points you there.

## Scope

FS.GG.SDD owns:

- project charter and policy workflow;
- specify, clarify, checklist, plan, tasks, analyze, implement, verify, and ship
  lifecycle commands;
- the cross-cutting `fsgg-sdd scaffold` command and its generic, schema-versioned
  template-provider contract (provider selection, invocation protocol, produced-file
  provenance, diagnostics, and report projections);
- structured lifecycle artifact schemas;
- normalized work model generation;
- agent command and skill generation;
- generated readiness views for SDD artifacts;
- the scheduled/on-demand `fsgg-sdd validate` exhaustive validation harness over
  SDD's broad matrices (separate from the inner loop, no Governance required);
- the SDD-owned release-readiness contract, SemVer versioning policy, schema
  reference, and `dotnet tool` distribution of the `fsgg-sdd` CLI;
- integration contracts with FS.GG.Governance.

FS.GG.SDD does not own the governance rule engine. Rule evaluation, evidence
freshness, routing, profiles, and gate enforcement belong in
[FS.GG.Governance](https://github.com/FS-GG/FS.GG.Governance).

## Current State

- Spec Kit initialized under `.specify/`.
- F# constitution ratified for this SDD product.
- Claude and Codex guidance files are present.
- `FS.GG.SDD.Artifacts` defines the first typed lifecycle artifact model.
- `FS.GG.SDD.Commands` and `FS.GG.SDD.Cli` provide the native command workflow
  slices through `fsgg-sdd ship`: a public MVU/report surface, SDD
  skeleton creation, charter/specification authoring, clarification decisions,
  requirements-quality checklist authoring, technical plan authoring, stable
  task graph authoring, authored evidence declarations, question/decision,
  checklist item/result, plan decision/contract/verification, task ids,
  cross-artifact analysis readiness, evidence readiness summaries,
  verification readiness (task/evidence/test/skill dispositions over a generated
  `readiness/<id>/verify.json` view pointing to ship), merge-boundary ship
  readiness (aggregated lifecycle/verification/evidence/generated-view state over a
  generated `readiness/<id>/ship.json` view pointing to the protected-boundary
  handoff), deterministic JSON/text reports, generated work-model, analysis-view,
  and verification-view refresh/diagnostics, and no required Governance runtime.
- `fsgg-sdd scaffold` delivers a one-command path from an empty directory to a
  buildable, SDD-managed product via an external template provider: a generic,
  schema-versioned provider contract (`.fsgg/providers.yml`), a `dotnet new` wrapper
  at a new MVU `RunProcess` edge (install + update + create), the
  `.fsgg/scaffold-provenance.json` record (externally-owned produced paths excluded
  by `refresh`), ten actionable `scaffold.*` diagnostics splitting user-input (exit 1)
  from provider defects (exit 2), and identical facts across the JSON/text/rich
  report projections. `fsgg-sdd init` stays byte-identical; no provider-specific id,
  path, or URL lives in generic SDD.
- The SDD-owned release-readiness machine contract
  ([docs/release/release-readiness.json](docs/release/release-readiness.json))
  declares the single reconciled version, the compatibility matrix, and the
  schema reference catalog. Its human projections — the SemVer
  [versioning policy](docs/release/versioning-policy.md),
  [compatibility matrix](docs/release/compatibility-matrix.md),
  [schema reference](docs/release/schema-reference.md), and
  [installation guide](docs/release/installation.md) for installing `fsgg-sdd`
  via `dotnet tool install` — live in [docs/release/](docs/release/).
- The detailed implementation roadmap lives in
  [docs/initial-implementation-plan.md](docs/initial-implementation-plan.md).

## Workflow

Use standard Spec Kit:

```text
charter -> specify -> clarify -> checklist -> plan -> tasks -> analyze -> evidence -> verify -> ship
```

For the native SDD product lifecycle, `fsgg-sdd analyze` runs after
`fsgg-sdd tasks`, `fsgg-sdd evidence` records declared implementation,
verification, synthetic, and deferral evidence, `fsgg-sdd verify` evaluates
SDD-owned verification readiness over the task/evidence/test/skill obligations,
emits `readiness/<id>/verify.json`, and points verification-ready work to ship,
and `fsgg-sdd ship` aggregates SDD-owned merge-boundary readiness, emits
`readiness/<id>/ship.json`, and points ship-ready work to the Governance-owned
protected-boundary handoff. `fsgg-sdd agents` is a cross-cutting generator (not a
lifecycle stage) that derives per-target Claude/Codex command and skill guidance
from `readiness/<id>/work-model.json` into
`readiness/<id>/agent-commands/<target>/`, marked generated with source digests
and never a second source of truth.

`fsgg-sdd scaffold` is a cross-cutting command (not a lifecycle stage;
`nextLifecycleCommand Scaffold = None`) that establishes the SDD skeleton (reusing
`init`'s effects unchanged) and then invokes an external template provider to
materialize a runnable product — the recommended starting point for a new project
(see [Getting started](#getting-started)). Selecting a provider never alters the
canonical `charter → ship` ordering.

`fsgg-sdd validate` is a separate cross-cutting validation harness (not a lifecycle
stage) that exhaustively exercises SDD's broad matrices — command × projection ×
state, determinism/degradation, release baseline-conformance, and Governance-handoff
compatibility — on demand and on a schedule, emitting one deterministic
`validation-report` JSON. It runs apart from the cheap inner loop, requires no
Governance runtime, and computes no Governance verdict.

The first implementation feature should create the structured SDD artifact model.
Markdown remains an authoring surface; schema-versioned structured artifacts are
the machine contract.

For a command-by-command walkthrough from `fsgg-sdd init` through `fsgg-sdd ship`
with no Governance installed, see the [Quickstart](docs/quickstart.md). To adopt
native SDD artifacts from an existing standard Spec Kit project additively, see
[Migration from Spec Kit](docs/migration-from-spec-kit.md).

## Agent Context

- Claude: read [CLAUDE.md](CLAUDE.md) and `.claude/skills/fs-gg-sdd-project/SKILL.md`.
- Codex: read [AGENTS.md](AGENTS.md) and `.codex/skills/fs-gg-sdd-project/SKILL.md`.

## License

MIT.
