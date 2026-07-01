---
name: fs-gg-sdd-project
description: Work on FS.GG.SDD, the FS.GG spec-driven development lifecycle product. Use when creating or editing SDD lifecycle specs, plans, schemas, generated views, agent commands, or Governance integration contracts.
---

# FS.GG.SDD Project Skill

Use this skill for work in the FS.GG.SDD repository.

## Boundaries

- FS.GG.SDD owns the lifecycle product: charter, specify, clarify, checklist,
  plan, tasks, analyze, evidence declarations, normalized work model, generated
  SDD views, and agent command/skill generation.
- `fsgg-sdd init` seeds the SDD skeleton, including an authored
  `.fsgg/constitution.md` lifecycle constitution — generic, deterministic, and
  no-clobber on re-run. Scaffold delivers it via the reused `init` effects; it is
  never app-only `generatedProduct` provenance and `refresh` never regenerates it.
- FS.GG.Governance owns rule evaluation, evidence freshness, routing, profiles,
  and gate enforcement.
- FS.GG.Rendering is a possible customer, not the shape of this repository.

## Required Reading

Before changing behavior, read:

1. `.specify/memory/constitution.md`
2. `docs/initial-implementation-plan.md`
3. `README.md`

## Working Rules

- Drive work through the native `fsgg-sdd` lifecycle: charter -> specify ->
  clarify -> checklist -> plan -> tasks -> analyze -> [implement] -> evidence ->
  verify -> ship. The per-stage skills are [[fs-gg-sdd-lifecycle]] and the
  `fs-gg-sdd-*` family; legacy `speckit-*` commands are not the active process
  (see `docs/migration-from-spec-kit.md` for coexistence only).
- Do not add source projects to the scaffold without a feature spec.
- Treat Markdown as authoring surface and schema-versioned structured artifacts
  as the machine contract.
- For every lifecycle artifact, identify the authored source, structured model,
  generated views, stale-view behavior, and diagnostics.
- Keep Claude and Codex guidance equivalent when workflow behavior changes.
- Integrate Governance only through explicit optional contracts.

## Authoring contracts

Three authored inputs are load-bearing — a strict grammar decides acceptance, so
a subtly wrong form blocks a gate. The durable reference (with copyable examples
and a drift guard) is `docs/reference/authoring-contracts.md`. Summary:

- **Acceptance coverage line.** `checklist` marks `FR-###` covered only when a
  list item leads with `- FR-###:` and carries the acceptance reference (`AC-###`,
  optional `US-###`) **on the same line** (e.g.
  `- FR-001: W/S move the left paddle. (covers AC-002)`). A bold `**FR-001**`, a
  colon-less line, or a separate `(covers AC-###)` line is counted by the loose
  scan but **not** covered.
- **`evidence.yml` satisfaction.** `kind` ∈ `implementation · verification ·
  review · generated-view · synthetic · deferral · note · missing` (an unknown
  kind silently becomes `verification`); `result` ∈ `pass · fail · deferred ·
  missing · stale · advisory · blocked`. An obligation is satisfied only by a
  matching **non-synthetic** declaration with `result: pass`; a synthetic `pass`
  and a `deferral` do not satisfy it. This lifecycle `evidence.yml` is distinct
  from any unrelated "evidence" document a scaffolded product may ship.
- **`specify --input` intent.** Supply `value:`, `scope:`, and `requirement:` on
  their own labeled lines; an unlabeled line is read as the user value.

## CLI output formats

`fsgg-sdd` projects the same `CommandReport` three ways, selected by flag with
precedence `--rich` > `--text` > `--json` > default:

- default / `--json` — deterministic JSON automation contract (unchanged default).
- `--text` — portable plain-text summary.
- `--rich` — human-oriented Spectre.Console rendering (panels, tables, color),
  a pure projection over the same report. It changes no JSON byte, stream, or exit
  code, and degrades to plain text with zero ANSI when output is
  non-interactive/redirected or color is disabled (`NO_COLOR`, or `TERM=dumb`).
  Rich output is presentation only and excluded from deterministic/golden contracts.

## Validation harness

`fsgg-sdd validate` is a cross-cutting validation harness (not a lifecycle stage;
reachable only via the CLI, never from a lifecycle command path). It exhaustively
exercises SDD's broad matrices — every command × output projection × representative
state, determinism/degradation, release baseline-conformance, and Governance-handoff
compatibility — on demand and on a schedule, separate from the cheap inner loop. It
emits one deterministic `validation-report` JSON (`--json` default, `--text`
projection; `--rich` renders the report richly via Spectre.Console, degrading to
plain text when non-interactive or color-disabled), requires no Governance runtime,
and computes no Governance verdict. The report is not catalogued in `release-readiness.json` (a
declared exception in `docs/release/schema-reference.md`).

## Scaffold

`fsgg-sdd scaffold` is a cross-cutting command (not a lifecycle stage;
`nextLifecycleCommand Scaffold = None`, FR-015) that takes a product author from an
empty directory to a buildable, runnable, SDD-managed product in one invocation. It
establishes the SDD skeleton (reusing `init`'s effects unchanged, so `init` stays
byte-identical), then invokes an external template provider selected by
`--provider <name>` and resolved from an author-/provider-owned `.fsgg/providers.yml`
registry through a generic, schema-versioned provider contract (v1). The provider is
realized via a generic `dotnet new` wrapper at the MVU `RunProcess` edge.

SDD owns only the contract, the invocation protocol, the
`.fsgg/scaffold-provenance.json` record (schema v1; produced paths marked
`generatedProduct` — externally owned, which `refresh` excludes per FR-007), the
`scaffold.*` diagnostics, and the three report projections — **never** any
provider-specific package id, template id, path, or docs URL (FR-002 / SC-005).

Scaffold requires `--provider`; with none it blocks with `scaffold.providerMissing`
pointing to `fsgg-sdd init` for the skeleton only. Options: `--param key=value`
(repeatable), `--force` (materialize into a non-empty target), `--root`, `--dry-run`.
User-input failures (`providerUnknown`/`providerVersionUnsupported`/
`providerParamMissing`/`targetCollision`) exit 1; provider defects
(`providerFailed`/`providerUnavailable`/`providerWroteSddTree`) exit 2; an incomplete
scaffold is never reported as complete (FR-009). On a provider defect the report carries
an additive `providerInvocation` block (json/text/rich, feature 054) — the invoked command
line, captured stdout/stderr (bounded per stream with a truncation flag), and exit code
(`null` when the engine never launched, FR-003) — so the failure is diagnosable from the
report alone. It is `null` on success/dry-run/user-input blocks (FR-006), reaches
`.fsgg/scaffold-provenance.json` never (schema v1, FR-010), and changes no outcome string
or exit code (FR-007). The reference provider (a full
runnable UI app) ships in the FS.GG.Rendering repo, demonstrated against the contract
without placing any Rendering knowledge in generic SDD. After a successful
instantiation, scaffold itself owns two generic post-instantiation steps —
initializing a git repository at the product root (skipped non-fatally inside an
existing work tree or when git is absent) and setting the executable bit on each
produced `.sh` script — reported in all three projections, never delegated to the
provider. The real-provider composition acceptance (`tests/FS.GG.SDD.Acceptance.Tests`)
is opt-in and network-gated — it drives the real published provider only when
`FSGG_SDD_ACCEPTANCE_REGISTRY` is set, stays out of the default offline inner loop, and
emits a deterministic `composition-acceptance-result` v1 verdict (a declared
release-catalog exception, not a lifecycle artifact).

## First Feature Bias

The first implementation feature should define the SDD artifact model and
schemas before commands or generators are built.
