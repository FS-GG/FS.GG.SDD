# FS.GG.Contracts

Shared, BCL-only typed source of truth for the FS.GG spec-driven development platform:
every `.fsgg` schema, the extended template-provider descriptor, and the cross-repo
dependency registry with a pure validator.

The package has no third-party dependency beyond `FSharp.Core` (the F# runtime), so it
is safe to reference from any FS.GG repo without pulling in a wider closure.

## Install

```sh
dotnet add package FS.GG.Contracts
```

## What's inside

- **Schemas** — the typed shapes for the `.fsgg` lifecycle artifacts.
- **Provider** — the schema-versioned template-provider descriptor contract (v1).
- **Registry** — the cross-repo dependency registry model plus a pure validator.
- **Version / ContractVersion** — the coherent version and contract-version types.

## Links

- Source & docs: <https://github.com/FS-GG/FS.GG.SDD>
- The `fsgg-sdd` CLI that produces and consumes these contracts: [`FS.GG.SDD.Cli`](https://www.nuget.org/packages/FS.GG.SDD.Cli)

Licensed under the MIT License.
