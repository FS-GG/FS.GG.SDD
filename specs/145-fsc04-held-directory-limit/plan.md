# Plan: Held Directory Descriptor Limit

1. Characterize the #1030 reader on disposable 256- and 257-child trees; require the latter to fail before repair.
2. Add a named fail-closed refusal before opening a child when 256 descriptors are already retained.
3. Verify the exact boundary and repeated-refusal descriptor cleanup using `/proc/self/fd` targets scoped to the disposable tree.
4. Run focused and full Commands tests plus a warning-clean Release build; open an exact-head stacked draft without output effects.
