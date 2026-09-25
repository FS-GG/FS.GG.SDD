# Plan: Typed Work-Model Verification-Wave Preview

## Pure seam

Add a new module after the v2 bundle verifier. Parse the actual proposed work-model JSON strictly enough to bind both source lists, work identity, and metadata output path to the independent candidate. Retain selected snapshots and candidate in a private prepared type. Compare supplied physical captures through the existing v2 verifier and return only a read-only preview.

## Verification

1. Show that the existing source-only bundle accepts valid selection/capture/candidate even if a separately proposed output JSON source digest is changed.
2. Require the typed preparation to refuse that changed output, stale candidate, wrong identity/path, and duplicate case alias.
3. Require the second step to refuse a changed physical capture and pass a valid generated body.
4. Run the Commands test project and warning-clean Release build.

## Dependency

No `CommandEffect` or driver integration is added. The #1018 producer decision on separate verification and source staging/rollback is required before any generated-view output can depend on this preview.
