# Optional Git Object-Directory Symlink Refusal

**Status:** read-only FSC-04 object-store custody prerequisite, stacked on draft #1053. **Owner:** FS.GG.SDD.

## Red-before physical redirect

The #1053 alternate-file check does not stop a registered repository from replacing its `.git/objects` directory with a symlink to a sibling object store. A disposable fixture moved A's original object directory aside and linked A's `objects` entry to B's store. Git still reported A as a registered worktree, and A's optional preview accepted B's full commit ID. Installing the link after the initial checks produced the same false green.

On Linux, the preview now asks Git for the absolute common Git directory and active object path, requires the active path to be the common directory's direct `objects` child, and uses no-follow `statx` to require that child itself to be a directory. It checks before and after selected blob reads. A preexisting or persistent late symlink refuses as `ObjectDirectoryRedirect`. Ordinary and linked worktrees with a direct object directory pass. Other platforms fail closed as `UnsupportedPlatform` in this optional profile.

## Temporal and policy limits

The independent redirect-then-restore control still accepts B's commit, showing that pre/post path and type checks do not bind Git subprocesses to one object-store handle. Ancestor symlinks, Git metadata replacement, alternate paths outside this direct-child case, promisor/lazy-fetch behavior, and local registry mutation remain unproven. The selected commit ID is caller supplied and unauthenticated. Complete-source selection, common-instant capture and installed parity remain open. `ObservedAgreement` and all optional Git previews remain non-authorizing. #1017 physical custody and #1018 verification/staging/rollback decisions remain open. No generation output, publication, receiver pin, Authority write, protected effect or merge occurs.
