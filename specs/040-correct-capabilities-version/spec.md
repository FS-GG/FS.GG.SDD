# Feature Specification: Correct capabilities schema version to 2 and republish FS.GG.Contracts 1.0.1

**Feature Branch**: `040-correct-capabilities-version`

**Created**: 2026-06-28

**Status**: Draft

**Input**: User description: "next non blocked sdd item on the project coordination board" — resolved to FS-GG/FS.GG.SDD#18 (status *Ready*, non-blocked; upstream prerequisite that *blocks* FS-GG/FS.GG.Governance#14).

## Overview

`FS.GG.Contracts` is the org-shared, SDD-owned typed source of truth for every
FS.GG `.fsgg` schema version. Its `Schemas.capabilitiesVersion` declares the
schema version that the Governance published reference uses for `capabilities`
configuration files. That declared value is currently **1**, but the Governance
validator ships — and must keep — **`capabilities` = 2**. The provisional
upstream constant is simply wrong.

Because Governance has decided that `FS.GG.Contracts` is the authoritative
single source for these constants (the value is reconciled upstream, never
overridden locally in Governance), FS.GG.SDD must correct the constant and
republish the package so downstream consumers can re-type onto it with zero
local literals and zero behaviour change. The other three Governance-owned
declared constants (`governance`, `policy`, `tooling` = 1) are already correct
and stay unchanged.

This is a cross-repo contract correction: the latent-drift case where a shared
constant disagrees with the current local value, resolved by fixing the shared
constant upstream rather than masking it downstream.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Declared capabilities version matches the Governance reference (Priority: P1)

As the owner of `FS.GG.Contracts`, the declared `capabilities` schema version
must equal the value the Governance validator actually supports (2), so that the
package is a truthful single source of truth and any consumer that re-types onto
it preserves which `capabilities.yml` configs validate.

**Why this priority**: This is the substance of the correction and the
prerequisite the downstream Governance re-type (FS.GG.Governance#14) is blocked
on. Without it the package mis-declares the contract and consuming it would
change validation behaviour (a v2 file would be rejected as unsupported).

**Independent Test**: Inspect the package's declared `capabilities` schema
version and confirm it equals 2; confirm the three other Governance-owned
declared constants remain 1; confirm the package's own version-constant
verification asserts 2 and passes, grounded against the Governance
published-reference value.

**Acceptance Scenarios**:

1. **Given** the corrected package, **When** a consumer reads the declared
   `capabilities` schema version, **Then** it is 2.
2. **Given** the corrected package, **When** a consumer reads the declared
   `governance`, `policy`, and `tooling` schema versions, **Then** each is still
   1 (unchanged).
3. **Given** the package's version-constant verification suite, **When** it runs,
   **Then** it asserts the declared `capabilities` version is 2 and the full
   suite passes.

---

### User Story 2 - Corrected package is republished as 1.0.1 on the shared feed (Priority: P1)

As a downstream consumer (Governance, Templates, Rendering), I must be able to
acquire a published `FS.GG.Contracts` `1.0.1` that carries the corrected
`capabilities` = 2 value, so I can pin to it instead of consuming the incoherent
`1.0.0`.

**Why this priority**: A corrected-but-unpublished constant cannot unblock the
downstream re-type. The package version is bumped (not replaced in place) so
`1.0.0` and `1.0.1` remain distinct, immutable identities and consumers move
forward by an explicit pin change.

**Independent Test**: Resolve `FS.GG.Contracts` `1.0.1` from the shared local
folder feed and confirm it carries `capabilities` = 2; confirm `1.0.0` is not
mutated in place.

**Acceptance Scenarios**:

1. **Given** the corrected package version is bumped to `1.0.1`, **When** it is
   packed and published to the shared local folder feed, **Then** `1.0.1`
   resolves from that feed and carries `capabilities` = 2.
2. **Given** the previously published `1.0.0`, **When** `1.0.1` is published,
   **Then** `1.0.0` remains unchanged (no in-place mutation).

---

### User Story 3 - Org dependency registry pin reflects the corrected package (Priority: P2)

As the `fsgg-contracts` contract owner (`owner: sdd`) on the org dependency
registry, the registered pin must advance from `1.0.0` to `1.0.1` so the org's
contract-coherence checks describe the package consumers should adopt, and so
the registry stays coherent and its coherence workflow stays green.

**Why this priority**: Keeps the cross-repo registry truthful and unblocks
consumers that follow the registry pin, but is a follow-on bookkeeping step
after the package itself is corrected and published (P1).

**Independent Test**: Read the `fsgg-contracts` entry in the org registry and
confirm the pin is `1.0.1`; confirm the contract-coherence workflow passes.

**Acceptance Scenarios**:

1. **Given** the org registry pins `fsgg-contracts` at `1.0.0`, **When** the pin
   is advanced, **Then** the registered version is `1.0.1`.
2. **Given** the advanced pin, **When** the contract-coherence check runs,
   **Then** it passes (the registry is coherent with the published package).

---

### Edge Cases

- **Only `capabilities` changes**: the three sibling Governance-owned constants
  (`governance`, `policy`, `tooling`) and all SDD-owned constants must stay at
  their current values; the correction must not perturb them.
- **No emitted-output coupling**: the Governance-owned constants are *declared
  reference* values, never values SDD emits. Correcting `capabilities` must not
  be asserted against any SDD output and must not change any SDD-emitted
  artifact's schema version.
- **Behaviour-change bar for consumers**: the correction exists precisely so the
  downstream re-type is a no-behaviour-change move; the package change itself
  alters only a declared constant and the package version, not SDD runtime
  behaviour.
- **Stale `1.0.0` consumers**: existing pins to `1.0.0` continue to resolve the
  old (incoherent) package until they explicitly move to `1.0.1`; the bump does
  not retroactively fix them.
- **org GitHub Packages feed deferral**: the org GitHub Packages publishing path
  (the H4 producer step) stays deferred for this correction; `1.0.1` is
  published to the shared local folder feed only.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The package MUST declare the `capabilities` schema version as **2**
  (corrected from 1), grounded against the Governance published-reference value.
- **FR-002**: The package MUST keep the `governance`, `policy`, and `tooling`
  declared schema versions at **1** (unchanged).
- **FR-003**: The package's version-constant verification MUST assert the
  declared `capabilities` version is **2** and the full verification suite MUST
  pass.
- **FR-004**: The package version MUST be bumped from `1.0.0` to **`1.0.1`**
  (a new immutable identity, not an in-place replacement of `1.0.0`).
- **FR-005**: The corrected `1.0.1` package MUST be published to the shared local
  folder feed so downstream consumers can resolve it.
- **FR-006**: The org dependency registry `fsgg-contracts` pin MUST advance from
  `1.0.0` to **`1.0.1`** (performed by the `owner: sdd`), keeping the
  contract-coherence check green.
- **FR-007**: The correction MUST NOT change any SDD-emitted artifact's schema
  version, any SDD runtime behaviour, or any SDD-owned declared constant.
- **FR-008**: The org GitHub Packages publishing path MUST remain out of scope
  for this correction (deferred); `1.0.1` is delivered via the shared local
  folder feed only.

### Key Entities *(include if feature involves data)*

- **Declared schema-version constant**: a Governance-owned value the package
  *declares* to the Governance published reference (not a value SDD emits). The
  set is `governance`, `policy`, `capabilities`, `tooling`; only `capabilities`
  changes (1 → 2).
- **FS.GG.Contracts package version**: the NuGet identity/version of the shared
  contract package, advancing `1.0.0` → `1.0.1`.
- **Org dependency registry pin (`fsgg-contracts`)**: the cross-repo registered
  version of the contract, owned by `sdd`, advancing `1.0.0` → `1.0.1`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The declared `capabilities` schema version reads as 2 and the three
  sibling Governance-owned constants read as 1, with 100% of the package's
  version-constant verification passing.
- **SC-002**: `FS.GG.Contracts` `1.0.1` resolves from the shared local folder
  feed and carries `capabilities` = 2, while `1.0.0` remains unchanged.
- **SC-003**: The org registry `fsgg-contracts` pin reads `1.0.1` and the
  contract-coherence check passes.
- **SC-004**: Zero change to SDD-emitted artifact schema versions and zero change
  to SDD runtime behaviour as a result of this correction (the downstream
  re-type is enabled as a no-behaviour-change move).
- **SC-005**: The downstream prerequisite is satisfied — a consumer can pin
  `1.0.1` and obtain `capabilities` = 2 single-sourced from the package with no
  local literal.

## Assumptions

- **`FS.GG.Contracts` is authoritative** for the Governance-owned declared
  schema constants; the reconciliation happens upstream in the package, and
  Governance does not override the value locally (per the Governance-side
  decision recorded 2026-06-28).
- **Republish, don't replace**: the corrected package is shipped as a new
  version `1.0.1`; `1.0.0` is never mutated in place.
- **Shared local folder feed is the delivery channel** for `1.0.1`; the org
  GitHub Packages feed publishing path stays deferred for this item.
- **The org dependency registry lives in the `FS-GG/.github` repository** and
  SDD is the `owner: sdd` permitted to advance the `fsgg-contracts` pin; the
  contract-coherence workflow there must stay green.
- **Only the `capabilities` constant is wrong**; the other three
  Governance-owned constants and all SDD-owned constants are already correct.
- **This is the upstream prerequisite** for FS.GG.Governance#14; landing it
  unblocks the Governance re-type but the Governance change itself is out of
  scope for this feature.
