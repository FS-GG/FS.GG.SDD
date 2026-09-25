# Plan

1. Reproduce foreign commit acceptance through symlinked loose-object fanout directories in a disposable registered repository, before and after initial checks.
2. Inspect all 256 possible two-hex fanout children with no-follow `statx` before and after selected blob reads.
3. Preserve ordinary loose-object and absent-foreign-object controls plus redirect-then-restore ABA characterization.
4. Run focused and full Commands tests and a Release warnings-as-errors build; submit an observed-only stacked draft.
