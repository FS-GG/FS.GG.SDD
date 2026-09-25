# Plan: Pure Work-Candidate Inventory Preview

1. Reuse #1025's independent duplicate-ID red-before characterization to define the supplied-capture seam.
2. Parse candidate identities from exact captured bytes, require digest/path integrity, and refuse duplicates without returning output bytes.
3. Exercise duplicate, unrelated, malformed, alias, missing selected spec and digest-drift controls.
4. Run focused and full Commands tests plus a warning-clean Release build; open a stacked draft.

Physical candidate discovery and closure are a separate producer-owned step. This preview has no filesystem read or write effect.
