# Optional Git Commit Custody Preview for Core Config

**Status:** read-only FSC-04 immutable-source design prerequisite, stacked on draft #1046. **Owner:** FS.GG.SDD.

## Contract and controls

The #1046 physical characterization shows that repeated pinned working-tree reads can agree on project `A0` and sdd `B1` even though those bytes never coexisted. This alternative preview reads `.fsgg/project.yml` and `.fsgg/sdd.yml` as regular blob entries of one caller-selected **full Git commit object ID**. It accepts only a 40- or 64-character lowercase hex ID whose object type is `commit`, then reads each exact tree entry and blob with bounded `git` subprocess output. It records the blob ID, regular-file mode and raw SHA-256, with defensive byte copies. Git replace-object resolution is disabled and ambient repository-selection variables are cleared for each subprocess. No working-tree file or output is read or written by the preview.

Disposable repository controls show that the same commit ID returns `A0/B0` after `HEAD` moves to an `A1/B1` commit and the working tree changes again. Neither commit yields mixed `A0/B1`. A replace ref makes ordinary `git show` redirect the old ID to `A1`, while this preview still reads the named old commit. Moving ref names, tree objects, missing paths, committed symlinks and a blob above the provisional 32 MiB cap refuse. The selected object can still be stale: immutability of a caller-chosen commit is not authority to use it for generation.

## Decision and limits

This is an **optional profile**, not an accepted producer policy. It covers only the two core `.fsgg` files and only committed bytes. It neither proves that the working tree matches the commit nor selects an authorized commit, closes all producer sources, or binds generation to returned bytes. A future owner decision would need a trusted commit-selection rule, complete work/performance path policy, object-store custody, and exact generator input binding before any common committed-source claim. Repository path/object-store replacement and installed Git parity are not qualified here. `ObservedAgreement` from mutable physical capture remains non-authorizing. #1017 custody, #1018 verification/staging/rollback, provisional Unicode/resource policy, Windows/installed parity, publication, receiver adoption, merge and GS2-10 freeze remain held. No protected effect occurs.
