# Feature Specification: Command Domain Services

**Work Item**: FS.GG.SDD#686
**Tier**: 2 — internal architecture, no public contract change

## Intent

Keep command orchestration at the MVU handler boundary while moving cohesive,
pure policy out of the command hotspots. Existing internal command functions remain
stable façades so downstream command assembly and tests do not change.

## Requirements

- **FR-001**: Diagnostic construction MUST delegate lifecycle repair routing to a
  dedicated diagnostic-routing service.
- **FR-002**: Task-graph authoring MUST delegate skill normalization and deferral
  mirror classification to a pure task-graph policy service.
- **FR-003**: Evidence handling MUST delegate source currency, declaration identity,
  and task-obligation derivation to a pure evidence mutation service.
- **FR-004**: Scaffold handling MUST delegate owned-path classification, collision
  calculation, skeleton mutation, and tool-manifest rendering to a pure scaffold
  mutation service.
- **FR-005**: Existing command-facing function names and serialized artifacts MUST
  remain unchanged.

## Acceptance

- Direct contract tests prove each stable façade agrees with its service.
- Existing task/evidence property tests and scaffold/report golden suites pass without
  fixture updates.
- The Commands project builds without warnings and introduces no public API surface.
