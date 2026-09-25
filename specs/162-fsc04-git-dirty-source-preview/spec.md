# Optional Core Working-Tree Byte Comparison

**Status:** read-only FSC-04 source-custody prerequisite, stacked on draft #1047. **Owner:** FS.GG.SDD.

## Observed gap and bounded refusal

The #1047 commit preview correctly reads two core config blobs from one full Git commit ID, but it remains green after either selected working-tree file changes or becomes a symlink. A disposable red-before control showed three false greens: changed `.fsgg/project.yml`, changed `.fsgg/sdd.yml`, and a symlink to a file with the expected bytes. Each commit blob remained unchanged.

`verifyCoreBytesObserved` first invokes the #1047 commit preview. It then captures each selected working-tree file with the existing Linux descriptor-relative, no-follow physical reader and compares raw bytes to the corresponding committed blob. It refuses a physical read failure or byte difference. The clean two-file case passes. An untracked, unselected `.fsgg/other.yml` does not affect this deliberately narrow observation. A mode-only change also passes: the physical selected-file API supplies bytes, not the opened file mode.

## Interpretation and remaining boundary

`WorktreeBytesMatchedCommitObserved` means only that two sequential pinned reads matched two commit blobs during this call. It is not a producer authorization or a common-instant guarantee. The first working-tree file may change while the second is read, or either may change after return; in-place ABA and timestamp-hidden changes remain possible. The selected commit ID is caller supplied and unauthenticated. The comparison does not close additional `.fsgg`, `work/`, readiness, or performance sources; it does not qualify Git repository/object-store custody, file mode, Windows, or installed-package parity. `ObservedAgreement` also remains non-authorizing.

An owner decision must define trusted commit selection and complete source policy, then bind generation directly to verified bytes or immutable committed blobs. #1017 physical custody and #1018 verification/staging/rollback decisions remain open. No generation output, publication, receiver pin, merge, Authority write, or protected effect is part of this draft.
