# Plan

1. Reproduce a registered repository reading a sibling's commit through `objects/info/alternates`, including a late introduction after initial checks.
2. Refuse present alternate declarations before and after blob reads, using Git's common-store path for linked worktrees.
3. Keep self-contained, linked-worktree, empty-declaration and add-then-remove ABA controls.
4. Run focused and full Commands tests and a Release warnings-as-errors build; submit an observed-only stacked draft.
