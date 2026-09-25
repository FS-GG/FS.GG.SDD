# Pinned Second-Pass Allocation Control

**Status:** read-only FSC-04 Linux physical-reader memory prerequisite, stacked on draft #1035. **Owner:** FS.GG.SDD.

## Red-before and repair

The #1035 pinned regular-file reader creates a full `MemoryStream` and byte array for each of two byte passes before comparing them. In a warmed disposable 8 MiB sparse-file capture, an independent `GC.GetAllocatedBytesForCurrentThread` control measures 67,129,552 allocated bytes on that base and fails a 48 MiB threshold.

Keep the first byte pass as the retained raw source snapshot. Rewind the same opened descriptor and compare the second pass in a fixed 80 KiB buffer directly with those bytes. Refuse a changed byte, short or long second pass, a per-file or remaining aggregate-budget excess, or changed opened-fd metadata. The second pass no longer allocates another file-sized output. The allocation control and existing pinned race, file-limit and stable-source tests pass after repair.

## Boundary

The 48 MiB test threshold characterizes this 8 MiB fixture on the current .NET runtime; it is not a process peak-memory ceiling or a portable performance contract. The first-pass `MemoryStream`, raw result, immutable captured-file copy, rosters and caller copies still allocate. #1031's 256-child descriptor, #1032's 32 MiB per-file, #1033's 64 MiB aggregate raw-byte, #1034's 4,096-file and #1035's 1,024-code-unit path limits remain provisional. Descriptor-pinned byte comparisons remain observed-stability checks: ABA, timestamp-hidden/post-check mutation, caller-supplied inventory authority and non-atomic `.fsgg`/`work`/performance capture remain open. Windows pinned-reader parity, #1017 physical custody, #1018 verification/staging/rollback, installed parity, publication, receiver adoption, merge and GS2-10 freeze remain held. No output or live effect is produced.
