# Plan

1. Reproduce foreign commit acceptance through symlinked loose-object files inside direct fanout directories, before and after initial checks.
2. Bound the observed leaf roster to 4096 entries and inspect each present leaf with no-follow `statx` before and after blob reads.
3. Preserve ordinary and absent-foreign-object controls, a 4097-leaf capacity negative, and link-then-remove ABA characterization.
4. Run focused and full Commands tests and a Release warnings-as-errors build; submit an observed-only stacked draft.
