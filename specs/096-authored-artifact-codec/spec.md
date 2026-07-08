# Feature Specification: Authored-Artifact Codec and Round-Trip Property

**Feature Branch**: `096-authored-artifact-codec`

**Created**: 2026-07-08

**Status**: Draft

**Input**: User description: "Retire the authored-artifact round-trip defect class (Gap A of ADR-0002; issues #161/#180/#181/#182 and the unfiled findings tracked under the Gap-A grouped issue (#201)). Every `evidence.yml` / `tasks.yml` emitter is hand-written string interpolation living in a different assembly from its parser, with no shared field list and no round-trip test, so the parsed field set and the emitted field set silently diverge — fields authored by a human are read into the model and then dropped on the next re-render, absent optionals are re-rendered as invented values, and a bare-null scalar reads as the string `\"null\"` and defeats a gate. Make read/write asymmetry structurally impossible: a single field-list-driven codec per authored artifact, null-aware reads where absence gates, and an FsCheck round-trip property `render(parse(x)) = x`."

## Overview

The lifecycle authors two structured artifacts by hand: `evidence.yml` and
`tasks.yml`. Each is *parsed* in `FS.GG.SDD.Artifacts`
(`LifecycleArtifacts/Evidence.fs`, `LifecycleArtifacts/Task.fs`) and *rendered*
in `FS.GG.SDD.Commands` (`CommandWorkflow/HandlersEvidence.fs`,
`CommandWorkflow/TaskGraphAuthoring.fs`). The two halves are independent
hand-maintained mirrors: the parser reads a set of keys via `tryScalarAt`, and
the renderer emits a separate set of keys via string interpolation, in a
different assembly, with no shared declaration of "the fields of this artifact."

Nothing makes the two sets agree. When they disagree the failure is silent:

- **Read-not-written → silent deletion.** `sourceRefs[]` parses six fields
  (`id, kind, path, uri, digest, relatedSourceId, result`) and renders four —
  `id`, `digest`, `relatedSourceId` are read into the model and unrecoverably
  deleted on the first re-render (#181). `evidence.yml` `lifecycleNotes` is read
  and replaced with a canned line on every run. `tasks.yml` front-matter `title`
  reverts to the humanized work id.
- **Written-not-read → invented value.** Absent snapshot `digest`/`schemaVersion`
  are rendered as `""`/`"1"` (#182); `tasks.yml` `publicOrToolFacingImpact: false`
  is re-rendered as `true` on every run.
- **Null-unaware read → gate bypass.** `syntheticDisclosure.standsInFor`/`reason`
  are read with the null-unaware reader, so a bare `null` parses as the string
  `"null"` and the undisclosed-synthetic gate never fires — at both the evidence
  and verify stages (#180). Six of ~123 scalar reads are null-aware today.

These are one defect class with one root cause: **there is no structural
coupling between the parsed field set and the emitted field set, and no test
that exercises the round trip.** Point-fixes (#161 fixed four top-level scalars;
the nested scalars were never migrated) leave the hole open at every field the
fix did not name.

This feature closes the class. Each authored artifact gets a **single codec** —
one field-list-driven definition that both reads and writes every field — so a
field cannot be parsed without also being emitted (and vice versa). Reads that
gate on `Option.isNone` use the null-aware reader. An **FsCheck round-trip
property** `render(parse(x)) = x` per artifact makes any future asymmetry a red
test rather than a field-lost-in-the-wild.

This is Gap A / invariant 1 of **ADR-0002** and the first feature of that
workstream.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A re-render preserves everything the author wrote (Priority: P1)

An author writes an `evidence.yml` whose `sourceRefs[]` carry `id`, `digest`,
and `relatedSourceId` (real evidence provenance), and adds `lifecycleNotes`.
They run `fsgg-sdd evidence` again (e.g. after editing a declaration). Every
field they authored survives byte-for-byte; only the fields the tool legitimately
regenerates (source snapshots, canonical status) change.

**Why this priority**: This is the data-loss defect (#181) — authored content
silently destroyed — and it fires on the most common operation (a re-run). It
is the reason the class is P1.

**Acceptance**:
1. **Given** an `evidence.yml` with a fully-populated `sourceRef`
   (`id`/`kind`/`path`/`uri`/`digest`/`relatedSourceId`/`result`) and authored
   `lifecycleNotes`, **When** `fsgg-sdd evidence` re-runs, **Then** all seven
   `sourceRef` fields and the authored `lifecycleNotes` are present and
   unchanged in the re-rendered file.
2. **Given** a `tasks.yml` with `publicOrToolFacingImpact: false` and a custom
   front-matter `title`, **When** `fsgg-sdd tasks` re-runs, **Then** the impact
   flag remains `false` and the title is unchanged.

### User Story 2 - Absence stays absence (Priority: P1)

An author writes an `evidence.yml` source snapshot with no `digest` and no
`schemaVersion` (they are optional). A re-render does not invent `""` or `1` for
them; an omitted optional stays omitted, and no trailing-whitespace line is
emitted.

**Why this priority**: The invented-value defect (#182) both violates the
omit-when-`None` convention #178 established and, via the null-unaware reader,
produces a permanent unfixable stale-source warning. It is the write-side twin
of Story 1.

**Acceptance**:
1. **Given** a snapshot with `Digest = None`, **When** it is rendered, **Then**
   no `digest:` line is emitted (not `digest: ` and not `digest: ""`).
2. **Given** any optional scalar absent in the source, **When** the artifact
   round-trips, **Then** the re-rendered bytes contain no line for that key.

### User Story 3 - A bare-null scalar is absence, not the string "null" (Priority: P1)

An author writes `syntheticDisclosure:` with `standsInFor: null` and
`reason: null` under a `synthetic: true`, `result: pass` declaration. The
undisclosed-synthetic gate fires (exit 1) rather than treating `"null"` as a
real disclosure.

**Why this priority**: This is a *gate bypass* (#180) — synthetic evidence
passing as real — the exact failure the gate exists to prevent, and it holds at
both the evidence and verify stages.

**Acceptance**:
1. **Given** a synthetic declaration whose `syntheticDisclosure` children are
   the bare tokens `null` / `Null` / `NULL` / `~` / empty, **When**
   `fsgg-sdd evidence` runs, **Then** `evidence.undisclosedSyntheticEvidence`
   fires and the command blocks (exit 1).
2. **Given** the same declaration, **When** `fsgg-sdd verify` runs, **Then**
   the same gate fires (parity across stages).
3. **Given** a re-render of such a file, **When** it round-trips, **Then** the
   bare-null child is not re-emitted as the quoted string `"null"`.

### User Story 4 - Any field combination survives the round trip (Priority: P1)

For every authored artifact, an arbitrary well-formed instance parsed and then
re-rendered yields a model equal to the original — regardless of which optional
fields are present or absent. No hand-picked fixture is required to discover a
lost field; the property covers the whole field space.

**Why this priority**: This is the invariant that makes the class
*unrepresentable* rather than merely fixing today's instances. Without it, the
next field added to an artifact reopens the gap.

**Acceptance**:
1. **Given** a generator over well-formed `evidence.yml` / `tasks.yml` models,
   **When** the round-trip property `parse(render(m)) = m` runs, **Then** it
   holds for all generated instances.
2. **Given** a field added to an artifact model without a corresponding codec
   entry, **When** the suite runs, **Then** a test fails (the codec and the
   type are structurally coupled).

### Edge Cases

- A YAML scalar that is legitimately the *string* `"null"` (quoted) must survive
  as the string, distinct from the bare `null` token.
- CRLF vs LF authored line endings must round-trip under the existing
  normalization, not desync the codec.
- An artifact the tool regenerates in part (source snapshots, digests) must
  distinguish "author wrote this" from "tool derives this" so the property
  holds only over the authored subset; tool-derived fields are excluded from the
  round-trip equality by construction, not by silent overwrite.
- The markdown authoring artifacts (`spec.md`, `plan.md`, `checklist.md`,
  `clarifications.md`, `charter.md`) are surgically edited, not full parse→render,
  and are **out of scope** here (they do not exhibit the field-count asymmetry);
  see Out of Scope.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each authored structured artifact (`evidence.yml`, `tasks.yml`)
  MUST be read and written through a single field-list-driven codec such that a
  field cannot be parsed without being emitted, and cannot be emitted without
  being parsed, for the authored (non-tool-derived) field set.
- **FR-002**: Every optional scalar MUST be omitted from output when `None`
  (no empty-value line, no invented default), matching the convention
  established for declaration optionals in #178.
- **FR-003**: Every read whose absence is semantically meaningful — i.e. a gate
  keys on `Option.isNone` — MUST use the null-aware reader, so a bare-null YAML
  scalar (`null`/`Null`/`NULL`/`~`/empty) reads as `None`, not as the string
  `"null"`. At minimum this covers `syntheticDisclosure.standsInFor`/`reason`
  and any snapshot `digest` that gates staleness.
- **FR-004**: The undisclosed-synthetic gate MUST fire on a bare-null
  `syntheticDisclosure` at both the evidence and verify stages.
- **FR-005**: A property-based round-trip test `parse(render(m)) = m` MUST exist
  per authored artifact, over a generator that ranges every optional field
  present/absent.
- **FR-006**: Tool-derived fields (source snapshots, digests, canonical
  status/notes the tool owns) MUST be explicitly designated as tool-owned, so
  the round-trip property is defined over the authored subset and regeneration
  of tool-owned fields is not silent data loss.
- **FR-007**: Adding a field to an authored-artifact model without a
  corresponding codec entry MUST fail a test (structural coupling of type and
  codec), so a future field cannot silently reopen the class.
- **FR-008**: The change MUST preserve byte-idempotence for artifacts already in
  the field: re-rendering an unchanged authored file writes no change.
- **FR-009**: No change to the JSON automation contract, exit codes, or stream
  routing of any command results from this feature except where a gate that was
  silently bypassed (#180) now correctly fires; that behavior change owes a
  migration note.

### Key Entities

- **Artifact codec**: the single definition, per authored artifact, that both
  reads a parsed model from YAML and writes it back, over one field list.
- **Authored field set**: the fields a human owns and the codec must
  round-trip.
- **Tool-owned field set**: the fields the tool regenerates (snapshots,
  digests, canonical status), excluded from the round-trip equality.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The unfiled Gap-A findings tracked under the Gap-A grouped issue (#201)
  (the `lifecycleNotes` clobber, the `publicOrToolFacingImpact` flip, the
  `tasks.yml` title revert, the `sourceRef` bare-null corruption) no longer
  reproduce, and #180/#181/#182 are closed.
- **SC-002**: A round-trip property covers `evidence.yml` and `tasks.yml`; a
  deliberately introduced read/write asymmetry reddens it.
- **SC-003**: The count of null-unaware reads at gate-bearing sites is zero.
- **SC-004**: A field added to an authored-artifact model with no codec entry
  fails the build/test.

## Assumptions

- The generated-view serializers (`Serialization.fs`, the readiness-view JSON
  writers) are out of scope; they are one-way projections, not authored
  round-trips, and are addressed by ADR-0002 invariant 2.
- FsCheck (or an equivalent property library) may be added to the test
  dependencies; there is currently no property-based testing in the repo.

## Out of Scope

- The markdown authoring artifacts (`spec.md`, `plan.md`, `checklist.md`,
  `clarifications.md`, `charter.md`), which are surgically edited (append-missing
  sections, replace machine sections) and do not exhibit the field-count
  asymmetry.
- The report/version contract (ADR-0002 invariant 2, #198), diagnostic dedupe
  and dropped-diagnostic surfacing (invariant 3, #191/#193), path containment
  (invariant 4, #185/#196), and work-model field semantics (invariant 5,
  #189/#192). Each is its own feature under ADR-0002.

## Deferred

- Whether the codec abstraction is later extended to the generated-view
  serializers to unify the two serialization styles is a separate decision;
  this feature scopes only authored artifacts.
