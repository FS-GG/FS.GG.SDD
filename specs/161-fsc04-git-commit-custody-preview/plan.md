# Plan

1. Read both exact core config blobs from one full immutable Git commit object ID in a disposable repository.
2. Refuse moving refs, noncommit IDs, missing/nonregular entries and oversized blobs; prove replace refs do not redirect the pinned ID.
3. Run focused and full Commands tests and a Release warnings-as-errors build, then open a stacked source-only draft without choosing producer policy.
