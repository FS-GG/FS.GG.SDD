# FSC-04 generation source contract

## Scope

A producer may select one dedicated, closed source root and one digest policy. A candidate versioned source contract binds every regular file below that root to a digest. Verification refuses unsupported versions, a different root or policy, malformed digests, incomplete physical membership, and changed bytes. The producer-owned selection is independent of the candidate record.

This source seam is provisional. It does not write an output, establish physical race freedom, or authorize an installed package or receiver. It depends on the physical snapshot seam in #1001 and the path guard in #1000.

## Current producer inventory and selection gap

The existing work-model generation source list spans `.fsgg/*.yml`, selected `specs/<id>/*` files, and optional performance evidence paths. `ViewGeneration.workModelSnapshots` rewrites the `evidence.md` source-snapshot section before hashing. `SchemaVersion.sha256Text` folds CRLF to LF. `Serialization.sourceStale` checks recorded sources against current sources but does not enforce complete membership. Analysis, refresh, and governance handoff also derive text digests, while evidence artifacts can use exact bytes. No single existing work-model directory is an owned closed input root. A producer must select and document a dedicated root and policy before wiring this generic contract into a live generated view.

## Acceptance

- A v1 contract for a producer-selected closed root verifies all declared file digests using the physical snapshot bytes.
- Wrong version, root, policy, omitted physical file, malformed digest, and digest drift refuse.
- Verification is read-only and returns the captured immutable bytes only on success.
