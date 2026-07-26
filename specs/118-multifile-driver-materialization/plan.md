# Implementation Plan: Complete Driver Directory Materialization

## Design

Extend `DriverManifest` with a schema-v2 file record and validate the compact ordered `files` JSON
against `tree-sha256`. Keep schema v1 readable as a one-file compatibility projection.

Change the Commands project resource glob from `skills/**/SKILL.md` to every file below
`skills/**`. `DriverSkills` indexes embedded raw bytes by `(skill id, relative path)`, verifies a
closed set and raw digests, decodes text fail-closed, then emits deterministic no-clobber writes
for every agent root plus executable effects where declared.

Reuse the existing owner-sourced remediation seam. Because its expected write set becomes the
complete directory, `doctor` naturally identifies missing legacy auxiliaries. `upgrade` filters
the same verified plan to missing paths and amends `driverPaths` provenance for affected ids.

## Constitution Check

- Public parser types are declared in `.fsi` before their implementation and mirrored to the API
  surface baseline.
- The schema-v2 manifest is the structured machine contract.
- Verification precedes effects; malformed/unreadable/tampered input produces no partial writes.
- Real filesystem scaffold and upgrade tests cover the behavior change.
- The MVU effect boundary remains unchanged; only the pure plan grows.

No new dependency, schema publication, or provider-specific identity is introduced.

## Verification

- Focused `DriverManifestTests`, `DriverSkillsTests`, scaffold, and remediation tests.
- Full Artifacts, Commands, CLI, and Acceptance test projects.
- API-surface and locked-restore gates.
- Pack the CLI and inspect the package/build outputs before release.
