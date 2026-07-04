# FS.GG.SDD.Cli — `fsgg-sdd`

Spec-driven development lifecycle tooling for FS.GG. `fsgg-sdd` is a .NET tool that
takes a product team from an empty directory to a buildable, lifecycle-managed
workspace, then through a structured development lifecycle — charter, specification,
plan, tasks, evidence, verification, and ship — giving humans, agents, CLI automation,
and optional Governance gates the same machine contract.

Markdown is the authoring surface; schema-versioned structured artifacts are the
machine contract.

## Install

```sh
dotnet tool install --global FS.GG.SDD.Cli   # exposes the `fsgg-sdd` command
```

## Create a new workspace

```sh
fsgg-sdd scaffold --root ./MyApp --provider <name> --param productName=MyApp

cd ./MyApp && dotnet build && dotnet run   # the runnable app
fsgg-sdd charter                           # continue the lifecycle
```

`--provider <name>` resolves from an author-owned `.fsgg/providers.yml` registry
through a generic, schema-versioned provider contract. SDD embeds no
provider-specific id, path, or URL.

## Output formats

Every command projects the same `CommandReport` three ways: `--json` (the default
deterministic automation contract), `--text` (portable plain text), and `--rich`
(Spectre.Console panels/tables/color, degrading to plain text when non-interactive).

## Links

- Source & docs: <https://github.com/FS-GG/FS.GG.SDD>
- Contracts package: [`FS.GG.Contracts`](https://www.nuget.org/packages/FS.GG.Contracts)

Licensed under the MIT License.
