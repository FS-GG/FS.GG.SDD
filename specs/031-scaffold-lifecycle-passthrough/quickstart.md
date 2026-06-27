# Quickstart: Verify scaffold lifecycle pass-through & app-only provenance

This guide runs the verification suite that closes the **P2 · sdd** gate. Everything runs in
the SDD repo against repo-owned fixtures — no FS.GG.Rendering dependency.

## Prerequisites

- .NET SDK with `dotnet new` available (the scaffold process edge shells to it).
- Repo restored: `dotnet restore FS.GG.SDD.sln`.

## 1. Run the full verification suite

```bash
dotnet test FS.GG.SDD.sln
```

Expected: green. The new scenarios live in the existing scaffold test modules:

- `ScaffoldCommandTests.fs` — forwarding (set/order + verbatim echo), app-only provenance
  (precision/recall, no skeleton, init byte-identity), determinism, value-agnosticism, and
  the FR-008 edges, all under `--param lifecycle=sdd`.
- `ScaffoldGuardTests.fs` — identifier deny-list, scoped lifecycle-**value** (`spec-kit`) scan,
  and the automated planted-violation proof (research Decision 9).
- `ScaffoldParityTests.fs` — three-projection produced-path fact parity for a `lifecycle=sdd`
  run.

## 2. Target just the lifecycle scenarios (faster inner loop)

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj \
  --filter "FullyQualifiedName~Scaffold"
```

## 3. What each acceptance scenario proves

| You want to confirm | Scenario asserts | Spec |
|---|---|---|
| `lifecycle=sdd` reaches the provider verbatim | recording fixture's `scaffold-manifest.txt` contains `lifecycle=sdd` | US1.1 / FR-002 |
| SDD adds/drops/renames no params | dry-run create-arg `--key value` vector == `defaults ⊕ --param` (research Decision 8) | US1.3 / FR-003 / SC-001 |
| order doesn't matter | reversed `--param` order → identical vector | FR-008 |
| provenance is app-only | producedPaths == provider files, all `generatedProduct`, no skeleton path | US2 / FR-004,005 / SC-002,003 |
| skeleton is unchanged | each skeleton file byte-identical to a plain `init` run | FR-005 |
| determinism | two clean runs → byte-identical provenance + JSON report | US2.4 / FR-006 / SC-004 |
| no rendering leak | identifier deny-list + scoped lifecycle-value (`spec-kit`) scan clean, plus behavioral value-agnosticism (arbitrary value forwards identically) | US3.1,3.2 / FR-007 |
| the scan actually bites | planted-violation unit test catches + locates | US3.3 / SC-005 |

## 4. Confirm the leak scan fails on a planted violation

The planted-violation behavior is itself automated (no manual edit needed) — see the
`ScaffoldGuardTests` planted-violation fact. To sanity-check manually:

```bash
# Plant a forbidden identifier in scaffold source, then run the guard — it must fail
#   and name the file. (Revert afterwards.)
dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj \
  --filter "FullyQualifiedName~ScaffoldGuardTests"
```

Expected with a planted token: failure naming `"{path}: {token}"`. Clean tree: green.

## 5. Confirm no public surface moved (SC-007)

```bash
git status --porcelain   # specs/031-*, tests/fixtures/scaffold-provider/lifecycle*,
                         # the three scaffold test modules, the 030→031 feature pointer
                         # (.specify/feature.json, CLAUDE.md), and the one documented
                         # forwarding-fix in HandlersScaffold.fs (research Decision 8)
```

The four `PublicSurface.baseline` snapshots and all golden outputs MUST be unchanged — the
forwarding fix touches no public surface, schema, or projection (research Decision 8); the rest
of the feature ships verification + fixtures only.

## Contracts & details

- Forwarding rules: [contracts/forwarding-invariant.md](./contracts/forwarding-invariant.md)
- Provenance rules: [contracts/app-only-provenance.md](./contracts/app-only-provenance.md)
- Fixture shape: [contracts/recording-fixture-provider.md](./contracts/recording-fixture-provider.md)
- Leak scan: [contracts/leak-invariant-scan.md](./contracts/leak-invariant-scan.md)
- Decisions & rationale: [research.md](./research.md) · Entities: [data-model.md](./data-model.md)
