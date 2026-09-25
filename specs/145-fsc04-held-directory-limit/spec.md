# Held Directory Descriptor Limit

**Status:** read-only FSC-04 Linux resource-bound prerequisite, stacked on draft #1030. **Owner:** FS.GG.SDD.

## Contract

The #1030 complete-root reader retains visited child directory descriptors until its final roster recheck. On its base, a disposable `work/` tree with 257 empty children and one declared regular file returns successfully while holding all 257 child handles. This is the red-before resource-control case; a 256-child tree also succeeds.

For this preview, permit at most 256 held child directory descriptors in one capture. Refuse `HeldDirectoryLimit` with the 257th child's relative path before opening it. Keep the existing `finally` closure for every already held child. Independent controls verify the 256-child boundary and that repeated over-limit refusals leave no descriptors pointing into the disposable source tree.

## Boundary

The cap is a provisional source-reader policy, not a system-wide file-descriptor guarantee. Workspace ancestor and selected-root handles are outside the 256-child count; a lower or concurrently exhausted process limit can still produce a fail-closed `Unreadable`. A repository with more than 256 child directories is refused pending producer-owner policy. The roster recheck and #1029 two-pass comparison remain observed-stability screens: ABA, hidden metadata mutation, changes after the check, and non-atomic `.fsgg`/`work`/performance capture remain possible. Caller-supplied inventory authority, Windows pinned-reader parity, #1017 physical custody, #1018 verification/staging/rollback, installed parity, publication, receiver adoption, merge and GS2-10 freeze remain held. No output or live effect is produced.
