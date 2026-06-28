# Feature Specification: Composition-Acceptance Consumes the Dispatched Registry

**Feature Branch**: `041-composition-acceptance-dispatch`

**Created**: 2026-06-28

**Status**: Draft

**Input**: User description: "next non blocked sdd item on the project coordination board" — resolved to FS-GG/FS.GG.SDD#10 (`H4 · sdd — composition-acceptance consumes the dispatched registry → goes GREEN (closes the unwired-registry gap)`), the next non-blocked SDD roadmap item (its blockers FS.GG.Templates#15 and FS.GG.Rendering#9 both closed 2026-06-28).

## Context *(informative)*

SDD's real-provider composition acceptance (feature 034) drives the real published rendering
template through an **external** registry — generic SDD carries no rendering identity, so the
real template is reached only through an author-supplied `.fsgg/providers.yml` named by the
`FSGG_SDD_ACCEPTANCE_REGISTRY` environment variable (034 FR-009/FR-010).

Today that registry reaches CI by exactly two paths: a hand-maintained
`FSGG_SDD_ACCEPTANCE_REGISTRY` repository **secret** (used on the nightly schedule) and a
**manual** `registry_path` workflow input. The canonical registry, however, is **owned by
FS.GG.Templates** (`providers/rendering.providers.yml`). The secret is a copy, so it silently
**drifts** from the source of truth — the "unwired-registry gap." Templates#15 closed the
producer half: on every registry change it now PUSHES the *current* registry content to SDD via
the org reusable cross-repo dispatch sender (`.github` dispatch-sender, #22), as a
`repository_dispatch` event SDD does not yet listen for. This feature is the **consumer half**:
SDD's composition-acceptance must accept that dispatched registry as a first-class source so it
tests the live registry, not a stale copy. With Rendering#9 (root `.slnx` + build wrappers in the
fs-gg-ui template, closed 2026-06-28) the composed product is now buildable/runnable, so the
acceptance's build/run facts can pass — letting the scheduled acceptance go **green for the first
time**.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Test the live registry, drift-free (Priority: P1)

A Templates maintainer edits the canonical provider registry
(`providers/rendering.providers.yml`) — e.g. bumps the published rendering template version.
Without anyone touching an SDD secret, SDD's composition-acceptance runs against that exact, current
registry content and reports a verdict tied to it.

**Why this priority**: This is the whole point of the issue — closing the unwired-registry gap.
Until the dispatched registry is consumed, SDD tests a hand-copied secret that drifts from the
source of truth, so a passing acceptance can be meaningless (it validated stale identity). It is
the smallest slice that delivers the drift-free guarantee.

**Independent Test**: Send a `composition-registry-updated` dispatch (manually, or by editing the
Templates registry) carrying registry content, and confirm SDD's composition-acceptance run
materializes that exact content and exercises the acceptance facts over it — verifiable without
the nightly schedule and without any secret being set.

**Acceptance Scenarios**:

1. **Given** the Templates-owned registry changes, **When** Templates dispatches
   `composition-registry-updated` with the current registry content, **Then** SDD's
   composition-acceptance triggers, materializes that content to a registry file, points
   `FSGG_SDD_ACCEPTANCE_REGISTRY` at it, and runs the network-gated acceptance facts over it.
2. **Given** a dispatch-triggered run, **When** the acceptance resolves the provider from the
   materialized registry, **Then** it exercises exactly the same facts (skeleton, constitution,
   build, run, git, chmod, provenance partition, refresh exclusion, completeness) as the
   secret-sourced path — only the registry *source* differs, never the behavior.
3. **Given** no secret edit was made, **When** the registry content changes upstream, **Then** the
   content SDD tests matches the upstream registry byte-for-byte (drift = 0).

---

### User Story 2 - First green nightly across the composed boundary (Priority: P2)

A FS-GG maintainer watching the nightly composition-acceptance sees it pass for the first time:
the real rendering template scaffolds, builds, and runs end-to-end over the live registry.

**Why this priority**: The issue's headline outcome ("goes GREEN"). It is the observable proof
that the cross-repo composition is coherent. It depends on Story 1 (a real registry to test) plus
the now-merged Rendering root-build wrappers, so it lands after the consumer wiring exists.

**Independent Test**: Run the composition-acceptance with a live registry available (dispatched or
secret) and confirm the verdict is a pass — build and run facts succeed over the composed product —
with the result document recording the registry identity that was tested.

**Acceptance Scenarios**:

1. **Given** a live registry resolving the published rendering template and the merged root-build
   wrappers, **When** the composition-acceptance runs, **Then** the build fact and the run fact
   both pass and the overall verdict is a pass (green).
2. **Given** a green run, **When** the result document is produced, **Then** it records the
   registry content identity (the drift signal) so the run is traceable to the exact registry it
   tested.

---

### User Story 3 - Existing sources and the offline inner loop are untouched (Priority: P3)

An SDD contributor runs the default offline test suite locally and in PR CI; a maintainer can still
trigger the acceptance manually or rely on the nightly secret-sourced run. None of these change.

**Why this priority**: The new dispatch source must be additive. The cheap offline inner loop and
the established secret/manual paths must keep working exactly as before; regressing them would be
worse than the gap this feature closes.

**Independent Test**: With no registry env set, run the default `dotnet test` and confirm the
network-gated facts stay skipped and the suite is green; separately trigger the workflow via the
manual `registry_path` input and via the scheduled secret and confirm each still runs the
acceptance.

**Acceptance Scenarios**:

1. **Given** no registry source is present, **When** the default offline test suite runs, **Then**
   every composition-acceptance fact is skipped and the suite is green, with no network touched.
2. **Given** a manual `registry_path` input or the scheduled secret, **When** the workflow runs,
   **Then** the acceptance runs against that source exactly as it does today.

---

### Edge Cases

- **Dispatch with no/empty registry content**: the run MUST fail closed with a clear diagnostic —
  never report a false green and never silently skip — because a missing registry on an explicit
  dispatch is a wiring defect, not the "opt-in/unset" offline case.
- **Multiple sources present on one run**: source selection MUST be deterministic. A
  dispatch-triggered run uses the dispatched payload; a manual run uses its input; a scheduled run
  uses the secret. An explicit manual `registry_path` input, when supplied, overrides.
- **Registry content with multi-line YAML / special characters**: the dispatched content MUST be
  materialized verbatim (byte-for-byte) so the provider resolves identically to the canonical file.
- **Wrong or malformed event**: an event that is not `composition-registry-updated` MUST NOT
  trigger the acceptance; a triggering event whose payload is malformed MUST fail closed.
- **Burst of upstream registry edits**: only the latest content needs to be tested; superseded
  dispatches need not each produce a run.
- **Sender dormant (org App/secrets not yet provisioned)**: when no dispatch ever arrives, the
  consumer side is simply not triggered by dispatch and the secret/manual paths continue to work —
  the consumer wiring can exist ahead of the App provisioning without breaking anything.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The composition-acceptance workflow MUST accept a `repository_dispatch` event of type
  `composition-registry-updated` as a registry source, in addition to the existing scheduled-secret
  and manual-input sources.
- **FR-002**: On a dispatch-triggered run, the workflow MUST materialize the dispatched registry
  content (`client_payload.registry_content`) verbatim to an ephemeral file and point
  `FSGG_SDD_ACCEPTANCE_REGISTRY` at that file, so the acceptance resolves the provider from the
  live registry.
- **FR-003**: The dispatched (or any) registry content MUST NOT be committed to the SDD repository,
  and no rendering package id, template id, path, or docs URL it carries may appear in SDD source,
  the acceptance code, or the result document (preserves 034 FR-009 / SC-003). The materialized file
  is ephemeral run state only.
- **FR-004**: Source selection MUST be deterministic across the three sources: a dispatch-triggered
  run uses the dispatched payload; a manual run uses its `registry_path` input; a scheduled run uses
  the secret. When a manual `registry_path` input is explicitly supplied it overrides the secret.
- **FR-005**: When triggered by dispatch with a missing or empty registry content, the workflow MUST
  fail with a clear diagnostic — it MUST NOT report a pass and MUST NOT silently skip.
- **FR-006**: A dispatch-sourced acceptance run MUST exercise the identical set of acceptance facts,
  gating, outcome→verdict mapping, and result-document contract as a secret-sourced run. The
  registry *source* is the only thing that varies; there is no behavioral fork per source.
- **FR-007**: The default offline inner loop (`dotnet test FS.GG.SDD.sln` with no registry env) MUST
  be unaffected: the new trigger adds no work to PR/local runs and the network-gated facts remain
  discovery-skipped when the registry env is unset, keeping the suite green and its wall time
  unchanged.
- **FR-008**: A dispatch-sourced run MUST surface the registry content identity it tested (the
  drift signal — the 12-character sha256 the sender publishes as `version` /
  `registry_sha256_12`) so the run is traceable to the exact registry content.
- **FR-009**: SDD MUST consume the cross-repo dispatch contract as published by the Templates
  producer — event type `composition-registry-updated` and the `client_payload` fields
  `registry_content`, `registry_path`, `registry_sha256_12`, and `version` — without inventing
  rendering-specific identity. This is a versioned cross-repo contract owned jointly with
  FS.GG.Templates; a change to it is a coordinated change on both sides.
- **FR-010**: With a live registry available and the composed product buildable/runnable, the
  scheduled composition-acceptance MUST be able to reach a passing (green) verdict — the build and
  run facts pass over the produced product.

### Key Entities

- **Dispatched registry payload**: the `composition-registry-updated` `repository_dispatch` event.
  Carries `version` (12-char sha256 content identity / drift signal), `registry_path`
  (`providers/rendering.providers.yml`), `registry_sha256_12` (the same hash, explicit),
  `registry_content` (the full registry YAML, materialized to a file by SDD — no secret), plus
  `source_repo` / `source_sha` / `source_ref` added by the reusable sender.
- **Registry source**: exactly one of {dispatched payload, manual `registry_path` input, scheduled
  secret} resolved per run into the `FSGG_SDD_ACCEPTANCE_REGISTRY` path the acceptance reads.
- **Composition-acceptance result**: the existing deterministic verdict document (034) — its body
  and `sensed` block are unchanged; only the registry it tested may now originate from the dispatch.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A change to the Templates-owned registry results in SDD's composition-acceptance
  running against that exact content within one dispatch cycle, with zero manual SDD secret edits
  (observed drift between tested content and the canonical registry = 0).
- **SC-002**: The scheduled composition-acceptance run reaches a passing verdict (green) — build and
  run facts pass over the composed product — for the first time.
- **SC-003**: No rendering package id, template id, path, or docs URL appears anywhere in SDD source
  or in the produced result document (zero leaked identity tokens).
- **SC-004**: The default offline `dotnet test FS.GG.SDD.sln` stays green with the network-gated
  facts skipped, and its wall-clock time is unchanged versus before this feature (no inner-loop
  regression).
- **SC-005**: A dispatch carrying missing/empty registry content produces a failed run with a
  diagnostic in 100% of cases — never a false green and never a silent skip.
- **SC-006**: Every dispatch-sourced run records the registry content identity it tested, so each
  run is traceable to one exact registry content hash.

## Assumptions

- **Producer side is live**: FS.GG.Templates#15 (`acceptance-dispatch`, merged 2026-06-28) is the
  sender; it defines the contract this feature consumes (event type `composition-registry-updated`,
  `client_payload.registry_content` et al.). This feature implements only the SDD consumer half.
- **Composed product is buildable/runnable**: FS.GG.Rendering#9 (root `.slnx` + `Directory.Build.props`
  + `global.json` + build wrapper in the fs-gg-ui template, closed 2026-06-28) is merged, so the
  build/run facts can pass — the precondition for the green outcome (SC-002 / FR-010).
- **The consumer may be authored ahead of org-App provisioning**: the reusable sender is dormant
  until the org GitHub App and its two secrets (`.github`#21) exist; until then no dispatch arrives
  and the secret/manual paths keep working. The SDD consumer wiring does not depend on the App being
  provisioned to be correct.
- **Single product per dispatch**: each dispatch advertises one registry content; the acceptance
  tests the latest content (superseded dispatches need not each run).
- **No new lifecycle surface**: this feature adds CI/automation wiring around the existing
  network-gated acceptance (034); it introduces no new `fsgg-sdd` command, lifecycle stage, or
  release-catalog artifact, and does not change the `composition-acceptance-result` v1 contract.
