# Optional Git Alternate-Store Refusal

**Status:** read-only FSC-04 object-store custody prerequisite, stacked on draft #1052. **Owner:** FS.GG.SDD.

## Red-before foreign object read

The #1052 preview clears inherited `GIT_ALTERNATE_OBJECT_DIRECTORIES`, but Git also reads a repository's `objects/info/alternates` file. In a disposable pair of registered repositories, A cannot read B's full commit ID until an alternates file points to B's object directory. Before this repair, the optional preview then accepted B's commit from A's worktree. A second red-before case added the alternates file after initial registration checks and also passed.

The preview now asks Git for its active common-store `objects/info/alternates` path and refuses if an entry exists, before and after selected blob reads. A persistent preexisting or late alternate refuses as `AlternateObjectStore`. An empty declaration also refuses by provisional policy. A registered linked worktree observes the common store's declaration and refuses; self-contained disposable main and linked worktrees pass. The path lookup and attribute probe are read-only and bounded, but are separate from Git object reads.

## Remaining boundary

An alternate file added for the reads and removed before the final check still returns B's commit: the independent ABA control remains a false green. Other ways to route objects outside the expected store, including object-directory symlinks, promisor/lazy-fetch configuration, and replacement of Git metadata or object storage between subprocesses, are not excluded. Rejecting an alternates file is a provisional optional profile and may exclude legitimate shared clones; the producer has not selected this policy. The caller-selected commit is unauthenticated, complete source selection and cross-root instant are open, and Windows/installed parity is unqualified. `ObservedAgreement` and all optional Git previews are non-authorizing. #1017 physical custody and #1018 verification/staging/rollback decisions remain open. No generation output, publication, receiver pin, Authority write, protected effect or merge occurs.
