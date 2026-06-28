# Implementation Plan: Correct capabilities schema version to 2 and republish FS.GG.Contracts 1.0.1

**Branch**: `040-correct-capabilities-version` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/040-correct-capabilities-version/spec.md`

## Summary

`FS.GG.Contracts` mis-declares the Governance-owned `capabilities` schema version
as **1**; the Governance validator ships and supports **2**. This feature corrects
the single declared constant (`Schemas.capabilitiesVersion`, 1 → 2), updates its
version-constant verification to assert 2, bumps the package identity
`1.0.0` → `1.0.1` (new immutable identity, never an in-place replacement), and
delivers `1.0.1` through a committed in-repo `nuget.config` shared local folder
feed. The org GitHub Packages publishing path stays **deferred** (FR-008), and the
cross-repo `fsgg-contracts` registry pin advance (FR-006) is tracked as a
follow-on issue/PR against `FS-GG/.github`. The three sibling Governance-owned
constants (`governance`, `policy`, `tooling` = 1) and every SDD-owned constant are
untouched, and **no SDD-emitted artifact schema version or SDD runtime behaviour
changes** (FR-007) — the correction exists precisely so the downstream
FS.GG.Governance#14 re-type is a no-behaviour-change move.

**Change tier**: **Tier 1 (contracted change)** — a cross-repo integration/contract
change to a declared shared constant and the package identity. The `.fsi`
signature and the reflection surface baseline do **not** change (the constant's
type is unchanged; only its value and the package version move).

## Technical Context

**Language/Version**: F# on .NET, `net10.0`.

**Primary Dependencies**: None new. `FS.GG.Contracts` (namespace `Fsgg`) is the
shared typed contract package; Xunit for the verification suite.

**Storage**: N/A at runtime. Delivery artifact is a transient
`FS.GG.Contracts.1.0.1.nupkg` deposited into the committed local folder feed.

**Testing**: Xunit — `tests/FS.GG.Contracts.Tests` (`SchemaVersionConstantTests.fs`
for the declared constants; `PublicSurface` reflection baseline for surface
stability).

**Target Platform**: Linux/CI build host; the package is consumed cross-repo by
Governance, Templates, Rendering.

**Project Type**: Single project — an org-shared F# contract library plus its test
project (the constitution's SDD-owned cross-repo namespace carve-out, already
justified for `FS.GG.Contracts` / `Fsgg`).

**Performance Goals**: N/A (a constant correction; no hot path).

**Constraints**: Zero SDD runtime/behaviour change (FR-007); `1.0.0` never mutated
in place (FR-004); GitHub Packages path deferred (FR-008); no provider-/rendering-/
Governance-specific identity embedded in the generic package.

**Scale/Scope**: One constant value, one package version, one verification
assertion, one committed feed config; one cross-repo follow-on.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.0.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | `.fsi` signature is unchanged (`val capabilitiesVersion: int`). Order is honoured by tightening the test assertion to `2` (fails) before flipping the constant (passes). |
| II. Structured Artifacts Are the Machine Contract | **PASS** | The declared constant is the machine contract; prose (spec Assumptions / data-model) and the constant agree, with the constant authoritative. |
| III. Visibility Lives in `.fsi` | **PASS** | No public surface change → `Schemas.fsi` and `PublicSurface.baseline` are unchanged and must still pass. |
| IV. Idiomatic Simplicity | **PASS** | A single literal change; no new abstraction. |
| V. Elmish/MVU Boundary | **PASS / N/A** | No stateful or I/O SDD workflow is added. Pack/publish/resolve happen at the `dotnet`/`nuget.config` tooling edge, outside SDD runtime (FR-007). |
| VI. Test Evidence Is Mandatory | **PASS** | `SchemaVersionConstantTests` is updated to fail-before/pass-after; the surface baseline guards against accidental surface drift. |
| VII. Agent And Human Workflows Share One Contract | **PASS / N/A** | No agent command/skill surface changes. |
| VIII. Observability And Safe Failure | **PASS / N/A** | No new runtime failure paths. The deferred GH Packages path and the cross-repo follow-on are documented, not silently dropped. |

**Engineering Constraints**: `net10.0` ✔; `Fsgg` cross-repo namespace carve-out is
the constitution-sanctioned exception and stays justified ✔; no
rendering/Governance identity introduced ✔; SDD remains buildable/usable without
Governance ✔.

**Result**: PASS — no violations; Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/040-correct-capabilities-version/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── declared-constants.md   # corrected declared-constant contract + verification
│   └── delivery.md             # local folder feed + cross-repo registry follow-on
├── checklists/
│   └── requirements.md  # pre-existing
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Contracts/
├── Schemas.fs            # capabilitiesVersion: 1 → 2  (line 164)
├── Schemas.fsi           # UNCHANGED (val capabilitiesVersion: int)
└── FS.GG.Contracts.fsproj # <Version> 1.0.0 → 1.0.1   (line 9)

tests/FS.GG.Contracts.Tests/
├── SchemaVersionConstantTests.fs  # assert capabilitiesVersion = 2 (line 61); siblings stay 1
└── PublicSurface.baseline         # UNCHANGED (no surface change)

# New, in-repo shared local folder feed (decision: nuget.config + feed path in-repo)
nuget.config                       # adds local folder feed source for FS.GG.Contracts
.fsgg-local-feed/.gitkeep          # the committed feed directory (exists → restore-safe)
```

**Structure Decision**: Single shared-contract library plus its Xunit test
project, already established. The only structural additions are a repo-root
`nuget.config` declaring the shared local folder feed and the committed feed
directory (`.fsgg-local-feed/`) so the configured local source path exists and
`dotnet restore` does not error on a missing source.

## Cross-cutting decisions (from clarification)

- **Shared local folder feed = committed in-repo `nuget.config`.** A repo-root
  `nuget.config` adds a local folder source (`.fsgg-local-feed/`) alongside the
  inherited sources. The feed directory is committed (`.gitkeep`) so the source
  path exists and restore stays clean while the folder is empty. `1.0.1` is packed
  and pushed into that folder; consumers resolve from the same conventional path.
  See [contracts/delivery.md](./contracts/delivery.md).
- **Registry pin (FR-006) = cross-repo follow-on.** The `fsgg-contracts` pin
  (`owner: sdd`) advances `1.0.0` → `1.0.1` in the `FS-GG/.github` org registry via
  a coordination issue + PR, tracked on the Coordination board, executed after
  `1.0.1` is published. It is **out of this repo's tree** and not a code
  deliverable here. See [contracts/delivery.md](./contracts/delivery.md).
- **GitHub Packages (FR-008) = deferred.** `release.yml` is untouched. Bumping the
  fsproj `<Version>` to `1.0.1` means a future un-deferred release would publish
  `1.0.1`; that is coherent and intended, not triggered by this feature.

## Complexity Tracking

> No constitution violations. Section intentionally empty.
