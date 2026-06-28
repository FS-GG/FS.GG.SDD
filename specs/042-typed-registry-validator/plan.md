# Implementation Plan: Typed Registry Validator

**Branch**: `042-typed-registry-validator` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/042-typed-registry-validator/spec.md`

## Summary

Make `Fsgg.Registry` (in the BCL-only `FS.GG.Contracts` package) able to validate the
**real** on-disk `registry/dependencies.yml` so it can replace the Python stand-in
(`scripts/validate-registry.py`) that FS-GG/.github#18's reusable contract-coherence
gate runs today. Two gaps close:

1. **Model + grammar convergence** — add a typed model of the *actual* file shape
   (`schemaVersion` / `repos` / `contracts[]` / `dependencies[]` / `coherence[]`) and a
   **pure** validator over it whose rule *kinds* match the Python authority
   (MissingField / UnknownComponent / MalformedVersion / DuplicateComponent /
   MalformedDocument). The version grammar accepts full SemVer **with prerelease**
   (`0.1.52-preview.1`), **bare-integer** schema versions (`1`, `2`), and permissive
   ranges (`1.x`).
2. **A way in** — a `load`/parse entrypoint that turns the YAML file into the typed
   model. Because `FS.GG.Contracts` is a **BCL-only leaf** (FSharp.Core only, no
   third-party package — part of its published contract surface and guarded by the
   apicompat gate), the YAML I/O lives at an **edge** that already depends on
   YamlDotNet, not inside the pure Contracts leaf (Constitution Principle V).

**Approach (additive, recommended):** keep the existing `RegistryModel`/`validate`
untouched and *add* the real-schema `RegistryDocument` + pure `validateDocument`, plus an
edge loader. This is the approach the originating issue (FS-GG/FS.GG.SDD#12) expects
("No registry version bump expected if additive"), keeps the apicompat gate green, and
breaks nothing. See [research.md](./research.md) for the convergence/version-posture
decision and the rejected breaking alternative.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (constitution default).

**Primary Dependencies**:
- `FS.GG.Contracts` — **FSharp.Core only** (BCL-only leaf; no new package may be added).
- Edge loader — **YamlDotNet 16.3.0**, already referenced by `FS.GG.SDD.Artifacts`
  (`Directory.Packages.local.props`), reused; no new package introduced anywhere.

**Storage**: Files — reads `registry/dependencies.yml` (canonical file owned by
FS-GG/.github; vendored into tests as a real fixture). No database.

**Testing**: xUnit (`FS.GG.Contracts.Tests` for the pure validator over the real-file
fixture + broken variants; the edge loader project's test suite for YAML→model parsing).

**Target Platform**: Linux/CI (the .github contract-coherence workflow) and developer
machines.

**Project Type**: Single repo — a published contract library (`FS.GG.Contracts`) plus an
SDD-side edge that exposes a file→verdict entrypoint.

**Performance Goals**: One-shot CLI/gate validation of a single small file; sub-second.
Not a hot path.

**Constraints**:
- `FS.GG.Contracts` stays BCL-only (FSharp.Core closure) — **hard**; the YAML loader may
  not live there.
- Verdict MUST be deterministic (same diagnostics, same order) for a stable CI gate.
- The typed validator MUST agree with `scripts/validate-registry.py` on the canonical
  file (no behavioral disagreement) so the swap is safe.

**Scale/Scope**: One registry file (~10 contracts, ~5 repos, ~5 dependency edges, ~8
coherence entries today); designed to tolerate additive growth.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.0.*

| Principle | Status | How this plan satisfies it |
|---|---|---|
| I. Spec → FSI → tests → impl | PASS | New surface sketched in `Registry.fsi` before `.fs`; FSI/prelude exercise the model; semantic tests precede the body. |
| II. Structured artifacts are the machine contract | PASS | `registry/dependencies.yml` is the contract; the typed validator is the machine reader. Prose folded-scalar bodies are not authoritative — only declared scalar fields are validated. |
| III. Visibility lives in `.fsi` | PASS | All new public types/functions declared in `Registry.fsi`; surface baseline updated. |
| IV. Idiomatic simplicity | PASS | Plain records/DUs + a pure function. **No** hand-rolled YAML parser (reuse YamlDotNet at the edge), avoiding a clever subset reader. |
| V. Elmish/MVU is the boundary for I/O | PASS | `load` (file read + YAML parse) is I/O → it lives at an edge interpreter, **not** in the pure Contracts leaf. `validateDocument` is a pure function. |
| VI. Test evidence is mandatory | PASS | Real fixture = the actual `dependencies.yml`; failing-then-passing tests; broken variants for each rule kind. Golden coverage for the diagnostic projection. |
| VII. Agent + human share one contract | PASS | The same edge entrypoint is invoked by the .github CI gate, by humans, and by agents — one contract, deterministic output. |
| VIII. Observability & safe failure | PASS | Parse/load failure (`MalformedDocument`) is distinguished from content diagnostics; malformed user file ≠ tool defect; deterministic, actionable messages. |

**Change classification**: **Tier 1 (contracted change)** — public model/validator surface
in a published package + a cross-repo gate contract. Requires `.fsi`, tests, docs, and a
**version/migration note** (below). No new package dependency, no namespace deviation
beyond the already-sanctioned `Fsgg`.

**Version & migration posture**: additive bump **`FS.GG.Contracts` 1.0.1 → 1.1.0**
(new types/functions added; legacy `RegistryModel`/`validate` retained → apicompat-clean).
Resolution ripples (tracked, not all in this repo): update the registry's `fsgg-contracts`
pin + republish (mirrors feature 040), then FS-GG/.github#18 swaps the Python stand-in for
the typed entrypoint and flips coherence id `registry-validator-typed` to `coherent: true`.
The .github swap is a **cross-repo follow-up**, not a deliverable of this repo's feature.

**Gate result**: PASS — no violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/042-typed-registry-validator/
├── plan.md              # This file
├── research.md          # Phase 0 — convergence approach, version grammar, loader home, version posture
├── data-model.md        # Phase 1 — RegistryDocument entities + validation rules
├── quickstart.md        # Phase 1 — runnable validation scenarios
├── contracts/
│   ├── registry-document.md      # Typed model + pure validateDocument rule contract
│   └── cli-registry-validate.md  # The file→verdict edge entrypoint contract (what .github#18 invokes)
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Contracts/
├── Registry.fsi                 # EXTEND (additive): RegistryDocument model, extended RegistryRule,
│                                #   val validateDocument; legacy RegistryModel/validate retained
└── Registry.fs                  # EXTEND: pure validateDocument + a BCL-only version-grammar helper
                                 #   (accepts SemVer+prerelease, bare-integer, and the range field)

src/FS.GG.SDD.Artifacts/LifecycleArtifacts/
└── RegistryDocument.fs(+.fsi)   # NEW edge: YamlDotNet → Fsgg.Registry.RegistryDocument mapping
                                 #   (load : path -> Result<RegistryDocument, parse error>)

src/FS.GG.SDD.Commands/CommandWorkflow/
└── (thin) registry-validate     # NEW cross-cutting edge: `fsgg-sdd registry validate <path>`
                                 #   load |> validateDocument, projected as the standard CommandReport
                                 #   (json/text/rich), exit 1 on diagnostics — the gate-callable contract

tests/FS.GG.Contracts.Tests/
├── RegistryValidatorTests.fs        # unchanged (legacy validator)
└── RegistryDocumentTests.fs         # NEW: pure validateDocument over the real fixture + broken variants
tests/FS.GG.SDD.Artifacts.Tests/
└── RegistryDocumentParseTests.fs    # NEW: YAML→model parse (real fixture; malformed-doc handling)
tests/fixtures/registry/
└── dependencies.yml                 # NEW: vendored copy of the real canonical registry file
```

**Structure Decision**: Single repo, three touch-points along the I/O boundary:
the **pure** model + validator stay in the BCL-only `FS.GG.Contracts` leaf; the **YAML
load** edge reuses `FS.GG.SDD.Artifacts`' existing YamlDotNet; a **thin `fsgg-sdd registry
validate` command** composes `load |> validateDocument` into the standard `CommandReport`
so the same entrypoint serves the CI gate, humans, and agents (Principle VII). Exact
command-wiring details are deferred to `/speckit-tasks`.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
