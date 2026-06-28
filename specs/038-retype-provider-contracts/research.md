# Phase 0 Research: Re-type Provider Registry onto FS.GG.Contracts

All open decisions the spec deferred "to the plan" are resolved below. No
`NEEDS CLARIFICATION` items remain.

## D1 — How is `FS.GG.Contracts` consumed by `FS.GG.SDD.Artifacts`?

**Decision**: Add a `<ProjectReference>` to `src/FS.GG.Contracts/FS.GG.Contracts.fsproj`
from `FS.GG.SDD.Artifacts.fsproj`. The reference is placed before the existing
`<Compile>` ItemGroup (project references resolve independently of compile order).

**Rationale**: Every in-repo consumer of `FS.GG.Contracts` today (only
`FS.GG.Contracts.Tests`) uses a `ProjectReference`, and `FS.GG.Contracts` is a
BCL-only leaf (FSharp.Core only) — referencing it from `Artifacts` introduces no
cycle (`Artifacts` does not feed `Contracts`). A versioned `PackageReference` would
add packaging/version-bump friction for a same-repo, same-solution dependency and
is unnecessary while both ship from this repo.

**Alternatives considered**:
- *PackageReference with version 1.0.0* — rejected: no published feed is required
  in-repo; couples the inner loop to a pack/restore step.
- *Copy the extended fields into the local type* — rejected: that is exactly the
  re-encoding the feature retires (FR-001, SC-001).

## D2 — `.fsgg/providers.yml` encoding for the extended fields

**Decision**: The extended fields use natural keys that mirror the canonical
record. Each declared command is a nested mapping with a scalar `executable` and a
sequence `arguments`; `nameParameter` is a top-level scalar on the provider entry:

```yaml
schemaVersion: 1
providers:
  - name: fixture
    contractVersion: "1.0.0"
    templateId: fsgg-fixture-app
    source: __FIXTURE__/ok
    parameters:
      - key: productName
        required: true
    build:                       # optional
      executable: dotnet
      arguments: [build, -c, Release]
    test:                        # optional
      executable: dotnet
      arguments: [test]
    run:                         # optional
      executable: dotnet
      arguments: [run, --no-build]
    verify:                      # optional
      executable: ./verify.sh
      arguments: []
    nameParameter: projectName   # optional; default "name"
```

**Rationale**: The keys map 1:1 to `Fsgg.Provider.ProviderDescriptor`
(`Build/Test/Run/Verify` → `build/test/run/verify`; `NameParameter` →
`nameParameter`) and to `DeclaredCommand` (`Executable`/`Arguments` →
`executable`/`arguments`). This matches the existing camelCase convention already
used by the registry (`contractVersion`, `templateId`) and the `parameters`
sub-mapping (`key`/`required`/`default`). The `arguments` sequence reuses the same
YamlDotNet sequence-of-scalars shape already parsed for `parameters`.

**Alternatives considered**:
- *Flat keys (`buildExecutable`, `buildArguments`)* — rejected: does not reflect
  the `DeclaredCommand` record nesting and reads poorly for four commands.
- *Single command string parsed into exe+args* — rejected: shell-splitting is
  ambiguous and contradicts the explicit `(Executable, Arguments)` contract shape.

## D3 — Blank / whitespace declared executable

**Decision**: When a `build/test/run/verify` mapping is present but its
`executable` is null/empty/whitespace, the field parses to `None` (absent) — never
to a `DeclaredCommand` with an empty executable. Implemented by reading the
mapping into a candidate `DeclaredCommand` and discarding it when
`Fsgg.Provider.isMalformed` is true.

**Rationale**: Directly satisfies FR-005 and matches feature 035 FR-010 and the
contract's own `isMalformed` helper — a single shared definition of "not a
launchable command."

## D4 — `nameParameter` default

**Decision**: Read the optional `nameParameter` scalar; resolve the stored
`ProviderDescriptor.NameParameter` through `Fsgg.Provider.resolveNameParameter`
(or, equivalently, `Option.defaultValue defaultNameParameter` followed by the
blank-guard), yielding `"name"` when absent or blank.

**Rationale**: FR-004 and the contract's `defaultNameParameter = "name"` /
`resolveNameParameter` helpers already encode this; reuse them rather than
re-deriving the default.

## D5 — Probe declared-command parameter type unification

**Decision**: Delete the local `DeclaredCommand` in
`AcceptanceSupport.fs` and retype `buildProbe` / `runProbe` (and the resolvers
`resolveBuildCommand` / `resolveRunCommand`) to take
`Fsgg.Provider.DeclaredCommand option`. Add a `ProjectReference` to
`FS.GG.Contracts` from the acceptance project.

**Rationale**: Feature 035 introduced the local `DeclaredCommand` explicitly as a
"1:1 forward-compatible read" of the H2 descriptor fields; the canonical type is
field-identical (`Executable`, `Arguments`), so the resolvers need no logic change.
This retires the second copy (the spec's expected realization) and lets the harness
pass `descriptor.Build` / `descriptor.Run` straight through.

**Alternatives considered**:
- *Keep the local type and map at the call site* — rejected: leaves a third
  provider-shaped re-encoding alive, the opposite of this feature's intent.

## D6 — Does referencing `FS.GG.Contracts` from the acceptance project violate T021a?

**Decision**: No. The acceptance project may reference `FS.GG.Contracts` and use
`Fsgg.Provider` types.

**Rationale**: Invariant T021a scans `AcceptanceSupport.fs`, `CompositionResult.fs`,
and the `.fsproj` for the token `FS.GG.Governance` / the symbol `Governance`, and
the feature's intent forbids *provider-specific* identity (a concrete provider's
package id / template id / path / command / docs URL). `FS.GG.Contracts` is, by
constitution, provider-/rendering-/Governance-agnostic — it embeds none of those
strings. Adding `FS.GG.Contracts` to the fsproj does not match the scanned tokens,
and `Fsgg.Provider` carries no provider identity. The harness stays provider-agnostic
(SC-006). The verification still flows only through `.fsgg/providers.yml` copied at
run time, which remains the sole channel of real provider identity.

## D7 — How the acceptance harness obtains the resolved descriptor

**Decision**: In `CompositionAcceptanceTests.fs`, on the success path, parse the
registry already copied to `<root>/.fsgg/providers.yml` via
`ConfigModule.parseProviderRegistry`, select the descriptor whose `Name` equals the
provider the run scaffolded, and pass its `Build` / `Run` to the probes
(`buildProbe descriptor.Build root`, `runProbe descriptor.Run root`). When parsing
yields no matching descriptor (defensive), fall back to `None` (today's behavior).

**Rationale**: Keeps the harness provider-agnostic — it reads whatever the gated
registry declares rather than hard-coding any provider's commands (FR-008, FR-011).
The reference provider declares no `build`/`run`, so both resolve to `None` and the
`dotnet` defaults run, preserving the verdict (FR-009, SC-004). The provider name is
already known to the run (it is the `--provider` value the harness scaffolds with),
so no new identity is introduced.

**Alternatives considered**:
- *Read declared commands from the scaffold provenance record* — rejected: the
  provenance schema records `ProviderName`/`ProviderContractVersion`, not declared
  commands; re-parsing the registry is the authoritative source and avoids a
  provenance-schema change (out of scope, FR-012).

## D8 — Surface baselines and golden fixtures

**Decision**: No baseline edits for `FS.GG.SDD.Artifacts` or `FS.GG.SDD.Commands`.
The scaffold golden/diagnostic fixtures are asserted unchanged.

**Rationale**: Both `PublicSurface.baseline` files are produced by reflecting
*static method names* on modules (`{type.FullName}.{method.Name}`), not type/field
signatures. `parseProviderRegistry`'s name is unchanged and the deleted records were
never in those baselines; the canonical types' baseline already lives in
`FS.GG.Contracts.Tests/PublicSurface.baseline` (current). Byte-for-byte report
preservation (SC-002) is enforced by re-running the existing scaffold test matrix.
