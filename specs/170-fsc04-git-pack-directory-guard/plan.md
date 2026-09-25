# Plan

1. Pack a sibling repository's commit and reproduce acceptance through a symlinked `objects/pack` child, before and after initial checks.
2. Require Git's reported pack path to be the checked object directory's direct child and use no-follow `statx` before and after blob reads.
3. Preserve ordinary and linked-worktree controls plus a redirect-then-restore ABA characterization.
4. Run focused and full Commands tests and a Release warnings-as-errors build; submit an observed-only stacked draft.
