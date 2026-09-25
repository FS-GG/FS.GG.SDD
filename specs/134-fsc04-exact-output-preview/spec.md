# Exact Work-Model Output Preview

**Status:** pure FSC-04 source prerequisite, stacked on draft #1019. **Owner:** FS.GG.SDD.

## Contract

- FR-001: Before treating proposed generated work-model JSON as a verification-wave candidate, independently regenerate the complete JSON from the selected snapshots, work ID, expected path, and independently supplied generator version. The proposed bytes must match exactly; a changed model body, stale selected source, or wrong generator version refuses with `OutputDrift`.
- FR-002: Recursively inspect every JSON object in the proposed body. Duplicate or case-aliased property names refuse as `AmbiguousJson` before the #1019 metadata parser chooses a value. Malformed JSON refuses.
- FR-003: Preserve #1019's typed source/candidate binding and pure v2 captured-source comparison. A successful result remains a read-only preview with no output bytes or `CommandEffect`.

## Evidence

One disposable red-before control changes `modelVersion` while leaving source rows intact. #1019's typed preparation accepts it; exact regeneration refuses. A second red-before control inserts an earlier conflicting root `workId` property; #1019 selects the later value and accepts. The new recursive check refuses that body, duplicate root `sources`, and a nested `SOURCEDIGEST` case alias. Independent controls cover a wrong generator version, changed selected text, and a valid exact body with captured sources.

## Acceptance boundary

Exact regeneration binds a proposed body to in-memory selected snapshots, not to a simultaneous physical snapshot. #1017 physical custody and #1018's separate verification wave plus authored-source staging/rollback policy remain prerequisites to live output. ABA/timestamp-hidden/post-check changes, cross-root instant consistency, Windows concurrency, installed parity, publication, receiver pinning, output rollback, and GS2-10 freeze remain held.
