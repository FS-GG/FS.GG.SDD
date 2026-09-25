# Optional Git Registry Recheck After Commit Reads

**Status:** read-only FSC-04 temporal custody prerequisite, stacked on draft #1051. **Owner:** FS.GG.SDD.

## Red-before physical switch

The #1051 preview checks that its supplied root is registered before reading Git objects. A disposable hook then moves repository A's `.git` directory aside and replaces it with a symlink to sibling repository B's `.git`. The caller selects B's full commit ID, which A cannot read. Before this repair, the preview read B's commit after A's registration check and returned an observed result: a false green for continuous repository custody.

The preview now repeats the top-level and exact-root registration checks after both selected blob reads. A persistent switch to B refuses as `RepositoryChanged`. A stable registered A still passes. The hooks exist only for deterministic disposable race tests; ordinary calls supply no actions.

## ABA control and limits

The independent swap-back control restores A's `.git` directory immediately before the final check. It still returns B's bytes and commit ID, showing that two registration checks cannot establish continuous custody or a common instant. Repository identity and object-store handles are not pinned across Git subprocesses. Local Git metadata can also be rewritten. The selected commit remains caller supplied and unauthenticated; complete-source selection, cross-root atomicity, Windows and installed parity remain unresolved. `ObservedAgreement` and all optional Git previews remain non-authorizing. #1017 physical custody and #1018 verification/staging/rollback decisions remain open. No generation output, publication, receiver pin, Authority write, protected effect or merge occurs.
