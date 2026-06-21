# Quickstart / Validation: Release and Distribution Readiness

Runnable validation scenarios proving the feature works end-to-end. Each maps to
a user story / functional requirement. Run from the repo root unless a clean
environment is specified.

**Prerequisites**: .NET SDK (`net10.0`), repo checkout (except Scenario 5, which
uses a clean environment). Contracts: [release-readiness.md](contracts/release-readiness.md),
[versioning-policy.md](contracts/versioning-policy.md),
[schema-reference.md](contracts/schema-reference.md). Data model:
[data-model.md](data-model.md).

## Scenario 1 — Single version identity & compatibility record (US1 / FR-002, FR-003)

```bash
dotnet build -c Release
cat docs/release/release-readiness.json
```

**Expected**: `identity.version` equals the `<Version>` in
`Directory.Build.props` and the version reported by every `FS.GG.SDD.*` package;
`identity.cliCommandName = "fsgg-sdd"`; `compatibility[]` states the supported
Spec Kit range and the optional `governanceContractVersionRange` (or `null`). No
source reading needed to learn the version or compatibility surface (SC-001).

## Scenario 2 — Versioning policy maps every change class (US1 / FR-001)

Read [contracts/versioning-policy.md](contracts/versioning-policy.md) (published
to `docs/release/versioning-policy.md`).

**Expected**: an additive schema/command change ⇒ minor bump, no note;
a backward-incompatible change ⇒ major bump + required migration note; a
clarifying change ⇒ patch (acceptance scenarios US1-2/3). The version delta
between any two releases is explainable solely from the table (US1-4).

## Scenario 3 — Schema reference covers 100% of public outputs (US2 / FR-004, FR-005)

```bash
dotnet test --filter ReleaseReadinessCheck
```

**Expected**: the coverage test enumerates all 7 `GeneratedViewKind` outputs +
the public `--json` report and asserts each has a catalog entry (version, field
inventory, determinism, stability) and a `sourceArtifact` back-reference; any gap
fails as **not-ready** (SC-002, FR-012). The conformance test confirms a produced
artifact matches its documented schema — no undocumented/absent field (SC-003).

## Scenario 4 — Baselines catch accidental breaking changes (US3 / FR-006, FR-007)

```bash
dotnet test --filter Baseline        # green on a clean tree
# introduce a breaking change to a public schema/--json shape, then:
dotnet test --filter Baseline        # FAILS with an actionable diff naming the contract
```

**Expected**: an unaccounted breaking change fails the golden-fixture / surface
baseline with a diff (SC-004). An intentional, version-bumped, migration-noted
change regenerates the baseline deterministically and review shows only the
intended surface change (US3-2).

## Scenario 5 — Determinism (US3 / FR-008, SC-005)

```bash
dotnet test --filter Determinism
```

**Expected**: producing `release-readiness.json` and the baselines twice over
identical inputs yields byte-identical output — no clock, host path, ordering
nondeterminism, or ANSI styling.

## Scenario 6 — Clean-environment install through `ship` (US4 / FR-011, SC-007)

In a fresh environment with **no FS.GG checkout and no Governance runtime**,
following [docs/release/installation.md]:

```bash
dotnet tool install --global FS.GG.SDD.Cli   # (registry per installation docs)
fsgg-sdd --version
fsgg-sdd init && fsgg-sdd ... && fsgg-sdd ship
```

**Expected**: the CLI installs and runs the lifecycle through `ship` with no prior
FS.GG knowledge and no Governance present (SC-007).

## Scenario 7 — Migration note obligation (US4 / FR-009, FR-010, SC-006)

```bash
dotnet test --filter Migration
```

**Expected**: a release with a breaking change has a
`docs/release/migrations/<version>.md` enumerating each breaking change and its
adaptation step; an additive-only release has none and the absence is consistent
with the policy (US4-2/3).

## Scenario 8 — Boundary exclusion (FR-014 / SC-008)

```bash
dotnet test --filter BoundaryExclusion
```

**Expected**: no Governance-owned gate/route/profile/freshness/publish/provenance
vocabulary appears in any artifact produced by this feature; Governance
compatibility is only a declared `contractVersion` range.

## Full suite

```bash
dotnet test
```

**Expected**: all existing tests plus the new Release-readiness suites pass; the
updated `PublicSurface.baseline` reflects the added `ReleaseContract` surface.
