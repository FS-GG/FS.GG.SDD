# Pinned Complete-Root Aggregate Byte Cap

**Status:** read-only FSC-04 Linux physical-reader capacity prerequisite, stacked on draft #1032. **Owner:** FS.GG.SDD.

## Red-before and repair

The #1032 reader caps each pinned regular file at 32 MiB but retains every raw file array until the complete-root roster check. A disposable source root containing three sparse 24 MiB files succeeds on that base with 72 MiB of retained raw payload. The over-budget refusal test fails before repair; a 24 + 24 + 16 MiB exact-boundary capture succeeds.

For this preview, complete-root capture retains at most 64 MiB of raw file payload. Before reading each file, pass the remaining budget to the descriptor-pinned reader. Refuse `CaptureLimitExceeded` with the file path when its opened-fd length exceeds that budget; each of the two fixed-chunk byte passes also refuses before a growing file can exceed the remaining budget. Count bytes only after a successful pinned read. Selected-file capture remains governed by the existing 32 MiB per-file cap and has no multi-file aggregate.

## Boundary

The 64 MiB threshold is provisional producer policy and bounds only the sum of retained raw payload bytes for one complete-root capture. It is not a peak process-memory ceiling: the second read, immutable captured-file copies, path/roster structures and callers' copies coexist. The number of files is also unbounded by this slice. #1031's 256-child descriptor and #1032's 32 MiB file caps remain provisional. Descriptor and byte checks are observed-stability screens, with ABA, timestamp-hidden changes, post-check changes, caller-supplied inventory authority and non-atomic `.fsgg`/`work`/performance capture still open. Windows pinned-reader parity, #1017 physical custody, #1018 verification/staging/rollback, installed parity, publication, receiver adoption, merge and GS2-10 freeze remain held. No output or live effect is produced.

The later `specs/148-fsc04-pinned-file-count/` slice adds a provisional 4,096-file complete-root count cap. The unbounded-count statement above records the #1033 base; peak process memory and path/declaration storage remain outside that later cap.
