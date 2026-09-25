# Plan: Pinned Core Work-Model Source Selection

## Source seam

Add a read-only `verifyFromPinnedCoreSources` entry point to the v2 bundle module. It probes the fixed required and optional core paths through `GenerationSourceSnapshot.captureSelectedFile`, treating only `MissingFile` on an optional path as absence. The resulting physical captures feed the #1016 performance join and v2 selected/candidate comparison.

## Verification

1. Preserve red-before tests for stale supplied core bytes and an omitted optional work source.
2. Require fresh physical capture to refuse both, plus changed/omitted required config and linked/case-aliased optional work files.
3. Require unrelated siblings in `.fsgg`, `work/<id>`, and the performance parent to remain acceptable.
4. Run the full Commands test project and warning-clean Release build.

## Boundary

The probe is sequential and Linux-only. No output, cross-root atomic snapshot, or installed-package parity follows from this unit.
