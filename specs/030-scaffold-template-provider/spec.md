# Feature Specification: Scaffold Runnable Products via Template Providers

**Feature Branch**: `030-scaffold-template-provider`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "Add an easy, supported way to create a new FS.GG product project that is wired for both the SDD lifecycle and a runnable Rendering UI app, via an optional template-provider delegation — NOT by hardcoding Rendering into SDD."

## Overview

Today `fsgg-sdd init` scaffolds only the SDD lifecycle skeleton (`.fsgg/`, `work/`,
`readiness/`, agent guidance). It deliberately produces no runtime/product code,
and the roadmap (`docs/initial-implementation-plan.md`, Phase 9) records "add
project templates for a new SDD-governed product skeleton" and "optionally call a
template provider for runtime code while keeping runtime ownership outside SDD" as
optional and not started.

This feature closes that gap with a **generic, schema-versioned template-provider
contract**. A new product author can run one supported command and go from nothing
to a buildable, runnable product that is already under SDD lifecycle management.
The runtime shape is supplied by an external *template provider* selected through
configuration; SDD owns only the contract, the invocation protocol, and the
record of what was generated. FS.GG.Rendering ships as the reference provider that
delivers a full runnable UI app — but no FS.GG.Rendering package id, template path,
or docs URL lives in generic SDD code.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One command to a runnable, SDD-managed product (Priority: P1)

A product author starts in an empty directory, names a template provider, and runs
a single scaffold command. The command initializes the SDD lifecycle skeleton,
invokes the named provider to materialize runnable product code into the target
directory, and reports exactly what was created. The author can then build and run
the product immediately and continue with the normal SDD lifecycle (`charter`,
`specify`, …).

**Why this priority**: This is the headline value — the "easy way" the feature
exists to deliver. Without it, authors hand-wire runtime code after `init`.

**Independent Test**: Run the scaffold command in a temporary empty directory with
a test provider registered; assert the SDD skeleton exists, the provider's runtime
files exist, the report enumerates them as provider-generated, and the produced
product builds and runs.

**Acceptance Scenarios**:

1. **Given** an empty target directory and a registered provider, **When** the
   author runs the scaffold command naming that provider, **Then** the SDD skeleton
   and the provider's runtime product are created and the report lists both, marking
   provider files as owned outside SDD.
2. **Given** a successfully scaffolded product, **When** the author runs the
   product's standard build and run steps, **Then** the product builds and runs
   without further manual wiring.
3. **Given** a freshly scaffolded product, **When** the author runs the next
   lifecycle command, **Then** the lifecycle continues normally over the created
   skeleton (the work item itself is created by the subsequent `charter` step,
   not by scaffold).

---

### User Story 2 - SDD stays useful with no provider (Priority: P1)

An author who wants only the lifecycle skeleton, with no runtime template, gets
exactly today's `init` behavior. The provider delegation is strictly optional:
omitting a provider produces the skeleton alone, with no provider machinery
required, no Governance runtime, and no assumption of a monorepo checkout.

**Why this priority**: The constitution requires SDD to remain useful without any
template provider or Governance. Regressing the no-provider path would break the
core product boundary.

**Independent Test**: Run init/scaffold with no provider selected and confirm the
output is byte-equivalent to today's `init` skeleton, with no provider artifacts
and no provider-related diagnostics.

**Acceptance Scenarios**:

1. **Given** no provider is named, **When** the author initializes a project,
   **Then** only the SDD skeleton is created and the report contains no
   provider-generated entries.
2. **Given** no provider is installed on the machine, **When** the author
   initializes without naming one, **Then** the command succeeds with no
   provider-related error.

---

### User Story 3 - Actionable diagnostics when a provider is missing, incompatible, or fails (Priority: P2)

When an author names a provider that cannot be found, declares an incompatible
contract version, or fails partway through materializing code, SDD reports a clear,
actionable diagnostic that distinguishes the cause. The SDD skeleton is never left
in a misleading "looks complete" state: either the skeleton-only result is clearly
reported as such, or the failure is surfaced with the partial state identified.

**Why this priority**: Observability and safe failure are constitutional
requirements; a one-command scaffold that fails silently or ambiguously is worse
than no command.

**Independent Test**: Drive the command against (a) an unknown provider name, (b) a
provider declaring an unsupported contract version, and (c) a provider that errors
mid-run; assert each yields a distinct, actionable diagnostic and a well-defined
result state.

**Acceptance Scenarios**:

1. **Given** a provider name that resolves to nothing, **When** the author runs the
   scaffold command, **Then** the report identifies the unknown provider and how to
   register or correct it, and the result distinguishes "skeleton created, provider
   not run" from a hard failure.
2. **Given** a provider declaring a contract version SDD does not support, **When**
   the command runs, **Then** the report names the version mismatch and the
   supported range, without attempting to run the provider.
3. **Given** a provider that fails partway, **When** the command runs, **Then** the
   report distinguishes the provider defect from malformed user input and states
   what was and was not created.

---

### User Story 4 - Provenance: generated vs authored is recorded (Priority: P2)

After scaffolding, the project records which files were produced by a template
provider, which provider produced them, and that their ongoing ownership lies
outside SDD. This record is a structured artifact, consistent with how SDD already
marks generated views, so later lifecycle and refresh steps never mistake
provider-generated runtime code for SDD-authored or SDD-owned content.

**Why this priority**: The boundary ("runtime ownership stays outside SDD") must be
machine-checkable, not just documented, so generators and Governance can respect it.

**Independent Test**: Scaffold with a provider, then read the provenance artifact
and assert it names the provider, its contract version, and the produced paths, and
marks them as externally owned; assert a refresh/currency step treats them as
out-of-scope for SDD regeneration.

**Acceptance Scenarios**:

1. **Given** a scaffolded product, **When** the provenance record is read, **Then**
   it identifies the provider, the provider contract version, and the produced
   paths, and marks them owned outside SDD.
2. **Given** a scaffolded product, **When** an SDD generated-view refresh runs,
   **Then** provider-produced runtime files are not treated as stale SDD views and
   are not regenerated by SDD.

---

### Edge Cases

- **Non-empty / colliding target**: A provider would overwrite existing files in
  the target directory. The author must be able to choose to abort by default and
  proceed only with an explicit opt-in; collisions are reported per path.
- **Provider produces nothing**: A named provider runs but materializes no files.
  This is reported as a successful-but-empty provider run, distinct from a failure.
- **Provider needs parameters**: A provider requires scaffold parameters (e.g. a
  product name) the author did not supply. SDD surfaces the missing required
  parameters declared by the provider rather than guessing.
- **Output format parity**: Every result and diagnostic is available across all
  three report projections (default/JSON, text, rich) with identical facts.
- **Repeat scaffold**: Running the scaffold command again in an
  already-initialized project is reported clearly and does not silently duplicate
  or clobber the existing skeleton or provenance.
- **Provider unaware of SDD layout**: A provider writing into `.fsgg/`, `work/`, or
  `readiness/` (SDD-owned trees) is detected and reported rather than silently
  corrupting lifecycle state.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: SDD MUST define a generic, schema-versioned template-provider contract
  that specifies how a provider is identified, what scaffold inputs it receives
  (at minimum a target directory and named parameters), what it returns (the set
  of produced paths and a success/failure outcome), and how it declares the
  contract version it implements.
- **FR-002**: The contract and all SDD code that invokes it MUST contain no
  provider-specific package identifiers, template contents, file paths, or
  documentation URLs. Any provider-specific value MUST come from configuration or
  the provider itself, never from generic SDD code.
- **FR-003**: A user MUST be able to select a template provider by name/reference
  through configuration and/or a command option when scaffolding a project.
- **FR-004**: When a provider is selected, the system MUST first establish the SDD
  lifecycle skeleton, then invoke the provider to materialize runtime product code
  into the target, then report the combined result.
- **FR-005**: The system MUST remain fully functional with **no** provider selected,
  producing exactly the existing `init` skeleton with no provider artifacts and no
  provider machinery required, and with no Governance runtime and no monorepo
  assumption.
- **FR-006**: The system MUST record provenance of provider-produced files as a
  structured, schema-versioned artifact that identifies the provider, the provider
  contract version, the produced paths, and that those paths are owned outside SDD.
- **FR-007**: SDD generated-view currency/refresh behavior MUST treat
  provider-produced runtime files as externally owned and MUST NOT regenerate or
  flag them as stale SDD views.
- **FR-008**: The system MUST emit actionable diagnostics that distinguish at least:
  unknown/unresolvable provider, unsupported provider contract version, missing
  required provider parameters, target collisions, and provider runtime failure —
  and MUST distinguish malformed user input from provider defects.
- **FR-009**: On provider failure or incompatibility, the system MUST report a
  well-defined result state that makes clear what was and was not created, and MUST
  NOT present an incomplete scaffold as complete.
- **FR-010**: The system MUST NOT overwrite existing files in a non-empty target by
  default; overwriting MUST require an explicit opt-in, and affected paths MUST be
  reported per path.
- **FR-011**: The system MUST detect and report when a provider attempts to write
  into SDD-owned trees (`.fsgg/`, `work/`, `readiness/`) rather than silently
  allowing lifecycle state to be corrupted.
- **FR-012**: All scaffold results and diagnostics MUST be expressible through the
  existing `CommandReport` projections — default/`--json` (deterministic automation
  contract), `--text`, and `--rich` — adding and dropping no facts across
  projections and changing no JSON byte for the rich projection.
- **FR-013**: Claude and Codex agent guidance MUST be updated equivalently to
  describe the new scaffold capability and the provider contract.
- **FR-014**: A reference template provider (FS.GG.Rendering, delivering a full
  runnable UI app) MUST be demonstrable against the contract **without** placing any
  FS.GG.Rendering-specific knowledge in generic SDD code or tests of the generic
  contract; the reference provider's specifics live in provider-owned fixtures or
  the provider itself.
- **FR-015**: The scaffold capability MUST be a cross-cutting concern, not a new
  lifecycle stage — selecting a provider MUST NOT alter the canonical
  `charter → ship` ordering or emit a lifecycle successor of its own.

### Key Entities

- **Template Provider**: An external, named producer of runtime product code. Has an
  identity/reference, a declared contract version, optionally a set of required and
  optional scaffold parameters, and produces files in a target directory.
- **Provider Contract**: The schema-versioned agreement between SDD and a provider —
  inputs (target, parameters), outputs (produced paths, outcome), version
  declaration, and compatibility rules.
- **Provider Selection / Descriptor**: The configuration (and/or command option)
  that names which provider to use and supplies its parameters.
- **Scaffold Provenance Record**: A structured artifact capturing which provider ran,
  its contract version, the produced paths, and the externally-owned marking.
- **Scaffold Result**: The combined, projectable outcome of skeleton creation plus
  provider invocation, including per-path provenance and any diagnostics.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From an empty directory, an author can produce a runnable,
  SDD-managed product with a single scaffold command and zero manual file edits
  before it builds and runs.
- **SC-002**: The produced product builds and runs successfully using its standard
  build/run steps with no additional wiring.
- **SC-003**: With no provider selected, the scaffold output is equivalent to the
  current `init` skeleton, verified byte-for-byte against the existing init contract.
- **SC-004**: 100% of the defined failure modes (unknown provider, unsupported
  version, missing required parameter, target collision, provider runtime failure)
  produce a distinct, actionable diagnostic and a well-defined result state.
- **SC-005**: Generic SDD source and the generic contract's tests contain zero
  occurrences of FS.GG.Rendering-specific package ids, template paths, or docs URLs
  (verifiable by search).
- **SC-006**: Every scaffold result and diagnostic is present and fact-identical
  across the default/JSON, text, and rich projections, with no JSON byte changed by
  the rich projection.
- **SC-007**: After scaffolding with a provider, an SDD refresh/currency run reports
  zero provider-produced runtime files as stale or regenerable SDD views.
- **SC-008**: Claude and Codex guidance describe the scaffold capability equivalently
  (no behavioral divergence between the two surfaces).

## Assumptions

- The audience is product authors using the `fsgg-sdd` CLI; the scaffold surface is
  a CLI command/option, consistent with the rest of the product.
- A template provider is an external concern; this feature owns the generic contract,
  selection, invocation, provenance, diagnostics, and report projections — not any
  particular provider's runtime template.
- FS.GG.Rendering is the first/reference provider and supplies a full runnable
  Elmish/MVU + SkiaSharp/OpenGL app, but is delivered and owned outside generic SDD
  code; its inclusion here is as a demonstration of the contract, not as SDD content.
- Whether the surface is a new option on `init` or a dedicated scaffold command is a
  design decision deferred to the plan; both satisfy these requirements.
- `--dry-run` is a standard projection/affordance consistent with other `fsgg-sdd`
  commands (plan and report effects without executing real I/O); it is an
  implementation affordance, not an additional functional requirement.
- Governance remains optional and is not required by any part of this feature.
- The change is Tier 1 (new CLI surface and new schema-versioned provider contract),
  requiring spec, plan, tasks, `.fsi` surface where code exists, tests, docs, and
  migration/compat notes.
