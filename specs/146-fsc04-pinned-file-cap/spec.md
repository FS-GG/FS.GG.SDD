# Pinned Regular-File Byte Cap

**Status:** read-only FSC-04 Linux physical-reader capacity prerequisite, stacked on draft #1031. **Owner:** FS.GG.SDD.

## Red-before and repair

The pinned reader on #1031 uses `CopyTo` into a `MemoryStream` for each of two regular-file byte passes. A disposable sparse file of 32 MiB plus one byte is accepted and fully copied twice on that base; a file of exactly 32 MiB is accepted as a boundary control. The new over-cap refusal test fails before repair.

For this preview, a pinned regular file may contain at most 32 MiB. Refuse `FileLimitExceeded` using the opened descriptor's length before allocating the byte buffer. Each pass also reads in fixed chunks and refuses before writing a chunk that would exceed the cap, so growth after the initial length check cannot make a pass allocate beyond the per-file threshold. Both complete-root capture and selected-file pinned capture use the same reader and refusal. Tests verify over-cap refusal, exact-cap acceptance and selected-file refusal.

## Boundary

The 32 MiB threshold is provisional producer policy. This is a per-file pass cap, not a cap on the sum of files or peak process memory: raw results, the comparison pass and immutable captured-file copies coexist. The pre-read length and bounded loop do not establish a stable content snapshot under adversarial in-place writes; the existing two-pass byte/stamp check still has ABA, timestamp-hidden and post-check limits. #1031's 256-child descriptor cap remains provisional. Caller-supplied inventory authority, non-atomic `.fsgg`/`work`/performance capture, Windows pinned-reader parity, #1017 physical custody, #1018 verification/staging/rollback, installed parity, publication, receiver adoption, merge and GS2-10 freeze remain held. No output or live effect is produced.
