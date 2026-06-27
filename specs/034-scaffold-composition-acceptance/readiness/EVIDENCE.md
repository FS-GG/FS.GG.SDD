# Composition acceptance — verification evidence

## Offline inner loop (no registry)

`dotnet test tests/FS.GG.SDD.Acceptance.Tests` with `FSGG_SDD_ACCEPTANCE_REGISTRY`
**unset**: `Passed: 6, Skipped: 3, Failed: 0`. The six offline facts run (verdict-mapping
unit test ×2, result-schema byte-exact golden, env-unset gate proof, config-error mapping,
no-Governance guard); the three network-gated facts self-skip at discovery. The inner loop
stays green and network-free (FR-010 / SC-003).

## Real composition spine (end-to-end, real `dotnet new`/build/run/git)

The orchestration spine was exercised end-to-end against a **neutral in-repo fixture provider**
to prove the harness mechanics with real evidence (Principle VI — real `dotnet new install` +
`dotnet new` + `dotnet build` + `dotnet run --no-build` + `git init` + in-process `refresh`,
no mocks). The registry named the provider `rendering` and resolved it to the
`fsgg-fixture-lifecycle` template under `tests/fixtures/scaffold-provider/lifecycle/`.

`dotnet test --filter kind=composition-acceptance` with the fixture-backed registry set:
`Passed: 3, Skipped: 0, Failed: 0` — the orchestration PASS (all nine facts true), the
two-run determinism check (byte-identical bodies modulo the null-normalized sensed block),
and the best-effort unavailable-provider check.

The emitted verdict=`pass` result document is captured at
[`composition-acceptance.fixture-pass.json`](./composition-acceptance.fixture-pass.json).

### SYNTHETIC disclosure (Principle V)

The **template identity** in the real-spine run above is the neutral in-repo fixture
(`fsgg-fixture-lifecycle`), **not** the real published rendering template. This substitutes
for the external provider, which is reached only through `FSGG_SDD_ACCEPTANCE_REGISTRY` and
lives in the rendering repo (out of scope for this repo by FR-009). Everything else is real:
real process/filesystem I/O, the real scaffold MVU loop, real provenance, real refresh. The
real-PASS path against the **published** rendering template is exercised by the scheduled
workflow (`.github/workflows/composition-acceptance.yml`) with the registry secret set; it was
not run in the implementation environment (no published registry available). The
`(providerFailed, scaffold.providerUnavailable) → skip-unavailable` mapping is proven
deterministically offline by the verdict-resolution unit test.
