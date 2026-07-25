# Feature 111 — Cohesive command-contract families

**Tier:** 1 (additive public source surface; no persisted or serialized contract change)

## Outcome

Callers can discover the broad `CommandTypes` contract through cohesive family modules without moving,
renaming, or retyping any existing public type. Existing binaries and source continue to use the
canonical `CommandTypes.*` names, while new F# source may use family aliases.

## Requirements

- **FR-001:** Expose every public `CommandTypes` type through exactly one cohesive family under
  `CommandTypes.CommandFamilies`.
- **FR-002:** Keep every existing canonical type at its current CLR/F# name. Family entries are type
  abbreviations over those types, never replacement definitions or wrappers.
- **FR-003:** Preserve all union cases, record fields, helper behavior, JSON names, ordering, report
  version, and serialized bytes.
- **FR-004:** Document the compatibility rules for additions, deprecations, moves, and serialized
  representations.
- **FR-005:** Keep implementation/signature parity and the committed API-surface baseline coherent.

## Acceptance scenarios

1. A caller can annotate values with aliases from invocation, artifact, lifecycle, guidance,
   scaffold, remediation, surface, lint, reporting, and runtime families.
2. Each family annotation has the same runtime `System.Type` as its canonical root type.
3. Serializing a report through the family alias produces byte-identical JSON.
4. The existing reflection surface baseline has no removal or renamed member.

## Non-goals

- Moving existing types to new CLR namespaces or modules.
- Changing `CommandReport` JSON, report versions, schemas, command behavior, or lifecycle routing.
- Splitting serialized records into command-specific wire formats.
