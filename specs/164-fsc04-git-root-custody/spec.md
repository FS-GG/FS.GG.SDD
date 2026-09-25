# Optional Git Worktree-Root Custody Check

**Status:** read-only FSC-04 source-selection prerequisite, stacked on draft #1049. **Owner:** FS.GG.SDD.

## Red-before false green

Git commands launched in a nested directory discover an enclosing repository. The #1049 optional preview accepted a copied `nested/.fsgg/project.yml` and `nested/.fsgg/sdd.yml` as matching the parent repository's commit, even though the supplied nested workspace was not the Git worktree root. A disposable red-before test returned an observed match where the new root-custody refusal was expected.

The commit preview now asks Git for `--is-inside-work-tree` and `--show-prefix` before reading the selected commit. It proceeds only when the supplied directory is the root of that Git worktree. A nested copied workspace and a bare object store refuse as `NotRepositoryRoot`. The actual repository root and a linked worktree root with a `.git` file pass. Downstream byte/mode observations inherit the refusal. This check is source selection only; it does not write output or select an authorized commit.

## Limits

This is a point-in-time Git discovery check, followed by separate Git subprocesses and Linux physical reads. Repository replacement between subprocesses, object-store custody, commit authority, cross-file common instant, ABA and post-check mutation are unresolved. Other `.fsgg`, `work/`, readiness and performance sources remain outside the two-path profile. Windows and installed-package parity remain unqualified. `ObservedAgreement` and all optional Git previews remain non-authorizing. #1017 physical custody and #1018 verification/staging/rollback decisions remain open. No generation output, publication, receiver pin, Authority write, protected effect or merge occurs.
