# Plan: Work-Model Generation Output Ordering Characterization

## Test seam

Use `CommandEffects.interpretAll` on a disposable root with a generated work-model `WriteFile` and an independent failing effect. Exercise failure before and after the generated write. Assert each result state and the resulting file bytes, without running a live generation command.

## Verification

1. Run the two focused ordering controls.
2. Run the full Commands test project and a Release build with warnings as errors.
3. Confirm no product source or output file changes are included in this draft.

## Producer decision required

Choose an explicit verification wave and a source-byte custody/rollback policy before integrating #1017 with output. The current effect batch has no failure barrier or batch rollback. A pure source verifier cannot repair that execution behavior by itself.
