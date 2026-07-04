# FS.GG.SDD

Spec-driven development lifecycle tooling for FS.GG. `fsgg-sdd` is a CLI that takes a
product team from an empty directory to a buildable, lifecycle-managed product, then
through a structured development lifecycle — charter, specification, plan, tasks,
evidence, verification, and ship — giving humans, agents, CLI automation, and optional
Governance gates the same machine contract. Markdown is the authoring surface;
schema-versioned structured artifacts are the machine contract.

## Install

```sh
dotnet tool install --global FS.GG.SDD.Cli   # exposes the `fsgg-sdd` command
```

See the [installation guide](docs/release/installation.md) for versions and feeds.

## Create a new project

The default way to start is `fsgg-sdd scaffold`: it establishes the SDD lifecycle
skeleton and invokes an external **template provider** to materialize a runnable
product, in one command.

```sh
fsgg-sdd scaffold --root ./MyApp --provider <name> --param productName=MyApp

cd ./MyApp && dotnet build && dotnet run   # the runnable product
fsgg-sdd charter                           # continue the lifecycle
```

- `--provider <name>` resolves from an author-/provider-owned `.fsgg/providers.yml`
  registry through a generic, schema-versioned provider contract; the provider's
  `templateId` + `source` are realized via `dotnet new`.
- The template is refreshed each run (`dotnet new install` + `dotnet new update`);
  `--no-update` skips the refresh, `--force` materializes into a non-empty target,
  `--dry-run` plans without executing.
- Produced files are recorded in `.fsgg/scaffold-provenance.json` as externally owned
  (out of SDD's refresh/freshness scope). SDD embeds **no** provider-specific id, path,
  or URL — the reference runnable-UI provider ships in
  [FS.GG.Rendering](https://github.com/FS-GG/FS.GG.Rendering).
- For the skeleton **only** (no runtime template), use `fsgg-sdd init`; `scaffold`
  without `--provider` points you there.

Diagnostics are actionable and split by cause: malformed user input (unknown provider,
unsupported contract version, missing required parameter, target collision) exits `1`;
a provider defect (provider failed, engine unavailable, provider wrote into SDD-owned
trees) exits `2`. An incomplete scaffold is never reported as complete.

## The lifecycle

Drive a work item through the lifecycle:

```text
charter -> specify -> clarify -> checklist -> plan -> tasks -> analyze -> evidence -> verify -> ship
```

Each command reads and writes structured artifacts and emits a deterministic report.
`analyze` runs after `tasks`; `evidence` records declared implementation,
verification, synthetic, and deferral evidence; `verify` evaluates verification
readiness over the task/evidence/test/skill obligations into
`readiness/<id>/verify.json`; `ship` aggregates merge-boundary readiness into
`readiness/<id>/ship.json` and points ship-ready work to the Governance-owned
protected-boundary handoff.

Cross-cutting commands sit outside the lifecycle chain: `agents` and `refresh`
regenerate views, `validate` runs the deep conformance matrices, and `doctor` /
`upgrade` reconcile a scaffolded product's drift from its coherent set — `doctor`
is a read-only drift report, `upgrade` the interactive/`--yes` remediation. See
[Doctor & Upgrade](docs/reference/doctor-upgrade.md). For which lifecycle artifacts
are durable (commit) versus regenerable (gitignore), see the
[Artifact Taxonomy](docs/reference/artifact-taxonomy.md).

For a command-by-command walkthrough from `fsgg-sdd init` through `fsgg-sdd ship` with
no Governance installed, see the [Quickstart](docs/quickstart.md).

## Cross-cutting commands

These are not lifecycle stages and never alter the `charter -> ship` ordering:

- **`fsgg-sdd scaffold`** — create a runnable, SDD-managed product from a template
  provider (above).
- **`fsgg-sdd agents`** — generate per-target Claude/Codex command + skill guidance
  from `readiness/<id>/work-model.json`, marked generated and never a second source of
  truth.
- **`fsgg-sdd refresh`** — bring a work item's generated views back to currency.
- **`fsgg-sdd validate`** — exhaustively exercise SDD's command × projection × state
  matrices (determinism, degradation, release baseline-conformance, Governance-handoff
  compatibility) on demand or on a schedule, emitting one deterministic
  `validation-report`.

## Output formats

Every command projects the same report three ways, selected by flag with precedence
`--rich` > `--text` > `--json` > default:

- default / `--json` — the deterministic JSON automation contract.
- `--text` — a portable plain-text summary.
- `--rich` — a human-oriented Spectre.Console rendering; a pure projection over the
  same report that degrades to plain text with zero ANSI when output is
  non-interactive/redirected or color is disabled (`NO_COLOR`, `TERM=dumb`).

## Governance is optional

FS.GG.SDD builds, installs, and runs the full lifecycle through `fsgg-sdd ship` with
**no Governance present**. Rule evaluation, evidence freshness, routing, profiles, and
gate enforcement belong to
[FS.GG.Governance](https://github.com/FS-GG/FS.GG.Governance); SDD integrates with it
only through explicit, versioned, optional contracts. To adopt Governance, see
[adopting Governance](docs/adopting-governance.md).

## Documentation

- [Quickstart](docs/quickstart.md) — `init` through `ship`, no Governance.
- [Installation](docs/release/installation.md) ·
  [versioning policy](docs/release/versioning-policy.md) ·
  [compatibility matrix](docs/release/compatibility-matrix.md) ·
  [schema reference](docs/release/schema-reference.md).
- [Developing FS.GG.SDD](DEVELOPING.md) — contributing to this repository itself.

## License

MIT.
