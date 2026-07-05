# Feature Specification: Composition Smoke — Hyphenated Scaffold Name Builds and Tests Green

**Feature Branch**: `083-scaffold-name-smoke`

**Created**: 2026-07-05

**Status**: Draft

**Input**: FS.GG framework development-feedback report — *Hollow Depths* build (`001-hollow-depths`), 2026-07-05 (§2.1, Appendix B). Epic FS.GG.SDD#148, child #150. Implements feature 080 **FR-011** (the CI smoke deferred when #150 was blocked). Unblocked by FS-GG/FS.GG.Rendering#142 (reference provider now declares `identifierParameter`; `FS.GG.UI.Template 0.1.66-preview.1` live, registry coherence `scaffold-provider-identifier` flipped `true`).

## Overview

Feature 080 (#149) taught `scaffold` to derive a valid F# namespace/identifier from a
product name whose raw form is an illegal F# identifier (a hyphen, a leading digit), while
preserving the raw name in string-literal / path / `.fsproj` / `.slnx` contexts. That
derivation is exercised offline by unit tests, but **nothing in CI proves it end-to-end
against the real provider**: the *Hollow Depths* build scaffolded `Roquelike-DungeonCrawler`
(a legal product name, an illegal F# identifier — and misspelled) and the generated code did
not compile (`FS0010: Unexpected keyword 'open' in implementation file` — a hyphen templated
verbatim into a `namespace`/`module`/`open`). The defect reached an author because no smoke
test asserts a freshly scaffolded product with such a name compiles.

The existing network-gated composition acceptance (feature 034/035/038/050) already scaffolds
the real provider once, builds it, and starts it — but it scaffolds into a **sensed temp
directory** whose name never exercises the illegal-identifier path, and it never runs the
product's own **test** suite. So the one lane that drives the real provider does not guard C1
(the 080 sanitization) from regressing.

This feature closes that gap: extend the composition-acceptance lane with a fact that
scaffolds a product **whose name is a legal product name but an illegal F# identifier**
(hyphenated, matching the report's `Roquelike-DungeonCrawler` shape) and asserts the produced
product's `dotnet build` **and** `dotnet test` are green. It stays inside the existing opt-in,
network-gated, provider-neutral lane — no rendering identity enters generic SDD, and the
offline inner loop stays green via the same discovery-time skip.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - CI catches an illegal-identifier scaffold name that won't compile (Priority: P1)

As an FS.GG maintainer, I need CI to fail when a product scaffolded under a name that is a
legal product name but an illegal F# identifier does not compile — before that defect reaches
a product author — so the 080 name→identifier sanitization cannot silently regress.

**Why this priority**: This is the whole point of #150 and the root-cause guard for epic #148.
Without it, C1 has no regression fence and the *Hollow Depths* footgun can return unnoticed.

**Independent Test**: On a run with the acceptance registry configured, scaffold the real
provider into a product root whose base name is a hyphenated/misspelled identifier
(`Roquelike-DungeonCrawler`), then run the product's `dotnet build` and `dotnet test`; assert
both succeed. Deliberately reverting the 080 derivation turns this fact red.

**Acceptance Scenarios**:

1. **Given** the acceptance registry resolves the real published provider, **When** the smoke
   scaffolds a product whose name is `Roquelike-DungeonCrawler` (a legal product name, an
   illegal F# identifier) and runs the produced product's build then tests, **Then** both
   `dotnet build` and `dotnet test` exit 0 and the composition verdict is `pass`.
2. **Given** the same run, **When** the produced sources are inspected, **Then** the raw
   hyphenated name appears only in string-literal / path / `.fsproj` / `.slnx` contexts and the
   derived valid identifier appears in `namespace`/`module`/`open` positions (no `FS0010`
   hyphen-in-namespace failure).
3. **Given** the 080 identifier derivation is reverted (raw name templated into identifiers),
   **When** the smoke runs, **Then** `dotnet build` fails and the composition verdict is a
   `fail` naming the failing build fact — the regression is caught.

### User Story 2 - The smoke stays inside the opt-in, provider-neutral acceptance lane (Priority: P2)

As a contributor running the default offline inner loop, I need this new smoke to be gated
exactly like the existing composition acceptance — opt-in, network-gated, carrying no rendering
identity — so it never slows or breaks the offline `dotnet test` and never leaks a
provider-specific token into generic SDD.

**Why this priority**: The composition-acceptance lane's contract (FR-009/FR-010) is
load-bearing; a new fact that violated gating or embedded provider identity would regress the
architecture, not just miss a case.

**Independent Test**: With `FSGG_SDD_ACCEPTANCE_REGISTRY` unset, the default `dotnet test`
reports the new fact as Skipped and touches no network; the acceptance project and its
non-guard sources contain no rendering package id / template id / path / docs URL.

**Acceptance Scenarios**:

1. **Given** `FSGG_SDD_ACCEPTANCE_REGISTRY` is unset, **When** the default offline `dotnet
   test` runs, **Then** the new smoke fact is reported Skipped (discovery-time static skip) and
   no result document is written.
2. **Given** the repository sources, **When** the acceptance project is scanned, **Then** the
   hyphenated product name is a generic author-chosen token only and no rendering
   package/template/path/docs identity is present in generic SDD.

### Edge Cases

- **Provider unreachable / registry offline**: the smoke resolves to the honest
  `skip-unavailable` verdict (never a false `pass`, never a `fail` of SDD) — the same posture
  as the existing end-to-end fact.
- **A name already valid as an F# identifier**: out of scope for this fact; the existing
  temp-dir composition fact already covers the byte-identical valid-name path. This fact adds
  the illegal-identifier case, not a second valid-name case.
- **`dotnet test` present but empty in the produced product**: an empty-but-green test run
  (exit 0, zero tests) still satisfies the fact — the fact proves the product's test project
  compiles and runs, not a minimum test count.
- **Build passes but tests fail**: the fact fails naming the failing test step — build-green
  alone does not satisfy it.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The composition-acceptance lane MUST include a fact that scaffolds the real
  provider forwarding, on the **provider-declared name parameter** (`nameParameter`, resolved
  from the registry descriptor — not hardcoded), a value that is a **legal product name but an
  illegal F# identifier** (hyphenated), using the report's `Roquelike-DungeonCrawler` shape.
- **FR-002**: The fact MUST assert the produced product's `dotnet build` **and** `dotnet test`
  both succeed (exit 0); either failing yields a `fail` verdict naming the failing step.
- **FR-003**: A `pass` verdict MUST additionally imply the produced sources place the raw name
  only in string-literal / path / `.fsproj` / `.slnx` contexts and the derived valid identifier
  in `namespace`/`module`/`open` positions (no `FS0010` hyphen-in-namespace failure).
- **FR-004**: The fact MUST be gated identically to the existing composition acceptance:
  discovery-time static skip when `FSGG_SDD_ACCEPTANCE_REGISTRY` is unset/empty, so the default
  offline inner loop stays green and touches no network.
- **FR-005**: The fact MUST resolve an unreachable/unavailable provider to `skip-unavailable`
  (never a false `pass`, never a `fail` of SDD), consistent with the existing verdict mapping.
- **FR-006**: Generic SDD (the acceptance project and its non-guard sources) MUST carry no
  rendering package id / template id / path / docs URL; the hyphenated product name is a generic
  author-chosen token and the real provider identity is reached only through the external
  registry.
- **FR-007**: The existing `composition-acceptance` CI workflow MUST exercise the new fact
  without a new job — it runs under the same `--filter "kind=composition-acceptance"` selection
  on schedule / dispatch / manual, with the same fail-closed vs neutral-skip preflight.
- **FR-008**: The new fact MUST NOT change the offline determinism/golden contracts of the
  existing `composition-acceptance-result` v1 document (it is an additional gated fact, not a
  schema change).

### Key Entities

- **Hyphenated product name**: a scaffold input that is a legal product name but an illegal F#
  identifier (contains a hyphen), used to drive the 080 derivation end-to-end. Represented by
  the report's `Roquelike-DungeonCrawler`.
- **Composition-acceptance fact**: a network-gated xUnit fact in the acceptance lane
  (`[<Trait("kind","composition-acceptance")>]`) that scaffolds, builds, tests, and resolves a
  verdict.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A scaffold under `Roquelike-DungeonCrawler` against the real provider produces a
  product whose `dotnet build` and `dotnet test` both exit 0 on the acceptance run.
- **SC-002**: Reverting the 080 identifier derivation makes the new fact fail on the next
  acceptance run (the guard demonstrably fences C1).
- **SC-003**: The default offline `dotnet test` (registry unset) reports the new fact Skipped,
  adds no measurable wall-clock beyond discovery, and touches no network.
- **SC-004**: A scan of the acceptance project and its non-guard sources finds zero rendering
  package/template/path/docs tokens.
- **SC-005**: FS.GG.SDD#150 turns green on its next scheduled/dispatched composition-acceptance
  run.

## Assumptions

- The reference provider's released template (`identifierParameter` adoption, Rendering#142) is
  live and resolvable through the acceptance registry — the precondition that unblocked this
  work.
- The product name reaches scaffold as a forwarded `--param` keyed by the **provider-declared
  `nameParameter`** (feature 080 FR-004; the reference provider declares `productName`). The
  fact resolves that key from the registry descriptor and forwards the hyphenated value, so the
  parameter **key** stays provider-owned (no hardcoded `productName` in generic SDD) while only
  the **value** `Roquelike-DungeonCrawler` is a generic author-chosen token (preserves FR-006).
- The produced product exposes a `dotnet test`-runnable test project (the reference provider's
  template ships one); an empty-but-green test run still satisfies FR-002.
- This feature adds a gated fact only; it introduces no new persisted schema and no new CI job,
  reusing the existing `composition-acceptance` workflow and the `composition-acceptance-result`
  v1 document.
