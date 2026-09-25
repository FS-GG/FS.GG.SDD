# Pinned Complete-Root File Count Cap

**Status:** read-only FSC-04 Linux physical-reader capacity prerequisite, stacked on draft #1033. **Owner:** FS.GG.SDD.

## Red-before and repair

The #1033 reader caps raw payload bytes but counts no captured-file objects. A disposable complete root with 4,097 declared zero-byte regular files succeeds on that base; its new refusal test fails before repair. A 4,096-file control succeeds.

For this preview, permit at most 4,096 captured regular files in one complete-root pinned capture. Refuse `CapturedFileLimit` with the 4,097th supplied path before building the expected-path set or opening the tree. Also check the physical traversal before opening its 4,097th regular file, so an undeclared extra file cannot bypass the count policy. Independent controls verify the declared over-limit case, the exact boundary, and an over-limit physical roster with only 4,096 declared paths.

## Boundary

The 4,096-file threshold is provisional producer policy. It bounds captured-file count but is not a peak process-memory or execution-time ceiling: path lengths, declaration storage supplied by the caller, result copies and traversal structures still matter. #1031's 256-child descriptor cap, #1032's 32 MiB per-file cap and #1033's 64 MiB complete-root raw-payload cap remain provisional. These checks establish observed refusals rather than an atomic source snapshot; ABA, timestamp-hidden/post-check mutation, caller-supplied inventory authority and non-atomic `.fsgg`/`work`/performance capture remain. Windows pinned-reader parity, #1017 physical custody, #1018 verification/staging/rollback, installed parity, publication, receiver adoption, merge and GS2-10 freeze remain held. No output or live effect is produced.
