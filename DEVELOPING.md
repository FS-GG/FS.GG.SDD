# Developing FS.GG.SDD

This document covers contributing to the **FS.GG.SDD component itself**. To *use* the
`fsgg-sdd` CLI on your own workspaces, see the [README](README.md).

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
- F# `.fsi` signature files for submodules under `CommandReports/` and
  `CommandWorkflow/` follow a two-tier convention keyed on **module visibility**, not
  per-file taste:
  - A **public** submodule (`namespace FS.GG.SDD.Commands`, `module <Name>`) carries
    its own paired `.fsi` to pin and document that public surface (today:
    `LifecycleFooter`, `LintEngine`, `ProcessSkillManifest` — deliberately public so
    `Cli` / a test project can reference them as ordinary public API, e.g.
    `LintEngine.classify` for its coupling regression test).
  - An **internal** implementation submodule (`namespace FS.GG.SDD.Commands.Internal`,
    `module internal <Name>`) has **no** individual `.fsi`; its surface is already
    closed by `module internal` plus the aggregating `CommandReports.fsi` /
    `CommandWorkflow.fsi`. Adding a per-file `.fsi` here is redundant. (A test project
    may still reach these via `InternalsVisibleTo` — that is *not* a reason to make
    the module public or to add a `.fsi`.)

  So a new submodule gets a `.fsi` iff it is declared a public
  `namespace FS.GG.SDD.Commands` module.

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

### Testing tiers

A whole-solution `dotnet test` takes minutes, which is too slow for a casual inner loop.
The cost is **concentrated, not diffuse**: it lives in the ~140 tests that spawn a real
`dotnet`/CLI/git subprocess (scaffold's real `dotnet new`, the CLI-raw smokes, the apphost
help/validate/lint smokes, the git-backed gitignore checks). The other ~1,130 tests —
parsers, codec, work model, serializers, command handlers, report projections — run
in-process in seconds. But ~770 of those cheap tests live *inside* the `Commands.Tests` and
`Cli.Tests` projects, next to their subprocess-spawning siblings, so project granularity
alone cannot reach them cheaply. They are separated by a trait: the subprocess-spawning
tests carry `[<Trait("tier", "slow")>]`, and the cheap tiers exclude them with
`--filter tier!=slow` ([#209](https://github.com/FS-GG/FS.GG.SDD/issues/209)).

[`scripts/test.sh`](scripts/test.sh) runs a **tier** matched to what you changed:

| Tier | Command | Runs | Tests | Wall | Run it when |
|---|---|---|---|---|---|
| **fast** | `scripts/test.sh fast` | pure + `Commands`/`Cli` in-process (`tier!=slow`) | 1,132 | ~14s | every save — parser, work model, codec, handlers, report work |
| **component** | `scripts/test.sh component` | + Validation + full `Cli` (incl. CLI process smokes) | 1,189 | ~25s | before pushing a CLI, report-projection, or validation change |
| **full** | `scripts/test.sh` | every project, unfiltered | ~1,297 | ~2–3m | before push, and whenever you touched the scaffold/CLI subprocess tests |

The trait is deliberately **not** a CI filter: the PR gate runs every project unfiltered,
so the union of the tiers is the whole suite and no test is reachable only under a filter.
Add `--no-build` to reuse existing binaries, and `-- <args>` to forward to `dotnet test`
(e.g. `scripts/test.sh fast -- --filter 'FullyQualifiedName~Codec'` — a user `--filter`
wins over the tier's). `scripts/test.sh --help` prints the same table.

**Tiering removes no CI coverage.** The per-PR gate (`.github/workflows/gate.yml`) runs
`bash scripts/test.sh --no-build` — the same `full` tier defined here, looping every test
project unfiltered — and is still required, so a `Commands`-layer regression is caught at
PR CI even if you only ran the fast tier locally, and the gate can't drift from the local
`full` runner (nor reintroduce the solution-wide resource-exhaustion hazard the loop
avoids). The tiers speed the *local* loop; the gate remains the backstop.

The script loops the six test projects rather than running `dotnet test FS.GG.SDD.sln`.
That covers exactly the same tests (those six *are* the solution's test projects), avoids
the resource exhaustion a solution-wide run hits when every test host starts at once
(`Failed to create CoreCLR, HRESULT: 0x80070008`, or a 90s protocol-negotiation timeout —
both misread as a red suite), and prints a per-project timing table so the cost stays
visible. `Acceptance.Tests` is network-gated and self-skips unless
`FSGG_SDD_ACCEPTANCE_REGISTRY` is set, so even `full` stays offline by default.

> **If a run stops producing output, it may be hung rather than slow.** The CLI-smoke helper
> `runCliRaw` can deadlock against a chatty stderr ([#212](https://github.com/FS-GG/FS.GG.SDD/issues/212)),
> and its own timeout cannot fire. Check with `ps -o pcpu -p <pid>` — 0% CPU means wedged, not
> working. Wrapping a long run in `timeout -k 10 900 …` keeps a wedge from eating the session.

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
