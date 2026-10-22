# Implementation Plan: Publish the `fsgg-sdd` CLI as a dotnet tool

## Authoritative visibility and retained promotion — issue1113

Keep the existing workflow and publisher. Add an explicit protected-main retained
selection of run/hash/source plus existing version input. Evaluate versions and
execute current source-test job definitions at original source; use current
protected verifier source for transport/custody/occupancy. Authenticate the original
successful no-push archive and preserve its manifest/dependency/source identities.
Exclude packing whenever any retained-selection field is present. Reauthenticate
ZIP custody and occupancy in the publisher, then keep the ordinary eight writes,
eight archive readbacks and existing public consumer qualification. Serialize normal
and retained rails through one non-cancelling workflow fence. This fence coordinates
this workflow; it supplies no provider-side atomic no-overwrite guarantee.

Implement only the preferred same-token native owner proof. A separate member token
would prove account role without proving package-token private visibility and adds
an unnecessary authority surface. Require explicit package-read credential and actor
binding, read:packages/read:org response capabilities, native active/admin membership,
numeric joins and bounded unfiltered organization inventory. Never export inventory;
retain only numeric witnesses, count/pages/time and canonical population digest.
Keep Actions credentials separate and strip unused credentials from custody child.

Pipeline-preflight choice: extend static dependency/ordering guards and semantic
real-helper fault controls, rather than add a scheduling model. Literal dependencies
already gate publication on the six source jobs and retained qualification; there is
no new cancellation/retry policy to model. These reusable checks catch bypassed gates,
credential exposure, repacking and selection drift before expensive hosted tests.
Initial extension effort cap30minutes; warm offline controls measure under1second
on this workspace, with no setup dependency. CI savings are unmeasured. The changed
concurrency key does not control older workflow revisions; root must independently
retain/fence their original actors and Unknown reservations before any dispatch.

Preflight: dependency-free source guards and synthetic real-helper controls before
SDK setup; no CLR/build or package upload locally. The retained runner already caps
its custody/occupancy subprocesses. Source tests, cold package consumption and required
CI remain genuine release gates and must run on hosted admitted capacity. Source
review is required at the exact final head. Original39161526465/a1 Unknown and c8
candidate custody remain independently reserved. No-push mode never consumes the
new fallback. A passed observation supplies no publication authority.

## Bounded retained occupancy extension — issue1113

Extend the existing release workflow, custody verifier and strict occupancy checker;
add no workflow, release rail or product API. Two optional occupancy inputs select
one read-only job and exclude all existing jobs even on malformed partial input.
A small runner bounds/authenticates the original candidate archive and calls the
existing custody verifier before strict `--candidate` occupancy. The SDK setup is
only for signature verification. Original source/candidate identities and Unknown
occupancy survive independently of the current workflow checkout.

Preflight choice: existing static custody checks plus semantic runner/negative
guard controls. They catch unintended release-job reachability, malformed/mixed
inputs, failed/foreign runs, incomplete/duplicate/expired artifacts, substituted
ZIP bytes/paths/source, failed custody and Unknown feed access before expensive
restore/build gates. A custom scheduling model would add little for this isolated
sequential read-only path; no savings are asserted. Initial source-control effort
cap30minutes; expected offline runtime under1second. Wire runner controls before
SDK setup in the normal gate. Keep actual runtime/occupancy separate from these
synthetic controls and source delivery. No F# signatures/baselines/migrations apply.

**Branch**: `044-publish-cli-tool` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/044-publish-cli-tool/spec.md`

## Summary

Extend the existing feature-039 release producer (`.github/workflows/release.yml`) so a single
release run publishes **two** packages to the org GitHub Packages feed: `FS.GG.Contracts` (behavior
preserved) and the already-`PackAsTool` `FS.GG.SDD.Cli` (`ToolCommandName=fsgg-sdd`, new). This
unblocks FS-GG/.github#49 (coherence id `registry-validator-typed`): once the CLI tool is on the
feed and public, the org `contract-coherence` gate can install it and run
`fsgg-sdd registry validate` with only a run-scoped token — no full SDD source checkout. Because a
dotnet tool package bundles its full dependency closure (the `RegistryDocument` YAML loader in
`FS.GG.SDD.Artifacts` + `YamlDotNet`), the published tool also closes gap #2 from the issue: it can
parse `dependencies.yml` standalone, which the feed `FS.GG.Contracts` package alone cannot.

The CLI tracks the **SDD product line** (`0.2.0`, inherited from `Directory.Build.local.props`),
independent of the Contracts line (`1.1.0`). The one behavioral delta to the Contracts publish is a
*generalization* of the version-bearing-tag guard from "tag must equal the single package version"
to "tag must equal **at least one** of the two evaluated versions" — the minimal change that lets
the repo cut a product-line release (`v0.2.0`) without the Contracts job failing on the mismatch
(research Decision 2; FR-014 reconciliation there).

The committed in-repo changes are: an edit to `release.yml` (shared `resolve-versions` +
`cli-tests` + `publish-cli`), the authoritative two-package contract doc, and (at `/speckit-tasks`
time) the offline self-containment smoke. The CLI `.fsproj` already packs as the `fsgg-sdd` tool and
needs no edit. Feed package visibility is a one-time operational step.

## Technical Context

**Language/Version**: No product F# change. The producer is GitHub Actions YAML invoking .NET SDK
`10.0.x` (`dotnet msbuild -getProperty:Version`, `dotnet pack`, `dotnet nuget push`,
`dotnet tool install`). Product F# / `net10.0` code is untouched.

**Primary Dependencies**: Existing `.github/workflows/release.yml` (feature 039); the org GitHub
Packages feed (`nuget.pkg.github.com/FS-GG`); the `PackAsTool` `src/FS.GG.SDD.Cli` project and its
project-reference closure (`FS.GG.SDD.Artifacts` YAML loader + `YamlDotNet`, `.Commands`,
`.Validation`, `FS.GG.Contracts`, `Spectre.Console`); the consumer `contract-coherence` gate in
`FS-GG/.github` (cross-repo, not edited here).

**Storage**: N/A — transient `artifacts/packages/*.nupkg` on the runner; published `.nupkg`s land on
the org feed.

**Testing**: The CLI publish is gated on `dotnet test tests/FS.GG.SDD.Cli.Tests -c Release` (which
already covers `registry validate` via `ValidateCommandTests`); Contracts keeps its
`FS.GG.Contracts.Tests` gate. The genuinely new behavioral property — the packed tool's runtime
self-containment — is verified by a deterministic **offline pack→install→run smoke** over real
fixtures (Constitution VI; `quickstart.md` C6), not a mock. No new unit test is owed for the
packaging/publish wiring itself (mirrors features 039/043).

**Target Platform**: `ubuntu-latest` GitHub-hosted runner; canonical repo `FS-GG/FS.GG.SDD`.
Consumers install the tool from the org feed on any platform with the .NET SDK.

**Project Type**: Release-engineering / cross-repo integration — a workflow edit + a contract doc +
a verification smoke. The deliverable is a published cross-repo artifact (the `fsgg-sdd` tool
package), not new product code.

**Performance Goals**: N/A (release cadence, not the inner loop).

**Constraints**: Release-time only; does not run in the default offline inner loop and changes no
deterministic CLI/golden contract. Least-privilege publish creds (run-scoped `GITHUB_TOKEN`, no PAT)
and the canonical-repo guard are inherited unchanged from feature 039. The published tool must run
`registry validate` with no SDD source checkout (FR-010). The feed package must be public (FR-011).

**Scale/Scope**: One new published package (`FS.GG.SDD.Cli`) added to the existing producer; one
workflow edit; one contract doc; one verification smoke.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change tier**: **Tier 1 (contracted change)** per *Change Classification* — it adds a new
published **cross-repo integration** artifact (the `fsgg-sdd` tool package consumed by
FS-GG/.github#49) and modifies the release-engineering contract. Requires spec, plan, the updated
contract doc, and verification; no `.fsi`/public-F#-surface change (the CLI/Commands are unchanged),
so no signature/baseline edits are owed.

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec→FSI→Tests→Impl | N/A | No F# change; deliverable is a workflow edit + contract doc + verification smoke. No public surface added. |
| II. Structured artifacts are the machine contract | **PASS** | The producer contract is documented authoritatively in `contracts/release-workflow.md` (version-resolution table, gating, conformance C1–C6); the YAML is its implementation, not folklore (FR-013). |
| III. Visibility in `.fsi` | N/A | No F# public surface added/changed; the CLI is already `PackAsTool`. |
| IV. Idiomatic simplicity | **PASS** | Extends the existing producer with one shared resolve step + one gated publish job; no new machinery, no new dispatch input (Decision 3), no AOT/self-contained packaging (Decision 4). |
| V. Elmish/MVU is the boundary for stateful/I-O | **PASS (justified)** | The only I/O is the GitHub Actions publish + `dotnet` invocations — not an SDD lifecycle command/generator/validator (`nextLifecycleCommand` unaffected). No MVU ceremony owed (same posture as 039/043). |
| VI. Test evidence is mandatory | **PASS (justified)** | The publish is gated on `FS.GG.SDD.Cli.Tests`; the new self-containment property is proven by a real-fixture offline smoke (C6), not a mock. No unit test owed for the YAML-wiring itself (mirrors 039/043). |
| VII. Agent & human share one contract | **PASS** | Publishing the CLI gives agents, humans, and CI **one** installable lifecycle tool over the same artifacts; it introduces no rival source of truth. |
| VIII. Observability & safe failure | **PASS** | Loud-fail behavior is explicit and extended: unreadable version ⇒ fail (FR-006); tag matching neither line ⇒ fail (Decision 2); empty pack ⇒ fail (FR-007); non-duplicate push error ⇒ fail; either publish job failing fails the run (FR-012); fork/non-canonical ⇒ no publish (FR-009). |

**Engineering Constraints**: `net10.0` unchanged; the CLI command family is `fsgg-sdd` (constitution
Engineering Constraints) — this feature *publishes* exactly that command name. The `FS.GG.Contracts`
namespace exception is already sanctioned and unaffected. No FS.GG.Rendering/Governance/provider
package id, template, or docs URL is added to generic SDD. **PASS.**

**Result**: No violations. Complexity Tracking is empty. The single notable design choice (relaxing
the Contracts tag guard to the at-least-one-line rule) is documented and justified in research
Decision 2 and the contract's "Supersession" section, and is surfaced for a maintainer nod.

## Project Structure

### Documentation (this feature)

```text
specs/044-publish-cli-tool/
├── plan.md              # This file
├── research.md          # Phase 0 — version-line, tag-guard, dispatch, self-containment, visibility, gating, topology
├── data-model.md        # Phase 1 — the two packages, version-resolution state, publish-decision state
├── quickstart.md        # Phase 1 — offline self-containment smoke (C6) + publish/feed/consumer-install checks
├── contracts/
│   └── release-workflow.md  # Phase 1 — authoritative two-package producer contract (supersedes 039 for the producer)
├── checklists/
│   └── requirements.md  # spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
.github/workflows/
└── release.yml                    # EDITED — add `resolve-versions` (both versions + at-least-one-line tag guard),
                                   #   `cli-tests` gate, `publish-cli` job; preserve the Contracts path (FR-014)

src/FS.GG.SDD.Cli/
└── FS.GG.SDD.Cli.fsproj           # EXISTING — already PackAsTool=true, ToolCommandName=fsgg-sdd; UNCHANGED

src/FS.GG.Contracts/
└── FS.GG.Contracts.fsproj         # EXISTING — <Version>1.1.0; publish source unchanged

tests/FS.GG.SDD.Cli.Tests/
└── FS.GG.SDD.Cli.Tests.fsproj     # EXISTING — gates the CLI publish (Decision 6); unchanged

scripts/
└── verify-cli-tool.sh             # NEW (at /speckit-tasks/implement) — runnable offline self-containment smoke (C6);
                                   #   may instead be inlined as a gate.yml/release.yml step — tasks decides

Directory.Build.local.props        # EXISTING — single SDD product-line <Version>0.2.0 (CLI version source); UNCHANGED
```

**Structure Decision**: The only committed in-repo code change is the `release.yml` edit; the CLI
project already packs as the `fsgg-sdd` tool, so no `.fsproj`/`.fs`/`.fsi` change is needed. The
authoritative producer contract moves to this feature's `contracts/release-workflow.md` (extending
039). The offline self-containment smoke is added as a script (or an inline CI step) at task time.
Feed package visibility (FR-011) and the cross-repo registry/coherence-gate wiring (FS-GG/.github#49)
are operational/cross-repo, not in-repo source.

## Complexity Tracking

> No constitution violations — no entries.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| — | — | — |
