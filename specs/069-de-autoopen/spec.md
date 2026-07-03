# Feature Specification: Drop the blanket `[<AutoOpen>]` in `CommandWorkflow/`

**Feature ID**: 069-de-autoopen
**Branch**: `069-de-autoopen`
**Date**: 2026-07-03
**Roadmap**: closes [#76](https://github.com/FS-GG/FS.GG.SDD/issues/76) (US3)
**Source of truth**: this is the deferred **US3** of feature
[068-architecture-longterm](../068-architecture-longterm/spec.md). Feature 068
delivered US1/US2/US4/US5 (envelope, DU state, renames, partial purity) and
**deferred US3** to its own follow-up PR because a spike found a ~200-site
refactor with pervasive same-named helpers, ambiguous under warnings-as-errors.
This feature is that follow-up. FR/AC/SC ids below preserve the 068 numbering
(FR-006 / SC-004) so the trail is continuous.

## Context

Every file under `src/FS.GG.SDD.Commands/CommandWorkflow/` shares
`namespace FS.GG.SDD.Commands.Internal`. Sixteen of the eighteen are a flat
`[<AutoOpen>] module internal <Name>` — hundreds of helper functions land in one
namespace scope, and every inter-file dependency is expressed only implicitly by
fsproj compile order. Call sites therefore carry no provenance: reading
`renderX ()` gives no hint which of the sixteen modules defines it. `Drift.fs`
and `SeededSkills.fs` already model the target (plain `module internal`, no
`AutoOpen`).

## User Story

**US3 (P2)** — As a maintainer of `FS.GG.SDD.Commands`, I want the blanket
`[<AutoOpen>]` removed so each call site's helper is reachable by qualified module
path (or an explicit, file-scoped `open`), making inter-module dependencies
legible and no longer hostage to compile order — with zero change to any emitted
contract.

## Requirements

- **FR-006**: The blanket `[<AutoOpen>] module internal` scope across the
  `CommandWorkflow/` files MUST be removed in favor of qualified module access,
  **except** where a specific remaining `AutoOpen` is explicitly justified by a
  one-line comment; the reorganization MUST be internal-only and observable in no
  emitted contract.

## Acceptance Criteria

- **AC-1**: Given the `CommandWorkflow/` modules, when they are inspected, then
  the blanket `[<AutoOpen>]` on the internal modules is removed and call sites
  reference their helpers by qualified module path or a file-scoped explicit
  `open`.
- **AC-2**: Given the de-AutoOpened modules, when the solution is built, then it
  compiles with the warning count **not increased** vs the pre-change baseline
  (0) and the full test suite passes unchanged.

## Success Criteria

- **SC-004**: The blanket `[<AutoOpen>]` on `CommandWorkflow/` internal modules is
  removed (any surviving `AutoOpen` is individually justified with a comment), and
  the solution builds with no new warnings.

## Scope / Non-goals

- **In scope**: module attribute + call-site/`open` changes across the sixteen
  `CommandWorkflow/*.fs` files that still carry `AutoOpen`.
- **Out of scope**: any behavior change; any `.fsi` change; any rename (068
  already renamed the Parsing slabs); the deferred `projectIdFromRoot` edge
  removal (documented-and-deferred in 068, unchanged here).

## Edge Cases

- **De-AutoOpen creates an ambiguity or ordering conflict**: resolving it MUST NOT
  change behavior — only qualification / added `open` / ordering, never semantics.
  Where two opened modules expose the same name, the call site is qualified.
- **A genuinely ubiquitous foundation module** (e.g. `Foundation`) MAY retain an
  explicit file-scoped `open` rather than qualifying every site; a surviving
  `AutoOpen` is allowed only with a one-line justification (none is expected).

## Preserved contracts (Tier-2 guardrail)

Same as 068: no `.fsi`, no committed `**/*.baseline`, no readiness/JSON golden
fixture, no persisted schema/version, no exit-code/stream/rendering behavior
changes. Verified by an empty `git diff` over `src/**/*.fsi`, `**/*.baseline`, and
the golden fixtures, plus a green full test suite.
