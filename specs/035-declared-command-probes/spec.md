# Feature Specification: Declared-or-Default Acceptance Build/Run Probes

**Feature Branch**: `035-declared-command-probes`

**Created**: 2026-06-28

**Status**: Draft

**Input**: User description: "next non blocked sdd item on the project coordination board" → resolved to FS-GG/FS.GG.SDD#7 (H1 · sdd — Acceptance build/run probes invoke declared-or-default command; default = `dotnet build` / `dotnet run --project`).

## Context

The opt-in, network-gated composition acceptance harness (`tests/FS.GG.SDD.Acceptance.Tests`)
drives a real template provider's product through a **build probe** and a **run
probe** to prove the scaffolded product is not just files but a working app. Today
those probes hard-assume a single platform-standard invocation at the product root:
`dotnet build`, then `dotnet run --no-build`. This works for the reference provider
but bakes the build/run recipe into the harness, so a provider whose product builds
or runs with any other command cannot be composition-tested.

The cross-repo H2 work (FS-GG/FS.GG.SDD#8) will extend the provider contract so a
provider can **declare** how its product is built and run. This feature makes the
acceptance probes forward-compatible with that contract *now*: each probe invokes a
**declared** command when one is supplied, and otherwise falls back to today's exact
behavior. No provider declares a command yet, so the default offline/opt-in suite is
observably unchanged. The harness must stay provider-agnostic (invariant T021a: no
Governance or rendering identity in the acceptance project).

This is a **Tier 1** change: it touches the `scaffold-provider` contract surface (the
acceptance protocol's build/run probe behavior) and must be forward-compatible with
the H2 descriptor fields, even though no public SDD schema changes in this feature.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Default probes preserve today's green behavior (Priority: P1)

An SDD maintainer runs the opt-in composition acceptance suite against the reference
provider, which declares no build/run command. The probes must behave exactly as they
do today — build the product and smoke-run it — so the verdict (`AppBuilds`/`AppRuns`
facts and overall pass/fail) is unchanged.

**Why this priority**: Regression safety is the gating constraint of the whole
feature. The issue requires "no behaviour change until a provider declares a command
(offline tests stay green)." If the default path drifts, every downstream consumer of
the acceptance verdict breaks.

**Independent Test**: With no declared command supplied (the only state available
today), run the build and run probes over a known-good product and confirm the same
facts and result document the harness produces now. Fully tested without any provider
declaring a command.

**Acceptance Scenarios**:

1. **Given** a successfully scaffolded reference product and no declared build command,
   **When** the build probe runs, **Then** it invokes the platform-standard build of
   the product (`dotnet build`) and reports the same `AppBuilds` outcome as today.
2. **Given** a successfully built product and no declared run command, **When** the run
   probe runs, **Then** it invokes the platform-standard run of the discovered runnable
   project (`dotnet run --project <discovered>`) and reports the same `AppRuns` outcome
   as today (clean exit within the grace window, or survives the grace window without a
   non-zero exit).
3. **Given** the opt-in suite is run with `FSGG_SDD_ACCEPTANCE_REGISTRY` set and no
   provider-declared command, **When** the full composition runs, **Then** the emitted
   `composition-acceptance-result` verdict is identical in pass/fail to the pre-change
   harness.

---

### User Story 2 - Probes honor a provider-declared command (Priority: P2)

A provider author whose product is built or run via a non-default command (e.g. a build
script, a different entrypoint, or extra arguments) declares that command. The
acceptance probes invoke the declared command instead of the default, so the provider's
product can be composition-tested through the same harness.

**Why this priority**: This is the actual capability the feature adds. It is P2 because
it cannot regress anything today (no provider declares a command yet), but it is the
reason the work exists and unblocks the H2 contract consumers.

**Independent Test**: Supply a synthetic declared build command and a synthetic declared
run command to the probes and confirm each invokes the declared command (and its
arguments and working directory) rather than the `dotnet` default — verified with a
trivial declared command that exits deterministically, with no real provider involved.

**Acceptance Scenarios**:

1. **Given** a declared build command is supplied, **When** the build probe runs, **Then**
   it invokes the declared command (not `dotnet build`) and reports the probe outcome
   from that command's exit.
2. **Given** a declared run command is supplied, **When** the run probe runs, **Then** it
   invokes the declared command (not `dotnet run`) under the same bounded grace/overall
   timeout semantics and pass/survive rules as the default run probe.
3. **Given** a declared command is supplied, **When** the probe resolves it, **Then** the
   command shape is read in a form forward-compatible with the H2 `ProviderDescriptor`
   build/run fields, so adopting the H2 contract requires no probe rewrite.

---

### User Story 3 - Harness stays provider-agnostic (Priority: P3)

The acceptance project must carry no Governance or rendering identity. Adding
declared-command support must not introduce any provider-specific package id, template
id, path, command string, or docs URL into the harness.

**Why this priority**: A standing invariant (T021a) that the change must not violate.
P3 because it is a guardrail on the implementation rather than new user value.

**Independent Test**: The existing "acceptance project carries no Governance reference"
invariant test still passes, and no rendering/provider-specific identifier appears in
the probe resolution code or its defaults.

**Acceptance Scenarios**:

1. **Given** the implemented declared-command resolution, **When** the provider-agnostic
   invariant test runs, **Then** it finds no Governance or rendering identity in the
   acceptance project.
2. **Given** the default commands, **When** they are inspected, **Then** they reference
   only generic, platform-standard tooling (`dotnet`) — never a provider, template, or
   package name.

---

### Edge Cases

- **Declared command that cannot start** (missing executable): the probe reports a
  not-started, non-zero, diagnosed outcome — the same failure shape as a default command
  that fails to start, never a hang.
- **Declared command that hangs**: the bounded build timeout and the run probe's
  grace/overall window apply equally to declared commands, so a hung declared command
  fails rather than hangs.
- **No runnable project discoverable** for the default run probe: the probe reports a
  diagnosed, non-zero outcome rather than silently passing or hanging.
- **Multiple runnable projects** produced: the run probe's project discovery must be
  deterministic (same product → same target) so the verdict is reproducible.
- **Empty or whitespace-only declared command**: treated as "no declared command" and
  resolved to the default, not as an attempt to launch an empty executable.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The build probe MUST accept an optional declared build command and, when
  none is supplied, fall back to the platform-standard build of the product
  (`dotnet build` over the product root).
- **FR-002**: The run probe MUST accept an optional declared run command and, when none
  is supplied, fall back to running the discovered runnable project of the product
  (`dotnet run --project <discovered>`).
- **FR-003**: When a declared command is supplied, the corresponding probe MUST invoke
  the declared command — its executable, arguments, and product-rooted working directory
  — instead of the `dotnet` default.
- **FR-004**: The optional declared command MUST be read in a shape that is
  forward-compatible with the H2 `ProviderDescriptor` build/run fields
  (FS-GG/FS.GG.SDD#8), so adopting the H2 contract does not require re-authoring the
  probes.
- **FR-005**: With no provider-declared command (the only state reachable today), the
  build and run probes MUST produce the same facts and the same overall
  `composition-acceptance-result` pass/fail verdict as the pre-change harness — no
  observable behavior change.
- **FR-006**: Declared commands MUST be subject to the same bounded execution semantics
  as the defaults: the build probe's completion timeout, and the run probe's grace
  window (pass on clean exit) plus overall cap (survive-without-crash → started),
  killing and diagnosing any process that exceeds its bound.
- **FR-007**: Both probes MUST distinguish a command that could not start, a command that
  exited non-zero, and a command that timed out, each as a diagnosed non-zero probe
  outcome — never a silent pass and never a hang.
- **FR-008**: The run probe's default MUST discover the product's runnable project
  deterministically, so repeated runs over the same product target the same project.
- **FR-009**: The change MUST preserve the provider-agnostic invariant (T021a): no
  Governance or rendering identity, and no provider-specific package id, template id,
  path, command, or docs URL, may appear in the acceptance harness or the probe defaults.
- **FR-010**: An empty or whitespace-only declared command MUST be treated as "no
  declared command" and resolved to the default.

### Key Entities

- **Probe command**: The resolved (executable, arguments, working directory) the build
  or run probe actually invokes. Either declared or default.
- **Declared command (optional)**: A provider-supplied build or run command. Absent
  today; supplied via the H2 `ProviderDescriptor` build/run fields once that contract
  lands. Its read shape must be forward-compatible with H2.
- **Default command**: The platform-standard fallback — `dotnet build` over the product
  root for build; `dotnet run --project <discovered>` over the discovered runnable
  project for run.
- **Probe outcome**: The existing started / exit-code / diagnostic result the probes
  already return, unchanged in shape.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With no declared command, the opt-in composition acceptance suite produces
  a `composition-acceptance-result` verdict identical in pass/fail to the pre-change
  harness for the reference provider (zero regression).
- **SC-002**: A probe given a synthetic declared command invokes that command rather than
  the `dotnet` default in 100% of runs, demonstrated by a test that never starts a
  `dotnet` process for the declared case.
- **SC-003**: The provider-agnostic invariant test (no Governance/rendering identity in
  the acceptance project) continues to pass after the change.
- **SC-004**: Adopting the H2 declared-command fields requires no change to the probe
  resolution logic — only wiring the descriptor's value into the existing optional
  parameter (verified by the forward-compatible read shape).
- **SC-005**: Every probe failure mode (cannot start, non-zero exit, timeout) yields a
  diagnosed non-zero outcome within its bound; no probe path can hang the suite.

## Assumptions

- **Audience and tier**: The "users" are SDD maintainers and external provider authors;
  the harness is developer-facing, so platform-standard tooling names (`dotnet`) are
  acceptable in defaults. This is a Tier 1 change against the `scaffold-provider`
  contract surface.
- **Default run form**: The default run probe moves from `dotnet run --no-build` at the
  product root to `dotnet run --project <discovered>`. This is treated as observably
  equivalent for a single-runnable-product (offline tests stay green, FR-005); the
  `--project` form is the normalized shape the declared-command path also targets.
- **H2 not yet landed**: The `ProviderDescriptor` does not carry build/run fields in this
  feature. The probes expose the optional declared-command parameter now and resolve to
  the default; the composition test passes no declared command. The H2 wiring
  (FS-GG/FS.GG.SDD#8 / #9) is out of scope here.
- **Project discovery mechanism** (how the default run probe selects the runnable
  project) is an implementation detail for the plan; the spec only requires it be
  deterministic and diagnosed on failure (FR-008).
- **Test approach**: The declared-command path is exercised with a trivial synthetic
  command (deterministic exit), not a real non-default provider, to keep the test offline
  and provider-agnostic.
- **Scope boundary**: This feature changes only the acceptance harness probes and their
  tests. It does not change the scaffold command, the provider contract schema, the
  provenance record, or any lifecycle artifact.
- **Network gating unchanged**: The real-provider composition path remains opt-in and
  network-gated on `FSGG_SDD_ACCEPTANCE_REGISTRY`; the declared-command unit coverage
  runs in the default offline inner loop.

## Dependencies

- Forward-compatible with, but not blocked by, FS-GG/FS.GG.SDD#8 (H2 — extended
  `ProviderDescriptor` build/test/run/verify fields). This feature is the non-blocked H1
  predecessor that makes the probes ready to consume those fields.
- Part of FS-GG/.github#16 (Homogeneous build · contracts · auto-update fabric, Pillar 1).
