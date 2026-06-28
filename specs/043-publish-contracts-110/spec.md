# Feature Specification: Publish FS.GG.Contracts 1.1.0 to the org feed and make source/feed/registry coherence durable

**Feature Branch**: `043-publish-contracts-110`

**Created**: 2026-06-28

**Status**: Draft

**Change Tier**: Tier 2 (release-engineering / process; no `.fsgg` schema, contract surface,
contract version, or CLI behavior change — the source contract version is already at 1.1.0,
shipped by feature 042)

**Input**: User description: "start the next unblocked sdd item on the coordination board" →
resolved to the only non-Done FS.GG.SDD item, **FS-GG/FS.GG.SDD#27** (`[cross-repo] Publish
FS.GG.Contracts 1.1.0 to the org feed`, Status: Ready, Blocked by: None, Phase: P5
Versioning).

## Context

`FS.GG.Contracts` is the SDD-owned, org-shared typed source of truth for every `.fsgg`
schema, the template-provider descriptor, and the cross-repo dependency registry. Three
authorities must agree on its version:

1. **Source** — the `FS.GG.Contracts` fsproj `<Version>` (and the matching
   `Fsgg.ContractVersion.value`), the authority every other layer tracks.
2. **Feed** — the package actually obtainable from the org GitHub Packages feed
   (`nuget.pkg.github.com/FS-GG`), which downstream consumers and Renovate resolve.
3. **Registry** — `FS-GG/.github` `registry/dependencies.yml`, which records both
   `fsgg-contracts.version` (the source pin the reusable `contract-coherence` gate enforces)
   and `package-version` (the last version actually on the feed).

Feature 042 (this repo #26, an additive typed registry validator) advanced the **source**
version `1.0.1 → 1.1.0` but did **not** publish 1.1.0 to the feed and did **not** update the
org registry. Two incoherences resulted:

- The `contract-coherence` gate (registry pin must equal SDD source) went **red on every
  `.github` PR** (`registry 1.0.1 != SDD 1.1.0`); `main` was latently red.
- The feed fell **a version behind the source**: it still serves only `1.0.1`.

The `.github` side has already taken the first corrective step (FS-GG/.github#42): it
advanced `registry fsgg-contracts.version 1.0.1 → 1.1.0` to track the source authority, which
turns the coherence gate green again. It deliberately left `package-version` at `1.0.1`
because that is still the only version on the feed.

This feature completes the loop from the **SDD side, which owns the package and the publish**:
it publishes `FS.GG.Contracts 1.1.0` to the org feed, drives the registry's `package-version`
forward to match, and adds a durable guardrail so a future source `<Version>` bump can never
again silently outrun the feed and the registry. The publish mechanism itself already
exists — feature 039 (#17) shipped the release-time pack+push workflow whose source of truth
is the evaluated fsproj `<Version>`; this feature exercises that path for 1.1.0 and closes the
process gap that let the gap open.

It is a release-engineering / process change: it alters no contract surface, schema, contract
version, or CLI behavior, and adds no new versioned cross-repo contract. It makes the
already-declared `fsgg-contracts@1.1.0` actually obtainable and keeps the three authorities
coherent going forward.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - FS.GG.Contracts 1.1.0 is obtainable from the org feed (Priority: P1)

A maintainer publishes `FS.GG.Contracts 1.1.0` so that the package version matching the
current source is resolvable by any authorized consumer (other FS-GG repos, Renovate), closing
the feed-behind-source gap.

**Why this priority**: This is the core ask of #27. Until 1.1.0 lands on the feed, the feed
stays a version behind the source, the registry `package-version` cannot advance, and any
consumer that pins or resolves to the current source version cannot restore it. Every other
story depends on the package actually landing.

**Independent Test**: Run the existing release publish path resolving to version 1.1.0, then
query the org feed for `fs.gg.contracts`; `1.1.0` is listed (not 404) alongside the prior
`1.0.1`, and a fresh authorized consumer in a clean environment can restore exactly `1.1.0`
from the feed.

**Acceptance Scenarios**:

1. **Given** the `FS.GG.Contracts` source is at `<Version>` 1.1.0 on the canonical
   FS.GG.SDD repository, **When** the release publish path runs and the contracts tests have
   passed, **Then** `FS.GG.Contracts 1.1.0` is pushed to the org feed and a subsequent feed
   query returns `1.1.0` rather than 404.
2. **Given** `1.1.0` has already been published, **When** the publish path runs again for
   `1.1.0`, **Then** the run succeeds without error and does not fail on the duplicate
   (idempotent re-publish).
3. **Given** an authorized consumer with feed read access, **When** it restores
   `FS.GG.Contracts` at `1.1.0` from the org feed, **Then** the restore succeeds and resolves
   the published package.

---

### User Story 2 - Registry package-version advances so source, feed, and registry agree (Priority: P2)

After 1.1.0 is live on the feed, the org registry's `package-version` is advanced from
`1.0.1` to `1.1.0` so that all three authorities — source, feed, and registry — record the
same version and the compatibility projection reflects reality.

**Why this priority**: The publish (US1) is the prerequisite; this story makes the publish
visible at the coordination layer and removes the last remaining incoherence (`package-version`
trailing the feed). Without it, the registry continues to advertise `1.0.1` as the published
package even though `1.1.0` is live.

**Independent Test**: After the feed shows `1.1.0`, confirm (via the cross-repo request loop)
that `FS-GG/.github` `registry/dependencies.yml` records `fsgg-contracts.package-version: 1.1.0`
and its `docs/registry/compatibility.md` projection agrees; the `contract-coherence` gate stays
green.

**Acceptance Scenarios**:

1. **Given** `FS.GG.Contracts 1.1.0` is confirmed live on the feed, **When** the SDD side
   notifies the registry coordinator on FS-GG/.github#42 (or its successor), **Then** the
   registry `package-version` is advanced to `1.1.0` and source/feed/registry are fully
   coherent.
2. **Given** the registry now records `version` 1.1.0 and `package-version` 1.1.0, **When** a
   `.github` PR runs the `contract-coherence` gate, **Then** the gate is green (no
   `registry != SDD` mismatch).

---

### User Story 3 - A source contract bump can no longer silently outrun the feed and registry (Priority: P3)

A maintainer bumping the `FS.GG.Contracts` source `<Version>` in a future change is reminded —
durably and in-repo — that the same change set must publish the new version to the feed and
update the `.github` registry, so the exact gap feature 042 opened cannot recur unnoticed.

**Why this priority**: This is the root-cause fix. The publish (US1) and registry sync (US2)
clear the current incoherence; this guardrail keeps the three authorities coherent on every
future bump. It is lower priority only because it prevents recurrence rather than resolving the
present gap.

**Independent Test**: Inspect the repo's release documentation; a contracts-version-bump
checklist exists that names the three required, same-change actions (bump source, publish to
feed, update `.github` registry version + package-version) and references the
`contract-coherence` gate per ADR-0001. A maintainer following it produces a coherent
source/feed/registry set.

**Acceptance Scenarios**:

1. **Given** a maintainer is about to bump the `FS.GG.Contracts` source `<Version>`, **When**
   they consult the repo's release documentation, **Then** a checklist explicitly requires the
   same change set to publish the new version to the org feed and update the `.github` registry
   (`version` and `package-version`), citing the `contract-coherence` gate / ADR-0001.
2. **Given** the checklist is followed for a future bump, **When** the change merges, **Then**
   source, feed, and registry record the same version with no coherence-gate red.

---

### Edge Cases

- **Source version unreadable at publish time** — the publish path must fail loudly rather
  than silently degrade to publishing nothing (already guaranteed by feature 039's
  version-resolution contract); this feature relies on, and must not weaken, that behavior.
- **Re-publishing 1.1.0** — a second publish of an already-present version is an idempotent
  no-op success, never a failure.
- **Registry advanced before the feed** — the registry `version` already tracks the source
  (1.1.0) ahead of the feed; `package-version` MUST trail the actual feed and advance only
  after 1.1.0 is confirmed live, never speculatively.
- **Publish on a fork or non-canonical repo** — the publish path runs only on the canonical
  `FS-GG/FS.GG.SDD` repository (feature 039 guard); this feature does not change that.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The `FS.GG.Contracts` package at version `1.1.0` MUST be published to the org
  GitHub Packages feed (`nuget.pkg.github.com/FS-GG`) so that it is resolvable by authorized
  consumers, using the existing release-time pack+push path (feature 039) whose published
  version is the evaluated fsproj `<Version>`.
- **FR-002**: Publishing MUST be gated on the `FS.GG.Contracts` tests passing; a red test run
  MUST NOT reach the push (preserve feature 039's gate).
- **FR-003**: Re-publishing an already-present `1.1.0` MUST be an idempotent success, not a
  duplicate failure.
- **FR-004**: After `1.1.0` is confirmed live on the feed, the SDD side MUST notify the
  registry coordinator (FS-GG/.github#42 or successor) so that
  `registry/dependencies.yml` `fsgg-contracts.package-version` advances `1.0.1 → 1.1.0` and its
  compatibility projection agrees.
- **FR-005**: The repo MUST gain a durable, in-repo release checklist for a `FS.GG.Contracts`
  source version bump that requires, in the same change set: (a) bumping the source
  `<Version>`/`Fsgg.ContractVersion.value`, (b) publishing the new version to the org feed, and
  (c) updating the `.github` registry `version` and `package-version` — citing the
  `contract-coherence` gate and ADR-0001.
- **FR-006**: This feature MUST NOT alter any `.fsgg` schema, contract surface, contract
  version, or CLI behavior; the source contract version stays `1.1.0` as shipped by feature
  042.
- **FR-007**: The registry `package-version` MUST never advance ahead of the feed; it advances
  only after the corresponding package version is confirmed obtainable.

### Key Entities *(include if feature involves data)*

- **Contract source version** — the `FS.GG.Contracts` fsproj `<Version>` and matching
  `Fsgg.ContractVersion.value` (`1.1.0`); the authority the other layers track.
- **Feed package version** — the `FS.GG.Contracts` version actually obtainable from the org
  GitHub Packages feed (currently `1.0.1`; target `1.1.0`).
- **Registry pins** — `fsgg-contracts.version` (source pin enforced by the coherence gate;
  already `1.1.0`) and `fsgg-contracts.package-version` (last feed version; currently `1.0.1`,
  target `1.1.0`) in `FS-GG/.github` `registry/dependencies.yml`.
- **Contracts-bump release checklist** — the durable in-repo artifact enumerating the
  same-change actions required for any future source bump.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A feed query for `fs.gg.contracts` against the org feed lists `1.1.0` (not 404),
  and a clean authorized consumer can restore exactly `1.1.0`.
- **SC-002**: All three authorities record `1.1.0`: source `<Version>`, the feed's newest
  obtainable version, and the registry `package-version`.
- **SC-003**: The `contract-coherence` gate is green on `.github` PRs and `main` (no
  `registry != SDD` mismatch and no feed-behind-`package-version` mismatch).
- **SC-004**: A documented contracts-version-bump checklist exists in the repo's release
  documentation and names all three same-change actions, such that a maintainer who follows it
  produces a coherent source/feed/registry set on the next bump.
- **SC-005**: No `.fsgg` schema, contract surface, contract version, or CLI output byte changes
  as part of this feature (verified against the existing deterministic/golden contracts).

## Assumptions

- The feature 039 release publish workflow (`.github/workflows/release.yml`) is the
  sanctioned publish mechanism and is functioning; this feature invokes it for `1.1.0` rather
  than building a new publish path. The publish trigger (a cut release, a `v1.1.0` tag, or a
  `workflow_dispatch` with `version=1.1.0`) is an operational choice left to the maintainer and
  is out of scope as a code change.
- The `.github` registry coordinator advances `package-version` on the cross-repo request loop
  (per ADR-0001); the SDD side's obligation is to publish and to notify, not to edit the
  `.github` registry directly.
- The durable guardrail in scope is a **documented release checklist line** (as the issue
  suggests), not a new automated CI gate. An automated advisory check that flags the source
  `<Version>` running ahead of the feed is a possible future enhancement and is out of scope
  here.
- `FS.GG.Contracts 1.1.0` is functionally the additive 042 source; no migration or breaking
  change accompanies the bump, so publishing it does not require a compatibility-matrix entry
  beyond recording the new version.
