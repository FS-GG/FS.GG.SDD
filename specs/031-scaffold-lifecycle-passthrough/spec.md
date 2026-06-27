# Feature Specification: Scaffold lifecycle-parameter pass-through & app-only provenance

**Feature Branch**: `031-scaffold-lifecycle-passthrough`

**Created**: 2026-06-27

**Status**: Draft

**Input**: User description: "next sdd item on the project coordination board" — resolved to Coordination board item **P2 · sdd — Verify scaffold passes `lifecycle=sdd` through the provider wrapper; provenance records app-only paths.**

**Change Tier**: Tier 1 (contracted change) — this feature adds a published conformance obligation (a contract-conformance fixture + scenario verification + leak-invariant scan) that becomes part of SDD's verifiable provider-contract surface and gates downstream cross-repo work (P4 Templates). No public API, schema, command, or artifact-layout change is intended; if verification surfaces a real defect, fixing it stays within the existing scaffold contract.

## Context & Boundary

`fsgg-sdd scaffold --provider <name> [--param key=value ...]` already forwards
arbitrary parameters verbatim to an external template provider and records a
`.fsgg/scaffold-provenance.json` whose produced paths are marked
`generatedProduct` (externally owned), with the SDD skeleton excluded. The real
reference provider — a runnable UI app that accepts a `lifecycle` choice
(`spec-kit|sdd|none`) — lives in the **FS.GG.Rendering** repo and is **not** a
dependency of generic SDD.

This feature does **not** add `lifecycle` knowledge to SDD. `lifecycle` is, and
must remain, an opaque provider-owned template parameter that SDD passes through
like any other `key=value`. The goal is to **prove**, with deterministic
fixtures and tests owned by this repo, that the generic composition path behaves
correctly for the specific real-world shape the org roadmap depends on:
`scaffold --provider <provider> --param lifecycle=sdd` producing an app-only tree
alongside the SDD skeleton, with provenance recording only the app-only paths —
all without any rendering-specific identifier entering generic SDD.

This closes the P2 SDD gate so that P4 Templates work (repointing the provider
registry at the published Rendering template with `lifecycle=sdd`) can rely on a
verified contract rather than on hope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Lifecycle parameter is forwarded verbatim through the wrapper (Priority: P1)

A product author runs scaffold against a provider, passing `--param
lifecycle=sdd` (plus whatever required params the provider declares). SDD must
forward `lifecycle=sdd` to the provider invocation byte-for-byte alongside every
other parameter, treating it as an opaque key=value — never interpreting,
renaming, defaulting, special-casing, or dropping it.

**Why this priority**: This is the core claim of the board item. If the generic
wrapper mangles or swallows the parameter, the entire composition direction
(one command yields an app-only product wired to the SDD skeleton) fails, and no
downstream phase can proceed.

**Independent Test**: With a repo-owned fixture provider that records or echoes
the parameters it received, run scaffold with `--param lifecycle=sdd` and assert
the provider's recorded invocation contains the lifecycle parameter exactly as
supplied, with no SDD-side transformation.

**Acceptance Scenarios**:

1. **Given** a registered fixture provider and an empty target directory,
   **When** the author runs scaffold with `--param lifecycle=sdd` (and any
   required provider params), **Then** the provider invocation receives
   `lifecycle=sdd` verbatim among its forwarded parameters.
2. **Given** the same run, **When** the report is produced, **Then** the outcome
   is the success outcome and the provider-invoked indicator is true.
3. **Given** a parameter value containing nothing rendering-specific is supplied,
   **When** SDD forwards it, **Then** SDD adds, removes, and renames no
   parameters relative to (provider defaults overlaid with author `--param`
   values).

---

### User Story 2 - Provenance records only the provider's app-only paths (Priority: P1)

After the provider produces an app tree, the author (and the `refresh`
generator) must be able to trust that `.fsgg/scaffold-provenance.json` lists
exactly the externally-owned product paths and nothing from the SDD skeleton, so
that generated-view currency and ownership are never confused with author/SDD
content.

**Why this priority**: The second explicit claim of the board item. Provenance
is the boundary record that lets `refresh` exclude externally-owned paths
(FR-007 of the scaffold feature). If skeleton paths leak in, or app paths are
missing, ownership and refresh semantics break.

**Independent Test**: Run the `lifecycle=sdd` scaffold against the fixture
provider, then read the provenance file and assert every recorded path is an
app-only product path marked `generatedProduct`, the SDD skeleton paths are
absent, and the set equals the files the provider actually produced.

**Acceptance Scenarios**:

1. **Given** a successful `lifecycle=sdd` scaffold, **When** provenance is read,
   **Then** every produced path carries the `generatedProduct` owner.
2. **Given** the same provenance, **When** its path set is compared to the SDD
   skeleton, **Then** no skeleton path (the `.fsgg/`, `work/`, `readiness/`
   trees and the agent context files) appears in provenance.
3. **Given** the same provenance, **When** its path set is compared to the files
   the provider produced, **Then** the two sets are equal (no app path missing,
   no extra path recorded).
4. **Given** the report projections for the run, **When** produced paths are
   listed, **Then** they match the provenance app-only set across JSON, text,
   and rich projections (rich adds/drops no facts).

---

### User Story 3 - No rendering-specific knowledge leaks into generic SDD (Priority: P1)

The maintainers must be able to prove on every build that demonstrating the
`lifecycle=sdd` composition did **not** introduce any rendering-specific
identifier — package id, template id, provider name, path, docs URL, or the
`lifecycle` parameter's semantics — into generic SDD source.

**Why this priority**: This is the constitutional invariant (no FS.GG.Rendering
knowledge in generic SDD; FR-002 / SC-005 of the scaffold feature). A passing
behavior test that quietly hard-codes "rendering" or branches on
`lifecycle=sdd` would violate the boundary while looking green. The guard must
be explicit and enforced.

**Independent Test**: Run a repo-owned invariant check over generic SDD source
(everything outside test fixtures) that fails if any rendering-specific
identifier or any `lifecycle`-value special-casing is present; confirm it passes
for the shipped code and fails when a deliberate violation is introduced.

**Acceptance Scenarios**:

1. **Given** the generic SDD source tree (excluding test fixtures and this
   spec), **When** the invariant scan runs, **Then** it finds no
   rendering-specific package id, template id, provider name, path, or docs URL.
2. **Given** the generic SDD source, **When** the scan runs, **Then** it finds no
   code that branches on or special-cases the literal value `sdd` (or any other
   `lifecycle` value) — the parameter is handled only as an opaque key=value.
3. **Given** a deliberately planted rendering identifier in generic SDD,
   **When** the scan runs, **Then** it fails and names the offending location.

---

### Edge Cases

- **Provider declares `lifecycle` as a required parameter and the author omits
  it**: SDD must block with the existing required-parameter diagnostic before
  invoking the provider — SDD does not invent a default for `lifecycle` (it owns
  no knowledge of its values). Verified with a fixture registry that marks
  `lifecycle` required.
- **Provider produces nothing for `lifecycle=sdd`**: the existing
  provider-produced-no-paths advisory applies; provenance records an empty
  produced set and the outcome reflects the empty-success case.
- **Provider, while building the `lifecycle=sdd` product, writes into an SDD
  tree** (`.fsgg/`, `work/`, `readiness/`): the existing
  wrote-into-SDD-tree provider-defect guard fires (exit 2), the scaffold is
  reported incomplete, and those paths are not laundered into provenance as
  app-only.
- **Two consecutive identical `lifecycle=sdd` runs** (into clean targets) yield
  byte-identical provenance and byte-identical JSON report output (determinism).
- **Parameter ordering**: author supplies `--param` in a different order than
  the provider declares its defaults; the forwarded parameter set is identical
  regardless of supplied order (no order-dependent loss or duplication).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repo MUST ship a deterministic, repo-owned fixture provider
  that mimics the contract shape of the real Rendering provider for the
  `lifecycle=sdd` composition: it accepts a `lifecycle` template parameter and
  produces an app-only product tree (no files under any SDD-owned tree). The
  fixture MUST NOT reference any real FS.GG.Rendering package id, template id, or
  docs URL.
- **FR-002**: A verification MUST assert that `--param lifecycle=sdd` is
  forwarded to the provider invocation verbatim, as an opaque key=value, with no
  SDD-side interpretation, rename, defaulting, or omission.
- **FR-003**: A verification MUST assert that the forwarded parameter set equals
  (provider-declared defaults overlaid with author `--param` values) and that SDD
  neither adds nor drops parameters beyond that overlay.
- **FR-004**: A verification MUST assert that after a successful `lifecycle=sdd`
  scaffold, `.fsgg/scaffold-provenance.json` records exactly the provider's
  app-only produced paths, each marked `generatedProduct`.
- **FR-005**: A verification MUST assert that no SDD skeleton path appears in the
  provenance produced-path set, and that the SDD skeleton itself is established
  unchanged (the `init`-equivalent effects remain byte-identical).
- **FR-006**: A verification MUST assert that the three report projections (JSON,
  text, rich) present the same app-only produced-path facts for the run, and that
  the JSON projection is byte-deterministic across identical runs.
- **FR-007**: An enforced invariant scan MUST fail the build if any
  rendering-specific identifier (package id, template id, provider name, path,
  docs URL) or any special-casing of a `lifecycle` value appears in generic SDD
  source (test fixtures and specs excluded).
- **FR-008**: The verifications MUST cover the edge cases above: required-but-
  missing `lifecycle` blocks pre-invocation; empty-product success; provider
  writing into an SDD tree fails as a provider defect (exit 2) without laundering
  those paths into provenance; and parameter-order independence.
- **FR-009**: All added verification MUST use real filesystem/process fixtures
  exercised through the public scaffold surface (not mocks of internal stages),
  consistent with the test-evidence principle; any unavoidable synthetic stand-in
  MUST be disclosed and tied to the real path it represents.
- **FR-010**: This feature MUST NOT change the public scaffold command surface,
  the provenance schema, the diagnostics set, or the report projections. If
  verification reveals a genuine defect, the corrective change MUST stay within
  the existing scaffold contract and be called out explicitly in the plan.

### Key Entities

- **Lifecycle parameter**: an opaque, provider-owned `key=value` (`lifecycle`)
  carried unchanged from author `--param` through the wrapper to the provider
  invocation; SDD attaches no meaning to its values.
- **App-only product tree**: the set of provider-produced files owned externally
  (`generatedProduct`), distinct from the SDD skeleton; the exact set recorded in
  provenance.
- **SDD skeleton**: the `init`-equivalent files SDD establishes (`.fsgg/`,
  `work/`, `readiness/`, agent context files) — never recorded as app-only,
  never modified by the provider.
- **Scaffold provenance record**: the schema-versioned `.fsgg/scaffold-provenance.json`
  boundary record whose produced-path set this feature pins to the app-only tree.
- **Composition fixture provider**: the repo-owned, rendering-agnostic fixture
  that stands in for the real Rendering provider to exercise the `lifecycle=sdd`
  shape deterministically.
- **Leak-invariant scan**: the enforced check that keeps rendering knowledge and
  lifecycle-value semantics out of generic SDD.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a `scaffold --provider <fixture> --param lifecycle=sdd` run,
  100% of the parameters the provider receives equal the (defaults ⊕ author
  `--param`) overlay — no added, dropped, renamed, or reinterpreted parameters.
- **SC-002**: For a successful `lifecycle=sdd` run, 100% of provenance
  produced-paths are app-only and marked `generatedProduct`, and 0 SDD skeleton
  paths appear in provenance.
- **SC-003**: The provenance produced-path set exactly equals the set of files
  the provider created (precision and recall both 100%).
- **SC-004**: Two identical `lifecycle=sdd` runs into clean targets produce
  byte-identical provenance files and byte-identical JSON report output.
- **SC-005**: The leak-invariant scan reports 0 rendering-specific identifiers
  and 0 lifecycle-value special-cases in generic SDD source, and demonstrably
  fails (with the offending location named) when a violation is planted.
- **SC-006**: All edge-case verifications pass: required-but-missing `lifecycle`
  blocks before provider invocation; empty product yields the empty-success
  outcome; SDD-tree intrusion fails at exit 2 without laundering paths; parameter
  order does not change the forwarded set.
- **SC-007**: The public scaffold surface, provenance schema, diagnostics set,
  and report projections are unchanged (no signature-baseline or golden-output
  diff except newly added fixtures/tests).

## Assumptions

- The real Rendering provider and its `lifecycle` choice symbol
  (`spec-kit|sdd|none`) are produced and published by the FS.GG.Rendering /
  Templates phases (P1/P4) and are intentionally **not** dependencies of this
  repo; SDD verifies the contract shape via a repo-owned fixture, mirroring the
  existing scaffold fixture approach.
- `lifecycle` is purely a provider/template parameter. SDD owning no `lifecycle`
  semantics is a requirement, not a limitation to be relaxed later.
- The current scaffold implementation already forwards arbitrary `--param`
  values and already marks produced paths `generatedProduct` while excluding the
  SDD skeleton; this feature's primary deliverable is the published, enforced
  **verification** of that behavior for the `lifecycle=sdd` shape, plus the
  leak-invariant guard — not a re-implementation.
- The P2 board item depends on the P0 "constitution ownership" decision only for
  the *sibling* item (shipping an F# lifecycle constitution in the skeleton); the
  parameter-pass-through and app-only-provenance verification specified here is
  independent of that undecided decision and can proceed now.
- Verification fixtures run through the existing process edge (a `dotnet new`-
  style wrapper) using the repo's established scaffold fixture machinery and
  determinism conventions (sorted paths, no timestamps, no absolute paths).
