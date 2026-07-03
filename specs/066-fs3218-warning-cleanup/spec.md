# Feature Specification: Clear the FS3218 / FS3262 warning debt and drop the ratchet exemption

**Feature Branch**: `066-fs3218-warning-cleanup`

**Created**: 2026-07-03

**Status**: Draft

**Input**: FS.GG.SDD roadmap issue #85 §2 — the deferred **warning-debt half** of the feature-064 build/CI-hygiene work. Feature 064 flipped the full compiler ratchet (`TreatWarningsAsErrors=true`) but had to exempt two pre-existing warning classes via `WarningsNotAsErrors` so the ratchet could land without a warning-fixing campaign (spec 064 Assumptions / FR-009 "no warning-fixing campaign"). This feature pays that debt down and removes the exemption. Repo-local, not cross-repo.

**Change Tier**: Tier 2 (source-layout / build-config change). This feature aligns implementation argument names to their `.fsi` signatures and replaces two dead null-guards, then removes `FS3218;FS3262` from the repo's `WarningsNotAsErrors` line. It introduces **no** change to any `fsgg-sdd` CLI output, JSON automation contract, persisted schema, or golden baseline, and **no** change to any `.fsi` public surface. The managed org files (`Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`) stay byte-identical to `FS-GG/.github` `dist/dotnet/`; the only build-file edit is to the repo-owned `Directory.Build.local.props`.

## Overview

The 2026-07-02 review and feature 064 established a full compiler warning ratchet:
every compile-phase F# warning is a build error, so a new warning class cannot
silently re-accumulate. To land that ratchet without a large warning-fixing
campaign, feature 064 exempted the two pre-existing classes it inherited:

- **FS3218 (×452 raw / 226 canonical)** — signature/implementation argument-name
  mismatch, a byproduct of the 062/063 `.fsi` splits. All of them live in a single
  file, `src/FS.GG.SDD.Commands/CommandReports.fs`, a pure re-export facade whose
  functions are written `let f a0 a1 … = Inner.f a0 a1 …` while the committed
  `CommandReports.fsi` names the arguments (`id`, `severity`, `path`, …). The
  compiler uses the signature names and warns on every mismatch.
- **FS3262 (×4 raw / 2 canonical)** — nullness: `Option.ofObj` applied to a value
  the compiler already knows is non-null under `Nullable enable`. Both sites are in
  `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs` (a `version: string`
  and a `text: string` parameter), where the `ofObj |> defaultValue ""` guard is
  dead code.

While these two classes are exempted, the ratchet has a hole: a new FS3218 or
FS3262 anywhere in the tree is demoted to a warning and can accrete unnoticed.
This feature closes the hole by fixing the existing occurrences and removing the
exemption, so the ratchet covers every compile-phase warning class with no
carve-outs (except the deliberately-retained restore-phase / API-gate / audit
exemptions, which are unrelated).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The warning ratchet covers FS3218 and FS3262 (Priority: P1)

A contributor introduces a new argument-name mismatch or a redundant `ofObj`
null-guard. Today the ratchet exempts those classes, so the build stays green and
the debt accretes.

After this feature the exemption is gone: a new FS3218 or FS3262 fails the build
like every other compile-phase warning.

**Why this priority**: This is the whole feature — it removes the last two holes
in the 064 ratchet. It is P1 because without the exemption drop there is no
deliverable; the code cleanup (US2) exists only to let the exemption be removed
without reddening the build.

**Independent Test**: With the exemption removed, introduce a deliberate FS3218
(rename one impl arg away from its `.fsi`) and confirm the build fails; revert and
confirm it is green.

**Acceptance Scenarios**:

1. **Given** `Directory.Build.local.props`, **When** inspected, **Then** its
   `WarningsNotAsErrors` line no longer contains `FS3218` or `FS3262`.
2. **Given** the cleaned tree, **When** a clean rebuild runs under the full
   ratchet, **Then** it is green (zero FS3218, zero FS3262).
3. **Given** the cleaned tree, **When** a new FS3218 or FS3262 is deliberately
   introduced, **Then** the build fails (the ratchet now covers the class).

---

### User Story 2 - The existing FS3218 / FS3262 occurrences are cleared, behaviour-preserving (Priority: P1)

The exemption cannot be removed while the tree still emits these warnings. As part
of this feature every occurrence is fixed, and the fix is proven to change no
behaviour and no public surface.

**Why this priority**: The exemption drop (US1) cannot land until the tree is
clean, and behaviour-preservation is the load-bearing risk. Co-P1 with US1;
neither ships without the other.

**Independent Test**: Fix the occurrences, then run the full suite and confirm it
is green and every `.fsi` signature and deterministic/golden baseline is
byte-identical; confirm `fsgg-sdd validate` stays `overallPassed`.

**Acceptance Scenarios**:

1. **Given** the FS3218 fix (impl arg names in `CommandReports.fs` aligned to
   `CommandReports.fsi`), **When** the module's public surface is compared,
   **Then** `CommandReports.fsi` is byte-identical (only impl parameter *names*
   changed — no type, arity, or order change).
2. **Given** the FS3262 fix (dead `ofObj` guards replaced with direct use of the
   known-non-null value), **When** the affected `HandlersScaffold` behaviour is
   exercised, **Then** it is unchanged for every input (a non-null string and the
   guard's `defaultValue ""` fallback are observationally identical).
3. **Given** the full cleanup, **When** the suite runs, **Then** it is green with
   zero golden/deterministic baseline drift.

### Edge Cases

- **Parameter-name shadowing**: aligning an impl arg to a signature name such as
  `id` shadows `FSharp.Core.Operators.id` within that function scope. Because each
  facade body only forwards its arguments, no shadowed operator is used, so no new
  warning or behaviour change results.
- **A signature that names an argument the impl currently leaves positional**: the
  fix always renames the *implementation* to match the *signature* (the committed
  `.fsi` is the contract and must not move).
- **A future `ofObj` that is genuinely nullable**: the FS3262 fix applies only to
  the two sites the compiler proves non-null; it does not blanket-remove `ofObj`
  where the value can actually be null.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every FS3218 occurrence MUST be cleared by renaming the
  implementation argument names in `src/FS.GG.SDD.Commands/CommandReports.fs` to
  match the argument names declared in `src/FS.GG.SDD.Commands/CommandReports.fsi`.
- **FR-002**: `src/FS.GG.SDD.Commands/CommandReports.fsi` MUST stay byte-identical
  — the public surface does not change; only implementation parameter names move.
- **FR-003**: Every FS3262 occurrence MUST be cleared by replacing the
  `Option.ofObj`-on-a-known-non-null-value guard in
  `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs` with direct use of
  the value, preserving observable behaviour.
- **FR-004**: `FS3218` and `FS3262` MUST be removed from the `WarningsNotAsErrors`
  line in `Directory.Build.local.props`, and the accompanying comment MUST be
  updated to reflect that the classes are now cleared and covered by the ratchet.
- **FR-005**: After the cleanup and exemption drop, a clean rebuild MUST be green
  under the full ratchet (both classes now promoted to errors), the full test
  suite MUST stay green, every `.fsi` signature MUST be unchanged, every
  deterministic/golden baseline MUST be byte-identical, and `fsgg-sdd validate`
  MUST stay `overallPassed`.
- **FR-006**: The managed org build files (`Directory.Build.props`,
  `Directory.Packages.props`, `.config/dotnet-tools.json`) MUST stay
  byte-identical to the `FS-GG/.github` source (the `build-config-drift` gate
  passes unchanged) — only the repo-owned `Directory.Build.local.props` is edited.

### Key Entities

- **`CommandReports.fs`**: the re-export facade whose forwarding functions get
  their parameter names aligned to the signature; the only FS3218 site.
- **`HandlersScaffold.fs`**: carries the two dead `ofObj` null-guards; the only
  FS3262 site.
- **`Directory.Build.local.props`**: the repo-owned MSBuild overrides file whose
  `WarningsNotAsErrors` line drops the two exemptions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A clean rebuild emits **zero** FS3218 and **zero** FS3262 warnings
  (was 452 / 4 at feature start).
- **SC-002**: `WarningsNotAsErrors` in `Directory.Build.local.props` no longer
  lists `FS3218` or `FS3262`; the retained restore-phase / API-gate / audit
  exemptions are untouched.
- **SC-003**: The full test suite is green with **zero** `.fsi` signature changes
  and **zero** golden/deterministic baseline changes attributable to the cleanup.
- **SC-004**: With the exemption removed, a deliberately introduced FS3218 or
  FS3262 fails the build (the ratchet demonstrably covers the classes).
- **SC-005**: The managed org build files stay byte-identical to `FS-GG/.github`
  `dist/dotnet/` (the `build-config-drift` gate passes unchanged).

## Assumptions

- **Signature is the contract**: the committed `.fsi` argument names are correct
  and load-bearing; FS3218 is fixed by moving the implementation to them, never by
  editing the signature.
- **Both FS3262 sites are provably non-null**: the compiler flags them precisely
  because the parameter types (`string` under `Nullable enable`) guarantee
  non-null; replacing the guard with direct use is behaviour-preserving.
- **No CLI/schema surface**: this feature touches only implementation-file
  parameter names, two expression bodies, and the repo-owned build-config
  overrides — no `fsgg-sdd` command, automation contract, persisted schema, or
  public `.fsi` surface changes.
- **Scope inheritance**: this feature is exactly §2 of issue #85 (the warning-debt
  half); §1 (the format gate) already shipped as feature 065.
