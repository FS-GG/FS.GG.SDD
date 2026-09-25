# Optional Git Pack-Directory Symlink Refusal

**Status:** read-only FSC-04 object-store custody prerequisite, stacked on draft #1055. **Owner:** FS.GG.SDD.

## Red-before packed-object redirect

The #1054 guard checks the final `.git/objects` entry, while Git may still follow a symlinked `objects/pack` child. In a disposable fixture, repository B packs its commit and A replaces its empty `objects/pack` directory with a symlink to B's pack directory. A remains a registered worktree with a direct object directory and no alternates file, yet the optional preview accepts B's full commit ID. Redirecting the pack child after initial checks also passed before this repair.

On Linux, the preview now requires Git's active `objects/pack` path to be the direct `pack` child of its checked object directory. A no-follow `statx` check requires the child to be a directory if present; an absent pack child is allowed for loose-only stores. The checks run before and after selected blob reads. A preexisting or persistent late symlink refuses as `PackDirectoryRedirect`. Ordinary and registered linked worktrees pass.

## Remaining boundary

The independent redirect-then-restore control still returns B's commit. These path and type observations do not pin pack files, object-directory handles or a common source instant. Other child paths, metadata ancestry, promisor behavior beyond lazy fetch, local registry/object replacement, caller-selected commit authority, complete-source selection, Windows and installed parity remain unproven. `ObservedAgreement` and all optional Git previews remain non-authorizing. #1017 physical custody and #1018 verification/staging/rollback decisions remain open. No generation output, publication, receiver pin, Authority write, protected effect or merge occurs.
