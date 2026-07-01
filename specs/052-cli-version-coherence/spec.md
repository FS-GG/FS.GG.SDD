# Feature Specification: CLI Version Coherence in Scaffold Provenance

**Feature Branch**: `052-cli-version-coherence`

**Created**: 2026-07-01

**Status**: Draft

**Input**: User description: "start the next sdd owned item on the coordination board." → resolved to FS.GG.SDD#49 (sub-issue of FS-GG/.github#85): record the `fsgg-sdd` CLI version in scaffold provenance and warn when the installed CLI is behind the pin's required minimum.

## Overview

A scaffolded product is produced by **two** independent inputs: the template pin
(`fs-gg-ui-template@<version>`, pin-controlled and covered by the coherent-set
guarantee) and the **`fsgg-sdd` CLI** that orchestrates the scaffold (until now,
un-pinned and un-recorded). CLI-sourced artifacts — the 15 seeded `fs-gg-sdd-*`
process skills and `.fsgg/early-stage-guidance.md` — are therefore **not** governed
by any pin. A product on the newest template pin, scaffolded by an old CLI, silently
lacks those artifacts and nothing detects the gap: reading the newest pin actively
*masks* the stale-CLI hole.

This feature closes the SDD-owned part of that gap. It makes the CLI a first-class,
auditable input to a scaffolded product by (1) recording the CLI version used into the
scaffold provenance record, alongside the required minimum declared by the selected
provider, and (2) emitting a clear, non-blocking advisory when the installed CLI is
behind that minimum, pointing the author at the re-seed path.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Provenance records which CLI produced the scaffold (Priority: P1)

As a product author (or an auditor reading a scaffolded product later), I can open the
scaffold provenance record and see exactly which `fsgg-sdd` CLI version produced the
product, and what minimum CLI version the selected provider considered coherent — so the
CLI input is as auditable and reproducible as the template pin already is.

**Why this priority**: This is the foundational, always-correct fact. The provenance record
already captures the producing CLI version (as the recorded `generator` version); the missing
half is the **required minimum** recorded next to it. Without both facts side by side, no
staleness can be detected, no audit can attribute a missing-skills gap to an old CLI, and the
coherent-set guarantee keeps its hole. It delivers standalone value even before any warning
exists: a reader can compare the two recorded values by hand.

**Independent Test**: Run `fsgg-sdd scaffold` with a provider and inspect the produced
`.fsgg/scaffold-provenance.json`. It records the CLI version used and the provider-declared
required-minimum CLI version. Verifiable without any warning behavior.

**Acceptance Scenarios**:

1. **Given** a provider whose registry entry declares a minimum coherent `fsgg-sdd`
   version, **When** an author runs `fsgg-sdd scaffold --provider <name>`, **Then** the
   produced provenance record contains both the CLI version that ran the scaffold and the
   provider-declared required minimum, in a stable, machine-readable form.
2. **Given** a provider whose registry entry declares **no** minimum, **When** the author
   scaffolds, **Then** the provenance still records the CLI version used, and the
   required-minimum field is recorded as absent/unset rather than fabricated.
3. **Given** the same CLI, provider, and inputs, **When** the author scaffolds twice,
   **Then** the recorded CLI-version and required-minimum values are byte-identical
   (deterministic; the JSON automation contract is unchanged in shape aside from the
   additive fields).

---

### User Story 2 - Author is warned when the installed CLI is behind the pin minimum (Priority: P1)

As a product author, when I scaffold with a `fsgg-sdd` CLI that is older than the minimum
the selected provider declares coherent, I get a clear, non-blocking advisory telling me my
CLI is behind and what that costs me (missing seeded skills / early-stage guidance), so I am
not silently handed an incomplete product.

**Why this priority**: The recorded facts (US1) are only actionable if the author is told at
scaffold time. This turns an invisible, mask-by-newest-pin failure into a visible signal at
the moment of production. It is the observable outcome the board item promises.

**Independent Test**: Scaffold with a CLI whose version is below the provider-declared minimum
and confirm a `scaffold.*` advisory is emitted naming the installed version, the required
minimum, and the remedy — while the scaffold still completes successfully (exit 0).

**Acceptance Scenarios**:

1. **Given** an installed CLI older than the provider-declared minimum, **When** the author
   scaffolds, **Then** a clear advisory is emitted stating the installed version, the required
   minimum, and how far behind it is, and the advisory appears in all three report projections
   (json/text/rich).
2. **Given** an installed CLI at or above the provider-declared minimum, **When** the author
   scaffolds, **Then** no CLI-staleness advisory is emitted.
3. **Given** the CLI is behind the minimum, **When** the scaffold otherwise succeeds, **Then**
   the advisory is **non-blocking**: the scaffold completes and reports success, and the exit
   code is unchanged from the equivalent up-to-date-CLI run (the advisory does not reclassify a
   complete scaffold as failed).
4. **Given** the provider declares no minimum, **When** the author scaffolds, **Then** no
   staleness advisory is emitted (nothing to compare against).

---

### User Story 3 - Author of an existing scaffold learns the re-seed path (Priority: P2)

As an author whose product was scaffolded by an old CLI (and therefore lacks the seeded
`fs-gg-sdd-*` skills and `.fsgg/early-stage-guidance.md`), I am pointed at the supported way to
bring an existing scaffold back to currency, so I can recover without re-scaffolding from
scratch.

**Why this priority**: Detection (US2) without a documented remedy leaves the author stuck. This
is the guidance half. It is P2 because it is a pointer/documentation obligation rather than the
core recorded-fact-plus-warning behavior, and it depends on the re-seed path already existing.

**Independent Test**: Follow the advisory's next-action pointer and the referenced documentation
and confirm it names the re-seed command path (`fsgg-sdd init` / the seeding effects — **not**
`refresh`, which does not re-seed) that re-materializes the seeded skills and early-stage guidance
into an existing scaffold.

**Acceptance Scenarios**:

1. **Given** a stale-CLI advisory was emitted, **When** the author reads its next-action
   pointer, **Then** it names the re-seed path an author uses to gain the seeded `fs-gg-sdd-*`
   skills and `.fsgg/early-stage-guidance.md` in an existing scaffold.
2. **Given** the feature documentation, **When** an author looks for how to remediate a
   behind-minimum CLI, **Then** the re-seed path is documented as the supported remedy.

---

### Edge Cases

- **Provider declares no minimum**: record CLI version, leave required-minimum unset, emit no
  staleness advisory (US1-2, US2-4).
- **CLI version exactly equal to the minimum**: treated as coherent — no advisory (boundary is
  "behind", not "at or below").
- **Malformed / unparseable minimum in the provider registry**: this is provider/registry input,
  not author input. The scaffold must not silently drop the coherence check; it surfaces the
  malformed-minimum condition rather than treating it as "no minimum". (Exact classification is a
  planning decision; the requirement is that a malformed minimum is not silently ignored.)
- **CLI version cannot be determined at runtime**: the provenance CLI-version field must not be
  fabricated; the record and any comparison must degrade honestly (record it as unknown and skip
  the comparison rather than assert a false version).
- **Pre-existing provenance consumers**: adding fields is additive; existing readers that ignore
  unknown fields must continue to parse the record (schema stays v1-compatible / additive).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The `.fsgg/scaffold-provenance.json` record MUST identify the `fsgg-sdd` CLI version
  that produced the product (today captured as the recorded `generator` version); the feature treats
  that as the authoritative "CLI version used" and MUST NOT drop or duplicate it inconsistently.
- **FR-002**: On `scaffold`, the system MUST record the selected provider's declared minimum
  coherent `fsgg-sdd` version into the provenance record when the provider declares one, and MUST
  record it as absent/unset (not fabricated) when the provider declares none.
- **FR-003**: The provenance change MUST be additive and MUST NOT break existing `scaffold-provenance`
  consumers; the schema version MUST be handled per the contract's versioning rules (a `contract-change`
  coordinated with the registry, per epic FS-GG/.github#85), and the additive fields MUST be
  serialized deterministically like the rest of the record.
- **FR-004**: When the installed CLI version is **behind** the provider-declared minimum, the system
  MUST emit a `scaffold.*` advisory naming the installed version, the required minimum, and the amount
  behind.
- **FR-005**: The CLI-staleness advisory MUST be **non-blocking**: it MUST NOT change the scaffold's
  success classification or exit code relative to an equivalent up-to-date-CLI run, and an incomplete
  scaffold MUST still never be reported as complete (unchanged from today).
- **FR-006**: The system MUST NOT emit a CLI-staleness advisory when the installed CLI is at or above
  the declared minimum, or when the provider declares no minimum.
- **FR-007**: The CLI-staleness advisory and the two new provenance facts MUST appear in all three
  report projections (`--json`/default, `--text`, `--rich`) as pure projections over the same
  `CommandReport` — adding/dropping no facts across projections and changing no JSON byte for the
  non-advisory path beyond the additive fields.
- **FR-008**: The advisory MUST carry a next-action pointer to the supported re-seed path
  (`fsgg-sdd init` / the reused seeding effects — **not** `fsgg-sdd refresh`, which does not
  re-seed) by which an existing scaffold gains the seeded `fs-gg-sdd-*` skills and
  `.fsgg/early-stage-guidance.md`.
- **FR-009**: The system MUST NOT embed any provider-specific package id, template id, path, or
  version literal in generic SDD; the required minimum MUST be read value-agnostically from the
  provider registry / provider contract (consistent with the scaffold contract's FR-002/SC-005).
- **FR-010**: The behavior MUST be documented — the additive provenance fields recorded in the
  contract/schema reference, and the re-seed remedy documented for authors.

### Key Entities *(include if feature involves data)*

- **Scaffold provenance record** (`.fsgg/scaffold-provenance.json`): the SDD-owned, versioned
  record (schema stability `additiveOptional`) of how a product was scaffolded. Already records the
  producing **CLI version** as the `generator` version; gains the additive **required-minimum CLI
  version** declared by the selected provider so both facts sit side by side for audit.
- **Provider registry entry** (`.fsgg/providers.yml`): author-/provider-owned registry the scaffold
  resolves `--provider` against. Source of the **minimum coherent `fsgg-sdd` version** (declared by
  the Templates-owned sibling work under epic #85). SDD reads it value-agnostically; it never embeds
  a provider-specific value.
- **CLI-staleness advisory** (`scaffold.*` diagnostic): a non-blocking advisory on the scaffold
  `CommandReport` describing installed-vs-required CLI versions and pointing at the re-seed remedy.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Given a provider that declares a minimum, 100% of `scaffold` runs record both the CLI
  version used and the required minimum in the provenance record.
- **SC-002**: Scaffolding with a CLI behind the declared minimum emits exactly one clear advisory that
  states the installed version, the required minimum, and the amount behind — verifiable in all three
  projections.
- **SC-003**: Scaffolding with a CLI at or above the minimum (or with a provider that declares no
  minimum) emits zero CLI-staleness advisories.
- **SC-004**: A stale-CLI scaffold's exit code and success classification are identical to the
  equivalent up-to-date-CLI run (the advisory is provably non-blocking).
- **SC-005**: No provider-specific package id, template id, path, or version literal appears in generic
  SDD source as a result of this feature (grep-verifiable).
- **SC-006**: An author following the advisory's next-action pointer reaches a documented re-seed path
  (`fsgg-sdd init`, not `refresh`) that restores the seeded `fs-gg-sdd-*` skills and
  `.fsgg/early-stage-guidance.md` in an existing scaffold.

## Assumptions

- The **provider-declared minimum** is delivered by the sibling epic-#85 work (Templates records the
  minimum coherent `fsgg-sdd` version in `providers/rendering.providers.yml`; registry adds the CLI
  dimension). This SDD feature **reads** that minimum value-agnostically and does not itself define any
  concrete minimum value. Where the minimum is absent (provider not yet updated), the feature degrades to
  "record CLI version, no comparison, no advisory" — so it is independently shippable ahead of the
  Templates/registry halves.
- The re-seed path already exists: `fsgg-sdd init` / the reused seeding effects re-materialize the
  seeded `fs-gg-sdd-*` skills and `.fsgg/early-stage-guidance.md` into an existing scaffold
  (idempotent / no-clobber). `fsgg-sdd refresh` does **not** re-seed. This feature documents and
  points at the `init` re-seed path; it does not build a new remediation command.
- "Version" comparison uses the same version grammar the registry/provider contract already uses; this
  feature does not introduce a new version format.
- The change is a `contract-change` on `scaffold-provenance` — an additive-optional field. The
  provenance **schema version field stays v1** (existing readers ignore the new field); the
  contract change is carried as a **minor package bump**, keeping backward compatibility and
  coordinated with the registry per the cross-repo protocol and epic FS-GG/.github#85.
- The producing CLI version is already recorded (the `generator` version, sourced from the assembly
  informational version); this feature does not re-derive it, only adds the required-minimum alongside
  it and the comparison between them.
- Warn-not-fail is the chosen default (the board item says "warn (or fail)"); a behind-minimum CLI is an
  advisory, not a hard error, consistent with SDD's report-readiness-not-enforce doctrine. Escalation to a
  hard failure, if ever wanted, is out of scope for this feature.
- Scaffold's existing determinism, three-projection, and "never report an incomplete scaffold as complete"
  guarantees are preserved unchanged.

## Out of Scope

- Defining or publishing the concrete minimum coherent `fsgg-sdd` version (Templates/registry, epic #85
  sub-issues FS-GG/.github#86/#87 and FS.GG.Templates#43).
- Writing the ADR or the registry CLI-dimension change (epic-#85 `.github` sub-issues).
- Any Governance-side enforcement of CLI coherence (SDD reports; Governance may enforce downstream, out of
  scope here).
- Building a new remediation/auto-upgrade command; the remedy is the existing re-seed path.
