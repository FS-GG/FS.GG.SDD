# Implementation Plan: Publish FS.GG.Contracts 1.1.0 to the org feed and make source/feed/registry coherence durable

**Branch**: `043-publish-contracts-110` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/043-publish-contracts-110/spec.md`

## Summary

Close the version gap opened by feature 042: the `FS.GG.Contracts` **source** advanced to
`1.1.0` but the **feed** still serves only `1.0.1` and the org **registry** `package-version`
still records `1.0.1`. Three deliverables, only one of which is an in-repo committed change:

1. **Publish `FS.GG.Contracts 1.1.0` to the org feed** (US1/FR-001) — *operational*. The
   producer workflow already exists (`.github/workflows/release.yml`, feature 039); its source
   of truth is the evaluated fsproj `<Version>` (already `1.1.0`). The publish is performed by
   invoking that workflow (recommended: `workflow_dispatch` with `version=1.1.0`, the manual
   "publish exactly what was asked" path — avoids minting a misleading `v1.1.0` *product* tag
   when the SDD product line is `0.2.0`). No workflow edit is needed.
2. **Advance the registry `package-version` `1.0.1 → 1.1.0`** (US2/FR-004) — *cross-repo*.
   Performed in `FS-GG/.github` via the cross-repo protocol by notifying FS-GG/.github#42 (or
   successor) once 1.1.0 is confirmed live. SDD does not edit the `.github` registry directly.
3. **A durable contracts-version-bump checklist** (US3/FR-005) — *the single in-repo committed
   artifact*: a new human-facing runbook under `docs/release/` enumerating the three same-change
   actions (bump source · publish to feed · update `.github` registry `version` +
   `package-version`), citing the `contract-coherence` gate and ADR-0001, so the exact 042 gap
   cannot recur unnoticed.

No `.fsgg` schema, contract surface, contract version, or CLI behavior changes (FR-006/SC-005):
the source contract version stays `1.1.0` as shipped by 042; this is release-engineering /
process only and touches no offline/golden contract. The in-repo registry **test fixture**
(`tests/fixtures/registry/dependencies.yml`, feature 042's validator input) is deliberately
left frozen — editing it would alter 042's golden expectations and is out of this feature's
scope (research Decision 5).

## Technical Context

**Language/Version**: No product code change. The publish path is the existing GitHub Actions
YAML workflow invoking .NET SDK `10.0.x`; the one new artifact is a Markdown runbook. F# /
`net10.0` product code is untouched.

**Primary Dependencies**: Existing `.github/workflows/release.yml` (feature 039,
`dotnet pack`/`nuget push`, `dotnet msbuild -getProperty:Version`); the org GitHub Packages
feed (`nuget.pkg.github.com/FS-GG`); the reusable `contract-coherence` gate and
`registry/dependencies.yml` in `FS-GG/.github` (cross-repo, not edited here).

**Storage**: N/A — transient `artifacts/packages/*.nupkg` on the runner; the published
`.nupkg` lands on the org feed.

**Testing**: No new F# tests are owed — no product behavior changes (Constitution VI, mirroring
feature 039). Verification is the publish workflow's own run plus a real feed query and a doc
presence check — see `quickstart.md`. The existing `FS.GG.Contracts.Tests` continues to gate
the publish (FR-002).

**Target Platform**: `ubuntu-latest` GitHub-hosted runner; canonical repo `FS-GG/FS.GG.SDD`.
Consumers resolve from the org feed.

**Project Type**: Release-engineering / process — one new docs runbook; the rest is operational
(invoke an existing workflow) and cross-repo (registry record advance).

**Performance Goals**: N/A (release cadence, not inner-loop).

**Constraints**: Release-time only; does not run in the default offline inner loop and changes
no deterministic CLI/golden contract (SC-005). The registry `package-version` MUST never run
ahead of the feed (FR-007). Least-privilege publish creds (run-scoped `GITHUB_TOKEN`, no PAT)
are inherited unchanged from feature 039.

**Scale/Scope**: One package (`FS.GG.Contracts`), one version (`1.1.0`), one new doc. The
cross-repo registry advance is handled in `FS-GG/.github`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change tier**: **Tier 2 (internal / release-engineering)** per *Change Classification*. No
public API, schema, generated-view, command, artifact-layout, or agent-skill contract changes;
the `1.1.0` contract surface was already shipped by 042. The registry `package-version` advance
is cross-repo **record-keeping** (handled in `FS-GG/.github` via the coordination protocol), not
a contract-surface change in this repo — the same posture feature 039 took for its FR-011.

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec→FSI→Tests→Impl | N/A | No F# change; deliverable is a Markdown runbook + operational publish. |
| II. Structured artifacts are the machine contract | **PASS** | The three-authority coherence invariant (feed == fsproj == registry `version`/`package-version`) is documented as a contract in `contracts/contracts-version-coherence.md`; the checklist doc is its human projection, not a second source of truth (it cites the registry + coherence gate as authoritative). |
| III. Visibility in `.fsi` | N/A | No F# public surface added/changed. |
| IV. Idiomatic simplicity | **PASS** | Reuses the existing publish workflow verbatim; adds one plain Markdown runbook. No new machinery, no new CI gate (scoped out in spec Assumptions). |
| V. Elmish/MVU is the boundary for stateful/I-O | **PASS (justified)** | The only I/O is the existing GitHub Actions publish + a cross-repo registry edit — neither is an SDD lifecycle command/generator/validator (`nextLifecycleCommand` unaffected). No MVU ceremony owed. |
| VI. Test evidence is mandatory | **PASS (justified)** | No product behavior changes ⇒ no new F# tests owed; the publish gate reuses `FS.GG.Contracts.Tests`. Verification is the workflow run + feed query + doc presence (`quickstart.md`), mirroring feature 039 which added no F# test. |
| VII. Agent & human share one contract | **PASS** | The checklist is a human runbook over the same registry/feed contracts agents and CI read; it introduces no rival source of truth. |
| VIII. Observability & safe failure | **PASS** | Inherits feature 039's loud-fail behavior (unreadable version = defect; tag/fsproj drift = fail; "packed nothing" = fail; `--skip-duplicate` idempotency). The checklist makes the bump→publish→registry obligation explicit so a silent gap (the 042 failure mode) is structurally discouraged. |

**Engineering Constraints**: `net10.0` unchanged; the `FS.GG.Contracts` namespace exception is
already sanctioned (constitution v1.1.0) and unaffected; no rendering/Governance/provider
identity is added to generic SDD; the checklist names no rendering-specific package/template/URL.
**PASS.**

**Result**: No violations. Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/043-publish-contracts-110/
├── plan.md              # This file
├── research.md          # Phase 0 — publish-trigger, checklist-home, registry-sync, fixture-freeze decisions
├── data-model.md        # Phase 1 — the three version authorities + coherence state model
├── quickstart.md        # Phase 1 — operational runbook: publish 1.1.0, verify feed, notify .github
├── contracts/
│   └── contracts-version-coherence.md   # Phase 1 — three-authority coherence invariant + bump protocol
├── checklists/
│   └── requirements.md  # spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
.github/workflows/
└── release.yml                 # EXISTING (feature 039) — invoked, not edited; publishes the fsproj <Version>

src/FS.GG.Contracts/
└── FS.GG.Contracts.fsproj      # EXISTING — <Version>1.1.0 already set by feature 042 (the publish source); unchanged

tests/FS.GG.Contracts.Tests/
└── FS.GG.Contracts.Tests.fsproj  # EXISTING — gates the publish (FR-002); unchanged

tests/fixtures/registry/
└── dependencies.yml            # EXISTING (042 validator input) — FROZEN; not edited (research Decision 5)

docs/release/
└── contracts-version-bump-checklist.md   # NEW — the durable runbook (FR-005); the only committed in-repo change
```

**Structure Decision**: The only committed in-repo change is the new
`docs/release/contracts-version-bump-checklist.md` runbook. The publish reuses the existing
feature-039 `release.yml` unchanged (invoked operationally), the source fsproj is already at
`1.1.0`, and the registry `package-version` advance happens cross-repo in `FS-GG/.github`. No
`src/`, `tests/`, or workflow file changes; the 042 registry fixture stays frozen.

## Complexity Tracking

> No constitution violations — no entries.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| — | — | — |
