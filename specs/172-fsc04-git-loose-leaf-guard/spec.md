# Optional Git Loose-Object Leaf Refusal

**Status:** read-only FSC-04 object-store custody prerequisite, stacked on draft #1057. **Owner:** FS.GG.SDD.

## Red-before object-leaf redirect

The #1057 fanout check accepts real `objects/<two-hex>` directories, while Git follows symlinked files inside them. In a disposable fixture, registered A has no commit and B has the selected core commit as loose objects. A creates ordinary fanout directories and symlinks each reachable object file to B. The optional preview accepted B's full commit ID, both when the links existed before capture and when a test hook installed them after initial checks.

On Linux, each fanout observation now enumerates present leaves, refuses after a provisional 4096-leaf cap, and uses no-follow `statx` to require every leaf to be a regular file. It runs before and after selected blob reads. Preexisting and persistent late leaf symlinks refuse as `LooseObjectLeafRedirect`; an oversized inventory refuses as `LooseObjectLeafLimit`. Ordinary B objects still read, and A without links cannot read B's commit.

## Remaining boundary

The independent link-then-remove control still returns B's commit. Path enumeration and pre/post type checks do not pin leaf handles or bytes while Git subprocesses read them. A leaf may be replaced or modified between checks, and ABA remains possible. Other Git metadata paths, local object replacement, caller-selected commit authority, complete-source selection, common-instant capture, Windows and installed parity remain unproven. The 4096-leaf cap is provisional and may refuse larger valid repositories. `ObservedAgreement` and all optional Git previews remain non-authorizing. #1017 physical custody and #1018 verification/staging/rollback decisions remain open. No generation output, publication, receiver pin, Authority write, protected effect or merge occurs.
