# Contract: Schema Migration

## Scope

This contract defines how the normalized work-model feature classifies schema
versions in SDD-owned lifecycle artifacts and generated work-model views. It
does not define Governance-owned schemas.

## Version Fields

Every structured SDD artifact must declare a schema version:

- `.fsgg/project.yml`
- `.fsgg/sdd.yml`
- `.fsgg/agents.yml`
- structured front matter in work-item lifecycle Markdown where applicable
- `work/<id>/tasks.yml`
- `work/<id>/evidence.yml`
- `readiness/<id>/work-model.json`

The normalized model records both the artifact schema version and the generated
work-model `modelVersion`.

## Compatibility Statuses

| Status | Meaning | Diagnostic | Blocking |
|---|---|---|---|
| `current` | Version is fully supported by this generator. | None | No |
| `deprecated` | Version is readable but should be migrated. | `deprecatedSchemaVersion` | No |
| `unsupported` | Version is known but cannot be safely read. | `unsupportedSchemaVersion` | Yes |
| `malformed` | Version value is missing or invalid. | `malformedSchemaVersion` | Yes |
| `future` | Version is newer than this generator understands. | `futureSchemaVersion` | Yes by default |

Future versions may become non-blocking only when a compatibility rule explicitly
declares that the older generator can safely read the newer version. This
feature does not assume such a rule exists.

## Migration Diagnostics

Each schema diagnostic must include:

- diagnostic id;
- severity;
- affected artifact;
- raw version value when available;
- supported range;
- expected correction;
- related schema or model version when useful.

Example correction:

```text
Update work/002-normalized-work-model/tasks.yml to schemaVersion 1 or run the
documented migration before generating the work model.
```

## Generated Work-Model Versioning

`schemaVersion` identifies the JSON shape family. `modelVersion` identifies the
normalized model contract within that shape family. This feature plans
`modelVersion` `1.1.0` for the completed normalized work-model contract.

Rules:

- Patch-level model changes may clarify diagnostics without changing JSON
  fields.
- Minor model changes may add optional fields while keeping existing fields
  compatible.
- Major model changes require migration notes, golden fixture updates, and
  explicit downstream compatibility review.

## Governance Boundary

If Governance files are present, SDD records them as optional boundary entries.
Malformed or unsupported Governance schema versions are not interpreted by this
feature because Governance owns those schemas. SDD may only report that a
configured boundary path is syntactically invalid when the SDD-owned project
config points at it.
