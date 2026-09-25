# Repeated Complete Candidate Tree Preview

**Status:** read-only FSC-04 observed-stability prerequisite, stacked on draft #1028. **Owner:** FS.GG.SDD.

## Contract

Given a caller-declared complete set of files under `work/`, capture the whole root twice with the Linux descriptor-pinned, no-follow reader. Interpret each capture through the pure candidate inventory verifier. Refuse if either capture or interpretation fails, if the captured path rosters differ, or if any raw bytes differ. Return only the candidate path preview after both observations agree. The operation does not generate or stage output.

The #1028 disposable interleaving is the red-before control. Adding a duplicate-ID `work/a/spec.md` after `work/a` was visited but while `work/z/spec.md` was being read lets one pinned pass return a selected-only false green. The repeated pass refuses the persistent late file as `UnexpectedFile`. Separate controls cover a stable unrelated candidate, changed bytes between valid passes, changed rosters between valid passes, and a linked candidate.

## Boundary

Two matching observations are an instability screen, not an atomic snapshot. An adversary can mutate and restore a tree between passes or after the second pass; metadata checks cannot rule out ABA or timestamp-hidden writes. The caller still supplies the candidate file declaration, and this preview does not bind `work/` to `.fsgg` or optional performance roots at one instant. The strict whole-root rule also needs producer-owner acceptance for unrelated entries. Windows has no equivalent pinned production path in this slice. #1017 physical custody, #1018 verification-wave/staging/rollback, installed parity, publication, receiver pinning, merge and GS2-10 freeze remain held.
