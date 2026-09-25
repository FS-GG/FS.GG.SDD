# Assembly-Bound Work-Model Generator Preview

**Status:** read-only FSC-04 generator-authority prerequisite, stacked on draft #1022. **Owner:** FS.GG.SDD.

## Contract

- FR-001: Derive the expected generator ID and version from the referenced FS.GG.SDD.Artifacts assembly using `SchemaVersion.currentGeneratorVersion()`. The verifier caller cannot supply that identity.
- FR-002: Join this expected identity with #1022 exact proposed-output regeneration and repeated #1017 pinned source capture. A proposal generated with another ID or version refuses before physical capture.
- FR-003: Return only a digest/path preview. No proposed output bytes, physical source bytes, or write effect is returned.

## Red-before control

An independently generated proposal and caller-supplied generator version are changed together. #1022 accepts the pair because its generator input comes from that same caller. The new preview derives the version independently from the assembly and refuses `OutputDrift` before capture. A second control changes only the generator ID; current assembly identity with stable pinned sources succeeds.

## Limit

This binds to the referenced local Artifacts assembly, not an authenticated package receipt or installed receiver. It does not prove an atomic cross-root source instant; ABA, repeated mixed sets, timestamp-hidden/post-check changes, and Windows concurrency remain unresolved. #1017 physical custody and #1018 verification-wave/staging/rollback decisions remain prerequisites. No generation output, publication, receiver pinning, merge, installed parity, or GS2-10 cutover follows.
