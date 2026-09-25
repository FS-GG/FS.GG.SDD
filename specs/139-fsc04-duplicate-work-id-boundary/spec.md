# Duplicate Work-ID Candidate Boundary

**Status:** read-only FSC-04 command-diagnostic characterization, stacked on draft #1024. **Owner:** FS.GG.SDD.

## Outcome

A disposable fixture adds `work/other/spec.md` whose authored front matter claims the selected work ID. The existing `EarlyStageAuthoring.duplicateWorkIdDiagnostics` reports `duplicateWorkId`, and `ViewGeneration.generatedViewPlan` withholds output effects. #1024's pinned source preview still succeeds because its recognized profile deliberately covers the selected work item, not every other candidate under `work/`. An unrelated sibling whose front matter claims its own work ID does not trigger the diagnostic.

## Boundary

This is a characterization, not a new authorizing check. A trustworthy pre-output duplicate-ID gate needs a producer-owned complete candidate inventory and physical closure of `work/` candidates, including no-follow reads, aliases, roster mutation and concurrent content changes. Passing a caller-supplied diagnostic list or a stale interpreted-effect log to the preview would not establish that closure. #1017 selected-source custody and #1018 verification-wave/staging/rollback decision remain separate. Cross-root atomicity, ABA/post-check, Windows concurrency, installed parity, publication, receiver pinning, merge and GS2-10 freeze remain held. No output is written by this test.
