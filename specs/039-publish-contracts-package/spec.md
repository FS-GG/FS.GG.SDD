# Feature Specification: Publish FS.GG.Contracts to the org package feed on release

**Feature Branch**: `039-publish-contracts-package`

**Created**: 2026-06-28

**Status**: Draft

**Change Tier**: Tier 2 (release-engineering; no `.fsgg` schema, contract surface, contract
version, or CLI behavior change — FR-010)

**Input**: User description: "next non blocked sdd item on the project coordination board" → resolved to FS-GG/FS.GG.SDD#17 (H4 · sdd — Publish FS.GG.Contracts to org GitHub Packages on release; producer half of the auto-update fabric)

## Context

`FS.GG.Contracts` is the SDD-owned, org-shared typed source of truth for every `.fsgg`
schema, the template-provider descriptor, and the cross-repo dependency registry. The
cross-repo **auto-update fabric** (epic FS-GG/.github#16, Pillar 4; tracking #21/#22) is
already wired and verified on the *consumer* side — Renovate is installed, the org preset
resolves, and `read:packages` authentication to the org feed succeeds. The fabric is
nonetheless inert because the **producer** side does not exist: nothing publishes
`FS.GG.Contracts` to the org package feed (`nuget.pkg.github.com/FS-GG`), so the package
404s there and every consumer/Renovate lookup returns `no-result`. The dependency registry
records `fsgg-contracts@1.0.0` as published, which is **incoherent** with the actually-empty
feed.

This feature adds the producer half for `FS.GG.Contracts`, mirroring the already-merged
sibling for the rendering libraries (FS-GG/FS.GG.Rendering#15). It is a release-engineering
change: it does not alter any contract surface, schema, or CLI behavior, and it does not add
or change a versioned cross-repo contract — it makes the already-declared `fsgg-contracts`
package actually obtainable from the feed the registry already points at.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A released version of the contracts package is obtainable from the org feed (Priority: P1)

When a maintainer cuts a release of FS.GG.SDD, the `FS.GG.Contracts` package for that release
version is published to the org package feed and becomes resolvable by any authorized consumer
(other FS-GG repos, Renovate). This is the core value: it closes the empty-feed gap that
makes the entire auto-update fabric inert.

**Why this priority**: Without it, the feature delivers nothing — the feed stays empty, the
registry stays incoherent, and no downstream consumer can resolve the package. Every other
story depends on a package actually landing on the feed.

**Independent Test**: Cut a release (or run the publish path with an explicit version), then
query the feed for `fs.gg.contracts`; the released version is listed (not 404) and a
fresh consumer in a clean environment can restore that exact version from the feed.

**Acceptance Scenarios**:

1. **Given** a release is cut on the canonical FS.GG.SDD repository, **When** the release
   publishing path runs and the build/tests have passed, **Then** the `FS.GG.Contracts`
   package at the release version is pushed to the org feed and a subsequent feed query
   returns that version rather than 404.
2. **Given** the same release version has already been published, **When** the publishing
   path runs again for that version, **Then** the run succeeds without error and does not
   fail on the duplicate (idempotent re-run).
3. **Given** an authorized consumer with feed read access, **When** it restores
   `FS.GG.Contracts` at the published version from the org feed, **Then** the restore
   succeeds against the org feed alone (no local/dev feed required).

---

### User Story 2 - The published version is derived from the release, not hand-set (Priority: P1)

The version stamped onto the published package is derived deterministically from the release
that triggers it, so the package version, the release tag, and the registry's declared
`fsgg-contracts` version cannot silently drift apart.

**Why this priority**: A package published under the wrong or stale version re-introduces the
exact incoherence this feature exists to remove, and would mislead Renovate's version
detection. Correct, release-derived versioning is part of the MVP, not a refinement.

**Independent Test**: Trigger the publishing path from a release/tag with a known version and
from a manual run with an explicit version; in each case the published package carries exactly
that version (with any release-tag decoration normalized consistently).

**Acceptance Scenarios**:

1. **Given** a release whose version-bearing tag matches the package's declared `<Version>`,
   **When** the publishing path runs, **Then** the published package version equals that
   declared version (and the matching tag) under the agreed normalization (a leading `v` and
   any pre-release/build decoration handled consistently with the rendering sibling).
2. **Given** a manual run that supplies an explicit version, **When** the publishing path
   runs, **Then** the published package carries that exact version.
3. **Given** a manual run that supplies **no** version, **When** the publishing path runs,
   **Then** it performs a pack-only dry run and pushes nothing to the feed.

---

### User Story 3 - Publishing is gated, safe, and only happens from the canonical source (Priority: P2)

Publishing happens only after the package's tests pass and only from the canonical FS.GG.SDD
repository, using least-privilege credentials, so forks and red builds can never push to the
shared org feed.

**Why this priority**: It protects the shared feed from bad or unauthorized artifacts. The
package can technically be published without these guards (P1), but shipping without them
would risk polluting an org-wide resource, so it is a required hardening rather than the core
slice.

**Independent Test**: Confirm that a forked repository and a run with failing tests do not
reach the push step, and that the push uses repo-scoped least-privilege credentials rather
than a long-lived personal token.

**Acceptance Scenarios**:

1. **Given** a fork of the repository, **When** a release/tag event occurs there, **Then** the
   publish step does not run and nothing is pushed to the org feed.
2. **Given** the package's tests fail, **When** the release path runs, **Then** publishing is
   skipped (the push step is never reached).
3. **Given** a release on the canonical repository with passing tests, **When** publishing
   runs, **Then** it authenticates with repository-scoped, write-to-packages credentials
   provisioned for that run (no personal access token).

---

### User Story 4 - The registry's recorded coherence reflects the real feed state (Priority: P3)

Once a real package exists on the feed, the cross-repo dependency registry's `fsgg-contracts`
record reflects a genuinely-published package instead of a declared-but-absent one, so the
registry stops asserting an incoherent state.

**Why this priority**: It closes the documentation/coherence loop the feature was filed to
fix, but it is a follow-on record-keeping step in the org `.github` repo (outside this
repository's product code) and is only meaningful once P1 has actually landed a package.

**Independent Test**: After a package is on the feed, the registry's `fsgg-contracts`
coherence note no longer describes the feed as empty / the package as 404, and any
contract-coherence check that compares the declared version to the feed passes.

**Acceptance Scenarios**:

1. **Given** `FS.GG.Contracts` is resolvable on the org feed, **When** the registry's
   `fsgg-contracts` entry is reviewed, **Then** its coherence note reflects a real published
   package rather than an empty feed.

---

### Edge Cases

- **Re-run for an already-published version**: must succeed idempotently (skip duplicate),
  never fail the release on a duplicate push.
- **Mismatched version-bearing release tag**: when a release/tag event carries a
  version-bearing tag whose version differs from the package's declared `<Version>`, the run
  must fail loudly (tag/package drift) rather than publish under a wrong version. A missing or
  non-version-bearing tag is acceptable — the declared `<Version>` is the version source, so
  the publish proceeds at that version.
- **Unreadable or empty package version on a release event**: the version cannot be
  determined; the run must fail loudly rather than silently degrade to a dry run or publish an
  empty version. (An intentional no-version *manual* run is the only benign empty-version path
  — it is a pack-only dry run.)
- **Tests pass but pack produces no package** (e.g. packability misconfigured): the run must
  not silently report success with nothing pushed.
- **Non-canonical repo or fork triggers the event**: must be a no-op for publishing.
- **Feed/credentials unavailable at push time**: the run fails visibly; it must not mark the
  release as fully published when the package did not land.
- **Manual dry run**: packs and validates the package set but pushes nothing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: On a release of FS.GG.SDD, the system MUST pack the `FS.GG.Contracts` package
  and publish it to the org package feed (`nuget.pkg.github.com/FS-GG`).
- **FR-002**: The system MUST publish at a release-derived version. On a release/tag event the
  version is the package's own declared `<Version>` (the version the release actually ships),
  and the triggering tag is a **coherence check** — a version-bearing tag whose version differs
  from the declared `<Version>` MUST fail the run. On a manual run the version is an explicitly
  supplied value. Supplied and tag versions are normalized (e.g. a leading `v` stripped)
  consistently with the rendering sibling (FS-GG/FS.GG.Rendering#15).
- **FR-003**: A manual run with no supplied version MUST perform a pack-only dry run and push
  nothing.
- **FR-004**: Publishing MUST be idempotent for an already-published version — a duplicate
  push MUST be skipped without failing the run.
- **FR-005**: Publishing MUST occur only after the `FS.GG.Contracts` tests pass; if those
  tests fail, the push MUST NOT run.
- **FR-006**: Publishing MUST occur only from the canonical FS.GG.SDD repository; forks MUST
  NOT publish to the org feed.
- **FR-007**: The push MUST authenticate with repository-scoped, least-privilege
  write-to-packages credentials provisioned for the run (no long-lived personal access token).
- **FR-008**: After publishing, the package version on the feed MUST equal the version
  declared for `fsgg-contracts` in the cross-repo dependency registry (no producer-side drift
  between declared and published version).
- **FR-009**: An incomplete or failed publish MUST surface as a failed run; the release MUST
  NOT be reported as published when the package did not land on the feed.
- **FR-010**: The feature MUST NOT change any `.fsgg` schema, contract surface, contract
  version, or CLI behavior — it adds only the release-time producer path for an
  already-declared package.
- **FR-011**: As part of resolution, the cross-repo dependency registry's `fsgg-contracts`
  coherence record MUST be updated to reflect a genuinely published package once one exists on
  the feed. *(Lands in FS-GG/.github, outside this repository's product code.)*

### Key Entities *(include if feature involves data)*

- **FS.GG.Contracts package**: the packable artifact produced from `src/FS.GG.Contracts`;
  identified by its package id and a release-derived version; the unit published to the feed.
- **Org package feed**: the shared org-scoped package feed (`nuget.pkg.github.com/FS-GG`) that
  consumers and Renovate resolve against; currently empty for `FS.GG.*`.
- **Release trigger**: the release/tag (or manual run with an explicit version) that supplies
  the version and authorizes a publish.
- **`fsgg-contracts` registry record**: the cross-repo dependency registry entry that declares
  the contracts package version and its coherence with the feed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a release is cut, querying the org feed for the contracts package returns
  the released version (not 404), within the same release run.
- **SC-002**: A consumer in a clean environment with feed read access can restore
  `FS.GG.Contracts` at the published version from the org feed alone, with no local/dev feed
  configured.
- **SC-003**: The version on the feed matches the triggering release version and the registry's
  declared `fsgg-contracts` version — three-way agreement, zero drift.
- **SC-004**: Re-running publish for an already-published version completes successfully and
  pushes no duplicate (100% idempotent on repeat).
- **SC-005**: No publish occurs from a fork or when the contracts tests fail (0 unauthorized or
  red-build publishes).
- **SC-006**: After the first successful publish, the registry no longer describes the feed as
  empty / the package as 404 for `fsgg-contracts`, and any declared-vs-feed coherence check for
  it passes.

## Assumptions

- **Release event shape**: FS.GG.SDD release versioning mirrors the rendering sibling — a
  version-bearing tag (e.g. `v0.1.x[-preview.N]`) with a leading `v` stripped, plus a manual
  trigger that accepts an optional explicit version (omit = pack-only dry run). The exact tag
  grammar is confirmed against Rendering#15's resolution at plan time.
- **Single-package scope**: SDD publishes exactly one package here (`FS.GG.Contracts`); unlike
  the rendering sibling there is no multi-package coherent set to version together.
- **Credentials & permissions**: the run can obtain repository-scoped write-to-packages
  credentials sufficient for same-org push without a personal access token; the read side of
  the feed is already provisioned and verified (epic #16 / #21 / #22) and is out of scope here.
- **Packability is already correct**: `FS.GG.Contracts` is already marked packable; this
  feature does not re-scope which projects pack.
- **No release workflow exists yet for publishing**: the repository currently runs only the
  per-PR gate and the nightly composition-acceptance paths; the release-time producer path is
  new.
- **Registry update location**: the `fsgg-contracts` coherence record (FR-011/SC-006) lives in
  FS-GG/.github and is updated there per the cross-repo coordination protocol; this repository
  owns only the producer path that makes the package real.
- **Determinism/offline inner loop unaffected**: this is a release-time path only; it does not
  run in the default offline inner loop and changes no deterministic CLI/golden contract.

## Dependencies

- **Sibling pattern (reference)**: FS-GG/FS.GG.Rendering#15 (merged) — the libraries+template
  pack→push producer step whose version-resolution and gating this mirrors.
- **Auto-update fabric (consumer side, done)**: epic FS-GG/.github#16 Pillar 4; tracking #21/#22
  — Renovate, org preset, and feed read-auth already verified.
- **Registry coherence (cross-repo)**: the `fsgg-contracts` entry in FS-GG/.github
  `registry/dependencies.yml` (and its compatibility projection) is updated as part of
  resolution.
- **Not blocked**: this item carries no open blocker on the Coordination board (the only other
  open SDD item, #10, is blocked by the still-open FS-GG/FS.GG.Rendering#9).
