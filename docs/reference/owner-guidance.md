# Owner-declared root guidance

An optional `.fsgg/owner-guidance.json` in the selected, verified Drivers workspace payload activates separately delivered Markdown in configured agent roots. Doctor and upgrade select owner bytes only for a workspace with provider scaffold provenance; the provider-less `devRepoInit` anchor stays neutral. A local descriptor alone grants no write authority. SDD contains no owner-specific prose or knowledge-store parser.

```json
{
  "schema": "fsgg.owner-guidance/1",
  "guidance": [{
    "id": "project-knowledge",
    "sourcePath": ".fsgg/project-guidance.md",
    "targets": ["claude", "codex"],
    "requiredPaths": [".fsgg/project-policy.json"]
  }]
}
```

The source fragment contains the body only. SDD derives `<!-- project-knowledge:begin -->` and `<!-- project-knowledge:end -->` from the ID. Targets resolve through `.fsgg/agents.yml` to distinct contained root Markdown files; names differing only by case collide. Paths cannot carry leading or trailing segment whitespace. IDs use lowercase letters, digits and hyphens, beginning with a letter (maximum 64 characters). Descriptor size is at most 64 KiB, with 1–16 unique declarations, 1–16 unique targets per declaration and 1–32 unique prerequisite paths. Fragments are UTF-8 without a BOM, at most 32 KiB, and cannot contain guidance markers. Unknown/duplicate members, escaping paths and malformed declarations refuse activation.

The fragment, descriptor and required files must belong to the verified selected workspace or driver-skill transport and match the installed bytes exactly. Differing files are preserved and named by `ownerGuidance.incomplete`; they are never re-attested as matching owner bytes. Reads refuse symbolic links, linked path components and non-regular files. Regular-file containment is supported on Linux and Windows; other platforms report incomplete activation. Root files must be UTF-8; an existing UTF-8 BOM is preserved. Guidance roots remain hybrid artifacts, outside whole-file driver provenance.

SDD preflights every selected root before writing any block. A missing pair appends one block; a valid pair replaces only the owned region. Duplicate, reversed, nested, partial or noncanonical markers refuse the whole activation. Text outside the block, including Unicode and newline style, is preserved. An unchanged block emits no write. Before each write, the effect edge rechecks observed target and prerequisite bytes and refuses a changed observation. This optimistic recheck does not claim race-free filesystem compare-and-swap or cross-file crash atomicity; a later write failure leaves activation incomplete and may leave an earlier target updated.

Ordinary scaffold activates after owner materialization, including early-stage workspaces without a work model. Bare `init`, bare typed initialization and `refresh` retain their existing behavior. `doctor` reports stale/missing/conflicting guidance without writes. `upgrade` repairs through the existing `artifactReSeed` confirmation or explicit `--yes`; newly seeded roots are read again before activation. Noninteractive mutation without `--yes` refuses. A second successful doctor/upgrade is coherent and emits no guidance write.

Diagnostics include selected descriptor/fragment digests and named target/prerequisite failures. Existing reports expose guarded writes as hybrid artifact changes and remediation target previews. Source qualification with explicit fixture content overrides proves the generic control flow; published compatible Drivers/SDD packages and downloaded-only consumer evidence are still required for installed adoption.
