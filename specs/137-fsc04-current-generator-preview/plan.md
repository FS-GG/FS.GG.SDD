# Plan: Assembly-Bound Work-Model Generator Preview

1. Keep #1022's caller-version seam for characterization, then prove a jointly forged proposal/version passes it.
2. Add a separate internal read-only wrapper with no generator-version argument. It supplies `SchemaVersion.currentGeneratorVersion()` to #1022.
3. Test wrong version, wrong ID, and stable current identity against disposable pinned source fixtures.
4. Run the focused and full Commands tests and warning-clean Release build before opening a stacked draft.

The wrapper is a proposed producer prerequisite; it does not wire live effects or settle staging and rollback policy.
