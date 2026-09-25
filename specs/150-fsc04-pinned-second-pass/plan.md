# Plan: Pinned Second-Pass Allocation Control

1. Measure warmed same-thread allocations for a disposable 8 MiB pinned capture on #1035 and require a red-before threshold below the full second-pass buffer cost.
2. Replace the second file-sized byte array with a fixed-buffer comparison against first-pass raw bytes, retaining file and aggregate limits and metadata checks.
3. Run the allocation control with existing pinned race and capacity tests, then full Commands tests and a warning-clean Release build.
4. Open an exact-head stacked draft without output effects or installed-parity claims.
