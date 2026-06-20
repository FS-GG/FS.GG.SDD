# Quickstart & migration validation (T019, T014)

Date: 2026-06-20

## T019 — Quickstart end-to-end validation (SC-001, SC-004, SC-007, SC-008)

The quickstart (`docs/quickstart.md`) was validated against real command output
on two surfaces:

- **In-process lifecycle smoke** (`LifecycleSmokeTests.fs`): drives
  `init → charter → … → ship` plus `agents` and `refresh` and asserts every stage
  succeeds, writes its documented authored source, and refreshes/reports its
  documented generated readiness view.
- **CLI process smoke** (`cli-smoke.txt`): the shipped `FS.GG.SDD.Cli` executable
  driven `init` through `ship` plus `agents`/`refresh` over a disposable project.

Confirmations:

- **SC-001 / SC-008 (init→ship with no Governance):** both smokes complete the
  full lifecycle with no `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, or
  `.fsgg/tooling.yml` present or required. CLI ship reports `shipReady`.
- **SC-004 (documented stages & next-action pointers match command behavior):**
  the quickstart's canonical order and next-action column match the smoke's
  asserted chain. The smoke test `smoke confirms the canonical lifecycle order
  map` pins `charter→specify→…→ship` (with `agents`/`refresh` terminal), and
  `smoke emits the documented next-action pointer per stage` pins each stage's
  emitted next-action id (charter–tasks → `nextLifecycleCommand`; analyze →
  `analysis.next.implement`; evidence → `evidence.next.verify`; verify →
  `verify.next.ship`; ship → `ship.next.protectedBoundary`; agents →
  `agentsGenerated`). These match the quickstart verbatim (T003 map).
- **SC-007 (no FS.GG.Rendering / no monorepo):** the smoke test `smoke depends
  only on the SDD projects with no Rendering or monorepo` asserts the run names no
  FS.GG.Rendering package, runtime template, or monorepo checkout; the quickstart
  prerequisites state the same.

Generated readiness views are framed in the quickstart as outputs whose currency
comes from `fsgg-sdd refresh`/`fsgg-sdd agents`, not from file presence (FR-015),
and all Governance references are optional/advisory (FR-016).

## T014 — Migration guide review by inspection (FR-008, FR-009, FR-012)

Reviewed `docs/migration-from-spec-kit.md` against
`contracts/consumer-docs.md` and family F:

- **Additive only (FR-008):** every step is `fsgg-sdd init` plus authoring native
  sources through the `fsgg-sdd` commands. No step deletes, rewrites, reorders, or
  normalizes `specs/` or `.specify/` content; the "Starting point" and
  "Coexistence" sections state both are left unchanged.
- **Re-apply safety + represent-or-defer (FR-009):** the "Coexistence" section
  states the steps are safe to re-apply; the "No-equivalent handling" section
  says to represent in the nearest SDD source or explicitly defer, never deleting
  authored Spec Kit content.
- **No new migration command (FR-012):** the guide states explicitly there is no
  migration command; migration is `init` plus the existing lifecycle commands.
  The artifact mapping table's "Authored through" column names only existing
  `fsgg-sdd` commands.
