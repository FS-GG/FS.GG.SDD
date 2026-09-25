# Repeated Bundle and Work-Tree Join Preview

**Status:** read-only FSC-04 temporal source prerequisite, stacked on draft #1043. **Owner:** FS.GG.SDD.

## Red-before and repair

The #1043 single joined bundle/tree observation can succeed even if `.fsgg/project.yml` acquires a UTF-8 BOM immediately after the tree capture returns. Its selected text and candidate digest remain unchanged. An independent disposable control confirms that success on the #1043 base.

The repeated preview captures bundle, complete work tree, bundle, complete work tree. Each pair passes the #1043 exact-overlap join, then bundle and work-tree path rosters and raw bytes are compared across passes. The controls show refusal for a raw `.fsgg` change, a raw selected work-spec change that preserves decoded text, and an unrelated candidate added between otherwise valid pairs. A stable tree with an unrelated candidate succeeds. Results contain only bundle and candidate paths; no output body, staged bytes, or effect is returned.

## Boundary

Four sequential captures do not prove a common `.fsgg`/`work`/performance instant. A schedule may mutate and restore bytes between observations or after the final check; ABA, timestamp-hidden writes and post-check mutation remain open. The preview does not run the current generator or authorize staging. #1040 Unicode and resource policy, strict whole-tree treatment of unrelated links/special entries, Windows/installed parity, #1017 custody, #1018 verification/staging/rollback, producer publication, receiver adoption, merge and GS2-10 freeze remain held. No live output or effect is produced.
