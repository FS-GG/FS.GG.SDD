# Contract: Bootstrap Assertion Families

This contract enumerates the verification families the feature must cover beyond
the happy-path lifecycle run, mapped to functional requirements and success
criteria. They are realized as cases in `LifecycleSmokeTests.fs` over disposable
projects, plus the migration guide's documented non-destructive guarantee
(verified by inspection in the quickstart validation, since migration is a
documentation deliverable, not a command).

## A. No-Governance lifecycle (FR-005, SC-002)

- A full `init`→`ship` + `agents` + `refresh` run completes with zero
  `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, or `.fsgg/tooling.yml` present.
- The run asserts no Governance file was created and none was required.

## B. Determinism (FR-006, SC-003)

- Two full lifecycle runs over identical authored inputs produce byte-identical
  machine-readable readiness outputs after temp-root normalization.

## C. Documented next-action chain (FR-014, SC-004, SC-008)

- Each stage's emitted next action matches the documented quickstart order.
- The authored source and generated readiness view named in the quickstart for
  each stage match what the stage actually writes.

## D. Governance present-but-incomplete (FR-011, SC-006)

- With deliberately present-but-incomplete `.fsgg/policy.yml` /
  `.fsgg/capabilities.yml` / `.fsgg/tooling.yml` placed in the temp project,
  every SDD lifecycle command still succeeds and SDD performs no Governance
  evaluation or enforcement.

## E. No Rendering / no monorepo (FR-013, SC-007)

- The lifecycle run depends only on the SDD projects; no FS.GG.Rendering package,
  monorepo checkout, or runtime product template is referenced or required.

## F. Migration non-destructiveness (FR-008, FR-009, SC-005) — documentation guarantee

- The migration guide's steps are additive: `init` plus authoring through
  `fsgg-sdd` commands; `specs/` and `.specify/` are never deleted, rewritten,
  reordered, or normalized.
- The guide states the steps are safe to re-apply and explains represent-or-defer
  handling for Spec Kit artifacts with no SDD equivalent.
- Verified by review against the consumer-docs contract during quickstart
  validation; no command performs migration (FR-012).

## Requirement coverage map

| Requirement | Covered by |
|---|---|
| FR-001 quickstart init→ship no Governance | quickstart doc; smoke happy path |
| FR-002 canonical stage order + source/view per stage | quickstart doc; family C |
| FR-003 readiness artifacts + agents/refresh currency | quickstart doc; smoke artifacts |
| FR-004 automated smoke creates disposable project | smoke harness |
| FR-005 smoke runs with no Governance | family A |
| FR-006 determinism | family B |
| FR-007 migration mapping | migration doc; family F |
| FR-008 preserve Spec Kit, no deletion | migration doc; family F |
| FR-009 additive, re-appliable, no destructive removal | migration doc; family F |
| FR-010 Governance added after init | adoption doc |
| FR-011 commands usable with Governance present/absent/incomplete | adoption doc; family D |
| FR-012 no new stage/command/schema | plan/data-model; surface baselines unchanged |
| FR-013 no Rendering/monorepo/runtime templates | family E; doc invariants |
| FR-014 docs reflect emitted order + next actions | family C |
| FR-015 generated views are outputs, currency from refresh | doc invariants |
| FR-016 no Governance routing/freshness/gate/audit/release | family A/D; doc invariants |
