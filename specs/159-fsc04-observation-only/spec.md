# Four-Capture Observation Boundary

**Status:** read-only FSC-04 temporal limitation characterization, stacked on draft #1044. **Owner:** FS.GG.SDD.

## Deterministic controls

The #1044 bundle/tree/bundle/tree preview observes matching raw bytes at four checks. A disposable callback changes `.fsgg/project.yml` and restores its original bytes after the first tree capture and before the second bundle capture. Every captured set matches, so the preview succeeds although an in-place ABA write occurred. Another callback changes that source after the final tree capture; the preview succeeds while the current file already differs. Both controls pass on the #1044 base and demonstrate why further finite checks cannot authorize a common-instant or post-return currentness claim.

The success type is now `ObservedAgreement` carrying the same path-only preview. Callers must handle this observational result explicitly. The four-capture checks and refusals are unchanged; no source or output is written by the preview. Tests cover both schedules and retain the stable result and previously detected mutation controls.

## Boundary

`ObservedAgreement` is not a producer authorization. ABA, timestamp-hidden mutation, repeated mixed instants, post-check changes and non-atomic `.fsgg`/`work`/performance capture remain open. A stronger claim needs a separately accepted producer transaction or custody contract, not another finite read. #1040 Unicode/resource policies, strict whole-tree policy, Windows/installed parity, #1017 custody, #1018 verification/staging/rollback, publication, receiver adoption, merge and GS2-10 freeze remain held. No live output or effect is produced.
