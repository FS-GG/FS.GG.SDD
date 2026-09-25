# Optional Git Promisor No-Lazy-Fetch Guard

**Status:** read-only FSC-04 object-read custody prerequisite, stacked on draft #1054. **Owner:** FS.GG.SDD.

## Red-before effect in a disposable partial clone

A local source repository allows Git blob filtering. A disposable `--filter=blob:none --no-checkout` clone has the selected commit and trees but lacks the core blob objects. Before this repair, the optional preview's `git cat-file -s` call lazily fetched a missing blob from the local promisor remote, changed the clone's object store and accepted the commit. An independent no-lazy `cat-file -e` check showed the blob absent before the preview and present afterward.

Every Git subprocess launched by the preview now sets `GIT_NO_LAZY_FETCH=1`. The partial clone refuses its missing local blob as `GitFailure`, and the independent no-lazy check confirms the blob remains absent. A fully materialized clone still reads both committed core blobs. The test uses only disposable local repositories and a local `file://` promisor source.

## Limits

This guard addresses Git's lazy promisor fetch for missing objects; it does not prove every Git command is effect-free or pin repository/object-store handles. Other Git metadata writes or helper behavior, local object replacement, alternate ABA, selected-commit authority, complete-source selection, common-instant capture, Windows and installed parity remain unqualified. `ObservedAgreement` and all optional Git previews remain non-authorizing. #1017 physical custody and #1018 verification/staging/rollback decisions remain open. No production generation output, package publication, receiver pin, Authority write, protected effect or merge occurs.
