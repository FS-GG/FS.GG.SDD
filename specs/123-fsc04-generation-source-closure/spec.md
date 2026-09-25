# Feature Specification: Generation Source Closure

**Status:** provisional FSC-04 source preparation. **Tier:** 1, existing tool-visible currency verdict changes. No producer publication or receiver adoption is authorized by this feature.

## User outcome

A consumer comparing a generated view with its current producers receives `stale` whenever the producer set is missing, gains a member, loses a member, contains duplicate paths, changes a digest, or names a different repository-relative source root. Source order does not affect the verdict.

## Requirements

- FR-001: `GenerationManifest.isStale` compares the complete `(artifact path, digest)` source set in both directions. A generated view with no declared producer is stale.
- FR-002: Duplicate source paths in either the current inputs or recorded manifest are stale; a map conversion must not silently overwrite one identity.
- FR-003: A different path is a different producer even when its basename and digest match. No path rewriting or suffix matching is permitted.
- FR-004: The existing signature, persisted artifact schemas, generation bytes and CLI output shape remain unchanged. This is a read-only currency decision.
- FR-005: Semantic tests prove green equality and red for missing, extra, duplicate, wrong-root and changed-digest sources. The missing-current-source case must fail before the implementation.
- FR-006: A source identity with a missing, malformed, or unsupported digest cannot make a view current even when the malformed value occurs on both sides. The comparator must return stale rather than crash for null or empty digest values.

## Boundaries

This is a pure comparison over already parsed `SourceIdentity` values. Filesystem containment, symlink resolution, package publication, installed consumer checks, and receiver adoption belong to later FSC-04 stages. A path that was normalized before this API arrives cannot be revalidated from its lost raw spelling.
