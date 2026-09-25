# Plan: Pinned First-Pass Allocation Control

1. Add a stricter warmed 8 MiB same-thread allocation control on #1036; require it to fail before repair.
2. Reserve the already budget-checked opened-file length as first-pass buffer capacity while retaining chunk and metadata refusal checks.
3. Run focused allocation, race, file and aggregate-cap controls, full Commands tests and a warning-clean Release build.
4. Open an exact-head stacked draft without output effects or installed-parity claims.
