# Phase 0 Research: Publish the `fsgg-sdd` CLI as a dotnet tool

Resolves the open design questions implied by the spec's Technical Context. No `NEEDS
CLARIFICATION` markers remained in the spec; the decisions below pin down *how* the existing
feature-039 producer extends to a second package without disturbing the Contracts line.

## Decision 1 — Version line: the CLI tracks the SDD product line, independently of Contracts

**Decision**: The CLI package version is the **evaluated `FS.GG.SDD.Cli` `<Version>`**, which
inherits the single SDD product-line value from `Directory.Build.local.props` (`0.2.0` today).
It is resolved independently of `FS.GG.Contracts` (`1.1.0`). Each package publishes its own
evaluated version in the same workflow run.

**Rationale**: `Directory.Build.local.props` declares `<Version>0.2.0</Version>` as the "single
semantic-version source of truth for all FS.GG.SDD.* packages and the fsgg-sdd CLI"; every
`.fsproj` inherits it and `FS.GG.Contracts` is the deliberate **exception** that overrides with
its own `<Version>1.1.0`. The CLI therefore *is* the product line. Feature 043 already
established this split — it published Contracts `1.1.0` via `workflow_dispatch` precisely "to
avoid minting a misleading `v1.1.0` *product* tag when the SDD product line is `0.2.0`."

**Alternatives considered**: Pin the CLI to the Contracts wave (`1.1.0`). Rejected — it would
mis-version the product line, contradict the props source-of-truth comment, and couple two
contracts that move on different cadences. The source issue explicitly permits "its own SemVer."

## Decision 2 — Version-bearing tag drift-guard generalized to "matches at least one line"

**Decision**: On a real publish event (`release: published` / `push: tags v*`), a version-bearing
tag (`v<semver>`) MUST equal **at least one** of the two packages' evaluated versions
(`{Contracts, CLI}`); otherwise the run fails loudly. Each package then publishes **its own**
evaluated version regardless of which line the tag named. This generalizes the feature-039
single-package guard ("tag must equal the one package's version") to the two-package producer.

**Rationale**: With two divergent version lines, no single tag can equal both, so the original
per-package "tag must equal *my* version" guard would fail every product release (a `v0.2.0`
product tag ≠ Contracts `1.1.0`). The generalization keeps the real protection — a stray/typo
tag matching *neither* live version still fails — while letting a coherent product tag (`v0.2.0`)
or a contracts tag (`v1.1.0`) both succeed, with each package publishing its evaluated version.

**FR-014 reconciliation (the one delta to Contracts behavior)**: This *relaxes* exactly one
prior Contracts rule — feature-039 contract conformance C2, "a mismatched version-bearing tag
fails loudly." Under the new rule, a tag that matches the CLI line but not Contracts no longer
fails the Contracts publish; Contracts simply publishes its evaluated `1.1.0`. Everything else
about the Contracts publish (its version source, gating, idempotency, least-privilege creds,
canonical-repo guard, single-package scope) is unchanged, and Contracts still publishes its own
evaluated version. The relaxation is strictly an improvement (it removes a spurious failure on
product-line releases) and is the minimal change required to let the repo cut a product release.
This is the single decision worth a maintainer nod; see `contracts/release-workflow.md`
"Supersession" and the plan completion note.

**Alternatives considered**:
- *Per-package fail (status quo, unchanged)*: rejected — every product `v0.2.0` release fails the
  Contracts job.
- *Publish Contracts only via dispatch, CLI only via tags/releases (split triggers)*: rejected —
  narrows Contracts' trigger surface more than the at-least-one-line rule, a larger behavior
  change for no extra safety.
- *Line-aware per-package guard (each package fails only on a same-line patch mismatch)*:
  rejected as needlessly clever; "matches at least one live version" is simpler and as safe.

## Decision 3 — `workflow_dispatch` inputs stay minimal and back-compatible

**Decision**: Keep the existing single optional `version` input **Contracts-scoped** (its
feature-039/043 meaning: "publish exactly this Contracts version"). The CLI always tracks its
evaluated `<Version>`. A `workflow_dispatch` with the `version` input **empty** remains a
**dry run**: pack both packages, push neither. With `version` non-empty, push is enabled and
both packages publish their resolved versions (Contracts = input override, CLI = evaluated).

**Rationale**: Preserves 043's exact recommended publish path (dispatch with `version=1.1.0`)
byte-for-byte while making that same run also publish the CLI `0.2.0` — which is the cheap,
unambiguous way to satisfy the immediate `.github#49` need right now. Adds no new input.

**Alternatives considered**: Add a second `cli_version` input or a `package` selector. Deferred
as unnecessary additive surface — noted as a future option, not built (idiomatic simplicity).

## Decision 4 — Self-containment: a dotnet tool pack bundles the full dependency closure

**Decision**: Rely on `dotnet pack` of the `PackAsTool=true` CLI to bundle the entire runtime
dependency closure — `FS.GG.SDD.Artifacts` (the `RegistryDocument` YAML loader), `.Commands`,
`.Validation`, `FS.GG.Contracts`, `Spectre.Console`, and `YamlDotNet` — into the tool package.
Verify the property directly with an **offline pack→install→run smoke**: pack the tool, install
it into a throwaway `--tool-path` from the local artifacts (no feed), and run
`fsgg-sdd registry validate <fixture>` against both a well-formed and a malformed fixture,
asserting success and non-zero exits respectively.

**Rationale**: A .NET tool package is a framework-dependent application package: `dotnet pack`
includes the project's publish output (all referenced assemblies) plus `DotnetToolSettings.xml`.
This is what closes gap #2 from the issue (the published Contracts package lacks the YAML loader;
the tool package bundles it). The loader lives in `FS.GG.SDD.Artifacts`
(`LifecycleArtifacts/RegistryDocument.fs`, `YamlDotNet` dependency) and is a project reference of
the CLI, so it is in the closure. The smoke is the real, fixture-based evidence (Constitution VI,
real-filesystem fixtures over mocks) and runs entirely offline.

**Alternatives considered**: Trust packaging without verification (rejected — self-containment is
the genuinely new behavioral risk and is cheap to assert); add `--self-contained`/AOT (rejected —
unnecessary; a framework-dependent tool is the standard, smallest form and matches `dotnet tool
install` expectations).

## Decision 5 — Feed package visibility is a one-time operational step, mirroring Contracts

**Decision**: After the first push, set the `FS.GG.SDD.Cli` org package visibility to **public**
(as was done for `FS.GG.Contracts`), so consumer CI restores/installs it with a run-scoped
`GITHUB_TOKEN`. Capture this as an explicit operational step in `quickstart.md`; it is a
package-settings action, not workflow YAML.

**Rationale**: A first-time GitHub Packages publish defaults to private/inherited visibility;
consumer CI in *other* repos (the `.github` coherence gate) cannot read a private org package
with only its own run-scoped token. `FS.GG.Contracts` was made public for exactly this reason.
The workflow cannot reliably flip visibility itself, so the feature owns the one-time step.

**Alternatives considered**: Automate visibility in the workflow (rejected — the REST surface for
NuGet package visibility on org packages is not a stable one-liner; a documented one-time op is
honest and matches the Contracts precedent).

## Decision 6 — Gate the CLI publish on the CLI test suite

**Decision**: Gate the CLI publish on a locked-restore + `dotnet test
tests/FS.GG.SDD.Cli.Tests/FS.GG.SDD.Cli.Tests.fsproj -c Release` job (mirroring the existing
`contracts-tests` → `publish` gate). The Contracts publish keeps its own `contracts-tests` gate
unchanged. A red CLI test run never reaches the CLI push.

**Rationale**: `FS.GG.SDD.Cli.Tests` already exercises the CLI surface including
`ValidateCommandTests` (the `registry validate` command) and degradation/format/rich projections;
it transitively builds the loader path the tool depends on. Gating the publish on it gives the
"fail before push" guarantee Constitution VIII and the producer contract require, scoped to the
package being published.

**Alternatives considered**: Gate the CLI publish on the whole solution's tests (rejected as
heavier than needed for a release cut; the CLI suite plus locked restore already covers the
publishable closure). No gate (rejected — violates the producer's gating contract).

## Decision 7 — Job topology: a shared resolve step feeding two independent publish jobs

**Decision**: Restructure `release.yml` into: a shared `resolve-versions` step/job that evaluates
both versions and applies the at-least-one-line tag guard (Decision 2), feeding two gated publish
jobs — `publish-contracts` (gated on `contracts-tests`) and `publish-cli` (gated on `cli-tests`).
Each publish job independently asserts a non-empty pack, pushes with `--skip-duplicate`, and fails
the run on any non-duplicate push error. If either publish job fails, the run fails (FR-012).

**Rationale**: The at-least-one-line guard needs both evaluated versions in one place, so a single
resolve point is cleanest and keeps the guard logic from being duplicated/divergent across jobs.
Two independent publish jobs keep each package's pack/push isolated and let the run-level failure
semantics satisfy "an incomplete publish is never reported complete."

**Alternatives considered**: One combined publish job packing+pushing both (rejected — couples the
two pushes; a Contracts push failure would obscure CLI status and vice versa). Keep Contracts'
inline version resolution and bolt CLI on separately (rejected — duplicates the tag guard and
leaves the two guards able to drift).
