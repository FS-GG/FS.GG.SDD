# Contract: Composition Acceptance Protocol

The behavioral contract for one run of the real-provider composition acceptance. It adds **no**
new lifecycle interface — it consumes the existing `scaffold-provider` v1, `scaffold-provenance`
v1, and the scaffold outcome/exit vocabulary unchanged.

## Inputs

| Input | Value | Notes |
|---|---|---|
| `--provider` | `rendering` | author-supplied provider *name* (generic token) |
| `--param lifecycle` | `sdd` | forwarded verbatim by scaffold |
| `FSGG_SDD_ACCEPTANCE_REGISTRY` | filesystem path | **required** to run; path to the external author-supplied `.fsgg/providers.yml` resolving the real published rendering template |
| `FSGG_SDD_ACCEPTANCE_RESULT_PATH` | filesystem path | **optional**; overrides where the result document is written. Default `<productRoot>/composition-acceptance.json` |
| product root | fresh empty directory | created per run (temp) |

The real template identity is **only** ever present in the external registry file. It MUST NOT
appear in SDD source, this contract, the acceptance code, or the result document (FR-009/SC-003).

## Gating (FR-010)

- `FSGG_SDD_ACCEPTANCE_REGISTRY` **unset or empty** ⇒ each acceptance fact **skips** (xUnit
  `Assert.Skip`). No network is touched; no result document is written; the default offline
  `dotnet test FS.GG.SDD.sln` stays green.
- The facts are tagged `[<Trait("kind","composition-acceptance")>]`; CI selects them with
  `dotnet test --filter kind=composition-acceptance` and the env set.

## Run sequence

1. Create an empty product root.
2. Copy the registry from `FSGG_SDD_ACCEPTANCE_REGISTRY` to `<root>/.fsgg/providers.yml`.
3. Run the scaffold MVU loop (`init`→…→`Scaffold`) with `Provider=Some "rendering"`,
   `Parameters=["lifecycle","sdd"]` over the real provider (real `dotnet new install`/`create`).
4. Read the scaffold `--json` report `outcome` **and its diagnostic code**; branch per the
   mapping below.
5. On a success outcome, assert each fact (skeleton, constitution, build, run, git, chmod,
   provenance partition, refresh exclusion, completeness).
6. Emit one result document (deterministic body + normalized `sensed` block); fail the test iff
   the verdict is `Fail`; skip iff `SkipUnavailable`.

The build/run probe (FR-003) is bounded so a hung app fails rather than hangs:

- `dotnet build` over the produced product with a **300 s** timeout ⇒ `appBuilds` iff exit 0.
- A **headless** run smoke (no display server required, so it is CI-reproducible): prefer a
  non-interactive `--help`/`--version`-style invocation; if the app exposes none, launch it
  headless and require it to survive a **10 s grace window** without a non-zero exit, then
  terminate it. Overall run-probe timeout **60 s**. `appRuns` iff the probe exits 0 **or** the
  process stayed alive through the grace window (it started and did not crash).

## Outcome → verdict mapping (FR-008)

The scaffold `--json` `outcome` is one of exactly **four** values from the existing
`ScaffoldOutcome` vocabulary — `providerSucceeded`, `providerSucceededEmpty`, `providerNotRun`,
`providerFailed`. Provider-**unavailable** and provider-**wrote-into-the-SDD-tree** are **not**
distinct outcomes: both surface as `providerFailed`, and are told apart **only** by the
accompanying **diagnostic code**. The acceptance therefore resolves the verdict from the
`(outcome, diagnostic code)` pair, not the outcome alone — otherwise an unavailable provider
(SKIP) is indistinguishable from an SDD-relevant provider defect (FAIL), defeating SC-004.

| Scaffold outcome | Diagnostic code | Exit | Verdict | Test result |
|---|---|---|---|---|
| `providerSucceeded` + all facts true | — | 0 | `pass` | pass |
| `providerSucceeded` + a fact false | — | 0 | `fail` | fail |
| `providerSucceededEmpty` | `scaffold.providerEmpty` | 0 | `fail` (incomplete, FR-007) | fail |
| `providerFailed` | `scaffold.providerUnavailable` | 2 | `skip-unavailable` | skip |
| `providerFailed` | `scaffold.providerWroteSddTree` | 2 | `fail` (provider defect) | fail |
| `providerFailed` | `scaffold.providerFailed` | 2 | `fail` (provider defect) | fail |
| `providerNotRun` | `scaffold.providerMissing` / `providerUnknown` / `providerVersionUnsupported` / `providerParamMissing` / `targetCollision` | 1 | `fail` (config error) | fail |

Guarantees:

- **No false PASS** — `pass` requires the success outcome *and* all facts (SC-002).
- **Unavailable is SKIP, never a FAIL of SDD** (FR-008/SC-004).
- **Incomplete is never complete** — an empty/partial scaffold cannot yield `pass` (FR-007).
- **Defect vs config vs SDD** are distinct verdicts (Edge Cases, constitution VIII).

## Edge cases (must hold)

- **Provider unavailable** → `skip-unavailable`; no false PASS/FAIL.
- **Provider defect / writes into SDD tree** → `fail` (provider defect) at exit 2.
- **App fails to build or run** → `fail`, distinguishing "files produced" from "working product".
- **git absent / pre-existing work tree** → `gitInitialized` holds via the scaffold's
  skip-non-fatal path; the run still passes on the rest (FR-004).
- **Registry omitted/misconfigured** → config-error `fail`; never silently falls back to a
  fixture or an embedded identifier.

## Non-goals (FR-012)

- No Governance runtime is required and no Governance verdict is computed.
- No new outcome/exit code is introduced; the acceptance only interprets existing ones.
- `fsgg-sdd validate` is unchanged; this acceptance is separate and network-gated.
