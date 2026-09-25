# Plan: Late Candidate in Previously Visited Directory

1. Build a disposable two-child `work/` root whose lexical order visits an empty directory before the selected spec.
2. Add a duplicate candidate to the earlier directory from the existing pinned reader's after-open test hook, then require the first pass to demonstrate the omission and a fresh pass to refuse it.
3. Prove a candidate present before traversal is detected as an independent negative control.
4. Run focused and full Commands tests and a warning-clean Release build; open a stacked draft with the remaining temporal boundary stated.

The test characterizes the existing reader and adds no production effect.
