# Plan: Blocking-Diagnostics Work-Model Preview

1. Independently reproduce an exact-output/current-generator false green with model-level error diagnostics; compare it with `generatedViewPlan`'s existing output suppression.
2. Add a separate read-only preview that regenerates the model, refuses its blocking diagnostic IDs, then delegates to #1023.
3. Test zero capture calls on refusal and successful pinned capture of a normalized, nonblocking fixture.
4. Run focused and full Commands tests and a warning-clean Release build before opening a stacked draft.

The preview does not settle the #1018 effect ordering or rollback policy.
