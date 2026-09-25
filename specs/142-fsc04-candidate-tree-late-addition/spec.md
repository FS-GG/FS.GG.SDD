# Late Candidate in Previously Visited Directory

**Status:** read-only FSC-04 pinned-tree temporal characterization, stacked on draft #1027. **Owner:** FS.GG.SDD.

## Outcome

A deterministic disposable tree contains empty `work/a` and selected `work/z/spec.md`. The Linux descriptor-pinned closed-root reader visits `work/a` first. Its after-open hook for `work/z/spec.md` then creates `work/a/spec.md` with the same logical work ID. The first capture returns only the selected spec and the #1026 pure candidate verifier accepts it; a fresh closed-root capture refuses `UnexpectedFile`. An independently present-before-traversal duplicate is refused on the first pass and, when fully captured, produces `DuplicateWorkId`.

## Boundary

The reader's per-directory name and metadata checks cover a child while it is visited, but do not recheck that child after later siblings. Adding a file to an already visited child need not change the held parent directory's stamp. A complete candidate inventory cannot be inferred from one traversal under concurrent mutation, even with no-follow descriptors. A bounded observed-stability repair could repeat the full pinned tree pass or retain child handles for a final roster check; both still have ABA, timestamp-hidden, post-check and non-atomic cross-root limits. #1017 selected-source custody, #1018 verification-wave/staging/rollback, Windows concurrency, installed parity, publication, receiver pinning, merge and GS2-10 freeze remain held. No output or live effect is produced.

The later held-child repair in `specs/144-fsc04-held-child-roster/` makes this a historical red-before finding. The stacked test now requires first-pass `DirectoryUnstable` refusal.
