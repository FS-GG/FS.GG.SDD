# Contract: `.fsgg/project.yml` — `project.testFramework` (schema v1, additive)

**Owner**: FS.GG.SDD (generic lifecycle product). **File**: `.fsgg/project.yml`.
**Schema version**: `1` (unchanged — additive optional field).

## Field

```yaml
schemaVersion: 1
project:
  id: my-app
  defaultWorkRoot: work
  testFramework: expecto        # OPTIONAL, new in this feature
sdd:
  config: .fsgg/sdd.yml
  agents: .fsgg/agents.yml
```

| Property | Value |
|----------|-------|
| Path | `project.testFramework` |
| Type | scalar string |
| Required | no |
| Default when absent | `None` → neutral test skill `automated-tests` |
| Blank/whitespace value | treated as absent (neutral skill) |
| Validation / allow-list | none — any non-blank string is accepted verbatim |
| Parsed onto | `ProjectLifecycleConfig.TestFramework : string option` |

## Rules

1. **Generic only (FR-007)**: the field carries a framework *name* the author
   chooses. SDD introduces **no** provider/rendering/template-specific package id,
   template id, path, or docs URL, and maintains **no** closed list of approved
   frameworks.
2. **Additive & backward-compatible**: pre-existing `project.yml` files without
   the field stay valid and parse to `TestFramework = None`. No migration, no
   version bump.
3. **No diagnostic surface change**: presence/malformed-config diagnostics emitted
   by `projectDiagnostics` are unchanged; the new optional field adds no new
   diagnostic code.
4. **`init`/`scaffold` do not author this field**: the generated skeleton's
   `project.yml` declares no framework. Declaring it is the author's or the
   external template provider's responsibility.

## Backward/forward compatibility

- Old SDD reading a new file: ignores `project.testFramework` (unknown optional
  scalar) — no break.
- New SDD reading an old file: `TestFramework = None` → neutral skill — the
  intended safe default.
