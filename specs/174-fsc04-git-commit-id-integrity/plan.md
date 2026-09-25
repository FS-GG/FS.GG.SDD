# Plan

1. In SHA-1 and SHA-256 disposable repositories, place a valid foreign commit payload under the selected commit ID and record `cat-file` acceptance versus `ls-tree` refusal.
2. Add an independent commit-body digest check and a provisional 1 MiB cap before tree selection; share the digest helper with #1059's blob check.
3. Keep clean-format controls, foreign-payload refusal controls and a large-commit capacity negative.
4. Run focused/full Commands tests and a Release warnings-as-errors build; submit a non-authorizing stacked draft.
