# Feature Specification: The Driver Skill Class, Known But Not Yet Enforced

**Feature Branch**: `item/591-driver-skill-class`

**Created**: 2026-07-19

**Status**: Draft

**Input**: FS.GG.SDD#591 — "teach `Fsgg.Registry` the skill-registry `driver` class (scope: driver,
owner: .github, composed materializes-when) and publish the CLI — step 1 of ADR-0054". Filed from
`FS-GG/.github`, resolving `.github#1224`. Contract: `skill-registry` (ADR-0015, ADR-0017, ADR-0054).

## Overview

`FS-GG/.github` `registry/skills.yml` is the org's authoritative skill catalog, and `Fsgg.Registry`
(shipped in the `FS.GG.SDD.Cli` tool) is the typed validator that owns it (feature 104). Today a row's
`scope` is one of exactly two producer classes — `process` or `product`. **ADR-0054** adds a third,
the **driver** class: a skill materialized in a scaffolded workspace when *two* producer families are
both present, owned not by a producer repo but by `.github` itself, and materialized under a **composed**
predicate.

This feature is **step 1 of the ADR-0037 publish-before-flip sequence** (over ADR-0015 §3): SDD teaches
the validator the new vocabulary and publishes a CLI carrying it *before* `.github` bumps its schema and
pins the validator (the second, `.github`-side PR is blocked on this one). The two never span one PR.

### The hard constraint that shapes the whole feature (ADR-0037 §3)

The published validator **must still accept `registry/skills.yml` exactly as it stands at `.github`
HEAD.** The `skills.yml` document still declares `schemaVersion: 1` with the `mirrored` 1→2 bump owed
and unpaid, so **step 1 may reject nothing the live document accepts**. The driver rule is therefore
*known* here and *enforced* only in step 2, against the bumped `schemaVersion`.

Concretely, "known but not enforced" means:

- `scope: driver` becomes a **recognized, accepted** value — a `driver` row validates rather than
  drawing today's `UnknownComponent` diagnostic. This is a strict **loosening**: the validator accepts
  strictly more documents, so every document valid before this change stays valid.
- A **non-producer `owner`** (`.github`) and a **composed `materializes-when`** (the AND of two producer
  predicates) require *no code change at all*: the validator already accepts any non-blank `owner` and
  any present, non-blank `materializes-when`, and asserts nothing about what either *means* (feature 104
  deliberately checks the predicate is present, never what it evaluates to). This feature pins that
  acceptance with tests so a future tightening cannot silently break the driver shape.

What this feature is **not**: it does not bump the `skill-registry` `schemaVersion`; it does not pay the
owed `mirrored` 1→2 bump (sequenced to step 2 with the flip); and it adds no *enforcement* — no rule that
a `driver` row *must* carry a composed predicate or a `.github` owner. Those belong to step 2's bumped
schema. Adding them here would violate the ADR-0037 §3 constraint by rejecting shapes today's HEAD omits.

## Requirements

### Functional

- **FR-001**: `Registry.validateSkillRegistry` MUST recognize `driver` as a valid skill `scope`
  alongside `process` and `product` — a row whose only novelty is `scope: driver` MUST validate.
- **FR-002**: The change MUST be a monotone loosening: no document that validates before this change may
  become invalid after it. No `schemaVersion` bump; no new rejection of any currently-accepted document.
- **FR-003**: A `driver` row carrying a **non-producer `owner`** (`.github`) and a **composed
  `materializes-when`** (an AND of two producer predicates) MUST validate. The feature adds no `owner`- or
  `materializes-when`-shape *enforcement*; it relies on the existing non-blank rules and pins them.
- **FR-004**: An unknown `scope` (none of `process` / `product` / `driver`) MUST remain an
  `UnknownComponent` diagnostic, and its message MUST enumerate the three accepted scopes.

### Acceptance Criteria

- **AC-001**: `validateSkillRegistry` returns `Valid` for a document whose one row has `scope: driver`.
- **AC-002**: `validateSkillRegistry` returns `Valid` for a `driver` row with `owner: .github` and a
  composed `materializes-when` such as `has fs-gg-sdd-* and has fs-gg-feedback-*`.
- **AC-003**: An unknown scope still yields `UnknownComponent`, and the diagnostic message names
  `driver` among the expected scopes.
- **AC-004**: `process` and `product` still validate (regression witness for the monotone loosening,
  FR-002).

## Out of Scope

- The `schemaVersion` 1→2 bump and the owed `mirrored` bump (step 2, `.github`-side).
- Any *enforcement* that a `driver` row must carry a composed predicate or a `.github` owner (step 2).
- The YAML load edge and CLI dispatch — unchanged; `scope`/`owner`/`materializes-when` are already
  parsed as free strings, so no edge change is needed to carry a `driver` row through to the validator.
