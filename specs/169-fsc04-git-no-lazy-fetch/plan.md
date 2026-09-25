# Plan

1. Construct a disposable local blob-filtered promisor clone and show the preview hydrates a missing core blob before repair.
2. Disable lazy fetching for every preview Git subprocess and require the missing blob to remain absent on refusal.
3. Preserve a fully materialized clone passing control.
4. Run focused and full Commands tests and a Release warnings-as-errors build; submit an observed-only stacked draft.
