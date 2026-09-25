# Plan

1. Reproduce foreign commit acceptance after replacing a registered repository's object directory with a symlink, both before and after initial checks.
2. Require Git's active object path to equal the common Git directory's direct child and inspect that child with no-follow `statx` before and after blob reads.
3. Preserve ordinary and linked-worktree controls plus a redirect-then-restore ABA characterization.
4. Run focused and full Commands tests and a Release warnings-as-errors build; submit an observed-only stacked draft.
