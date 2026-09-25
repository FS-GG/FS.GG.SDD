# Plan: Direct Pinned First-Pass Read

1. Add a stricter warmed 8 MiB allocation control on #1038 and require it to fail before repair.
2. Fill one budget-checked owned array directly from the opened descriptor, refusing short or extra-byte results before acceptance.
3. Add deterministic after-length mutation controls for shrink, growth within budgets and growth across the file cap; run existing race/capacity and full Commands tests plus warning-clean Release build.
4. Open an exact-head stacked draft without output effects or installed-parity claims.
