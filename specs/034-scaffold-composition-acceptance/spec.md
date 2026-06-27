# Feature Specification: Scaffold Composition Acceptance (real rendering provider)

**Feature Branch**: `034-scaffold-composition-acceptance`

**Created**: 2026-06-27

**Status**: Draft

**Input**: User description: "next non blocked sdd item on the project coordination board"

> **Board provenance**: This feature realizes the only non-blocked, not-yet-Done
> SDD item on the FS-GG **Coordination** board (Projects v2 #1): the P2 SDD epic
> *"scaffold --provider rendering --param lifecycle=sdd yields app-only +
> skeleton"* (Status `In progress`, Phase `P2 SDD`, Workstream `Composition`,
> Contract `scaffold-provider`, Target 2026-08-01). Its `blocked by` (P1) is
> marked ✅ delivered — `FS.GG.UI.Template@0.1.50-preview.1` carries the
> `lifecycle` symbol — and both of its child tasks (provider-wrapper
> pass-through + provenance, and constitution-in-skeleton) are Done. The epic's
> own deliverable — *confirming the real composition path through SDD's provider
> wrapper* — is what remains. Every existing scaffold test exercises only the
> neutral in-repo `dotnet new` fixture provider (so generic SDD carries no
> rendering knowledge); none exercises the real published rendering provider, so
> nothing yet proves the actual `rendering` + `lifecycle=sdd` composition is
> coherent end to end. Completing this feature closes the epic and the P2 SDD
> phase.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Prove the real composition path is coherent end to end (Priority: P1)

A maintainer needs a single, repeatable check that answers one question with a
deterministic verdict: *does `fsgg-sdd scaffold --provider rendering --param
lifecycle=sdd`, run against the real published rendering provider in an empty
directory, produce one coherent product?* "Coherent" means the same invocation
yields **both** the externally-owned, runnable app **and** the SDD-owned
skeleton (the reused `init` effects plus the authored `.fsgg/constitution.md`),
the produced app actually builds and runs, and the run is reported complete only
if every part succeeded.

**Why this priority**: This is the epic's reason for existing. Until the real
provider composition is exercised once, the lifecycle=sdd promise rests on
fixture tests that deliberately know nothing about rendering. This story alone —
a green end-to-end acceptance against the real provider — delivers the
confidence that closes P2; everything else refines what that acceptance asserts.

**Independent Test**: From an empty directory, point the scaffold at the
real rendering provider registry (author-supplied, not embedded in SDD), run
`scaffold --provider rendering --param lifecycle=sdd`, and confirm the produced
tree contains the runnable app, the SDD skeleton, and the authored constitution;
that the app builds and runs; and that the command's `--json` report declares
the scaffold complete. The acceptance fails loudly if any piece is missing.

**Acceptance Scenarios**:

1. **Given** an empty directory and an author-supplied registry whose
   `rendering` provider resolves the real published rendering template, **When**
   `fsgg-sdd scaffold --provider rendering --param lifecycle=sdd` runs, **Then**
   the result contains the externally-owned runnable app, the SDD skeleton
   (`init` effects), and the authored `.fsgg/constitution.md`, and the `--json`
   report's outcome is the success outcome with the scaffold marked complete.
2. **Given** a successful instantiation, **When** the produced product is built
   and launched, **Then** it builds without error and runs (it is a runnable UI
   app), confirming the composition is buildable/runnable, not merely present.
3. **Given** the same successful instantiation, **When** scaffold's own
   post-instantiation steps are inspected, **Then** a git repository has been
   initialized at the product root and every produced `.sh` script is
   executable, and both outcomes appear in the report.

---

### User Story 2 - Provenance is partitioned correctly between provider and SDD (Priority: P2)

A maintainer (and, later, `refresh`) needs the composition's provenance to draw
a clean line: paths written by the external provider are recorded as
externally-owned product, and the SDD-owned skeleton (init effects and the
authored constitution) is never mislabeled as provider output. This partition is
what lets `refresh` regenerate SDD-owned views without ever touching the
author's app code.

**Why this priority**: Coherent output (P1) is necessary but not sufficient; if
provenance is mispartitioned, a later `refresh` would either clobber app code or
fail to refresh SDD views. This story makes the boundary an asserted fact rather
than an assumption, but it depends on P1 producing a tree to inspect.

**Independent Test**: After the P1 scaffold, inspect the scaffold-provenance
record and confirm provider-produced paths are marked as externally-owned
`generatedProduct`, the `.fsgg/constitution.md` and other `init`-skeleton paths
are **not** `generatedProduct`, and a subsequent `refresh` reports the
provider-produced paths as excluded while bringing only SDD-owned views to
currency.

**Acceptance Scenarios**:

1. **Given** a successful `lifecycle=sdd` scaffold, **When** the
   `scaffold-provenance` record is read, **Then** every provider-produced path
   is marked externally-owned `generatedProduct`, and no `init`-skeleton path
   (including `.fsgg/constitution.md`) carries `generatedProduct` provenance.
2. **Given** that provenance record, **When** `refresh` runs on the product,
   **Then** it excludes the externally-owned `generatedProduct` paths and
   regenerates only SDD-owned views, never modifying the author's app code.

---

### User Story 3 - The acceptance is opt-in and keeps generic SDD provider-neutral (Priority: P3)

A maintainer needs this real-provider acceptance to coexist with the cheap inner
loop without contaminating it: the default test/build run must stay offline and
free of any rendering-specific identifier, and the real-provider acceptance must
run only when explicitly requested (and on a schedule), since it needs network
and package-feed access to resolve the real template.

**Why this priority**: It protects the architecture rather than adding
user-visible composition behavior, so it ranks below proving the path works and
its provenance. But it is required for the acceptance to be mergeable: without
gating, the inner loop would gain a network dependency and generic SDD source
would gain rendering knowledge, both of which are disallowed.

**Independent Test**: Run the default inner-loop build/test with no network and
confirm it passes and references no rendering package id, template id, path, or
docs URL anywhere in generic SDD source; then invoke the acceptance explicitly
and confirm it is the only path that consumes the author-supplied rendering
registry.

**Acceptance Scenarios**:

1. **Given** the default inner-loop test run with no network access, **When** it
   executes, **Then** it passes and the real-provider acceptance does not run.
2. **Given** generic SDD source and reports, **When** searched for rendering
   identifiers, **Then** no rendering-specific package id, template id, path, or
   docs URL appears; the real provider is reached only through the
   author-supplied registry.
3. **Given** the real-provider acceptance is invoked explicitly, **When** it
   runs, **Then** it consumes the author-supplied rendering registry and
   produces a deterministic per-run report distinguishing PASS from a
   provider-unavailable SKIP.

---

### Edge Cases

- **Provider unavailable** (network down, feed unreachable, template version
  unresolvable): the acceptance MUST report a clear unavailable/SKIP verdict
  mapped to the provider-unavailable outcome — never a false PASS and never a
  false FAIL of SDD itself.
- **Provider defect** (the provider fails to instantiate, or writes into the
  SDD-owned tree): the scaffold MUST surface the corresponding provider-defect
  outcome at the provider-defect exit code, and the acceptance MUST treat an
  incomplete scaffold as failed — an incomplete scaffold is never reported as
  complete.
- **Produced app fails to build or run**: the acceptance MUST fail with the
  build/run failure surfaced, distinguishing "scaffold produced files" from
  "scaffold produced a working product."
- **Pre-existing work tree or git absent** at the product root: git
  initialization is skipped non-fatally and reported as skipped; the acceptance
  still passes on the rest.
- **Registry omitted or misconfigured** for the acceptance: it reports a clear
  configuration error and does not silently fall back to a fixture or to an
  embedded rendering identifier.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a repeatable acceptance that exercises
  `fsgg-sdd scaffold --provider rendering --param lifecycle=sdd` against the
  **real** published rendering provider (resolved through an author-supplied
  provider registry), starting from an empty directory.
- **FR-002**: The acceptance MUST assert that a single invocation yields **both**
  the externally-owned runnable product **and** the SDD-owned skeleton — the
  reused `init` effects plus the authored, no-clobber `.fsgg/constitution.md`.
- **FR-003**: The acceptance MUST assert that the produced product builds and
  runs (it is a runnable UI app), not merely that files were written.
- **FR-004**: The acceptance MUST assert scaffold's own post-instantiation
  steps: a git repository is initialized at the product root (skipped
  non-fatally inside an existing work tree or when git is absent) and every
  produced `.sh` script is made executable — both reported in the result.
- **FR-005**: The acceptance MUST assert provenance partitioning: every
  provider-produced path is recorded as externally-owned `generatedProduct`, and
  no `init`-skeleton path (including `.fsgg/constitution.md`) is marked
  `generatedProduct`.
- **FR-006**: The acceptance MUST confirm that `refresh` excludes the
  externally-owned `generatedProduct` paths and regenerates only SDD-owned
  views, never modifying the author's app code.
- **FR-007**: The acceptance MUST confirm the `--json` report declares the
  scaffold complete only when every part succeeded; an incomplete scaffold MUST
  never be reported as complete.
- **FR-008**: The acceptance MUST map the run to a verdict using the existing scaffold result
  vocabulary — the `outcome` value (one of `providerSucceeded`, `providerSucceededEmpty`,
  `providerNotRun`, `providerFailed`) **together with** its diagnostic code, because a
  provider-unavailable run and a provider defect both surface as the `providerFailed` outcome
  (exit 2) and are distinguishable only by the diagnostic (`scaffold.providerUnavailable` vs
  `scaffold.providerWroteSddTree` / `scaffold.providerFailed`). It MUST report a
  provider-unavailable run as an explicit SKIP — never a PASS or a FAIL of SDD — and a
  user-input/config failure (`providerNotRun`, exit 1) and a provider defect (exit 2) as distinct
  FAILs.
- **FR-009**: Generic SDD source, contracts, and reports MUST contain **no**
  rendering-specific package id, template id, path, or docs URL; the real
  provider MUST be reachable only through the author-supplied registry, never an
  embedded identifier.
- **FR-010**: The real-provider acceptance MUST be opt-in and excluded from the
  default offline inner-loop build/test, which MUST continue to pass with no
  network access and no rendering knowledge; the acceptance is intended to run on
  demand and on a schedule.
- **FR-011**: The acceptance MUST emit a single deterministic per-run result
  (modulo legitimately sensed metadata such as the resolved provider version and
  availability) that records the verdict and the asserted facts, so a run is
  reproducible and diffable.
- **FR-012**: The acceptance MUST NOT require any Governance runtime and MUST NOT
  compute any Governance verdict; effective-evidence freshness and gate
  enforcement remain optional downstream concerns.

### Key Entities *(include if feature involves data)*

- **Composition acceptance run**: one execution of the real-provider scaffold
  acceptance — its inputs (provider name, `lifecycle=sdd`, the author-supplied
  registry reference), its verdict (PASS / FAIL / SKIP-unavailable), and the
  asserted facts (skeleton present, constitution present, app builds/runs,
  provenance partition, refresh exclusion).
- **Author-supplied provider registry**: the external `.fsgg/providers.yml`
  whose `rendering` entry resolves the real published rendering template; owned
  outside generic SDD and the only channel through which the real provider is
  reached.
- **Scaffold-provenance record**: the produced `.fsgg/scaffold-provenance.json`,
  partitioning provider-produced (`generatedProduct`) paths from SDD-owned
  skeleton paths — the contract the acceptance inspects for User Story 2.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A maintainer can obtain a single PASS/FAIL/SKIP verdict for the
  real `rendering` + `lifecycle=sdd` composition in one command, with no manual
  inspection steps required to interpret it.
- **SC-002**: On a PASS, 100% of the asserted facts hold: skeleton present,
  authored constitution present, app builds and runs, git initialized (or
  explicitly skipped), every produced `.sh` executable, provenance partitioned,
  and refresh exclusion confirmed.
- **SC-003**: The default offline inner loop passes with zero network access and
  contains zero rendering-specific identifiers, while the real-provider
  acceptance is the only path that reaches the real template.
- **SC-004**: When the provider is unavailable, the acceptance returns an
  explicit unavailable/SKIP verdict in 100% of such runs and never a false PASS
  or a false FAIL of SDD.
- **SC-005**: Two runs with the same inputs and an available provider produce
  byte-identical results apart from legitimately sensed metadata (resolved
  provider version, availability), making the run reproducible and diffable.
- **SC-006**: Completing this acceptance lets the P2 SDD epic and the P2 SDD
  phase move to Done on the Coordination board, with the epic's deliverable
  ("confirm the composition path through SDD's provider wrapper") demonstrably
  satisfied.

## Assumptions

- The "next non-blocked SDD item" resolves to the P2 SDD epic on the
  Coordination board: it is the only SDD-scoped item that is not Done, and it is
  not blocked (P1 delivered; both child tasks Done). Its remaining deliverable is
  the real-provider composition acceptance specified here.
- "Provider rendering" refers to the reference rendering provider published from
  the FS.GG.Rendering repo as `FS.GG.UI.Template` (the P1-delivered template
  carrying the `lifecycle` symbol). SDD never embeds that identity; it is
  supplied through the author/provider-owned registry (the P4 Templates work that
  repointed `rendering.providers.yml` at the real template is assumed available).
- The acceptance consumes the existing `scaffold-provider` (v1) contract,
  `scaffold-provenance` (v1) record, and the established scaffold outcome/exit
  vocabulary unchanged; this feature adds verification, not new contract surface.
- Whether the acceptance lives inside the existing `fsgg-sdd validate` harness
  (extending its matrix with a real-provider composition dimension) or as a
  separate gated acceptance is a planning decision; either way it stays opt-in,
  network-gated, and out of the default inner loop.
- Running the acceptance requires network and package-feed access to resolve and
  install the real template, plus a .NET toolchain capable of building and
  running the produced UI app; CI schedules this separately from the inner loop.
- Governance is out of scope: the acceptance produces no Governance verdict and
  requires no Governance runtime, consistent with the SDD/Governance boundary.
