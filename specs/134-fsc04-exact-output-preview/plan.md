# Plan: Exact Work-Model Output Preview

## Pure seam

Add a separate module after #1019's typed preview. Recursively reject duplicate or case-aliased JSON property names, then call #1019 preparation. Regenerate the work model through the existing deterministic `Serialization.generateWorkModel` using independently selected snapshots and generator version. Compare the entire proposed JSON body; retain #1019's read-only captured-source preview on equality.

## Verification

1. Preserve red-before controls in which #1019 accepts a changed non-source body and a duplicate root `workId` property.
2. Require exact-output refusal for changed body, wrong generator, and changed selected source.
3. Require recursive ambiguity refusal for duplicate root `workId`/`sources` and nested case-alias source properties.
4. Require an exact generated body to pass, then run the Commands test project and warning-clean Release build.

## Dependency

This module has no filesystem write or driver integration. The #1018 producer decision about a separate verification wave and authored-source staging/rollback remains required before any generated output can depend on it.
