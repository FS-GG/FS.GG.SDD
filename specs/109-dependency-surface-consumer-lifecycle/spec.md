# Feature 109: Dependency-surface consumer lifecycle

## Problem

Generated products can author `framework:` and `blocked-on-framework:` references, but
`dependency-surface --update` discovers only captures that already exist. A clean product therefore
cannot bootstrap the authoritative package surface, and `--check` silently examines zero targets.

## Requirements

- **FR-001** The command discovers package/version targets from every valid `work/**/plan.md`
  framework reference, resolving an omitted version from the workspace CPM pins, including the
  coherent-set `Version="$(PropertyName)"` form.
- **FR-002** Explicit command parameters and existing committed captures remain supported and are
  unioned with discovered targets.
- **FR-003** `--update` writes captures for every discovered target whose real restored surface is
  readable; it never reads `docs/api-surface/**/*.fsi` as a package oracle.
- **FR-004** `--check` fails when a readable discovered target has no matching committed capture, as
  well as when a committed capture has drifted.
- **FR-005** An unreadable real package surface remains advisory and never becomes a false negative
  verdict.
- **FR-006** `analyze` remains hermetic and reads only committed captures.
- **FR-007** Consumer guidance and CI name the parameter-free discovery commands used for initial
  capture, pin refresh, and drift checking.

## Acceptance

- **SC-001** A generated-product-shaped fixture with a real restored package reference creates its
  capture with parameter-free `dependency-surface --update`.
- **SC-002** The resulting committed capture lets `analyze` accept a real symbol and reject a
  missing symbol.
- **SC-003** Deleting that required capture makes parameter-free `--check` fail until `--update`
  recreates it.
