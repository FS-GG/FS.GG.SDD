# Work Bundle and Discovered Tree Byte Join

**Status:** read-only FSC-04 source-overlap prerequisite, stacked on draft #1042. **Owner:** FS.GG.SDD.

## Red-before and repair

The verified work-model bundle contains independently pinned selected `.fsgg`, `work/`, and any declared performance sources. The #1042 pinned candidate discovery captures the complete `work/` tree. Separately, both verifiers succeed if `work/sample/spec.md` changes from one valid body to another between their captures; they return evidence from different instants. A separate control changes `work/sample/tasks.yml`, which the inventory parser does not interpret as a candidate.

This preview re-verifies the bundle, verifies the complete discovered tree as a candidate inventory, then requires every selected `work/` bundle file to occur in the tree with identical raw bytes. Missing or changed overlap refuses. The physical wrapper uses the existing pinned core/performance bundle capture and pinned complete-tree discovery. Independent tests cover changed selected spec, changed noncandidate work source, missing selected work file, a stable unrelated candidate, and a duplicate work-ID candidate. The result contains paths only, without captured bytes or an output effect.

## Boundary

The one bundle capture followed by one tree capture is not a simultaneous `.fsgg`/`work`/performance snapshot. An adversary can mutate and restore data between checks or after return; ABA, timestamp-hidden writes and post-check mutation remain open. The preview does not run the current generator or authorize staging. #1040 Unicode and resource policies, strict whole-tree treatment of unrelated links/special entries, Windows/installed parity, #1017 custody, #1018 verification/staging/rollback, producer publication, receiver adoption, merge and GS2-10 freeze remain held. No live output or effect is produced.
