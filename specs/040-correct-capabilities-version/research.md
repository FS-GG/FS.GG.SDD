# Phase 0 Research: Correct capabilities schema version to 2

All unknowns from the Technical Context are resolved below. Each item records the
decision, the rationale, and the alternatives considered.

## R1. Grounding the corrected value (`capabilities` = 2)

- **Decision**: Set `Schemas.capabilitiesVersion = 2`, grounded against the
  Governance validator's actually-supported `capabilities` schema version (2), per
  the Governance-side decision recorded 2026-06-28 and the downstream re-type
  FS.GG.Governance#14.
- **Rationale**: `FS.GG.Contracts` is the authoritative single source for the
  Governance-owned declared constants; Governance does not override locally. The
  declared value must equal the value the validator supports, or a consumer that
  re-types onto the package silently changes which `capabilities.yml` files
  validate (a v2 file would be rejected as unsupported). The provisional `1` is
  simply wrong.
- **Alternatives considered**: (a) Mask the drift downstream by overriding the
  value in Governance — rejected; it re-introduces a local literal and contradicts
  the single-source decision. (b) Leave `1` and document the discrepancy —
  rejected; the package would remain an untruthful source of truth and would not
  unblock FS.GG.Governance#14.

## R2. Verification grounding (no external fixture)

- **Decision**: Update the existing Xunit assertion in
  `SchemaVersionConstantTests.fs` (the *"Governance-owned schema versions equal the
  declared reference values"* fact) to `Assert.Equal(2, Schemas.capabilitiesVersion)`,
  leaving the `governance`/`policy`/`tooling` assertions at `1`. Refresh the
  fact's comment to cite the Governance published-reference grounding
  (decision 2026-06-28 / FS.GG.Governance#14).
- **Rationale**: The grounding for Governance-owned constants is, by design, a
  declared-reference assertion (they are *declared*, never SDD-emitted), exactly as
  the existing test documents. There is no in-repo Governance fixture or golden
  file, and introducing one would couple SDD to a Governance runtime the
  constitution keeps optional. The comment-cited source is the appropriate grounding.
- **Alternatives considered**: Fetch/import a live Governance reference value as a
  fixture — rejected; violates "SDD must remain useful without Governance
  installed" and adds a cross-repo runtime dependency for a one-line constant.

## R3. Package version bump posture (`1.0.0` → `1.0.1`, patch)

- **Decision**: Bump `FS.GG.Contracts.fsproj` `<Version>` `1.0.0` → `1.0.1` (a
  SemVer **patch**), creating a new immutable identity; never re-pack `1.0.0`.
- **Rationale**: No public surface changes (`.fsi` and `PublicSurface.baseline`
  are identical) — the change is a corrected declared *value*, which the
  repo versioning policy classes as a patch-level correction. Consumers move
  forward by an explicit pin change, and `1.0.0`/`1.0.1` stay distinct immutable
  identities (FR-004).
- **Alternatives considered**: (a) Minor/major bump — rejected; no surface or
  breaking-shape change. (b) Re-publish `1.0.0` in place with the fix — rejected;
  violates immutability (FR-004) and would silently mutate existing pins.

## R4. Shared local folder feed (committed `nuget.config`)

- **Decision**: Add a repo-root `nuget.config` declaring a local folder source
  (key `fsgg-local`, value `./.fsgg-local-feed`) in addition to inherited sources,
  and commit the feed directory via `.fsgg-local-feed/.gitkeep`. Deliver `1.0.1`
  with `dotnet pack -p:Version=1.0.1 -o <tmp>` followed by
  `dotnet nuget push <tmp>/FS.GG.Contracts.1.0.1.nupkg --source fsgg-local`.
- **Rationale**: The clarified decision is an in-repo `nuget.config` + feed path.
  Committing an (empty) feed directory keeps the configured local source path
  present so `dotnet restore` does not warn/error on a missing source, while
  remaining empty until `1.0.1` is pushed. `dotnet nuget push --source <folder>`
  populates the hierarchical layout NuGet expects for a folder feed, so consumers
  resolve `1.0.1` by pointing their own `nuget.config` at the same conventional
  path. The repo `nuget.config` does **not** `<clear/>` inherited sources, so
  nuget.org and any existing GH Packages source keep working.
- **Alternatives considered**: (a) Documented manual pack-to-folder with no
  committed config — rejected per clarification (less reproducible). (b) Modify
  `release.yml` to add a local-feed target — rejected; couples the correction to
  CI the spec frames as deferred (FR-008). (c) An absolute well-known path — set
  aside in favour of a repo-relative committed path that is restore-safe and
  self-contained; the cross-repo "shared" convention is documented for consumers.

## R5. Org dependency registry pin (FR-006) — cross-repo follow-on

- **Decision**: Advance the `fsgg-contracts` pin `1.0.0` → `1.0.1` in the
  `FS-GG/.github` org registry as a follow-on coordination issue + PR
  (`owner: sdd`), tracked on the org Coordination board, executed **after** `1.0.1`
  is published. It is not a code change in this repo.
- **Rationale**: The registry physically lives in `FS-GG/.github`; this repo holds
  only documentary references. Sequencing the pin after publish keeps the registry
  truthful (it points at a version that actually resolves) and the contract-
  coherence workflow green. Use the `cross-repo-coordination` protocol.
- **Alternatives considered**: Direct in-feature edit to `FS-GG/.github` —
  set aside per clarification; the pin is bookkeeping that follows the P1 publish
  and is owned through the coordination protocol, not this repo's tree.

## R6. Surface baseline & SDD-emission isolation

- **Decision**: Make no change to `Schemas.fsi`, `PublicSurface.baseline`, the
  `Schemas.entries` shape, any SDD-emitted artifact schema version, or
  `docs/release/*` catalogued contracts; confirm the surface-baseline and
  entries tests still pass unchanged.
- **Rationale**: The Governance-owned constants are *declared reference* values,
  not SDD-emitted; they are absent from the `release-readiness.json` catalog
  (which records SDD-emitted views). The `.fsi` exposes `val capabilitiesVersion:
  int` regardless of value, so the reflection baseline is value-agnostic. This
  guarantees FR-007 / SC-004 (zero SDD behaviour change) and the no-emitted-output-
  coupling edge case.
- **Alternatives considered**: None needed — isolation is verified by the
  unchanged-baseline and unchanged-`entries` assertions acting as guards.
