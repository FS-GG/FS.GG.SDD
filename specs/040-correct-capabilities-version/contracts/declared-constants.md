# Contract: Corrected declared-constant set + verification

**Audience**: every consumer that re-types onto `FS.GG.Contracts` (Governance,
Templates, Rendering) and the package's own verification suite.

## Declared Governance-owned constants (post-correction)

`Fsgg.Schemas` declares four Governance-owned schema versions. They are *declared
reference* values — what the Governance published reference uses — **not** values
SDD emits.

| `Fsgg.Schemas.*` | Value | Status |
|------------------|:-----:|--------|
| `governanceVersion`   | 1 | unchanged |
| `policyVersion`       | 1 | unchanged |
| `capabilitiesVersion` | **2** | corrected (was 1) |
| `toolingVersion`      | 1 | unchanged |

- Public signature (`Schemas.fsi`) is unchanged: `val capabilitiesVersion: int`.
- `Schemas.entries` still enumerates exactly the 10 named schemas with unchanged
  owners; the `capabilities` entry's `SchemaVersion` derives from the constant
  (now 2).
- The constant is the **authoritative** machine contract; spec prose agrees.

## Verification contract

`tests/FS.GG.Contracts.Tests/SchemaVersionConstantTests.fs`, fact
*"Governance-owned schema versions equal the declared reference values"*:

```fsharp
Assert.Equal(1, Schemas.governanceVersion)
Assert.Equal(1, Schemas.policyVersion)
Assert.Equal(2, Schemas.capabilitiesVersion)   // corrected; grounded against the
                                                // Governance published reference
                                                // (decision 2026-06-28 / Governance#14)
Assert.Equal(1, Schemas.toolingVersion)
```

- Grounding is a **declared-reference assertion** (no Governance runtime, no
  external fixture), consistent with how the suite already documents these
  constants.
- The SDD-owned-constants fact and the `entries`/owner/surface-baseline facts are
  **unchanged** and MUST still pass — they guard FR-002 / FR-007 (no SDD emission
  or surface drift).

## Acceptance mapping

| Acceptance / SC | Verified by |
|-----------------|-------------|
| US1 AC1 (`capabilities` reads 2) | `Assert.Equal(2, …capabilitiesVersion)` |
| US1 AC2 (siblings read 1) | `governance`/`policy`/`tooling` assertions = 1 |
| US1 AC3 / SC-001 (suite asserts 2 and passes) | full `SchemaVersionConstantTests` green |
| FR-007 / SC-004 (no SDD emission/surface change) | unchanged SDD-owned + `entries` + `PublicSurface.baseline` facts pass |
