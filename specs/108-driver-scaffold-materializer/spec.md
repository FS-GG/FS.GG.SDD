# Feature Specification: Scaffold-Time Materializer for the Delivered `workRoadmap` Driver Skill

**Feature Branch**: `item/621-driver-scaffold-materializer`

**Created**: 2026-07-21

**Status**: Draft

**Input**: FS.GG.SDD#621 — "[cross-repo] scaffold-time materializer for the delivered workRoadmap
driver package (ADR-0054 byte-transport / .github#1300)". Filed from `FS-GG/.github`, origin
FS.GG.SDD#620. Contract: `scaffold-provider` (v1), `skill-registry` (ADR-0017). ADRs: ADR-0054
(driver skill class / byte-transport), ADR-0062 / ADR-0063 (versioned package + on-disk
materialize), ADR-0014 (one manifest, materialize-verify), ADR-0037 / ADR-0015 §3
(publish-before-flip), ADR-0061 (structural scope; semantic enforcement moves to the consumer).

## Overview

**ADR-0054** introduced a third skill class — the **driver** — a skill authored not by a producer repo
but by `.github` itself, delivered as bytes and materialized into a scaffolded product tree's skill
roots. Step 1 (feature 106, FS.GG.SDD#591) taught the registry that `scope: driver` is a well-formed
value. **This feature is step 2**: teach the SDD scaffold-time materializer to consume the now-published
delivered driver package and lay the driver skill into a scaffolded workspace.

The prerequisite is satisfied. Per publish-before-flip (ADR-0037), this could not start until the
transport package was published; it now is:

- **Package:** `FS.GG.Drivers`, version **`0.1.0`** — live on nuget.org and the org GitHub Packages feed.
- It carries `build/FS.GG.Drivers.props` (exposing `$(FsggDriversContentDir)`), a content-addressed
  `driver-skill-manifest.json` (ADR-0014, per-skill `sha256`), and `drivers/skills/<id>/SKILL.md` bytes.
- The one materializable row it delivers is **`workRoadmap`** (`scope: driver`,
  `materializes-when: always`). It also declares an inert `drive-board` row
  (`scope: operator`, `materializes-when: false`, bytes not shipped).

### The two constraints that shape the whole feature

1. **Offline at scaffold time → embed at build time (ADR-0054 §Byte-transport).** A published
   `fsgg-sdd` runs as an installed `dotnet tool`; a NuGet package's *content* files are consumed at the
   consumer's **build** time and are **not** carried into the installed tool, nor guaranteed present in
   an end user's NuGet cache at scaffold time. The package's own `build/FS.GG.Drivers.props` states the
   contract verbatim: *"the CLI embeds the bytes it restored online at build time."* So the driver
   manifest and bodies are **embedded resources** in the CLI's own assembly (the same seam
   `SeededSkills` uses for the `fs-gg-sdd-*` skeleton), restored online at build time from
   `$(FsggDriversContentDir)`, and read from compiled-in bytes at scaffold time — never the NuGet
   cache, never a `.github` clone.

2. **No cross-repo source literals (`scaffold` FR-002 / SC-005).** Generic SDD embeds no provider- or
   `.github`-specific package id, path, skill id, or docs URL as *behavior*. The package **identity and
   version** are a central pin (`Directory.Packages.local.props`, Renovate-managed), consumed exactly
   as ADR-0062's `FS.GG.Kit` is; the **set of driver skills and their predicates** is read from the
   delivered `driver-skill-manifest.json`, never from a compiled-in list. The materializer knows the
   *shape* of a driver manifest, not the *contents* of this one.

### Where enforcement lives now (ADR-0061)

Step 1's "known but not enforced" registry rail was retired by ADR-0061: the registry validates `scope`
as an opaque non-blank string and asserts nothing about a driver row's shape. **Semantic enforcement
moves to the consumer that materializes the token — this materializer.** It is therefore the component
that (a) content-addresses each body against its manifest `sha256` before writing, and (b) evaluates
each row's `materializes-when` predicate and materializes only rows whose predicate holds.

### What this feature is not

- It does not delete or rewrite the `spec-kit` lane, and it changes no lifecycle default (that is 107).
- It does not author driver skill *content* — the bytes are `.github`-owned and delivered; SDD lays them
  down verbatim and verifies them, never edits them.
- It does not add a new `fsgg-sdd` lifecycle stage. The materialize is a step of `scaffold`
  (post-instantiation), and — for backfilling an existing scaffold — a no-clobber `upgrade` re-seed.

## Requirements

### Functional

- **FR-001**: `fsgg-sdd scaffold`, after a successful provider instantiation, MUST materialize each
  delivered driver skill whose `materializes-when` predicate holds into **all three** agent skill roots
  (`.claude/skills/<id>/SKILL.md`, `.codex/skills/<id>/SKILL.md`, `.agents/skills/<id>/SKILL.md`),
  byte-identically (`claude ≡ codex ≡ agents`).
- **FR-002**: The driver manifest and bodies MUST be read from **compiled-in (embedded) bytes**, sourced
  at build time from the pinned `FS.GG.Drivers` package's `$(FsggDriversContentDir)`. At scaffold time
  the materializer MUST NOT read the NuGet cache, a `.github` clone, or any network resource.
- **FR-003**: Before writing any body, the materializer MUST verify that the embedded body's
  content-addressed digest (CRLF→LF-normalized SHA-256, lowercase hex, `\A[0-9a-f]{64}\z`) equals the
  `sha256` its manifest row declares (ADR-0014). A mismatch MUST fail closed — the driver is not written,
  and the scaffold reports the defect — never a blind copy.
- **FR-004**: The materializer MUST materialize a row **iff** its `materializes-when` predicate evaluates
  to true. The delivered vocabulary (`always` → true; `false` → false) MUST be honored; a predicate the
  materializer cannot evaluate MUST fail closed (row skipped, non-blocking advisory), never default to
  materializing.
- **FR-005**: Driver writes MUST be **no-clobber** (the `AgentGuidanceTarget` class): an existing
  same-path file — author-edited or previously seeded — is never overwritten.
- **FR-006**: Materialized driver paths MUST be recorded in `.fsgg/scaffold-provenance.json` with a
  distinct **driver** owner and each path's content `sha256`. The record schema stays **v1** (additive
  field only). These paths are `.github`-owned external content: `refresh` MUST exclude them (it never
  regenerates them), exactly as it excludes `generatedProduct` and the seeded skeleton.
- **FR-007**: The reserved `fs-gg-sdd-*` namespace MUST remain SDD-owned and untouched by driver
  materialization; a driver row whose `id` collides with a seeded `fs-gg-sdd-*` skill MUST be rejected
  (fail closed), never allowed to shadow the skeleton.
- **FR-008**: The embedded driver set MUST be pinned by a **drift guard** (`registry` sub-verb or test)
  asserting the compiled-in manifest + bodies are byte-identical and digest-coherent with the pinned
  package, so an out-of-band edit or a stale pin is caught in CI.
- **FR-009**: `scaffold` MUST report driver materialization in all three projections (json / text /
  rich) additively — the materialized skill ids and roots on success, the defect on a verify/predicate
  failure — and an incomplete driver materialization MUST NOT be reported as complete.
- **FR-010** *(carved to a follow-up — see Out of scope)*: `fsgg-sdd upgrade`'s no-clobber re-seed backfills
  a **missing** driver skill into an existing scaffold (missing-only, consumer-writes-only), and `doctor`
  reports a missing expected driver as read-only drift. This is the existing-scaffold **transition** path;
  it is separable from — and sequenced after — new-scaffold materialization, which this PR delivers in full.

### Acceptance criteria

- **AC-001** (FR-001/FR-004): A scaffold against a provider yields `workRoadmap/SKILL.md` under each of
  `.claude/skills`, `.codex/skills`, `.agents/skills`, byte-identical across the three, because its
  `materializes-when` is `always`.
- **AC-002** (FR-004): The delivered `drive-board` row (`materializes-when: false`) is **not** written to
  any root, and its bytes are not required to be present.
- **AC-003** (FR-003): Given a manifest row whose `sha256` disagrees with the embedded body, the
  materializer writes nothing for that row and surfaces `scaffold.driverVerifyFailed`; the scaffold does
  not report the driver as materialized.
- **AC-004** (FR-002): With no NuGet cache entry for `FS.GG.Drivers` and no network, a scaffold still
  materializes the driver (bytes are compiled in).
- **AC-005** (FR-006): After a scaffold, `.fsgg/scaffold-provenance.json` lists each materialized driver
  path with owner `driver` and a `sha256`, schema still `1`; a subsequent `refresh` neither rewrites nor
  removes the driver files and does not report them as stale.
- **AC-006** (FR-005/FR-007): A pre-existing `workRoadmap/SKILL.md` in a root is left untouched
  (no-clobber); a manifest whose row id is `fs-gg-sdd-plan` is rejected.
- **AC-007** (FR-008): The embedded-driver drift guard is green against the pinned package and goes red
  if the embedded manifest or a body is altered out of band.
- **AC-008** (FR-010, *carved to a follow-up*): `upgrade` on a scaffold missing `workRoadmap` re-seeds it
  into the roots no-clobber; `doctor` reports the same missing driver as drift without writing.

### User stories

- **US-001**: As a product author who scaffolds a new SDD workspace, I get the `workRoadmap` driver skill
  ready to use in my agent, with no manual copy from `.github` and no network dependency at scaffold time.
- **US-002**: As the platform maintainer, I bump the driver by bumping one pinned package version
  (Renovate), rebuild/publish the CLI, and every new scaffold carries the new bytes — with a drift guard
  proving the CLI's embedded set matches the pin.
- **US-003**: As an author of a workspace scaffolded before the driver existed, I run `fsgg-sdd upgrade`
  and the missing driver is backfilled no-clobber, without touching my edits.

## Success criteria

- **SC-001**: A fresh scaffold materializes `workRoadmap` into all three roots byte-identically, verified
  against its manifest `sha256`, with zero network access at scaffold time.
- **SC-002**: No provider-specific or `.github`-specific package id, skill id, path, or docs URL appears
  as behavior in generic SDD source; the pin is the only place the package identity lives, and the driver
  set is read from the delivered manifest.
- **SC-003**: The `scaffold-provenance.json` schema version is unchanged (`1`); the driver record is a
  purely additive field; `refresh` excludes driver paths.
- **SC-004**: The embedded-driver drift guard and the content-addressed verify both fail closed on any
  incoherence, and the existing `PublicSurface` / `surface --check` gates stay green.

## Out of scope

- **Existing-scaffold backfill (FR-010 / AC-008), carved to a follow-up item.** `doctor`-reports-missing
  and `upgrade`-`artifactReSeed`-backfills for the driver are the *transition* path for workspaces
  scaffolded before this feature; they extend the `Drift` expected-set and the remediation seam and are
  independently reviewable. New scaffolds materialize the driver in full in this PR, so the follow-up is a
  strict addition, not a fix. Sequenced after this PR lands.
- The `.github`-side `skills.yml` / `driver-skill-manifest.json` predicate widening (`.github#1247`).
- Any change to the `spec-kit` → `sdd` lane default (feature 107).
- Authoring or editing driver skill content (owned by `.github`).
