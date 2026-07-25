# Command contract compatibility

`FS.GG.SDD.Commands.CommandTypes` remains the compatibility facade for the command workflow's public
types. Its nested `CommandFamilies` module is an additive navigation surface: it groups those same
types by responsibility without moving them.

## Families

| Family | Responsibility |
|---|---|
| `Invocation` | command identity, request, output format, and outcome |
| `Artifacts` | write ownership, changes, and generated-view state |
| `Lifecycle` | charter-to-ship summaries and lifecycle status |
| `Guidance` | agent guidance and refresh summaries |
| `Scaffold` | provider invocation and scaffold results |
| `Remediation` | reconciliation, doctor, and upgrade |
| `Surfaces` | authored/dependency API-surface classification |
| `Lint` | artifact grammar and lint results |
| `Reporting` | help, routing, governance facts, and the aggregate report |
| `Runtime` | MVU effects, effect results, model, and messages |

## Compatibility rules

1. Existing `CommandTypes.*` definitions are canonical. They are not moved, renamed, or replaced by a
   family type. A family entry is an F# type abbreviation over the canonical definition, so adopting a
   family annotation is optional and does not create a new runtime type.
2. Add a new type to the family that owns its behavior. Cross-family aggregate types may reference
   canonical types from other families; that dependency does not transfer ownership.
3. Deprecation starts on the canonical type and its alias together. Removal or relocation is a breaking
   public-contract change and requires the repository's normal major-version and migration process.
4. Family grouping never changes wire behavior. JSON property names, union-case tokens, ordering,
   omission rules, and `reportVersion` remain owned by `CommandSerialization` and their golden gates.
5. `CommandTypes.fsi` is authoritative for visibility. Every alias must have a matching implementation
   declaration, and the signature/surface gates must remain green.

This policy intentionally favors stable canonical names over physically relocating definitions. The
large facade can therefore be navigated by family without making source organization a binary or
serialized migration.
