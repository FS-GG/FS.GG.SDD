# Optional Git Commit-ID Byte Integrity

**Status:** read-only FSC-04 commit-object custody prerequisite, stacked on draft #1059. **Owner:** FS.GG.SDD.

## Red-before commit-object controls

In disposable SHA-1 and SHA-256 repositories, a different valid compressed commit payload copied under the selected commit ID's loose-object filename was returned by `git cat-file -t`, `-s`, and `commit`. The prior preview refused later: `git ls-tree` detected a hash mismatch and returned a generic `GitFailure`. This was not an end-to-end preview false green. The new check reads at most 1 MiB of selected commit body and independently computes its Git `commit <length>\0` object ID before tree selection. Substitution now yields `CommitIdMismatch`; clean commits remain accepted. An independent large-message commit that previously passed now yields `CommitTooLarge` under the provisional cap.

The digest uses incremental input over the returned bytes, avoiding another commit-body copy. The selected blob digest check from #1059 uses the same helper.

## Remaining boundary

The commit body and later `ls-tree` calls are separate Git subprocess reads. This change does not pin a Git object-file handle, independently verify tree-object bytes, prevent an ABA replacement or later mutation, or prove a common instant across sources. Git's current `ls-tree` behavior provides a separate hash-mismatch refusal in this fixture, but that observation is not a substitute for pinned tree custody. The 1 MiB cap is provisional and can refuse valid large commits. A caller-selected commit ID is not producer authority. `ObservedAgreement` and the optional preview remain non-authorizing. #1017 physical custody, #1018 verification/staging/rollback, Windows and installed parity remain open. No generation output, publication, receiver pin, Authority write, protected effect or merge occurs.
