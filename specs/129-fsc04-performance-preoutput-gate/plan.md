# Plan: Declared Performance Source Pre-Output Gate

## Source seam

Use `ViewGeneration.generatedViewPlan`, the shared pure planner reached by the lifecycle handlers. Parse the same explicit or recovered `evidence.yml` text that `workModelSnapshots` uses. For each distinct declared `performanceBudget.artifactPath`, inspect `Foundation.readOf` before the planner can return generated-view output effects. Preserve the existing model consistency checks for observed reads.

## Verification

1. Capture the prior false green with an independently authored disposable planner fixture: `Absent` returns `WriteFile` despite a declared performance path.
2. Require no output effects and distinct blocking diagnostics for `Absent` and `Unreadable`; require that `Bytes` does not trigger either read-state diagnostic.
3. Run the complete Commands test project and a warning-clean Release build.

## Boundary

This is an interpreted-read preflight, not a physical source-closure or simultaneous multi-root capture. The v2 bundle comparator and Linux selected-file capture remain separate. No live generation, receiver pinning, package publication, or installed parity is authorized by this plan.
