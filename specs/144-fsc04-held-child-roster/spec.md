# Held Child Roster Stability

**Status:** read-only FSC-04 Linux physical-inventory repair, stacked on draft #1029. **Owner:** FS.GG.SDD.

## Red-before and repair

The #1028 two-child fixture adds a candidate to already visited `work/a` while selected `work/z/spec.md` is opened. Before this change, one pinned capture accepts the selected-only inventory. #1029's second pass catches a persistent addition, but a disposable controller can remove the candidate after the first pass and make both complete passes agree. A late empty directory has the same first-pass gap. The independently run red-before tests failed for both mutations on #1029.

During a complete Linux pinned capture, retain opened child directory descriptors and each visited directory's initial roster and descriptor stamp through the end of the traversal. Before returning files, recheck every held directory's roster and stamp. Refuse `DirectoryUnstable` when a late child mutation remains observable, including the transient candidate removed between #1029's two passes. Close all retained child handles on success or refusal. A stable nested child and unrelated candidate remain accepted.

## Boundary

The final roster recheck is an observed-stability control. It cannot prove a single instant across the tree or detect mutation restored before its check with hidden metadata changes; ABA, timestamp-hidden writes, and changes after the check remain. Deep trees may fail closed if the process cannot hold their descriptors. #1029's repeated pass remains useful for persistent mutation after this recheck. The caller still supplies the file inventory, and `.fsgg`/`work`/performance roots are not captured atomically. Windows has no equivalent pinned path here. #1017 physical custody, #1018 verification-wave/staging/rollback, installed parity, publication, receiver pinning, merge and GS2-10 freeze remain held. No output or live effect is produced.
