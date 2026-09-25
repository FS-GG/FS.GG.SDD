# Pinned Source Unicode Alias Refusal

**Status:** provisional read-only FSC-04 Linux source-reader policy, stacked on draft #1039. **Owner:** FS.GG.SDD.

## Red-before and repair

Linux permits `caf\u00e9.bin` and `cafe\u0301.bin` as distinct entries. On the #1039 base, a closed-root capture with both declared paths succeeds, and a selected-file capture of one succeeds while its canonical alias sits beside it. The independent disposable tests fail before this repair. With only one path declared, the prior closed-root reader reports `UnexpectedFile` for the alias; that is a closure refusal, but it does not identify the ambiguous physical roster.

The pinned reader now compares NFC-normalized names using its existing ordinal-ignore-case alias rule. It refuses duplicate canonical names in declarations and the physical roster, and refuses a selected child when its parent has a canonical alias. Original path spelling remains the exact physical match key and reported path; bytes and digests are unchanged. An invalid Unicode string supplied to this comparison is refused as `InvalidPath`. Tests cover declared aliases, physical aliases, selected files, selected directories, a combined case/canonical alias, and one valid Unicode name with exact bytes.

## Boundary

NFC plus ordinal-ignore-case is a conservative provisional source-selection policy, not a proof of every filesystem's collation or Unicode casefold behavior. The path-based Windows reader has not been qualified for this rule. The pinned reader still has observed-stability checks rather than an atomic snapshot: ABA, timestamp-hidden and post-check changes, caller-supplied inventory authority, and non-atomic `.fsgg`/`work`/performance capture remain open. The 256-child, 32 MiB per-file, 64 MiB aggregate, 4,096-file and 1,024-code-unit caps are provisional. #1017 physical custody, #1018 verification/staging/rollback, installed parity, producer publication, receiver adoption, merge and GS2-10 freeze remain held. No output or live effect is produced.
