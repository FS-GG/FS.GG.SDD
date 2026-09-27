# Plan: Provisional Performance Physical Join

## Source seam

Extract the v2 bundle's physical-evidence declaration parsing into a shared private function. Add a read-only `verifyWithPinnedPerformance` entry point that derives declared performance paths from those bytes, rejects omitted or duplicate captures, calls `GenerationSourceSnapshot.captureSelectedFile` for each path, then delegates final comparison to `verify`.

## Verification

1. Establish that a previously supplied performance capture stays accepted by the pure comparator after the physical file changes.
2. Require the pinned join to reject that change and independent linked-file, alias, and omitted-selection controls.
3. Require a valid declared performance file to pass despite unrelated parent siblings.
4. Run the Commands test project and warning-clean Release build.

## Boundary

The caller owns core capture closure and producer-complete selection. This unit performs no output write and cannot claim a simultaneous snapshot or installed parity.
