# Phase 0 Research: Scaffold Composition Acceptance

All NEEDS CLARIFICATION resolved. Each decision is grounded in the current tree
(verified 2026-06-27 @ `58b4b0c`).

## D1 — Housing: separate gated acceptance, NOT a `validate` dimension

- **Decision**: The real-provider composition acceptance lives as a **separate, opt-in,
  network-gated acceptance** (a new xUnit project), not as a new dimension inside
  `fsgg-sdd validate`.
- **Rationale**: `fsgg-sdd validate` is contractually **offline** and **byte-identical
  deterministic** — `ValidationHarness` even defines a *determinism* matrix and a
  `validation-report` whose sensed block is null-normalized (INV-5) so two runs are
  byte-identical. A real-template dimension would (a) require network + a package feed inside
  the validate contract, (b) inject non-determinism (resolved template version) that fights the
  determinism matrix, and (c) risk pulling rendering knowledge toward validate. The spec
  (Assumptions) explicitly leaves this to planning and requires the acceptance to stay opt-in,
  network-gated, and out of the inner loop — all of which a separate acceptance satisfies
  cleanly while leaving `validate` untouched.
- **Alternatives considered**: *Extend validate* — rejected (determinism + network + scope
  collision above). *CI-only black-box shell script* — viable and keeps test projects 100%
  offline, but loses in-repo structured assertions and reuse of the existing in-process driver,
  and yields a less diffable result; rejected in favor of the xUnit harness, which still drives
  real `dotnet new`/`build`/`run` end to end. (User-confirmed during planning.)

## D2 — Gating: env-gated dynamic SKIP via `Assert.Skip`

- **Decision**: Gate on the presence of the `FSGG_SDD_ACCEPTANCE_REGISTRY` environment variable
  (path to the author-supplied registry). When unset/empty, each acceptance fact **dynamically
  skips** using xUnit's `Assert.Skip` / `Assert.SkipWhen` (available in xUnit **2.9.3**, the
  version already pinned in `Directory.Packages.props`).
- **Rationale**: A dynamic skip is honest — the default offline `dotnet test` reports the
  acceptance as *skipped*, not as a hollow pass and not as a failure. The inner loop stays green
  with **no** network (FR-010/SC-003). Presence of the env var is also the natural "registry
  supplied" signal, so no second flag is needed.
- **Alternatives considered**: *Static `[<Fact(Skip=...)>]`* — can't react to runtime env, so it
  could never run. *Return-as-noop when unset* — reports a false PASS, violating honesty.
  *`Xunit.SkippableFact` package* — unnecessary given 2.9.3's built-in `Assert.Skip`.

## D3 — Isolation: new trait-tagged project `tests/FS.GG.SDD.Acceptance.Tests`

- **Decision**: Put the acceptance in a **new** test project added to `FS.GG.SDD.sln`, with every
  fact tagged `[<Trait("kind","composition-acceptance")>]`. It builds in the default solution
  build (so it can't rot) but self-skips offline; the scheduled workflow selects it with
  `dotnet test --filter kind=composition-acceptance` and the env set.
- **Rationale**: Keeps the three existing offline projects (`Artifacts.Tests`, `Commands.Tests`,
  `Cli.Tests`, `Validation.Tests`) byte-unchanged and network-free. A trait gives CI an explicit
  include/exclude lever independent of the env-skip, satisfying "excluded from the default inner
  loop" by both mechanisms (filter *and* skip).
- **Alternatives considered**: *Add facts to `Commands.Tests`* — would mix a network/real-template
  surface into the core offline suite and complicate the deny-list scan; rejected.

## D4 — Real provider identity reached only through the external registry

- **Decision**: The real rendering template identity is supplied **exclusively** by the
  author-owned `.fsgg/providers.yml` referenced by `FSGG_SDD_ACCEPTANCE_REGISTRY`. The acceptance
  copies that file into `<productRoot>/.fsgg/providers.yml` before invoking scaffold (scaffold's
  `resolveDescriptors` reads the registry from the product root). No rendering package id /
  template id / source path / docs URL appears in SDD source, contracts, reports, or the
  acceptance code.
- **Rationale**: FR-009/SC-003 forbid any embedded rendering identifier in generic SDD; the
  registry is the contract's designated external channel. The provider *name* `"rendering"` and
  the `ArtifactOwner.Rendering` / `ownerFromValue "rendering"` vocabulary in `ArtifactRef.fs` are
  **generic owner tokens**, not package/template identifiers, and are allowed (they already ship).
- **Enforcement**: Extend the existing `ScaffoldGuardTests` deny-list (`forbiddenTokens =
  [ "fs-gg-ui"; "FS.GG.Rendering" ]`) scan to also cover `tests/FS.GG.SDD.Acceptance.Tests/**`,
  so a leaked identifier in the acceptance fails the offline guard. The guard file itself stays
  excluded (it names the tokens), per its existing convention.
- **Alternatives considered**: *Commit a real registry fixture* — would embed the identifier,
  violating FR-009; rejected. *Hardcode the template id behind a flag* — same violation.

## D5 — Result document is harness output, not a release-catalog artifact

- **Decision**: The per-run **result document** (`composition-acceptance-result` v1) is sensed
  harness output, not a produced lifecycle artifact. Add it to the **declared-exception** list in
  `docs/release/schema-reference.md` (alongside the `validate` `validation-report`); do **not**
  add it to `release-readiness.json`.
- **Rationale**: It carries sensed metadata (resolved version, availability, host) and verdicts,
  exactly the profile `schema-reference.md` already exempts for the validate report. Adding it to
  the release catalog would force it into the determinism/golden contract it cannot satisfy.
- **Alternatives considered**: *Catalog it* — rejected (sensed/non-deterministic; not a lifecycle
  artifact). *Emit nothing structured* — rejected; FR-011/SC-005 require a diffable per-run record.

## D6 — build/run probe shape and timeout

- **Decision**: After a successful instantiation, the acceptance shells, at the test edge:
  `dotnet build` over the produced product root, then a **bounded** run. The run is a headless,
  timeout-guarded `dotnet run` (preferring a non-interactive entry — e.g. `--help`/a smoke flag
  if the produced app exposes one, otherwise a short run with a hard timeout treated as
  "started successfully"). Build failure ⇒ FAIL with the build diagnostic; run failure (non-zero
  before the grace window, or crash) ⇒ FAIL distinct from build; clean start within the window ⇒
  the build/run fact holds.
- **Rationale**: FR-003 requires proving the product *builds and runs*, not merely that files
  exist; a timeout-bounded run avoids hanging on a long-lived UI process while still proving it
  launches. The probe lives at the harness edge (constitution V), keeping the lifecycle MVU pure.
- **Alternatives considered**: *Build only* — insufficient for FR-003. *Unbounded run* — would
  hang a UI app; rejected.

## D7 — Outcome → verdict mapping (reuses the existing vocabulary, FR-008)

- **Decision**: Read the scaffold `--json` report `outcome` **and its diagnostic code** and map the
  `(outcome, diagnostic)` pair onto the verdict. The real `ScaffoldOutcome` DU has only four values
  (`providerSucceeded`, `providerSucceededEmpty`, `providerNotRun`, `providerFailed`); unavailable
  and wrote-SDD-tree are **not** outcomes — both surface as `providerFailed` and are separated only
  by the diagnostic, so the outcome alone cannot tell SKIP from FAIL:

  | Scaffold outcome | Diagnostic code | Exit | Verdict |
  |---|---|---|---|
  | `providerSucceeded` (+ all facts hold) | — | 0 | **PASS** |
  | `providerSucceeded` but a fact fails (no constitution / build / run / provenance / refresh) | — | 0 | **FAIL** |
  | `providerSucceededEmpty` (no app produced) | `scaffold.providerEmpty` | 0 | **FAIL** (incomplete ≠ complete, FR-007) |
  | `providerFailed` (feed/network/version unresolvable) | `scaffold.providerUnavailable` | 2 | **SKIP-unavailable** |
  | `providerFailed` (wrote into SDD tree / non-zero exit) | `scaffold.providerWroteSddTree` / `scaffold.providerFailed` | 2 | **FAIL** (provider defect) |
  | `providerNotRun` (registry missing/misconfigured, param/version/collision) | `scaffold.providerMissing` / `providerUnknown` / … | 1 | **FAIL** (config error) |

- **Rationale**: FR-008 requires mapping onto the *existing* outcome/exit vocabulary and reporting
  unavailable as SKIP — never a false PASS or a false FAIL of SDD. The acceptance adds no new
  outcome; it interprets the ones scaffold already emits. Because exit 2 + `providerFailed` is
  shared by unavailable and defect, the **diagnostic code** is the required discriminator (the fact
  surfaced in `/speckit-analyze` finding F1).
- **Alternatives considered**: *New acceptance-specific outcomes* — rejected; would add contract
  surface the spec forbids (Assumptions: "adds verification, not new contract surface").

## D8 — Determinism & sensed-metadata normalization (FR-011/SC-005)

- **Decision**: The result document's stable body (inputs, verdict, per-fact booleans, mapped
  outcome) is byte-deterministic; a separate, clearly-labeled **`sensed`** block carries the
  legitimately variable values (resolved provider/template version, availability, host,
  timestamp). For golden/diff comparison the `sensed` block is **null-normalized**, mirroring the
  `validate` report's INV-5 normalization in `ValidationContracts.fs`.
- **Rationale**: SC-005 requires two same-input runs to be byte-identical *apart from sensed
  metadata*. Reusing the established validate normalization pattern keeps the result diffable and
  the determinism assertion mechanical.
- **Alternatives considered**: *Inline sensed values in the body* — would break byte-identity;
  rejected. *Drop sensed values entirely* — loses reproducibility provenance (which version was
  exercised); rejected.

## Resolved unknowns summary

| Unknown | Resolution |
|---|---|
| Where the acceptance lives | Separate gated xUnit project (D1, D3) |
| How it's kept out of the inner loop | Trait filter + env-gated `Assert.Skip` (D2, D3) |
| How the real template is reached without leaking identity | External registry via `FSGG_SDD_ACCEPTANCE_REGISTRY`, copied into product `.fsgg/` (D4) |
| Whether it's a release artifact | No — `schema-reference.md` exception (D5) |
| How build/run is proven | `dotnet build` + bounded `dotnet run` at the test edge (D6) |
| Verdict vocabulary | Mapped from existing scaffold outcomes/exits (D7) |
| Reproducibility/determinism | Deterministic body + normalized sensed block (D8) |
