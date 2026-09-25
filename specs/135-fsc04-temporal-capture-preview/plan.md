# Plan: Repeated Physical Work-Model Capture Preview

## Read-only seam

Wrap the #1017 `verifyFromPinnedCoreSources` reader in a second pass. Verify each pass against the same selected snapshots and candidate, then compare exact path and byte sets. Return only a digest/path preview. Keep an injected capture function for deterministic interleaving tests; the production wrapper supplies the pinned reader.

## Verification

1. Show that the v2 verifier accepts a deliberately mixed first-pass capture that never existed as one physical state.
2. Require second-pass refusal after an observed source change.
3. Require refusal when raw BOM bytes change while decoded text and candidate remain equal; require stable sources to pass.
4. Run the Commands test project and warning-clean Release build.

## Boundary

This preview is conservative observation, not an atomic snapshot proof or live output gate. No filesystem output, package publication, or receiver change follows from it.
