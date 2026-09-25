# Plan

1. Reproduce foreign commit acceptance through symlinked `.idx`/`.pack` leaves inside a direct pack directory, before and after initial checks.
2. Bound the observed pack-child roster to 4096 entries and inspect each present child with no-follow `statx` before and after object reads.
3. Preserve ordinary/absent-foreign-object controls, a 4097-child capacity negative and a link-then-remove ABA characterization.
4. Run focused and full Commands tests and a Release warnings-as-errors build; submit a non-authorizing stacked draft.
