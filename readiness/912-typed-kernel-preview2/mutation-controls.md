# Package identity mutation controls

The accepted workflow packs `FS.GG.SDD.Artifacts` from its source-resolved
coherent version and passes no global identity property.

On 2026-08-24, the focused `PackedDependencyContractTests` pair was run against
each of these temporary mutations to `.github/workflows/release.yml`:

- add `-p:Version=${{ needs.resolve-versions.outputs.artifacts_version }}`;
- add `-p:PackageVersion=${{ needs.resolve-versions.outputs.artifacts_version }}`.

Each mutation produced one failing and one passing test (`exit 1`). The static
workflow assertion identified the exact forbidden substring. After restoring the
accepted workflow, both tests passed. The paired real-pack test also opened
`FS.GG.SDD.Artifacts.1.3.0-preview.2.nupkg` and required package version
`1.3.0-preview.2` plus dependency `FS.GG.Contracts` version `7.5.2`.

Command:

```text
dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj \
  -c Debug --no-build --no-restore \
  --filter FullyQualifiedName~PackedDependencyContractTests
```
