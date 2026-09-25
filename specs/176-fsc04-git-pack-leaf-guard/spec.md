# Optional Git Pack-File Leaf Refusal

**Status:** read-only FSC-04 packed-object custody prerequisite, stacked on draft #1061. **Owner:** FS.GG.SDD.

## Red-before packed-store borrowing

In disposable repositories, B's selected core commit was repacked. Registered A had its own real `objects/pack` directory but symlinked B's `.idx`, `.pack` and `.rev` files into it. Git resolved B's full commit ID from A and the optional preview accepted B's content-addressed object graph. The #1061 tree and blob digests were correct for B's objects; the false green concerned physical object-store custody. Links present before capture and installed after the initial checks were both accepted before this repair.

On Linux, the pack-directory observation now enumerates present children up to a provisional 4096-file cap and uses no-follow `statx` to require every child to be a regular file. It runs before and after selected object reads. Preexisting and persistent late symlinked pack files refuse as `PackFileRedirect`; an oversized roster refuses as `PackFileLimit`. B's ordinary packed objects still read, and A without links cannot read B's commit.

## Remaining boundary

The independent link-then-remove control still returns B's commit. Path enumeration and pre/post type checks do not pin pack-file handles or bytes while Git subprocesses read them; ABA and concurrent replacement remain possible. Other Git metadata paths, caller-selected commit authority, complete-source selection, common-instant capture, Windows and installed parity remain unproven. The 4096-child cap is provisional and can refuse larger valid repositories. `ObservedAgreement` and the optional preview remain non-authorizing. #1017 physical custody and #1018 verification/staging/rollback decisions remain open. No generation output, publication, receiver pin, Authority write, protected effect or merge occurs.
