# Pinned First-Pass Allocation Control

**Status:** read-only FSC-04 Linux physical-reader memory prerequisite, stacked on draft #1036. **Owner:** FS.GG.SDD.

## Red-before and repair

After #1036 removes the second file-sized byte array, the first pinned pass still grows a `MemoryStream` geometrically. An independently warmed 8 MiB sparse-file capture allocates 37,851,064 bytes on the test thread and fails a new 28 MiB threshold on the #1036 base.

Read the opened descriptor's length once, refuse it if it exceeds the 32 MiB per-file or remaining aggregate budget, and reserve that bounded length as the initial first-pass `MemoryStream` capacity. Keep fixed-chunk read guards for growth after the length observation and retain the second-pass fixed-buffer byte comparison and opened-fd metadata checks. The new allocation threshold and existing race, file-cap and aggregate-cap controls pass after repair.

## Boundary

The 28 MiB threshold is a focused fixture control for an 8 MiB file on this .NET runtime, not a portable peak process-memory ceiling. A concurrent shrink can leave an unused but bounded reservation; a concurrent growth remains subject to chunk and metadata refusals. Raw bytes, immutable result copies, rosters and caller copies still allocate. #1031's 256-child descriptor, #1032's 32 MiB per-file, #1033's 64 MiB aggregate raw-byte, #1034's 4,096-file and #1035's 1,024-code-unit path limits remain provisional. This reader does not prove atomic source capture: ABA, timestamp-hidden/post-check changes, caller-supplied inventory authority and non-atomic `.fsgg`/`work`/performance capture remain open. Windows pinned-reader parity, #1017 physical custody, #1018 verification/staging/rollback, installed parity, publication, receiver adoption, merge and GS2-10 freeze remain held. No output or live effect is produced.
