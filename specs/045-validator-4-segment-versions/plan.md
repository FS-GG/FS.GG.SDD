# Implementation Plan: Accept 4-Segment Versions in the Registry Validator

**Branch**: `045-validator-4-segment-versions` | **Date**: 2026-06-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/045-validator-4-segment-versions/spec.md`

## Summary

The typed registry validator `Fsgg.Registry` (shipped in `FS.GG.Contracts`, exposed as
`fsgg-sdd registry validate`) was built in feature 042 to mirror the Python authority
`scripts/validate-registry.py` "so the two cannot disagree." They disagree on exactly one case:
the typed `version`/`package-version` grammar accepts **exactly three** numeric segments, so it
emits a false `MalformedVersion` on the legitimate 4-segment `major.minor.patch.revision` form
(`governance-reference-gate-set` is genuinely `1.2.1.1`, per ADR-0007), while the Python regex
already admits an optional 4th segment. This blocks FS-GG/.github#49 (the `contract-coherence`
gate swap from the Python stand-in to the typed CLI) and keeps coherence id
`registry-validator-typed` at `coherent: false`.

The fix is a **one-line grammar widening**: add an optional 4th numeric segment to the private
`semVerRegex` in `src/FS.GG.Contracts/Registry.fs`, byte-for-byte mirroring the Python authority's
`(?:\.\d+)?` clause. This is the single path the canonical `dependencies.yml` flows through
(`validateDocument` → `isValidVersion` → `semVerRegex` for both `version` (line 311) and
`package-version` (line 346)); the legacy `validate`/`tryParseSemVer` parser operates on the
pre-042 `RegistryModel`, is not the Python-parity surface, and is not exercised by the canonical
file — so it stays unchanged (research Decision 2).

Because this changes `FS.GG.Contracts` **source behavior**, the coherence invariant
(`source == feed == registry`) requires a coordinated **patch** bump and republish:
`FS.GG.Contracts` `1.1.0 → 1.1.1` (the verdict-only fix touches no public type/`.fsi`/output
shape, so apicompat cannot trip and no major is incurred — FR-008), and the SDD product line
`0.2.0 → 0.2.1` so the republished `fsgg-sdd` tool actually carries the fix to the gate consumer
(`--skip-duplicate` would make re-pushing `0.2.0` a no-op). Both publish in one `release.yml` run
via the existing at-least-one-line tag guard (feature 044). The new versions are reported back on
FS-GG/FS.GG.SDD#32 and the `.github` registry is advanced for coherence — the gate swap (#49)
lands downstream.

## Technical Context

**Language/Version**: F# on `net10.0`. The product change is a single BCL `System.Text.RegularExpressions`
pattern-string edit in `Fsgg.Registry` (`FS.GG.Contracts`) — no new dependency (the constraint that
`Fsgg.Registry` is BCL-only holds), no new type, no public surface change.

**Primary Dependencies**: `FS.GG.Contracts` (`Fsgg.Registry`, the validator); `FS.GG.SDD.Cli` (the
`fsgg-sdd` tool that bundles Contracts via project reference and exposes `registry validate`); the
existing `.github/workflows/release.yml` two-package producer (feature 044); the org GitHub Packages
feed (`nuget.pkg.github.com/FS-GG`); the cross-repo Python authority
`scripts/validate-registry.py` in `FS-GG/.github` (the parity reference, not edited here) and the
canonical `registry/dependencies.yml` in `FS-GG/.github` (source of truth, consulted at plan time).

**Storage**: N/A — the validator is a pure function over an in-memory `RegistryDocument`; the YAML
`load` edge already lives in `FS.GG.SDD.Artifacts`.

**Testing**: xUnit, real-fixture discipline (Constitution VI). The behavior change is pinned by a
failing-before/passing-after test in `tests/FS.GG.Contracts.Tests/RegistryDocumentTests.fs` (a
`1.2.1.1` accepted case + `1.2.3.4.5`/`1.2.x.4`/`abc` still-rejected cases — FR-007) and by an
end-to-end check over the real fixture `tests/fixtures/registry/dependencies.yml`, refreshed to
carry `governance-reference-gate-set@1.2.1.1` to mirror the canonical `.github` registry (FR-005,
SC-001). Publish is gated on `FS.GG.Contracts.Tests` + `FS.GG.SDD.Cli.Tests` as today.

**Target Platform**: `ubuntu-latest` GitHub-hosted runner for publish; consumers install the tool
from the org feed on any platform with the .NET SDK.

**Project Type**: F# library/CLI grammar fix delivered as a coordinated cross-repo published-artifact
republish (Contracts patch + SDD-line patch).

**Performance Goals**: N/A — a compiled regex matched per contract entry; the file is small and the
validator is not on the inner loop's hot path.

**Constraints**: BCL-only, no new dependency; the widening MUST be numeric-only and bounded to one
extra segment (FR-004); exact grammar parity with the Python authority MUST be restored, not further
diverged (FR-006); no breaking (major) `FS.GG.Contracts` bump (FR-008); the `range` grammar is out
of scope and unchanged; the published tool must run `registry validate` with no SDD source checkout.

**Scale/Scope**: One regex pattern edit; two version-constant bumps (Contracts + SDD line) with their
projection updates; test additions; one fixture refresh; one grammar/parity contract doc; a publish
run and a cross-repo coherence/reporting follow-through. No new module, command, flag, or schema.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change tier**: **Tier 1 (contracted change)** per *Change Classification* — it changes the observable
**behavior of a published validator** (a `MalformedVersion` verdict flips for the 4-segment class) and
is a **cross-repo integration** deliverable (restores Python-authority parity; unblocks the
FS-GG/.github#49 gate swap; republishes two feed packages). Requires spec, plan, tasks, tests, and
docs. No public F# surface (`Registry.fsi`) changes — the widened `semVerRegex` is `private` and no
type/`val`/output shape changes — so no `.fsi`/baseline edit is owed, and no migration note is required
(the change is non-breaking; versioning-policy migration-note obligation applies only to Breaking
changes).

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec→FSI→Tests→Impl | **PASS** | No public surface added/changed (`Registry.fsi` unchanged). Order honored within the change: a failing `1.2.1.1` test is added before the regex widening makes it pass (Constitution VI). |
| II. Structured artifacts are the machine contract | **PASS** | The machine contract is the validator verdict over the typed `RegistryDocument`; the authoritative widened grammar + Python-parity invariant are recorded in `contracts/version-grammar.md`, with the regex its implementation. |
| III. Visibility in `.fsi` | **PASS (N/A edit)** | `Registry.fsi` public surface is unchanged; the edited regex is a `private` let-binding. No signature or surface-area baseline change. |
| IV. Idiomatic simplicity | **PASS** | One added optional group in an existing BCL regex; no new module, parser, type, or dependency. The simplest expression that restores parity (research Decision 1). |
| V. Elmish/MVU is the boundary | **PASS (justified)** | `validateDocument` is a pure validator; the constitution explicitly exempts "simple pure parsers, data models, and validators" from MVU ceremony. The only I/O (YAML load) already lives at the `FS.GG.SDD.Artifacts` edge and is untouched. |
| VI. Test evidence is mandatory | **PASS** | A real-fixture, failing-before/passing-after corpus addition pins both the accepted shape (`1.2.1.1`, `1.2.1.1-preview.1`) and its boundary (`1.2.3.4.5`, `1.2.x.4`, `abc` still rejected); an end-to-end check over the refreshed canonical fixture proves SC-001. |
| VII. Agent & human share one contract | **PASS** | The fix ships in the one `fsgg-sdd` tool that agents, humans, and the CI gate all run; no rival source of truth. |
| VIII. Observability & safe failure | **PASS** | `MalformedVersion` still fires (deterministically, in document order) on genuine defects; only the false positive is removed. Malformed user input vs. tool defect distinction is preserved. |

**Engineering Constraints**: `net10.0` unchanged; `Fsgg.Registry` stays BCL-only (regex is BCL — no new
package, FR research Decision 1); the `FS.GG.Contracts` cross-repo namespace exception is already
sanctioned and unaffected; no FS.GG.Rendering/Governance/provider package id, template, or docs URL is
introduced into generic SDD. **PASS.**

**Result**: No violations. Complexity Tracking is empty. The two notable design choices — scoping the
widening to the real-schema `semVerRegex` (leaving the legacy `tryParseSemVer` parser unchanged) and
the coordinated Contracts-patch + SDD-line-patch bump — are documented and justified in research
Decisions 2 and 3, and surfaced for a maintainer nod.

## Project Structure

### Documentation (this feature)

```text
specs/045-validator-4-segment-versions/
├── plan.md              # This file
├── research.md          # Phase 0 — grammar expression, legacy-path scope, bump strategy, parity, fixture, publish
├── data-model.md        # Phase 1 — the accepted version vocabulary + the coherence/version-bump state
├── quickstart.md        # Phase 1 — reproduce the false positive, prove the fix, validate end-to-end, publish/verify
├── contracts/
│   └── version-grammar.md   # Phase 1 — authoritative widened version grammar + Python-authority parity invariant
├── checklists/
│   └── requirements.md  # spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/FS.GG.Contracts/
├── Registry.fs                 # EDITED — widen private `semVerRegex` (line ~226-227): add optional `(\.\d+)?`
│                               #   4th numeric segment, mirroring scripts/validate-registry.py exactly.
│                               #   `validateDocument`/`isValidVersion` paths only; legacy `tryParseSemVer` UNCHANGED.
├── Registry.fsi                # UNCHANGED — public surface unaffected (regex is private)
├── ContractVersion.fs          # EDITED — value "1.1.0" → "1.1.1"; patch 0 → 1 (lockstep with fsproj <Version>)
└── FS.GG.Contracts.fsproj      # EDITED — <Version> 1.1.0 → 1.1.1

tests/FS.GG.Contracts.Tests/
└── RegistryDocumentTests.fs    # EDITED — add accepted `1.2.1.1` (+ `1.2.1.1-preview.1`) cases; extend the
                                #   malformed theory with `1.2.3.4.5`; keep `1.2.x.4`/`abc` rejected (FR-007);
                                #   add an end-to-end "valid over the canonical fixture" assertion (FR-005/SC-001)

tests/fixtures/registry/
└── dependencies.yml            # EDITED — refresh to mirror the canonical FS-GG/.github registry, adding the
                                #   `governance-reference-gate-set@1.2.1.1` contract so the end-to-end test is real

Directory.Build.local.props     # EDITED — single SDD product-line <Version> 0.2.0 → 0.2.1 (so the republished
                                #   fsgg-sdd tool carries the fix; --skip-duplicate makes re-pushing 0.2.0 a no-op)

docs/release/
├── release-readiness.json      # EDITED — identity.version + generatorVersion.version 0.2.0 → 0.2.1
└── versioning-policy.md         # EDITED — "currently 0.2.0" → "0.2.1" (projection; contracts remain authoritative)
```

**Out of repo (cross-repo follow-through, via `cross-repo-coordination`):** advance the `FS-GG/.github`
`registry/dependencies.yml` `fsgg-contracts` `version`/`package-version` to `1.1.1` (coherence, ordered
strictly after the feed confirms `1.1.1` — bump checklist step 3); report the new Contracts `1.1.1` /
CLI `0.2.1` versions on FS-GG/FS.GG.SDD#32; set Coordination board item #32 to `In progress`. The
FS-GG/.github#49 gate swap and flipping `registry-validator-typed` to `coherent: true` are the downstream
consumer step, tracked separately (spec Assumptions).

**Structure Decision**: A single-project F# library fix (`FS.GG.Contracts`) plus its lockstep version
constant and the SDD-line product version, delivered through the unchanged feature-044 two-package
producer. No new project, module, command, flag, or schema; the `release.yml` workflow is reused as-is
(both jobs pack their own resolved `<Version>`; the at-least-one-line tag guard already supports cutting
the run). `release-readiness.json`/`versioning-policy.md` edits are projection bookkeeping for the SDD-line
bump, not new contracts.

## Complexity Tracking

> No constitution violations — no entries.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| — | — | — |
