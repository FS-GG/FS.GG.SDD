# Feature Specification: Build/CI hygiene — hermetic restore, caching, format & warning gates, de-duplicated locked-restore

**Feature Branch**: `064-build-ci-hygiene`

**Created**: 2026-07-03

**Input**: FS.GG.SDD board item #74 — 2026-07-02 code-quality & architecture review (§5.2 / remediation #10, MEDIUM). Repo-local, not cross-repo.

**Change Tier**: Tier 2 (build / tooling / CI / repo-config change). This feature changes `nuget.config`, `Directory.Build.local.props`, the three GitHub Actions workflows, and adds repo-owned tooling config (`.editorconfig`, a fantomas config, a composite action). It introduces **no** change to any `fsgg-sdd` CLI output, JSON automation contract, persisted schema, or golden baseline. Fantomas may **reformat existing source**, but only whitespace/layout — no token, identifier, or runtime-behaviour change, and every deterministic/golden contract MUST stay byte-identical. Any managed org file (`Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`) MUST stay byte-identical to `FS-GG/.github` `dist/dotnet/` — repo-specific settings go in the `*.local.props` files (the `build-config-drift` gate enforces this).

## Overview

The 2026-07-02 review (§5.2, remediation #10) found the build/CI fabric is
mostly sound but carries five hygiene gaps, one of which is the most plausible
root cause of a recurring, actively-painful bug:

- **Non-hermetic restore (the root cause):** `nuget.config` adds the local feed
  but never `<clear/>`s inherited sources, so `dotnet restore` depends on
  per-machine NuGet configuration. This is the most plausible root of the
  recorded `FSharp.Core` `contentHash` local↔CI divergence and the repeated
  lockfile-regeneration churn (commit `3330a06`) — the same churn that dirties
  all 11 `packages.lock.json` files on a fresh local restore today. A `<clear/>`
  plus explicit sources plus package source mapping makes restore reproducible
  regardless of machine, killing the divergence class.
- **No restore caching:** none of the three workflows sets `cache: true` on
  `actions/setup-dotnet@v4`, despite committed lockfiles being a perfect cache
  key. Every CI job pays a full cold restore.
- **No format gate:** there is no `.editorconfig` and no fantomas config;
  formatting consistency is convention-borne and unenforced. A drifted format
  can land silently.
- **Narrow warning ratchet:** only `FS3261;FS0025` are promoted to errors (in
  `Directory.Build.local.props`), on top of the canonical `NU1603;NU1608`, with
  `TreatWarningsAsErrors=false`. The codebase currently sits at a near-zero
  warning count — cheap to widen the ratchet so new warning classes cannot
  silently re-accumulate.
- **Duplicated locked-restore + un-wired release smoke:** the `Restore (locked)`
  block is copy-pasted **5×** across `gate.yml` (1) and `release.yml` (4:
  `contracts-tests`, `cli-tests`, `publish-contracts`, `publish-cli`) — one copy
  already divergent in scope — and `scripts/verify-cli-tool.sh` (the packed-tool
  self-containment smoke) is never wired into `release.yml`, so a
  non-self-contained tool package could publish undetected. The packed tool also
  sets no `RollForward` policy.

This feature makes restore hermetic and reproducible, caches it in CI, enforces
formatting and a wider warning ratchet, and consolidates the duplicated
locked-restore into one composite action while wiring the release smoke in — all
without changing any `fsgg-sdd` behaviour, output, or contract.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Restore is hermetic and produces the same lockfile everywhere (Priority: P1)

A contributor clones the repo and runs `dotnet restore` (or an editor triggers
it) on a machine with its own inherited NuGet sources. Today the resolved
`contentHash` for `FSharp.Core` (and potentially others) can differ from the
value committed by CI, so the 11 `packages.lock.json` files show spurious
modifications and a locked-mode CI restore can fail on a graph that "looks"
identical — the recurring lockfile-regeneration pain.

After this feature, `nuget.config` clears inherited sources, declares its sources
explicitly, and maps package sources, so restore resolves the identical graph and
identical content hashes on any machine. A clean checkout restored locally leaves
the committed lockfiles unmodified.

**Why this priority**: It is the root-cause fix for the actively-recurring bug
(local↔CI hash divergence, lockfile churn) and underpins the determinism the
whole gate depends on. The other stories are hygiene; this one removes a standing
source of failed restores and misleading diffs.

**Independent Test**: On a machine with unrelated inherited NuGet sources, run
`dotnet restore FS.GG.SDD.sln` against a clean checkout and confirm no
`packages.lock.json` file is modified; run `dotnet restore --locked-mode` and
confirm it succeeds; confirm all lockfiles still agree on `FSharp.Core` (a single
resolved version and hash).

**Acceptance Scenarios**:

1. **Given** `nuget.config`, **When** inspected, **Then** it `<clear/>`s inherited
   sources and declares each required source explicitly so restore is independent of
   per-machine NuGet configuration (no package source mapping — see FR-001 amendment).
2. **Given** a clean checkout on a machine with unrelated inherited feeds,
   **When** `dotnet restore FS.GG.SDD.sln` runs, **Then** no committed
   `packages.lock.json` is modified.
3. **Given** the committed lockfiles, **When** `dotnet restore --locked-mode`
   runs on that same machine, **Then** it succeeds (no drift, no NU1603
   substitution).
4. **Given** all 11 `packages.lock.json` files after restore, **When** compared,
   **Then** they agree on a single resolved `FSharp.Core` version and content
   hash.

---

### User Story 2 - CI restores from cache instead of paying a full cold restore (Priority: P2)

A maintainer pushes a PR. Every CI job today re-downloads the full dependency
closure from scratch even though the committed lockfiles exactly pin it.

After this feature, each workflow job enables `actions/setup-dotnet@v4` NuGet
caching keyed on the committed lockfiles, so unchanged dependencies restore from
cache and only a genuine lockfile change triggers a cold restore.

**Why this priority**: Pure CI-time savings with no correctness risk once restore
is hermetic (US1); valuable but strictly secondary to the root-cause fix.

**Independent Test**: Confirm each `setup-dotnet` invocation across all three
workflows enables caching keyed on `**/packages.lock.json`; confirm two
successive runs of an unchanged branch show a cache hit on the second.

**Acceptance Scenarios**:

1. **Given** each workflow's `actions/setup-dotnet@v4` step, **When** inspected,
   **Then** it enables NuGet caching keyed on the committed `packages.lock.json`
   files.
2. **Given** an unchanged branch run twice, **When** the second run restores,
   **Then** it restores from cache (no full cold download).
3. **Given** caching is enabled, **When** the locked-mode restore runs, **Then**
   it still enforces the lockfile (caching changes speed, never the enforcement
   or the resolved graph).

---

### User Story 3 - Formatting is enforced, not convention-borne (Priority: P2)

A contributor opens a PR with F# formatted differently from the house style.
Today nothing catches it; the divergence lands and accretes.

After this feature the repo carries a `.editorconfig` and a fantomas
configuration, and a CI gate fails a PR whose tracked source is not
fantomas-clean, pointing at the one command that fixes it. Applying the gate's
formatter to the existing tree is part of this feature and MUST NOT change any
runtime behaviour or golden output.

**Why this priority**: Prevents a whole class of noisy, reviewer-time-wasting
style drift, but no runtime behaviour depends on it.

**Independent Test**: Introduce a deliberately mis-formatted `.fs` change and
confirm the format gate fails; run the documented fix command and confirm the
gate passes; confirm the full test suite (including every golden baseline) stays
green after formatting the existing tree.

**Acceptance Scenarios**:

1. **Given** a `.editorconfig` and a fantomas config in the repo, **When**
   inspected, **Then** they define the F# formatting rules for the tree.
2. **Given** a PR whose tracked source is not fantomas-clean, **When** the format
   gate runs, **Then** it fails and names the command that reformats.
3. **Given** the format gate applied to the existing tree, **When** the suite
   runs, **Then** it is green and every deterministic/golden baseline is
   byte-identical (formatting is layout-only).

---

### User Story 4 - A wider warning ratchet stops new warning classes silently re-accumulating (Priority: P2)

A future change reintroduces a warning class that is not currently promoted.
Today, with only `FS3261;FS0025` (plus canonical `NU1603;NU1608`) promoted and
`TreatWarningsAsErrors=false`, it compiles clean and the warning silently
accumulates.

After this feature the warning ratchet is widened (in the repo-owned
`Directory.Build.local.props`, never the drift-checked canonical
`Directory.Build.props`) so that the near-zero warning state cannot silently
regress. The exact widened set is chosen so the current tree still builds clean.

**Why this priority**: Cheap regression insurance while the count is near zero,
but it is a preventative ratchet, not a fix for a live defect.

**Independent Test**: Confirm the widened ratchet is declared in
`Directory.Build.local.props`; confirm the current tree still builds with zero
promoted-warning errors; confirm a deliberately introduced warning of a
newly-promoted class now fails the build.

**Acceptance Scenarios**:

1. **Given** `Directory.Build.local.props`, **When** inspected, **Then** the
   warning ratchet is widened beyond `FS3261;FS0025` while `NU1603;NU1608` remain
   promoted (appended, never dropped).
2. **Given** the current tree, **When** built, **Then** it compiles with zero
   errors under the widened ratchet.
3. **Given** a deliberately introduced warning of a newly-promoted class,
   **When** built, **Then** the build fails.
4. **Given** the canonical `Directory.Build.props`, **When** inspected, **Then**
   it is unchanged (byte-identical to the org source of truth).

---

### User Story 5 - Locked-restore lives in one place and the release verifies the tool (Priority: P3)

A maintainer editing CI must today update the `Restore (locked)` block in five
places to keep them consistent (one is already divergent), and a release can
publish a packed tool whose runtime closure was never smoke-tested.

After this feature the locked-restore logic lives in a single reusable composite
action consumed by all five jobs (identical error message and regenerate hint in
one place), `scripts/verify-cli-tool.sh` runs as a release gate before the CLI
tool is published, and the packed tool declares a `RollForward` policy.

**Why this priority**: Maintainability and release-safety hardening; the current
duplication and un-wired smoke are latent risks, not active failures.

**Independent Test**: Confirm exactly one composite action defines the locked
restore and all five jobs consume it; confirm `release.yml` runs
`verify-cli-tool.sh` on the packed tool before the publish push; confirm the CLI
fsproj sets `RollForward`.

**Acceptance Scenarios**:

1. **Given** the workflows, **When** inspected, **Then** the locked-restore logic
   is defined once (a composite action) and consumed by all five jobs that need
   it, with a single copy of the error/regenerate message.
2. **Given** `release.yml`, **When** the CLI tool is packed, **Then**
   `scripts/verify-cli-tool.sh` runs against the packed tool and a failure blocks
   the publish push.
3. **Given** the CLI fsproj, **When** inspected, **Then** it declares a
   `RollForward` policy for the packed tool.

---

### Edge Cases

- **Fork PRs**: a fork lacks org-feed tokens. Hermetic sources and the format
  gate MUST still let a fork PR restore, build, and run the offline suite; any
  source requiring a token MUST degrade cleanly on a fork (mirroring the existing
  api-compat gate's fork behaviour).
- **Legitimate dependency change**: when a lockfile genuinely changes, the cache
  key changes and CI performs a fresh cold restore; the locked-mode gate still
  enforces the newly-committed lockfile. Caching MUST NOT mask a real drift.
- **Fantomas reformatting the existing tree**: applying the formatter is
  permitted to touch many files but MUST be layout-only — no golden baseline, no
  deterministic contract, and no runtime behaviour may change.
- **Warning ratchet over-reach**: if a widened warning class would break the
  current clean build, it MUST NOT be promoted in this feature (the ratchet only
  locks in the already-clean state; it does not undertake a warning-fixing
  campaign).
- **Managed-file drift**: none of the changes may edit a managed org file; the
  existing `build-config-drift` gate MUST stay green.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `nuget.config` MUST `<clear/>` inherited package sources and declare
  every required source explicitly so `dotnet restore` is independent of per-machine
  NuGet configuration. **Amended during implementation**: package source mapping is
  *not* used — it is documented-incompatible with `dotnet tool install --add-source`
  (which the release tool-smoke and local-feed scaffold/consumer flows depend on), and
  `<clear/>` alone removes the inherited source that caused the divergence (verified:
  the authentic nuget.org hash is resolved once inherited sources are cleared). Mapping
  would add only marginal insurance at the cost of breaking `--add-source`.
- **FR-002**: After the change, restoring a clean checkout MUST leave every
  committed `packages.lock.json` unmodified, and `dotnet restore --locked-mode`
  MUST succeed against the committed lockfiles.
- **FR-003**: All 11 `packages.lock.json` files MUST continue to agree on a single
  resolved `FSharp.Core` version and content hash after a hermetic restore.
- **FR-004**: Every `actions/setup-dotnet@v4` invocation across `gate.yml`,
  `release.yml`, and `composition-acceptance.yml` MUST enable NuGet caching keyed
  on the committed `packages.lock.json` files.
- **FR-005**: Caching MUST NOT weaken enforcement: the locked-mode restore MUST
  still fail on graph drift or a substituted version (NU1603) exactly as today.
- **FR-006**: The repo MUST carry a `.editorconfig` and a fantomas configuration
  defining the F# formatting rules.
- **FR-007**: A CI gate MUST fail a PR whose tracked source is not fantomas-clean
  and MUST name the command that reformats the tree.
- **FR-008**: The existing tree MUST be reformatted to satisfy the format gate,
  and that reformatting MUST be layout-only — zero change to any deterministic
  contract, golden baseline, or runtime behaviour (the full suite stays green).
- **FR-009**: The warning ratchet in `Directory.Build.local.props` MUST be widened
  beyond `FS3261;FS0025` (appending to, never dropping, the canonical
  `NU1603;NU1608`), chosen so the current tree still builds clean; a warning of a
  newly-promoted class MUST fail the build.
- **FR-010**: No managed org file (`Directory.Build.props`, `Directory.Packages.props`,
  `.config/dotnet-tools.json`) may change; the `build-config-drift` gate MUST stay
  green.
- **FR-011**: The `Restore (locked)` logic MUST be defined once as a reusable
  composite action and consumed by all five jobs that currently duplicate it
  (`gate.yml` `gate`; `release.yml` `contracts-tests`, `cli-tests`,
  `publish-contracts`, `publish-cli`), with a single copy of the
  error/regenerate-command message. Each job's restore scope (solution vs single
  project) is preserved.
- **FR-012**: `release.yml` MUST run `scripts/verify-cli-tool.sh` against the
  packed CLI tool before pushing it to any feed; a smoke failure MUST block the
  publish.
- **FR-013**: The packed CLI tool MUST declare a `RollForward` policy.
- **FR-014**: No `fsgg-sdd` CLI output, JSON automation contract, persisted schema,
  or golden baseline may change as a result of this feature.

### Key Entities

- **`nuget.config`**: Repo-root NuGet configuration; gains `<clear/>`, explicit
  sources, and package source mapping.
- **`packages.lock.json` (×11)**: The committed lockfiles whose hashes must become
  machine-independent and stay unmodified on a clean hermetic restore.
- **`Directory.Build.local.props`**: The repo-owned MSBuild overrides carrying the
  widened warning ratchet (the canonical `Directory.Build.props` stays untouched).
- **`.editorconfig` / fantomas config**: New repo-owned formatting rules driving
  the format gate.
- **Locked-restore composite action**: The single reusable definition of the
  locked restore consumed by all five CI jobs.
- **`scripts/verify-cli-tool.sh`**: The packed-tool self-containment smoke, newly
  wired as a release gate.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A clean checkout restored on a machine with unrelated inherited
  NuGet sources produces **0** modified `packages.lock.json` files (down from 11
  today).
- **SC-002**: `dotnet restore --locked-mode` succeeds on that same machine, and
  all 11 lockfiles agree on one `FSharp.Core` version/hash.
- **SC-003**: **100%** of `setup-dotnet` invocations across the three workflows
  enable lockfile-keyed caching (up from 0); a second unchanged run restores from
  cache.
- **SC-004**: The format gate fails a mis-formatted PR and passes on the
  reformatted tree; the full suite stays green with **0** golden-baseline changes.
- **SC-005**: The current tree builds clean under the widened warning ratchet, and
  a newly-promoted warning class fails the build; `Directory.Build.props` is
  byte-identical to the org source of truth.
- **SC-006**: The locked-restore logic exists in exactly **1** place (down from 5),
  consumed by all five jobs; `release.yml` runs the CLI-tool smoke before publish;
  the packed tool declares `RollForward`.
- **SC-007**: The full test suite is green and `fsgg-sdd validate` reports
  `overallPassed` with **0** changes to any CLI output or JSON contract.

## Assumptions

- **Hermetic sources set (FR-001)**: The explicit source set is the public
  `nuget.org` feed plus the existing repo-local `./.fsgg-local-feed`; the org
  GitHub Packages feed and nuget.org Trusted Publishing are release-time push
  targets, not restore-time sources, so restore stays fork-friendly. The exact
  source-mapping entries are settled during planning against the actual resolved
  graph.
- **FSharp.Core divergence root cause (Overview)**: The review names the missing
  `<clear/>` as the *most plausible* root of the recorded hash divergence. If, in
  practice, the divergence persists after `<clear/>` + source mapping, the
  residual cause is pinned during implementation and the fix extended — the
  success criterion (SC-001) is the observable "0 modified lockfiles", not the
  mechanism.
- **Widened ratchet set (FR-009)**: The specific additional warning IDs are chosen
  during planning as the maximal set the current tree already satisfies; this
  feature does not undertake a warning-fixing campaign (any class that would
  require code changes to go clean is out of scope and left for a follow-up).
- **Fantomas style (FR-006/FR-008)**: Formatting rules follow fantomas defaults
  adjusted only where the existing house style already diverges intentionally;
  the reformatting diff is reviewed to confirm it is layout-only.
- **Composite action scope (FR-011)**: A local repo composite action (under
  `.github/actions/`) is sufficient; no org-level reusable workflow
  (`FS-GG/.github`) is required for this repo-local consolidation. Each job keeps
  its own restore *scope* (solution for `gate`, single project for the four
  release jobs) — the composite parameterises the target, not the enforcement.
- **RollForward policy (FR-013)**: `RollForward=Major` (or the project's standard
  tool policy) is assumed; the exact value is confirmed during planning.
- This is a repo-local change (board item #74); no cross-repo coordination is
  required.
