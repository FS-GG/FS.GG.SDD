# Developing FS.GG.SDD

This document covers contributing to the **FS.GG.SDD product itself**. To *use* the
`fsgg-sdd` CLI on your own projects, see the [README](README.md).

## Process

Work in this repository is itself spec-driven: use standard **Spec Kit**. Product
source and tests now exist; add or change them only through feature specs and plans
that define the artifact contract and verification plan. Follow the constitution at
[`.specify/memory/constitution.md`](.specify/memory/constitution.md).

> Spec Kit is the **contributor process for this repository** and is distinct from the
> `fsgg-sdd` product lifecycle (`charter → ship`) that ships to users. Don't conflate
> the two.

## Conventions

- Treat Markdown as an authoring surface and schema-versioned structured artifacts as
  the machine contract.
- Keep Claude and Codex behavior aligned; update **both** agent surfaces when the
  workflow changes.
- Do not add rendering-specific (or any provider-specific) package names, template
  ids, paths, or docs URLs to generic SDD behavior.

## Build and test

```sh
dotnet build FS.GG.SDD.sln -c Release
dotnet test  FS.GG.SDD.sln -c Release
```

The solution has five library/CLI projects (`Contracts`, `Artifacts`, `Commands`,
`Validation`, `Cli`) and their test projects; each test project carries a
`PublicSurface.baseline` snapshot that must stay in sync with the public surface (a
Tier-1 change updates it). The `WarningsAsErrors` ratchet
(`Directory.Build.local.props`) stays at zero.

## Formatting

F# formatting is enforced by [Fantomas](https://fsprojects.github.io/fantomas/),
configured entirely by the repo-root `.editorconfig` (Fantomas 6+ has no separate
config file). CI runs a **non-required** `format` job (`.github/workflows/gate.yml`)
that fails a mis-formatted PR with an advisory red X; it never blocks the merge.

Use the **same pinned version CI uses (7.0.5)** so your local verdict matches the
gate. Fantomas is installed to a repo-local path, deliberately **not** into
`.config/dotnet-tools.json` (that manifest is a managed org file pinned
byte-identical to `FS-GG/.github`):

```sh
# Install the pinned formatter (once):
dotnet tool install fantomas --version 7.0.5 --tool-path ./.fantomas-tool --allow-roll-forward

# Check (what CI runs) — exit 0 when clean, non-zero + a list when not:
./.fantomas-tool/fantomas --check .

# Fix — reformat in place, then commit the result:
./.fantomas-tool/fantomas .
```

`.fantomasignore` scopes both commands to authored source (no `obj/`/`bin/` or
generated files). `./.fantomas-tool/` is git-ignored — do not commit it.

## Current state

This repository started scaffold-only. Feature work has since added:

- **`FS.GG.SDD.Artifacts`** — the typed lifecycle artifact model and normalized
  work-model generation.
- **`FS.GG.SDD.Commands` / `FS.GG.SDD.Cli`** — the native MVU command workflow through
  `fsgg-sdd ship` (charter/specify/clarify/checklist/plan/tasks/analyze/evidence/
  verify/ship), plus the cross-cutting `agents`, `refresh`, `validate`, and `scaffold`
  commands, with deterministic JSON/text/rich report projections and no required
  Governance runtime.
- **`fsgg-sdd scaffold`** — the template-provider command and its generic,
  schema-versioned provider contract (`.fsgg/providers.yml`,
  `.fsgg/scaffold-provenance.json`), realized via a `dotnet new` wrapper at the MVU
  `RunProcess` edge.
- The SDD-owned **release-readiness machine contract**
  ([docs/release/release-readiness.json](docs/release/release-readiness.json))
  declaring the single reconciled version, the compatibility matrix, and the
  schema-reference catalog, with `dotnet tool` distribution of the `fsgg-sdd` CLI.

The detailed implementation roadmap lives in
[docs/initial-implementation-plan.md](docs/initial-implementation-plan.md); the
original design in [docs/initial-design.md](docs/initial-design.md).

## Agent context

- **Claude** — read [CLAUDE.md](CLAUDE.md) and
  [`.claude/skills/fs-gg-sdd-project/SKILL.md`](.claude/skills/fs-gg-sdd-project/SKILL.md).
- **Codex** — read [AGENTS.md](AGENTS.md) and
  [`.codex/skills/fs-gg-sdd-project/SKILL.md`](.codex/skills/fs-gg-sdd-project/SKILL.md).

## Adopting native SDD from an existing Spec Kit project

To adopt native SDD artifacts from an existing standard Spec Kit project additively,
see [Migration from Spec Kit](docs/migration-from-spec-kit.md).

## Release and versioning

- [Versioning policy](docs/release/versioning-policy.md)
- [Compatibility matrix](docs/release/compatibility-matrix.md)
- [Schema reference](docs/release/schema-reference.md)
- [Release-readiness contract](docs/release/release-readiness.json)
- [Migration notes](docs/release/migrations/)
