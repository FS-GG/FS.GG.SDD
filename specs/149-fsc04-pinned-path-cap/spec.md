# Pinned Relative-Path Length Cap

**Status:** read-only FSC-04 Linux physical-reader path-storage prerequisite, stacked on draft #1034. **Owner:** FS.GG.SDD.

## Red-before and repair

The #1034 reader caps file count and raw bytes but has no relative-path length limit. A disposable file under four ordinary 200-character directory segments has a 1,025-character relative path; the pinned complete-root capture accepts it on that base, so the new refusal test fails. The same shape at exactly 1,024 characters succeeds.

For this preview, limit each closed-root, declared or physically discovered relative path to 1,024 .NET UTF-16 code units. Refuse `PathLimitExceeded` before adding an overlong declaration to the expected-path set, before adding a discovered entry to the captured roster, and before traversing an overlong selected-file path. Tests cover declared and physical over-limit paths, exact-boundary acceptance and selected-file refusal. The physical-extra test supplies a short selected file and an overlong undeclared file to exercise traversal independently from declaration preflight.

## Boundary

The 1,024-code-unit threshold is provisional producer policy and is not a portable filesystem byte-length rule. It bounds each retained relative-path string, but not peak process memory, caller-owned declaration storage before this call, Unicode normalization policy or execution time. #1031's 256-child descriptor, #1032's 32 MiB per-file, #1033's 64 MiB aggregate raw-byte and #1034's 4,096-file caps remain provisional. The pinned reader still provides observed checks rather than an atomic snapshot: ABA, timestamp-hidden/post-check changes, caller-supplied inventory authority and non-atomic `.fsgg`/`work`/performance capture remain open. Windows pinned-reader parity, #1017 physical custody, #1018 verification/staging/rollback, installed parity, publication, receiver adoption, merge and GS2-10 freeze remain held. No output or live effect is produced.
