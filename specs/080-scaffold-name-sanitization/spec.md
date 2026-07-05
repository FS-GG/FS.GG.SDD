# Feature Specification: Guarantee a freshly scaffolded product compiles (name → valid F# identifier)

**Feature Branch**: `080-scaffold-name-sanitization`

**Created**: 2026-07-05

**Status**: Draft

**Input**: FS.GG framework development-feedback report — *Hollow Depths* build (`001-hollow-depths`), 2026-07-05 (§2.1, Appendix B). Tracks org Coordination epic **FS.GG.SDD#148** and its children **#149** (name→namespace sanitization) and **#150** (CI build/test smoke).

## Overview

`fsgg-sdd scaffold` takes a product author from an empty directory to a buildable,
runnable, SDD-managed workspace by invoking an external template provider. Today the
product name is forwarded **verbatim** to the provider, so a name that is a legal
product name but an **illegal F# identifier** (e.g. `Roquelike-DungeonCrawler` — a
hyphen, also a misspelling) is templated straight into `module`/`namespace`/`let`
identifier positions and the workspace **fails to compile** (`FS0010`, 121 occurrences
across `src/`+`tests/` in the reported build). The scaffold reported success; nothing
in the inner loop or CI caught that the "runnable" product was a phantom.

This feature makes a freshly scaffolded product **compile out of the box regardless of
how the author spells or punctuates the name**, and adds a regression guard so a
non-compiling scaffold can never silently reach an author again. Scaffold derives a
valid-F#-identifier form of the name generically (a language-level transform, never a
provider-specific value — FR-002 of `030-scaffold-template-provider`) and forwards it
alongside the raw name, so identifier contexts get the safe form and string-literal /
path / `.fsproj` / `.slnx` contexts keep the name verbatim.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A hyphenated/misspelled name still yields a product that builds (Priority: P1)

A product author scaffolds a new workspace with a name that contains characters illegal
in an F# identifier (a hyphen, a space) or is otherwise irregular. The scaffolded
workspace compiles and its tests run without the author hand-editing any generated
identifier.

**Why this priority**: This is the entire point of the feature and the single biggest
defect in the reported build ("it doesn't build"). Without it, `scaffold` fails its own
`.fsgg/tooling.yml` promise that `dotnet build`/`dotnet test` are runnable, not a phantom.

**Independent Test**: Scaffold a product named `Roquelike-DungeonCrawler` against a
provider whose templates place the name in identifier contexts; run `dotnet build` and
`dotnet test` in the produced workspace and observe both succeed with no `FS0010`.

**Acceptance Scenarios**:

1. **Given** a provider selected and a target directory, **When** the author scaffolds
   with a name containing a hyphen, **Then** every generated F# `module`/`namespace`/
   `let`-binding identifier is a valid F# identifier and `dotnet build` succeeds.
2. **Given** the same scaffold, **When** `dotnet test` runs in the produced workspace,
   **Then** it succeeds (the derived identifier is consistent across `src/` and `tests/`).
3. **Given** a name that is already a valid identifier (`Acme` / `Acme.Foo`), **When**
   scaffolded, **Then** the identifier the provider receives is unchanged from that name
   (the transform is a no-op on already-valid names).

---

### User Story 2 - The raw name is preserved where it legitimately belongs (Priority: P1)

The author's chosen name still appears verbatim in the contexts where it is correct — the
`.fsproj`/`.slnx` project and solution names, file/directory paths, and string literals
(display names, governance scan tokens like `"diagnostic-class=roquelike-dungeoncrawler-defect"`,
`Path.Combine(..., "Roquelike-DungeonCrawler", ...)`). Sanitization must not corrupt these.

**Why this priority**: A blanket find/replace that "fixes" the identifier by rewriting the
name everywhere corrupts the build a different way (§2.1: the same token legitimately
appears inside string literals and paths that must be preserved). The raw name and the
derived identifier are **two distinct values with distinct contexts**.

**Independent Test**: Scaffold with `Roquelike-DungeonCrawler`; assert the produced
`.fsproj`/`.slnx`, at least one path segment, and at least one string literal contain the
raw hyphenated name unchanged, while identifier positions contain the derived form.

**Acceptance Scenarios**:

1. **Given** a scaffold with a hyphenated name, **When** the workspace is produced,
   **Then** the raw name is what appears in string-literal / path / `.fsproj` / `.slnx`
   contexts and the derived identifier is what appears in identifier contexts.
2. **Given** the forwarded parameters, **When** provenance and the scaffold report are
   read back, **Then** the raw-name value is recorded byte-for-byte as the author gave it.

---

### User Story 3 - CI catches a scaffold that does not compile (Priority: P1)

A change to SDD, to the provider contract, or to the reference provider that would cause a
scaffolded product to stop compiling is caught by CI **before** it reaches a product author.

**Why this priority**: §2.1 / Appendix B — nothing in CI caught the defect; it reached an
author. Sentinel: `FS0010: Unexpected keyword 'open' … → scaffold hyphen-in-namespace`.
A guarantee with no automated guard regresses silently.

**Independent Test**: Introduce a regression that reverts the derivation; observe the
smoke test go red on `dotnet build`/`dotnet test`.

**Acceptance Scenarios**:

1. **Given** the CI smoke lane runs, **When** it scaffolds a product with a
   hyphenated/misspelled name against the real provider, **Then** it asserts both
   `dotnet build` **and** `dotnet test` are green and fails the run if either is not.
2. **Given** the offline inner loop (`dotnet test` with no registry), **When** it runs,
   **Then** the network-gated smoke self-skips (stays green) and does not slow the loop.

---

### User Story 4 - The derived identifier is auditable, and an unrepresentable name is reported (Priority: P2)

The author can see the derived identifier scaffold forwarded (for reproducibility/audit,
mirroring the existing `effectiveParameters` record), and a name that cannot be reduced to
a valid F# identifier at all is reported with an actionable diagnostic rather than
forwarding a value that will not compile.

**Why this priority**: Auditability parallels `050-scaffold-default-starter`'s
`effectiveParameters` guarantee. The unrepresentable-name path is an edge guard so the
"guaranteed to compile" promise degrades to a clear error, never to a broken build.

**Independent Test**: Scaffold with `Acme-Foo`; read the recorded forwarded parameters and
find both the raw name and the derived identifier. Separately, scaffold with a name whose
characters are all invalid in an identifier (e.g. `---`) and observe a `scaffold.*`
diagnostic at the user-input exit class with a `NextAction`, and no incomplete workspace
reported as complete.

**Acceptance Scenarios**:

1. **Given** a scaffold with a name needing derivation, **When** the provenance record and
   scaffold report (json/text/rich) are read, **Then** both the raw name and the derived
   identifier appear in the recorded forwarded/effective parameters, deterministically.
2. **Given** a name that reduces to no valid identifier, **When** scaffold runs, **Then**
   it blocks with an actionable `scaffold.*` diagnostic at exit 1 (user-input class),
   writes no partial provenance claiming success, and points the author at how to fix it.

---

### Edge Cases

- **Name already valid** (`Acme`, `Acme.Foo`): derivation is a no-op; the identifier
  equals the name. Dot-separated namespace segments are preserved as segment boundaries.
- **Leading digit** (`3Crawler`): the derived identifier must be a legal F# identifier
  (identifiers may not begin with a digit) — resolved deterministically.
- **Reserved-word / keyword collision** (e.g. a segment equal to an F# keyword): the
  derived identifier must remain compilable.
- **Internal punctuation and spaces** (`My App`, `foo.bar-baz`): invalid identifier
  characters are removed/normalized per segment; dots that denote namespace segments are
  preserved.
- **Provider declares no name parameter**: with no `nameParameter` declared and no name
  to derive, scaffold forwards parameters exactly as today (no derivation, no regression).
- **Author overrides the derived-identifier parameter explicitly** via `--param`: the
  precedence between an author override and the SDD-derived value is defined and recorded.
- **Unrepresentable name** (all-invalid characters): reported, not forwarded (US4).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: At scaffold time, the system MUST derive, from the author's product name, a
  value that is a **valid F# namespace/identifier** (dot-separated segments, each a legal
  F# identifier: no characters illegal in an identifier, not beginning with a digit, and
  compilable — no keyword collision that breaks the build).
- **FR-002**: The derivation MUST be **generic and language-level** — it MUST NOT embed any
  provider-specific package id, template id, path, or docs URL (consistent with
  `030-scaffold-template-provider` FR-002). "Valid F# identifier" is the only domain rule.
- **FR-003**: The derivation MUST be a **no-op on names already valid** as F# identifiers,
  and MUST be deterministic (same name → same derived identifier on every run/platform).
- **FR-004**: The system MUST identify which forwarded parameter carries the product name
  from the **provider-declared** `nameParameter` (activating the existing provider-contract
  field), defaulting to the contract default when the provider does not declare one.
- **FR-005**: The system MUST forward **both** values to the provider: the **raw name**
  (for string-literal / path / `.fsproj` / `.slnx` contexts) and the **derived identifier**
  (for `namespace` / `module` / `let`-binding contexts), under a generic, declared
  derived-identifier parameter convention.
- **FR-006**: The raw-name value the system forwards and records MUST be **byte-identical**
  to what the author supplied (no normalization of the raw name).
- **FR-007**: Both the raw name and the derived identifier the system forwards MUST be
  **recorded** in `.fsgg/scaffold-provenance.json` and in all three scaffold report
  projections (json/text/rich), deterministically ordered, consistent with the existing
  `effectiveParameters` record.
- **FR-008**: When precedence exists between an author `--param` override on the
  derived-identifier parameter and the SDD-derived value, the system MUST apply a defined,
  documented rule and record the effective value.
- **FR-009**: When a name **cannot** be reduced to any valid F# identifier, the system MUST
  emit an actionable `scaffold.*` diagnostic, resolve at the **user-input exit class**
  (exit 1), and MUST NOT report an incomplete scaffold as complete (consistent with
  `030-scaffold-template-provider` FR-009).
- **FR-010**: The change to the provider contract (activating `nameParameter` and adding the
  derived-identifier parameter convention) MUST be a **versioned, additive** contract change,
  reflected in the org dependency registry and its compatibility projection, and coordinated
  with the reference provider (FS.GG.Rendering) via the cross-repo request protocol before
  the reference provider is expected to consume the derived parameter.
- **FR-011**: A **CI smoke test** MUST scaffold a product with a **hyphenated/misspelled**
  name against the real provider and assert `dotnet build` **and** `dotnet test` succeed.
  It MUST run in the existing network-gated acceptance lane (self-skipping when the
  acceptance registry is unset) so the offline inner loop stays fast and green.
- **FR-012**: Claude and Codex agent surfaces MUST be updated **equivalently** wherever the
  scaffold parameter semantics they describe change (dual-surface rule).

### Key Entities *(include if feature involves data)*

- **Product name (raw)**: the author-supplied identity string. Legal as a product name;
  may be illegal as an F# identifier. Preserved verbatim in string/path/project contexts.
- **Derived identifier**: the language-level, valid-F#-namespace form of the raw name.
  Used in identifier contexts. Deterministic function of the raw name; no-op when the raw
  name is already valid.
- **Name parameter (`nameParameter`)**: the provider-declared descriptor field naming which
  forwarded parameter is the product name. Currently declared/parsed but unused; this
  feature activates it.
- **Forwarded/effective parameters**: the deterministic set of `key=value` pairs scaffold
  passes to the provider and records in provenance/report — now including both the raw name
  and the derived identifier.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A product scaffolded with a hyphenated/misspelled name (`Roquelike-DungeonCrawler`)
  compiles with **zero** `FS0010` identifier errors (down from the reported 121 occurrences).
- **SC-002**: For a scaffolded workspace, both `dotnet build` and `dotnet test` succeed
  without any manual edit to a generated identifier.
- **SC-003**: The author's raw name still appears verbatim in 100% of its legitimate
  contexts (`.fsproj`/`.slnx`, paths, string literals) after scaffold.
- **SC-004**: CI fails whenever a scaffolded product with a hyphenated name would not build
  or test (a reverted derivation is caught before merge, not by an author).
- **SC-005**: A name already valid as an F# identifier is forwarded unchanged (derivation
  is observably a no-op on the golden/deterministic contract).
- **SC-006**: The offline inner-loop test run stays green with the network-gated smoke
  self-skipped, adding no wall-clock cost to the inner loop.

## Assumptions

- The reference provider's templates (FS.GG.Rendering) will be updated to consume the
  derived-identifier parameter in identifier contexts and the raw name in string/path
  contexts; that adoption is a **cross-repo** dependency (FR-010) tracked as a
  `cross-repo:request` against FS.GG.Rendering and sequenced on the Coordination board.
  Until the provider adopts it, SDD forwards the derived parameter harmlessly (a provider
  that ignores an extra `--param` is unaffected).
- Recording both values reuses the existing `effectiveParameters` provenance/report
  surface; whether it needs a provenance schema change or fits schema v1 is a plan
  decision, but the schema is expected to remain additive.
- The derived-identifier parameter **key** is a generic, declared convention (no
  provider-specific value), consistent with `030` FR-002.
- The CI smoke reuses the existing network-gated composition-acceptance lane
  (`FSGG_SDD_ACCEPTANCE_REGISTRY`) and its build/run probe rather than adding a new
  always-on CI job, keeping the offline gate deterministic and provider-free.
- "Valid F# identifier / namespace" follows the F# language rules for identifiers and
  namespace segments; the exact normalization policy (drop vs. replace invalid chars,
  casing, leading-digit handling) is a plan decision constrained by FR-001/FR-003.
