# Direct Pinned First-Pass Read

**Status:** read-only FSC-04 Linux physical-reader memory prerequisite, stacked on draft #1038. **Owner:** FS.GG.SDD.

## Red-before and repair

After #1038 transfers owned raw bytes, the first pinned pass still allocates a `MemoryStream` buffer and a second file-sized array through `ToArray`. A warmed disposable 8 MiB capture allocates 16,961,280 bytes on that base and fails a new 12 MiB focused threshold.

After checking the opened descriptor's length against the per-file and remaining aggregate budgets, allocate one raw array of that length and fill it directly from the same descriptor. Refuse a short read; probe one additional byte and refuse growth, with per-file or aggregate-limit refusal when that extra byte crosses a budget. Retain the fixed-buffer second-pass byte comparison and pre/middle/post opened-fd metadata checks. Deterministic disposable after-length hooks prove short-read, within-budget growth and over-file-budget growth refusals. The new allocation and existing race/capacity controls pass after repair.

## Boundary

The 12 MiB threshold is a fixture-specific allocation control, not a portable peak-memory ceiling. The opened-file length can change after observation; the direct pass refuses the observed short or long result, while ABA, timestamp-hidden changes and post-check mutation remain possible. Result objects, rosters, caller `Bytes` copies and declarations still allocate. #1031's 256-child descriptor, #1032's 32 MiB per-file, #1033's 64 MiB aggregate raw-byte, #1034's 4,096-file and #1035's 1,024-code-unit path limits remain provisional. Caller-supplied inventory authority and non-atomic `.fsgg`/`work`/performance capture remain open. Windows pinned-reader parity, #1017 physical custody, #1018 verification/staging/rollback, installed parity, publication, receiver adoption, merge and GS2-10 freeze remain held. No output or live effect is produced.
