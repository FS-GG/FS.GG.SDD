# Physical Candidate-Inventory Omission Characterization

**Status:** read-only FSC-04 physical-closure characterization, stacked on draft #1026. **Owner:** FS.GG.SDD.

## Outcome

A disposable `work/` tree has a selected spec and a second spec whose front matter claims the same logical work ID. The #1026 pure interpreter accepts a supplied list containing only the selected pinned file. The existing Linux closed-root `GenerationSourceSnapshot.capture` refuses that omitted physical file as `UnexpectedFile`; when given both exact paths, it returns bytes that the pure interpreter refuses as `DuplicateWorkId`. A second control takes a pathname roster before adding the duplicate: the stale roster yields the same pure false green, while closed-root capture refuses. A complete root with an unrelated candidate succeeds.

## Boundary

This test does not establish a general physical candidate-inventory adapter. `capture` requires a declared exact file set; a path-based discovery pass cannot by itself authorize completeness, and must not be claimed as no-follow traversal. Whole-root capture also rejects unrelated symlinks and nonregular entries, a stricter policy requiring producer acceptance. A bounded follow-up can use an untrusted name proposal only if the closed-root pinned reader independently proves exact physical membership, or extend the pinned reader to discover names through held directory descriptors. Neither route proves a simultaneous cross-root instant or excludes ABA/post-check changes. #1017 selected-source custody, #1018 verification-wave/staging/rollback, Windows concurrency, installed parity, publication, receiver pinning, merge and GS2-10 freeze remain held. No live output or effect is performed.
