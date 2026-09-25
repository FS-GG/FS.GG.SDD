# Plan: Pinned Complete-Root Aggregate Byte Cap

1. Characterize #1032 with three individually allowed 24 MiB sparse files; require the 72 MiB aggregate to refuse while 64 MiB succeeds.
2. Track successfully captured raw bytes and pass the remaining 64 MiB budget into each pinned regular-file read, including both chunked byte passes.
3. Retain the selected-file path's independent per-file cap and run focused and full Commands tests plus a warning-clean Release build.
4. Open an exact-head stacked draft without output effects or installed-parity claims.
