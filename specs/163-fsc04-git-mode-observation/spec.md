# Optional Git Executable-Mode Observation

**Status:** read-only FSC-04 source-custody prerequisite, stacked on draft #1048. **Owner:** FS.GG.SDD.

## Red-before gap and bounded change

The #1048 byte comparison accepts a selected working-tree file whose executable bit differs from the corresponding Git blob entry. Disposable red-before controls showed both directions: a committed `100644` file made executable and a committed `100755` file made nonexecutable. The byte-only preview passed both.

The Linux selected-file reader now records mode from the same opened descriptor used for its two byte passes. Mode participates in the opened-fd pre/middle/post stability stamps, so a mode change during the read refuses as `FileUnstable`. `verifyCoreBytesAndGitModeObserved` compares each selected file's raw bytes and executable policy to the two named commit entries. Git regular-file modes store the executable distinction (`100644` versus `100755`); this preview therefore compares whether any of the POSIX execute bits are present. It refuses a byte difference, symlink or other physical read failure, unavailable opened mode, or executable-policy mismatch.

Controls include both mismatched directions, both clean committed modes, changed bytes, a symlink, and a deterministic mode mutation during an opened-fd read. The existing #1048 byte-only API retains its narrower behavior. This is an optional observation, not an accepted producer rule.

## Limits

The mode observation does not compare exact read/write bits, special permission bits, uid/gid, ACLs, xattrs, or Git index state. The two paths are read sequentially; neither a common instant nor absence of ABA or later mutation is proven. The selected commit ID is caller supplied and unauthenticated; other `.fsgg`, `work/`, readiness and performance sources are not closed. Git object-store/repository custody, Windows and installed-package parity remain unqualified. `ObservedAgreement` and this result remain non-authorizing. #1017 physical custody and #1018 verification/staging/rollback decisions remain open. No generation output, publication, receiver pin, Authority write, protected effect, or merge occurs.
