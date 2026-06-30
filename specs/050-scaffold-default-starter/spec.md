# Feature Specification: Honor Provider-Declared Default Starter Selection in Scaffold

**Feature Branch**: `050-scaffold-default-starter`

**Created**: 2026-06-30

**Status**: Draft

**Input**: User description: "start the next sdd owned item on the coordination board." — resolved to the next (and only) non-Done SDD-owned item on the FS-GG `Coordination` board (Projects v2 #1): **FS-GG/FS.GG.SDD#44** (`[cross-repo] Enumerate the new fs-gg-ui game profile and flip the game/rendering default app→game`), unblocked on 2026-06-30 when its blocker FS-GG/FS.GG.Rendering#33 closed (the `fs-gg-ui-template` `game` profile released to the org feed at `0.1.54-preview.1`; registry flipped in FS-GG/.github#78).

## Overview

Issue #44 asks SDD, as the owner of scaffold default-selection, to (1) "enumerate
the `game` profile" and (2) "flip the game/rendering default starter from `app` to
`game`." Both `game` and `app` are **provider-specific values** that generic SDD is
constitutionally forbidden to carry (FR-002 / SC-005 of feature 030, grep-enforced):
the canonical rendering provider registry (`providers/rendering.providers.yml`) is
**owned by FS.GG.Templates**, and SDD consumes it only through the versioned
provider contract and the network-gated composition-acceptance dispatch (feature
041).

In the SDD provider contract there is no first-class "profile" concept: a starter
selection is simply a provider-declared scaffold parameter (forwarded to the
external template as `--<key> <value>`), and the **default starter** is that
parameter's declared `default`. The mechanism for honoring such a default already
exists in code — the registry parser reads each parameter's `default`, scaffold
applies declared defaults under any author `--param` overrides, and forwards the
effective value verbatim to the provider. The literal `app → game` flip is
therefore a **data edit in the Templates-owned registry**, not a change to generic
SDD.

This feature is SDD's in-boundary half of #44. It does **not** introduce `game`,
`app`, or any rendering value into generic SDD. Instead it **locks and proves the
generic default-starter-selection capability** the flip depends on — so that when the
Templates-owned registry declares a different default starter, `fsgg-sdd scaffold`
provably honors it (default applied when the author omits the parameter; explicit
`--param` always wins; the effective value recorded in scaffold provenance) — and it
proves the capability end-to-end against the real published provider at
`0.1.54-preview.1` through the existing composition-acceptance. The literal data flip
is redirected to FS.GG.Templates via a cross-repo response on #44.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Author gets the provider's default starter without naming it (Priority: P1)

A product author scaffolds a runnable product by naming only the provider (and any
required parameters), without choosing a starter. The provider's registry declares a
default starter; scaffold supplies that declared default to the provider so the
author lands on the provider's intended default product with no extra flags.

**Why this priority**: This is the capability #44 depends on. If a declared default
is not honored, a Templates-side `default: game` edit has no effect and the
roadmap item cannot be satisfied in-boundary.

**Independent Test**: Using a generic test provider whose registry declares a
non-required parameter with a `default`, run scaffold without supplying that
parameter; assert the provider is invoked with the declared default value and the
scaffold report/provenance records it as the effective value. No rendering values
involved.

**Acceptance Scenarios**:

1. **Given** a registered provider whose registry declares a non-required parameter
   with a declared `default`, **When** the author runs scaffold naming that provider
   and omitting that parameter, **Then** the provider is invoked with the declared
   default value for that parameter.
2. **Given** the same scaffold, **When** it completes, **Then** the scaffold
   provenance/report records the effective value used (the declared default), so the
   produced product is reproducible.
3. **Given** a provider registry whose declared default is later changed (a
   data-only edit to the registry), **When** the author re-runs the same scaffold
   command unchanged, **Then** the new default value is the one forwarded to the
   provider, with no change to generic SDD code.

---

### User Story 2 - Author overrides the default starter explicitly (Priority: P1)

An author who wants a non-default starter passes it explicitly. The explicit choice
always wins over the provider's declared default.

**Why this priority**: Flipping the *default* must never remove the author's ability
to select the previous starter. #44 guarantees the old starter remains explicitly
selectable; SDD must guarantee the override path that makes that true.

**Independent Test**: With the same default-declaring provider, run scaffold passing
`--param <key>=<other>`; assert the provider is invoked with `<other>`, not the
declared default.

**Acceptance Scenarios**:

1. **Given** a provider with a declared default for a parameter, **When** the author
   passes `--param <key>=<value>` for that parameter, **Then** the provider is
   invoked with the author's value and the declared default is not applied.
2. **Given** an explicit override, **When** scaffold completes, **Then** provenance
   records the overriding value (not the default) as effective.

---

### User Story 3 - Default starter is proven against the real provider (Priority: P2)

A maintainer needs assurance that the default-starter capability works against the
real published rendering provider — that scaffolding with no explicit starter yields
a buildable product that passes governance with zero hand edits — before the
Templates-owned registry's default flip is trusted in production.

**Why this priority**: The generic mechanism (Stories 1–2) is necessary but
value-agnostic; #44's real-world claim ("generated default product builds + passes
governance 26/26 zero-edit") can only be observed against the real provider. This is
verification, gated to the opt-in/scheduled lane, so it does not slow the offline
inner loop.

**Independent Test**: With the composition-acceptance pointed at the real
`0.1.54-preview.1` rendering registry (network-gated), run the fixed composition
scaffold (no explicit starter parameter) and assert the produced product builds and
the composition verdict is GREEN — exercising whatever default starter the
Templates-owned registry declares, by reference, never by name.

**Acceptance Scenarios**:

1. **Given** the composition-acceptance pointed at the real published rendering
   registry at `0.1.54-preview.1` and the gating environment present, **When** the
   fixed composition scaffold runs with no explicit starter parameter, **Then** the
   produced product builds and the composition-acceptance verdict is GREEN.
2. **Given** the gating environment is absent (the default offline inner loop),
   **When** the test suite runs, **Then** the real-provider acceptance is reported
   Skipped and the suite stays green without touching the network.

---

### Edge Cases

- **Required parameter with a declared default**: a declared default does not make a
  required parameter optional — if the contract treats the parameter as required, an
  omitted value must still surface the existing missing-required-parameter diagnostic
  rather than silently substituting the default. (Document the precedence; do not
  change required-parameter semantics.)
- **Provider declares no default for a parameter**: scaffold behavior is unchanged —
  the parameter is simply absent from the provider invocation unless the author
  supplies it.
- **Blank/whitespace declared default**: treated as the existing contract treats a
  blank declaration (no silent invented value); surfaced rather than masked.
- **Default value carries no SDD meaning**: SDD forwards the declared default
  verbatim and never interprets, validates, or enumerates the set of allowed values —
  the provider owns which starters exist and which is default.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Scaffold MUST apply a provider-declared parameter `default` as the
  effective value for that parameter when the author does not supply it, and forward
  that effective value to the external provider unchanged.
- **FR-002**: An author-supplied `--param <key>=<value>` MUST take precedence over a
  provider-declared `default` for the same parameter; the declared default MUST NOT
  override an explicit author value.
- **FR-003**: The scaffold report and `.fsgg/scaffold-provenance.json` MUST record
  the effective value used for each forwarded parameter (declared default or author
  override), so a scaffolded product is reproducible and the chosen starter is
  auditable.
- **FR-004**: Generic SDD source and the generic provider contract's tests MUST
  contain **zero** occurrences of provider-specific starter values (e.g. `game`,
  `app`), rendering package ids, template ids, paths, or docs URLs — the existing
  FR-002 / SC-005 (feature 030) boundary, re-asserted and kept grep-clean by this
  feature's tests and fixtures.
- **FR-005**: The default-starter-selection capability (a provider-declared parameter
  `default`, its application when omitted, and `--param` override precedence) MUST be
  documented in SDD's provider authoring contract surface, value-agnostically, so a
  provider author knows how to declare and change a default starter without any
  SDD code change.
- **FR-006**: The composition-acceptance MUST exercise the default-starter path —
  i.e. drive the fixed real-provider scaffold with **no** explicit starter parameter —
  against the real published rendering registry at `0.1.54-preview.1`, and assert the
  produced product builds and the composition verdict is GREEN, by reference to the
  registry's declared default and never by naming a starter.
- **FR-007**: The composition-acceptance default-starter assertion MUST remain opt-in
  and network-gated: with the gating environment unset it is reported Skipped and the
  offline inner loop stays green and touches no network (the existing acceptance
  gating, unchanged).
- **FR-008**: This feature is **additive within the scaffold output only**: it
  introduces exactly one new field (`effectiveParameters`) on the scaffold JSON object
  and one corresponding line-group in the scaffold text projection — always present
  (an empty array / no lines when no parameters are forwarded). It MUST NOT change any
  other existing field, key order, stream routing, or exit code of the scaffold command,
  and MUST NOT change the byte-level JSON automation contract, stream routing, or exit
  codes of any **non-scaffold** command. (The scaffold object necessarily changes byte-
  for-byte because the new field is always emitted; the unchanged guarantee is scoped to
  existing scaffold fields and to every non-scaffold command — the scoped reading
  reconciled in research.md D2 / data-model.md §"FR-008 byte-level scope".)
- **FR-009**: The literal `app → game` default flip (a data edit in the
  Templates-owned `providers/rendering.providers.yml`) MUST be redirected to
  FS.GG.Templates via a cross-repo response on FS-GG/FS.GG.SDD#44, naming the
  `fs-gg-ui-template` contract at `0.1.54-preview.1`; it is explicitly **out of
  scope** for generic SDD source and SDD-owned fixtures.

### Key Entities *(include if feature involves data)*

- **Provider parameter declaration**: a provider-owned entry in `.fsgg/providers.yml`
  with a key, a required flag, and an optional `default`. A "starter"/"profile"
  selection is one such parameter; the "default starter" is its `default`. Owned by
  the provider/author (canonically FS.GG.Templates for the rendering provider), never
  by generic SDD.
- **Effective scaffold parameters**: the resolved key→value map scaffold forwards to
  the provider — declared defaults overlaid by author `--param` overrides — and
  recorded in scaffold provenance.
- **Composition-acceptance verdict**: the deterministic GREEN/RED result of driving
  the real published provider via the network-gated, Templates-dispatched registry;
  harness output, not a lifecycle artifact (a declared release-catalog exception).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With a generic default-declaring provider, scaffolding without the
  parameter forwards the declared default to the provider in 100% of runs; changing
  only the registry's declared default value changes the forwarded value with **zero**
  lines of generic SDD code changed.
- **SC-002**: An explicit `--param` override is honored over the declared default in
  100% of runs; the override value is the one recorded as effective in provenance.
- **SC-003**: A repository-wide search of generic SDD source and generic-contract
  tests/fixtures returns **zero** occurrences of `game`, `app`-as-starter, or any
  rendering package id / template id / path / docs URL (boundary preserved).
- **SC-004**: With the real `0.1.54-preview.1` registry and the gating environment
  present, the default-starter composition-acceptance run produces a product that
  builds and yields a GREEN verdict; with the gating environment absent, the default
  `dotnet test` run reports it Skipped and stays green with no network access.
- **SC-005**: A provider author can determine, from the documented provider authoring
  contract alone (no decompilation, no source reading), how to declare a default
  starter and how `--param` overrides it.

## Assumptions

- **Resolution of "the next SDD-owned item"**: the only non-Done SDD-owned item on
  the `Coordination` board is #44; it became unblocked on 2026-06-30 when
  FS.GG.Rendering#33 closed (template `game` profile released at `0.1.54-preview.1`,
  registry flipped in FS-GG/.github#78). #44 is therefore the item being started.
- **Boundary reframe (confirmed with the requester of this spec)**: generic SDD
  cannot carry `game`/`app`; the literal default flip is a data edit in the
  Templates-owned registry. This feature delivers SDD's in-boundary half — lock and
  prove the generic default-starter-selection mechanism, document it, and verify it
  end-to-end against the real provider — and redirects the data flip cross-repo
  (FR-009). Elevating "profile/default-profile" to a first-class versioned contract
  concept was considered and rejected as heavier than the board's effort sizing and
  unnecessary, since the parameter-`default` mechanism already exists.
- **Existing mechanism is the substrate**: the registry parser already reads
  `parameters[].default`, scaffold already overlays declared defaults under `--param`
  and forwards effective values, and provenance already records produced state —
  this feature locks these with regression coverage and documentation rather than
  building new selection machinery.
- **Composition-acceptance plumbing is in place**: feature 041 wired SDD's
  composition-acceptance to the Templates-dispatched canonical registry and the
  opt-in/network gating; this feature adds the default-starter assertion within that
  existing harness and lane, not a new test infrastructure.
- **Cross-repo dependency**: the observable `app → game` default in production
  depends on FS.GG.Templates landing `default: game` in the canonical registry
  (FR-009); that is tracked as the cross-repo response on #44 and is not a blocker
  for SDD's mechanism-lock and documentation deliverables.
