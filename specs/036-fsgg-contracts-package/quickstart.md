# Quickstart & Validation Guide: FS.GG.Contracts Package

Runnable scenarios that prove the package satisfies its success criteria. Types and
fields are defined in [data-model.md](./data-model.md) and [contracts/](./contracts);
this guide is run/validate only.

## Prerequisites

- .NET SDK with `net10.0` support (repo standard).
- Repo restored: `dotnet restore` from repo root.
- New projects added to `FS.GG.SDD.sln`: `src/FS.GG.Contracts` and
  `tests/FS.GG.Contracts.Tests`.

## Scenario A — Package builds BCL-only (SC-004, FR-001/002)

```bash
dotnet build src/FS.GG.Contracts/FS.GG.Contracts.fsproj -c Release
```

**Expected**: builds clean. Dependency closure contains only `FSharp.Core` — no
`YamlDotNet`, `System.Text.Json`, `Spectre.Console`, Governance, or rendering packages.
Verified by `DependencyClosureTests` (allowlist = `{FSharp.Core}`) and by inspecting the
`.fsproj` (`<PackageReference Include="FSharp.Core" />` only) and the generated
`.deps.json`.

## Scenario B — Every `.fsgg` schema is represented with a version constant (US1, SC-001)

Enumerate `Fsgg.Schemas.entries` and confirm all 10 named schemas appear, each with a
typed record and a named version constant:

```
providers · project · sdd · agents · scaffold-provenance · governance-handoff
governance · policy · capabilities · tooling
```

**Expected**: `entries` has 10 members; every name above is present exactly once.
Asserted by `SchemaVersionConstantTests`.

## Scenario C — Version constants equal today's emitted values (US1, FR-005, SC-002)

For each SDD-owned schema, the package constant equals the version SDD writes today:

| Constant | Expected |
|----------|----------|
| `providersVersion`, `projectVersion`, `sddVersion`, `agentsVersion` | `1` |
| `scaffoldProvenanceVersion`, `governanceHandoffVersion` | `1` |
| `governanceHandoffContractVersion` | `"1.0.0"` |

**Expected**: all equalities hold (`SchemaVersionConstantTests`). The SDD-side proof of
"zero byte diff when a consumer re-types onto these records" is exercised in #9; here we
assert the constants match the values grounded in
`src/FS.GG.SDD.Artifacts` (`ScaffoldProvenance.fs`, `GovernanceHandoff.fs`, the
`LifecycleArtifacts/Config` parsers).

## Scenario D — Provider descriptor defaults reproduce today's behaviour (US2, SC-003)

```
// no commands declared
let d = { Name=...; ContractVersion="1.0.0"; TemplateId=...; Source=...; Parameters=[]
          Build=None; Test=None; Run=None; Verify=None; NameParameter="name" }
```

**Expected**:
- `d.Build/Test/Run/Verify` are all `None` ⇒ consumers fall back to platform defaults
  (no observable change for existing providers).
- `Provider.resolveNameParameter d = "name"` (and `Provider.defaultNameParameter = "name"`).
- A descriptor that declares commands exposes each `DeclaredCommand` exactly as authored.
- A declared command with blank `Executable` ⇒ `Provider.isMalformed = true` (surfaced,
  not silently defaulted).
- The five preserved fields match SDD's current `ProviderDescriptor` shape (additive).

Asserted by `ProviderDescriptorTests`.

## Scenario E — Registry validator: coherent / incoherent / incomplete (US3, SC-007)

```
// coherent
{ Components=[{Id="FS.GG.Contracts";Version="1.0.0"}; {Id="FS.GG.SDD";Version="0.2.0"}]
  Edges=[{Consumer="FS.GG.SDD";Provider="FS.GG.Contracts";CompatibleRange=">=1.0.0 <2.0.0"}] }
```

**Expected**:
- Coherent model ⇒ `Registry.validate` returns `Valid` (no diagnostics).
- Incoherent model (edge `CompatibleRange` excludes the provider's declared `Version`)
  ⇒ `Invalid [ { Rule = IncompatibleVersion; Entry = <edge>; ... } ]`.
- Incomplete model (entry missing a required field) ⇒
  `Invalid [ { Rule = MissingField "<field>"; Entry = <entry>; ... } ]`.
- Edge referencing an absent component ⇒ `Invalid [ { Rule = UnknownComponent; ... } ]`.

Each diagnostic names the offending entry and the violated rule. Asserted by
`RegistryValidatorTests`.

## Scenario F — Self-describing contract version (FR-012)

**Expected**: `Fsgg.ContractVersion.value = "1.0.0"`, `major=1`, `minor=0`, `patch=0`.

## Scenario G — Pack & consume from a local feed (SC-005, FR-011)

```bash
dotnet pack src/FS.GG.Contracts/FS.GG.Contracts.fsproj -c Release -o ./artifacts/local-feed
# add ./artifacts/local-feed as a NuGet source and restore it from a throwaway probe project
```

**Expected**: `FS.GG.Contracts.<version>.nupkg` is produced; a probe project restores it
from the local folder feed and resolves `Fsgg.Schemas`/`Fsgg.Provider`/`Fsgg.Registry`.

## Scenario H — Additive guarantee: existing SDD unchanged (FR-010, SC-006)

```bash
dotnet test FS.GG.SDD.sln
```

**Expected**: the entire existing SDD suite passes unchanged; no file under
`src/FS.GG.SDD.*` is modified, and no existing golden fixture/baseline changes. SDD adds
**no** reference to `FS.GG.Contracts` in this item.

## Scenario I — Public surface baseline (Principle III)

**Expected**: the package's exported surface matches `PublicSurface.baseline`; any
addition/removal is a deliberate baseline update.

## Scenario J — Cross-repo registry registration (FR-013, SC-008)

**Expected**: `registry/dependencies.yml` and `docs/registry/compatibility.md` in
`FS-GG/.github` record the new `FS.GG.Contracts` surface, linking FS-GG/FS.GG.SDD#8 as
the tracking reference (filed via the coordination protocol / ADR-0001). This step is a
cross-repo PR, not a local build artifact.
