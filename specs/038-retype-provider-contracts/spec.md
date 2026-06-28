# Feature Specification: Re-type Provider Registry onto FS.GG.Contracts & Honor Declared Probe Commands

**Feature Branch**: `038-retype-provider-contracts`

**Created**: 2026-06-28

**Status**: Draft

**Input**: User description: "next non blocked sdd item on the project coordination board" → resolved to FS-GG/FS.GG.SDD#9 (H2 · sdd — Re-type `parseProviderRegistry` onto FS.GG.Contracts; honor declared commands in probes). Its blocker, FS-GG/FS.GG.SDD#8 (H2 · sdd — create `FS.GG.Contracts`), has landed on `main` (feature 036, commit `d80a8ae`), so this item is now non-blocked.

## Context

Two provider-shaped types currently exist in this repo as **independent re-encodings**
of the same concept:

- `FS.GG.Contracts` (`Fsgg.Provider`, shipped in feature 036) is the canonical,
  versioned home for the provider contract: a `ProviderDescriptor` carrying
  `Name / ContractVersion / TemplateId / Source / Parameters`, **plus** optional
  declared `Build / Test / Run / Verify` commands and a `NameParameter` (default
  `"name"`).
- `FS.GG.SDD.Artifacts` (`Config.fs`) carries a **local** `ProviderDescriptor` and
  `ProviderParameterSpec` — a thinner copy with no declared-command or
  `NameParameter` fields — that `parseProviderRegistry` builds from
  `.fsgg/providers.yml` and the scaffold command consumes.

This duplication is exactly the "local re-encoding" the contracts package was created
to retire. While both copies exist, the scaffold path cannot see a provider's declared
build/run commands even though the contract now defines them, and any future field
added to the contract has to be added in two places.

Separately, feature 035 made the opt-in composition acceptance probes
**declared-or-default ready**: `buildProbe` / `runProbe` already accept an optional
declared command and fall back to `dotnet build` / `dotnet run --project <discovered>`
when none is supplied. But the harness still calls them with `None` because nothing
yet flows a provider's declared command into the probe. The wiring is staged and idle.

This feature closes both gaps together:

1. Re-type `parseProviderRegistry` and the scaffold consumers onto the canonical
   `FS.GG.Contracts` provider types, deleting the local re-encoding and reading the
   extended fields (declared commands + `NameParameter`) from `.fsgg/providers.yml`
   with behavior-preserving defaults.
2. Flow the resolved descriptor's declared **build** and **run** commands into the
   acceptance probes, so a provider that declares them is composition-tested through
   its own commands — while the reference provider (which declares none) stays
   byte-for-byte unchanged.

This is a **Tier 1** change: it touches the `scaffold-provider` contract surface (how
SDD reads the provider registry and drives the build/run probes). It **adopts** the
already-published `FS.GG.Contracts` 1.0.0 surface; it introduces **no** new public
schema version and changes **no** JSON byte for any provider that declares only
today's fields. The harness must stay provider-agnostic (invariant T021a: no
Governance or rendering identity in the acceptance project).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One canonical provider type (Priority: P1)

An SDD maintainer reads or extends the provider contract. There is a **single**
authoritative `ProviderDescriptor` (and `ProviderParameterSpec`) — the one in
`FS.GG.Contracts`. `parseProviderRegistry` returns that type, and the scaffold command
consumes that type; the duplicate copy in `Config.fs` no longer exists. A field added
to the contract surfaces everywhere with no second edit.

**Why this priority**: This is the structural point of the work — retire the
re-encoding so the contract has one home. Until it lands, the declared-command fields
are unreachable from the scaffold path and every contract change risks divergence.

**Independent Test**: Build the solution after removing the local types; confirm
`parseProviderRegistry` and the scaffold handler reference only the `FS.GG.Contracts`
provider types, and the existing scaffold tests (over the current `.fsgg/providers.yml`
shape) produce identical `CommandReport`/scaffold-summary output and identical
diagnostics (`scaffold.providerUnknown`, `scaffold.providerMissing`, unsupported
contract, missing-required-parameter).

**Acceptance Scenarios**:

1. **Given** a `.fsgg/providers.yml` written to today's shape (name, contractVersion,
   templateId, source, optional parameters), **When** `parseProviderRegistry` runs,
   **Then** it returns `FS.GG.Contracts` `ProviderDescriptor` values whose
   `Name / ContractVersion / TemplateId / Source / Parameters` are identical to what
   the local type produced, and the scaffold command behaves byte-identically.
2. **Given** the local `Config.ProviderDescriptor` / `Config.ProviderParameterSpec`
   are removed, **When** the solution and its tests are built, **Then** every consumer
   (the scaffold handler, default-parameter resolution, missing-required-parameter
   detection) compiles and passes against the `FS.GG.Contracts` types with no behavior
   change.
3. **Given** a registry entry missing one of the required fields
   (name/contractVersion/templateId/source), **When** parsing runs, **Then** that entry
   is dropped exactly as before (a `--provider` naming it still resolves to
   `scaffold.providerUnknown`).

---

### User Story 2 - Registry parsing reads the extended contract fields (Priority: P2)

A provider author declares, in `.fsgg/providers.yml`, how their product is built/tested/
run/verified and which template parameter carries the product name.
`parseProviderRegistry` reads those optional `build / test / run / verify` declared
commands and `nameParameter` into the canonical descriptor. A provider that declares
none parses to "no declared command" and `NameParameter = "name"`, preserving today's
behavior.

**Why this priority**: Reading the extended fields is what makes the canonical type
*useful* on the scaffold path and is the prerequisite for honoring declared commands
(US3). It is P2 because it adds capability but, with behavior-preserving defaults,
cannot regress any current registry.

**Independent Test**: Parse a synthetic registry that declares a `build` command, a
`run` command, and a `nameParameter`, and confirm the descriptor carries those values;
parse a registry that declares none and confirm `Build/Test/Run/Verify = None` and
`NameParameter = "name"`.

**Acceptance Scenarios**:

1. **Given** a registry entry declaring a `build` command (executable + arguments),
   **When** parsing runs, **Then** the descriptor's `Build` carries that executable and
   argument list; the same holds independently for `test`, `run`, and `verify`.
2. **Given** a registry entry that declares no commands and no `nameParameter`, **When**
   parsing runs, **Then** the descriptor's command fields are all `None` and its
   `NameParameter` resolves to `"name"`.
3. **Given** a declared command whose executable is blank (null/empty/whitespace),
   **When** parsing runs, **Then** that command is treated as "not declared" (no
   declared command), never as a launchable empty executable.

---

### User Story 3 - Acceptance probes honor the declared build/run commands (Priority: P3)

The opt-in composition acceptance harness drives a scaffolded product through its build
and run probes. It now flows the **resolved descriptor's** declared `Build` and `Run`
commands into those probes (which feature 035 already made declared-or-default ready).
A provider that declares a build/run command is composition-tested through that command;
the reference provider, which declares none, falls through to today's `dotnet`
defaults — an observably unchanged verdict.

**Why this priority**: This is the user-facing payoff (the harness becomes
provider-honoring), but it is P3 because it depends on US1+US2 and, for the only
provider that exists today, produces zero observable change.

**Independent Test**: With a resolved descriptor that declares no build/run command,
run the probes and confirm the same `dotnet`-default invocations and the same
`AppBuilds`/`AppRuns` facts as today. With a synthetic descriptor that declares a
trivial build and run command, confirm the probes invoke the declared commands instead
of the `dotnet` default — without any real provider.

**Acceptance Scenarios**:

1. **Given** the composition harness resolves the reference provider's descriptor (no
   declared build/run command), **When** the build and run probes run, **Then** they
   invoke the `dotnet` defaults and the emitted `composition-acceptance-result` verdict
   is identical in pass/fail to the pre-change harness.
2. **Given** a descriptor that declares a `build` command, **When** the build probe
   runs, **Then** it invokes the declared command (not `dotnet build`) under the same
   bounded-timeout and outcome semantics; likewise the run probe honors a declared
   `run` command under the same grace/overall window.
3. **Given** the implemented wiring, **When** the provider-agnostic invariant test runs,
   **Then** it still finds no Governance or rendering identity, and no provider-specific
   package id, template id, path, command string, or docs URL, in the acceptance
   harness.

---

### Edge Cases

- **Entry declaring only some commands** (e.g. `build` but not `run`): the declared
  `build` is honored; the run probe falls through to its `dotnet` default. Each command
  field resolves independently.
- **Blank / whitespace-only declared executable**: treated as "no declared command" for
  that field, resolved to the default — never an attempt to launch an empty executable
  (consistent with feature 035 FR-010 and `Fsgg.Provider.isMalformed`).
- **`nameParameter` absent or blank**: resolves to the default `"name"`.
- **Registry entry missing a required field**: still dropped (unchanged drop-incomplete
  behavior); the extended fields never rescue an otherwise-incomplete entry.
- **Existing registry with none of the new keys**: parses identically to today — all
  command fields `None`, `NameParameter = "name"` — and every scaffold output byte is
  preserved.
- **Schema-version validation**: the provider-registry schema-version gate behaves as
  before; adopting the contract types does not relax or change which registries are
  accepted or how version diagnostics are emitted.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `parseProviderRegistry` MUST return the `FS.GG.Contracts`
  (`Fsgg.Provider`) `ProviderDescriptor` type; the local `Config.ProviderDescriptor`
  and `Config.ProviderParameterSpec` re-encodings MUST be removed.
- **FR-002**: Every scaffold-path consumer of the descriptor (the scaffold handler,
  effective-parameter/default resolution, missing-required-parameter detection,
  contract-version support check) MUST consume the `FS.GG.Contracts` type with no
  behavior change for any registry expressible today.
- **FR-003**: `parseProviderRegistry` MUST read the optional declared `build`, `test`,
  `run`, and `verify` commands (each an executable plus argument list) from a registry
  entry into the descriptor's corresponding fields, defaulting to "no declared command"
  when a key is absent.
- **FR-004**: `parseProviderRegistry` MUST read the optional `nameParameter` into the
  descriptor, defaulting to `"name"` when absent or blank.
- **FR-005**: A declared command whose executable is blank (null/empty/whitespace) MUST
  be treated as "no declared command" for that field, not as a launchable empty
  executable.
- **FR-006**: For any registry entry expressible under today's shape (no declared
  commands, no `nameParameter`), parsing MUST yield a descriptor with all command fields
  `None`, `NameParameter = "name"`, and `Name/ContractVersion/TemplateId/Source/
  Parameters` identical to the prior local result — and the scaffold command MUST produce
  byte-identical reports and diagnostics.
- **FR-007**: The drop-incomplete-entry behavior MUST be preserved: an entry missing any
  required field (name/contractVersion/templateId/source) is dropped, and the
  provider-registry schema-version gate behaves exactly as before.
- **FR-008**: The composition acceptance harness MUST flow the resolved descriptor's
  declared `Build` and `Run` commands into the build and run probes respectively,
  rather than always passing "no declared command".
- **FR-009**: When the resolved descriptor declares no build/run command (the reference
  provider today), the probes MUST resolve to the `dotnet` defaults and the composition
  verdict MUST be identical in pass/fail to the pre-change harness — no observable change.
- **FR-010**: When the resolved descriptor declares a build or run command, the
  corresponding probe MUST invoke the declared command under the same bounded-execution
  and outcome semantics feature 035 established (build completion timeout; run grace +
  overall window; cannot-start / non-zero / timeout each a diagnosed non-zero outcome).
- **FR-011**: The change MUST preserve the provider-agnostic invariant (T021a): no
  Governance or rendering identity, and no provider-specific package id, template id,
  path, command string, or docs URL, may appear in the acceptance harness.
- **FR-012**: The change MUST NOT introduce a new public schema version: it adopts the
  already-published `FS.GG.Contracts` 1.0.0 provider surface and changes no other
  lifecycle artifact, the scaffold provenance record, or the CLI report shape for
  registries expressible today.

### Key Entities

- **Provider descriptor (canonical)**: `Fsgg.Provider.ProviderDescriptor` in
  `FS.GG.Contracts` — the single source of truth, carrying
  `Name/ContractVersion/TemplateId/Source/Parameters`, optional `Build/Test/Run/Verify`
  declared commands, and `NameParameter`.
- **Declared command**: an optional provider-supplied `(executable, arguments)` for a
  build/test/run/verify step; blank executable ⇒ absent.
- **Provider registry**: `.fsgg/providers.yml`, parsed by `parseProviderRegistry` into a
  list of canonical descriptors; required-field and schema-version rules unchanged.
- **Probe command (build/run)**: the declared-or-default command the acceptance probe
  invokes; declared comes from the descriptor's `Build`/`Run`, default is the `dotnet`
  fallback from feature 035.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The repository contains exactly one `ProviderDescriptor` and one
  `ProviderParameterSpec` definition (in `FS.GG.Contracts`); no provider re-encoding
  remains in `FS.GG.SDD.Artifacts`.
- **SC-002**: For every registry expressible under today's shape, the scaffold command
  emits a `CommandReport`/scaffold summary and diagnostics byte-identical to the
  pre-change build (zero regression across the existing scaffold test matrix).
- **SC-003**: A registry that declares `build`/`test`/`run`/`verify` commands and a
  `nameParameter` parses into a descriptor carrying those exact values; a registry that
  declares none parses to all-`None` commands and `NameParameter = "name"` — both
  demonstrated by tests.
- **SC-004**: With the reference provider (no declared command), the opt-in composition
  acceptance suite produces a `composition-acceptance-result` verdict identical in
  pass/fail to the pre-change harness.
- **SC-005**: A probe given a descriptor with a synthetic declared command invokes that
  command rather than the `dotnet` default in 100% of runs, demonstrated by a test that
  never starts a `dotnet` process for the declared case.
- **SC-006**: The provider-agnostic invariant test (no Governance/rendering identity in
  the acceptance project) continues to pass after the change.

## Assumptions

- **Audience and tier**: The "users" are SDD maintainers and external provider authors.
  This is a Tier 1 change against the `scaffold-provider` contract surface that *adopts*
  the already-shipped `FS.GG.Contracts` 1.0.0 types; it introduces no new schema version.
- **Type unification for the probe parameter**: Feature 035 introduced a local
  `DeclaredCommand` in the acceptance harness explicitly as the "1:1 forward-compatible
  read" of the H2 descriptor fields. Re-pointing the probe's declared-command parameter
  at the `FS.GG.Contracts` `DeclaredCommand` (retiring that second local copy) is the
  expected realization, but the precise type-sharing mechanics are an implementation
  detail for the plan; the spec only requires that the descriptor's declared command is
  what the probe invokes (FR-008/FR-010).
- **Registry YAML keys**: the extended fields are read under the natural keys
  (`build`/`test`/`run`/`verify` with `executable` + `arguments`, and `nameParameter`)
  consistent with the `FS.GG.Contracts` descriptor; the exact key spellings/nesting are
  finalized in the plan against the contract's intended registry encoding.
- **No real non-default provider exists yet**: the declared-command parse and probe-honor
  paths are exercised with synthetic registries/descriptors (deterministic exit), keeping
  the tests offline and provider-agnostic. The reference provider continues to declare no
  build/run command, so the network-gated composition path is unchanged.
- **Scope boundary**: this feature changes only registry parsing, the scaffold path's
  consumption of the descriptor, and the acceptance harness wiring (plus their tests). It
  does not change the scaffold command's public contract, the provenance record, the
  three report projections, or any other lifecycle artifact.
- **Governance is downstream**: nothing here computes a Governance verdict or changes the
  governance handoff; provider/registry types remain SDD-owned and Governance-independent.

## Dependencies

- **Unblocked by** FS-GG/FS.GG.SDD#8 (H2 — `FS.GG.Contracts` package), delivered in
  feature 036 (commit `d80a8ae`) on `main`. This feature is FS-GG/FS.GG.SDD#9.
- **Builds on** feature 035 (FS-GG/FS.GG.SDD#7, declared-or-default probes), which staged
  the optional declared-command parameter this feature now feeds.
- Part of FS-GG/.github#16 (Homogeneous build · contracts · auto-update fabric — the
  coherence backbone). Sibling re-type tasks in Governance (FS-GG/FS.GG.Governance#14)
  and Templates (FS-GG/FS.GG.Templates#13) are independent and out of scope here.
