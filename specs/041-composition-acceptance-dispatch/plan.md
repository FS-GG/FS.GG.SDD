# Implementation Plan: Composition-Acceptance Consumes the Dispatched Registry

**Branch**: `041-composition-acceptance-dispatch` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/041-composition-acceptance-dispatch/spec.md`

## Summary

SDD's real-provider composition acceptance (feature 034) already drives the real published
rendering template entirely through an **external** registry named by the
`FSGG_SDD_ACCEPTANCE_REGISTRY` env var — generic SDD carries no rendering identity. Today that
registry reaches CI by only two paths (a hand-maintained repository **secret** used nightly, and
a **manual** `registry_path` input). The canonical registry is owned by FS.GG.Templates, so the
secret silently **drifts** — the "unwired-registry gap." Templates#15 closed the producer half:
on every registry change it PUSHES the current registry content to SDD as a `repository_dispatch`
event of type `composition-registry-updated`. This feature is the **consumer half**: SDD's
composition-acceptance workflow must accept that dispatched event as a first-class registry
source, materialize its content verbatim, point the existing env var at it, and run the
**unchanged** acceptance facts over it — so SDD tests the live registry, not a stale copy. With
Rendering#9 (root build wrappers) merged, the composed product is now buildable/runnable, so the
scheduled acceptance can go **green for the first time**.

This feature adds **no new lifecycle surface** (spec Assumptions): no `fsgg-sdd` command, no
lifecycle stage, no release-catalog artifact, and **no change to the `composition-acceptance-result`
v1 contract** (its body and `sensed` block are byte-identical). The acceptance facts, gating,
outcome→verdict mapping, and result document are all consumed unchanged. The only varying input is
the *source* of the registry file.

The work is almost entirely **CI-automation wiring** plus one **tested, deterministic registry-source
resolver** and one **versioned cross-repo contract document**:

1. **A `repository_dispatch` trigger (FR-001/FR-009).** Add `on: repository_dispatch: types:
   [composition-registry-updated]` to `.github/workflows/composition-acceptance.yml`, alongside the
   existing `schedule` and `workflow_dispatch` triggers.
2. **Deterministic source resolution (FR-002/FR-004/FR-005).** Extract the registry-source selection
   that today lives inline in the "Materialize the external provider registry" step into a small,
   POSIX-shell resolver script (`scripts/workflows/resolve-acceptance-registry.sh`) so it is testable
   off the YAML. It selects exactly one source by a deterministic precedence — explicit manual
   `registry_path` input > dispatched `client_payload.registry_content` > scheduled secret —
   materializes the chosen content verbatim to an ephemeral runner file, and prints the path the
   workflow exports as `FSGG_SDD_ACCEPTANCE_REGISTRY`. A dispatch event whose content is missing/empty
   **fails closed** (exit, clear diagnostic) — never a false green, never a silent skip (FR-005).
3. **Verbatim materialization, no leak (FR-002/FR-003).** The chosen content is written byte-for-byte
   (multi-line YAML / special characters preserved) to an ephemeral file under `RUNNER_TEMP`, never
   committed, and no rendering id/template/path/docs URL it carries appears in SDD source, the
   resolver, or the result document.
4. **Drift-signal surfacing (FR-008/SC-006).** The workflow records the registry content identity it
   tested — the 12-char sha256 the sender publishes as `version` / `registry_sha256_12` — to the run's
   GitHub Step Summary (and recomputes it from the materialized bytes as an integrity cross-check). The
   result document stays v1-unchanged; the drift signal is surfaced at the **run** layer, not the
   document.
5. **Identical behavior, untouched inner loop (FR-006/FR-007).** Because resolution only chooses which
   bytes land at `FSGG_SDD_ACCEPTANCE_REGISTRY`, the acceptance facts run identically regardless of
   source — there is no behavioral fork. PR/local `dotnet test FS.GG.SDD.sln` is untouched: the new
   trigger lives only in the network-gated workflow, and the network facts stay discovery-skipped when
   the env is unset.

**Change tier**: **Tier 1 (contracted change)** — it consumes a **versioned cross-repo integration
contract** (the `composition-registry-updated` dispatch, owned jointly with FS.GG.Templates;
constitution §Change Classification lists cross-repo integration as Tier 1). It introduces **no**
F# public surface (`.fsi`/baseline unchanged), **no** new lifecycle artifact, and **no** change to any
existing schema. The new structured contract is the consumer-side dispatch contract document; the new
behavior is the resolver script, covered by deterministic process-edge tests.

## Technical Context

**Language/Version**: GitHub Actions workflow YAML + POSIX shell (`bash`, ubuntu-latest runner); F#
on .NET `net10.0` for the resolver's test only. No product F# code changes.

**Primary Dependencies**: GitHub Actions (`actions/checkout@v4`, `actions/setup-dotnet@v4`); the
existing `FS.GG.SDD.Acceptance.Tests` xUnit project; `bash`, `sha256sum`/`shasum` for the integrity
cross-check. No new package dependencies.

**Storage**: Ephemeral runner files only (`RUNNER_TEMP`). Nothing is committed; the materialized
registry is run state.

**Testing**: xUnit (`FS.GG.SDD.Acceptance.Tests`), via the existing process-shell edge
(`AcceptanceSupport.runToCompletion`). New offline, non-network-gated facts drive the resolver script
across all three sources + the empty-dispatch fail-closed + verbatim-byte cases.

**Target Platform**: GitHub-hosted `ubuntu-latest`. The resolver is POSIX `bash`; its tests are
gated to a shell-available host (skipped on Windows) so they stay green in the cross-platform inner
loop.

**Project Type**: CI/automation wiring around an existing network-gated acceptance — not a lifecycle
command.

**Performance Goals**: Zero added wall-clock to the offline inner loop (SC-004). The resolver runs
once per acceptance run; cost is negligible.

**Constraints**: Deterministic single-source selection (FR-004); verbatim byte materialization
(FR-002); fail-closed on empty dispatch (FR-005); zero leaked identity tokens (FR-003/SC-003);
`composition-acceptance-result` v1 unchanged (Assumptions).

**Scale/Scope**: One workflow file edited, one shell script added, one test file added, one
cross-repo contract doc added. No `fsgg-sdd` command, stage, or release-catalog artifact.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.0. PASS.*

- **I. Spec → FSI → Semantic Tests → Implementation** — No new F# public API. The only executable
  artifact is the shell resolver, specified here (contract + behavior table) and tested before it is
  wired into the workflow. ✅
- **II. Structured Artifacts Are the Machine Contract** — The machine contract is the cross-repo
  dispatch payload (event type + `client_payload` fields), authored as
  [contracts/registry-dispatch.md](./contracts/registry-dispatch.md). Markdown (this plan, the spec)
  is authoring surface; the dispatch payload schema is authoritative. The acceptance result remains the
  034 v1 contract, unchanged. ✅
- **III. Visibility Lives in `.fsi`** — No public F# module added or changed; no baseline movement. ✅
- **IV. Idiomatic Simplicity** — Plain POSIX shell with a single precedence chain; no clever
  abstractions. Logic extracted into one script so it is testable rather than buried in YAML. ✅
- **V. Elmish/MVU Boundary** — Applies to F# stateful/I/O **product** workflows. This feature adds no
  such F# code; it is CI wiring plus a pure-ish env→file resolver (deterministic, fail-closed,
  side-effect limited to writing one ephemeral file and printing its path). No MVU ceremony is
  warranted; recorded as a deliberate, in-scope exception (CI shell at the process edge). ✅
- **VI. Test Evidence Is Mandatory** — Behavior-changing code (the resolver) gets automated tests that
  fail before and pass after, over a **real** shell process and **real** temp files (no mocks),
  covering all three sources, the explicit-override precedence, fail-closed-on-empty-dispatch, and
  verbatim multi-line/special-character materialization. The unchanged acceptance facts already have
  034/035 coverage. ✅
- **VII. Agent And Human Workflows Share One Contract** — No agent-facing artifact changes. The
  cross-repo coordination obligation (joint ownership with Templates, FR-009) is recorded; no second
  source of truth is introduced. ✅
- **VIII. Observability And Safe Failure** — Empty/malformed dispatch fails closed with an actionable
  diagnostic distinguishing a wiring defect (explicit dispatch, no content ⇒ FAIL) from the opt-in
  offline case (no source ⇒ already exit-1 error in the workflow; facts discovery-skip locally). Wrong
  event types do not trigger. The drift signal is surfaced for traceability. ✅

**Engineering Constraints** — `net10.0` unchanged; no namespace/package change; no Rendering identity
in generic SDD (the registry remains the only identity channel). SDD stays useful without Governance.
✅

**Gate result: PASS — no violations, Complexity Tracking not required.**

## Project Structure

### Documentation (this feature)

```text
specs/041-composition-acceptance-dispatch/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── registry-dispatch.md   # the consumed cross-repo dispatch contract (v1)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
.github/workflows/
└── composition-acceptance.yml         # EDIT: add repository_dispatch trigger; call the resolver;
                                        #       surface the drift signal to the Step Summary

scripts/workflows/
└── resolve-acceptance-registry.sh     # NEW: deterministic single-source resolver
                                        #      (input > dispatch content > secret), verbatim
                                        #      materialization, fail-closed on empty dispatch

tests/FS.GG.SDD.Acceptance.Tests/
├── FS.GG.SDD.Acceptance.Tests.fsproj  # EDIT: include the new test file (+ copy script to output)
└── RegistryResolverTests.fs           # NEW: offline, non-gated process-edge tests of the resolver
```

**Structure Decision**: The behavior is CI automation, so the home is `.github/workflows/` and the
existing `scripts/workflows/` directory (currently empty). The single decision of substance is to
**extract the source-selection shell out of the YAML into a script** so it is unit-testable against
real temp files and a real shell, satisfying constitution VI without trying to "test YAML." The
resolver test lives in the existing `FS.GG.SDD.Acceptance.Tests` project (which already hosts offline,
non-gated facts in `ProbeResolutionTests.fs`) and reuses its `runToCompletion` process edge — but as
plain `[<Fact>]`, **not** `RequiresRegistryFact`, because it is offline and deterministic. No new
project, no new release-catalog entry.

## Complexity Tracking

> Not required — Constitution Check passed with no violations.
