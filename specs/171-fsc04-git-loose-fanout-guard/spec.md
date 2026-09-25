# Optional Git Loose-Object Fanout Refusal

**Status:** read-only FSC-04 object-store custody prerequisite, stacked on draft #1056. **Owner:** FS.GG.SDD.

## Red-before foreign loose objects

The #1056 preview checks `objects` and `objects/pack`, but Git also reads loose objects through `objects/<two-hex>` directories. A disposable registered repository A has no commit; B holds a committed core configuration as loose objects. Symlinking the fanout directories for B's reachable objects into A's direct object directory made the optional preview accept B's full commit ID. Installing those links after initial checks produced the same false green before this repair.

On Linux, the preview now checks the fixed set of 256 lowercase two-hex fanout names under the already checked object directory. A missing fanout is allowed; a present entry must be a directory when inspected with no-follow `statx`. The bounded scan runs before and after selected Git blob reads. A preexisting or persistent late fanout symlink refuses as `LooseObjectDirectoryRedirect`. B's ordinary loose objects still read, and A without the links cannot read B's commit.

## Remaining boundary

The independent redirect-then-restore control still accepts B's commit. These observations do not pin fanout directories or object files through Git subprocesses. Symlinked object leaves within real fanout directories, other Git metadata paths, local replacement, caller-selected commit authority, complete-source selection, common-instant capture, Windows and installed parity remain unproven. `ObservedAgreement` and all optional Git previews remain non-authorizing. #1017 physical custody and #1018 verification/staging/rollback decisions remain open. No generation output, publication, receiver pin, Authority write, protected effect or merge occurs.
