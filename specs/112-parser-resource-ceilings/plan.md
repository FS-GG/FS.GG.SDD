# Plan — Authored-parser resource ceilings

Keep the production parser unchanged. Extend `AuthoredInputHardeningTests` beside the existing
adversarial fixtures with one maximum-supported-size evidence document.

The test warms the same public parser with a small valid document before measuring. It then records
`Stopwatch.Elapsed` and `GC.GetAllocatedBytesForCurrentThread()` around only the parse call; fixture
construction is intentionally outside the allocation window. The accepted ceilings are generous
absolute guards rather than a microbenchmark, making the check portable across CI runners while
catching accidental repeated parsing, quadratic scanning, or uncontrolled copies.

Verification:

- focused resource-ceiling test;
- complete `FS.GG.SDD.Artifacts.Tests` suite;
- Release solution build and formatting check.

No `.fsi`, surface baseline, generated view, migration note, or package release is expected because
the change adds only a test contract and its specification.
