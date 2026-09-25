# Optional Git Worktree Registration Check

**Status:** read-only FSC-04 repository-custody prerequisite, stacked on draft #1050. **Owner:** FS.GG.SDD.

## Red-before false greens

The #1050 top-level check is insufficient when a copied source directory supplies a `.git` entry that points at a sibling repository. Git reports the copied directory as its top level and reads the sibling's commit. Disposable red-before controls showed that both a symlinked `.git` directory and a forged regular `.git` gitfile let copied `.fsgg` bytes pass the optional commit and byte/mode previews.

Before reading the selected commit, the preview now asks Git for its NUL-delimited porcelain worktree roster and requires exactly one registered worktree whose path is the supplied root. The roster output is bounded to 1 MiB and decoded as strict UTF-8. The two copied-source variants refuse as `UnregisteredWorktree`. A conventional repository root and a registered linked worktree root still pass. This is a read-only source-selection check; it does not select an authorized commit.

## Remaining custody boundary

The worktree registry is mutable local Git metadata. An actor able to rewrite it or replace `.git` or the object store between subprocesses can defeat a point-in-time check. The Git executable and repository/object-store identity are not pinned across commands. Full source selection, trusted commit authority, cross-file common instant, ABA/post-check, Windows and installed-package parity remain open. `ObservedAgreement` and all optional Git previews are non-authorizing. #1017 physical custody and #1018 verification/staging/rollback decisions remain open. No generation output, publication, receiver pin, Authority write, protected effect or merge occurs.
