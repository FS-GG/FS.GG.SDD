# Plan: Pinned Regular-File Byte Cap

1. Characterize #1031 on disposable sparse files at 32 MiB and one byte over; require the latter to refuse as a red-before test.
2. Check opened-file length before allocation and enforce the same threshold during both chunked byte passes.
3. Verify the exact boundary and that complete-root and selected-file pinned paths share the refusal.
4. Run focused and full Commands tests plus a warning-clean Release build, then open an exact-head stacked draft without output effects.
