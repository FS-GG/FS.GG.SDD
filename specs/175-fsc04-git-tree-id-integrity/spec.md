# Optional Git Tree-ID Byte Integrity

**Status:** read-only FSC-04 selected-tree custody prerequisite, stacked on draft #1060. **Owner:** FS.GG.SDD.

## Red-before selected child-tree false green

In disposable SHA-1 and SHA-256 repositories, a different valid `.fsgg` tree payload copied under the selected child-tree ID's loose-object filename made `git ls-tree --full-tree <old-commit> -- .fsgg/project.yml` return the foreign blob ID. The foreign blob had its correct own ID, so #1059's blob digest check passed and the old preview returned the foreign bytes under the old selected commit. These two child-tree cases failed before the repair. Root-tree payload substitution was already refused downstream by Git with a generic failure; two independent root controls were also red before the explicit tree refusal.

The preview now reads the verified commit body's root-tree ID, checks the returned root-tree bytes against that ID, selects the `.fsgg` child ID from those held bytes, checks the returned child-tree bytes against its ID, and selects both core file modes and blob IDs from those held bytes. It no longer uses a later pathname-based `ls-tree` result for these selections. Root and child trees have a provisional 1 MiB per-tree cap. Wrong tree content refuses as `TreeIdMismatch`; malformed selected tree records and nonregular selected files refuse. Clean SHA-1 and SHA-256 controls and the existing Git preview tests pass.

## Remaining boundary

This provides a content-addressed chain from the selected commit body through both tree objects to selected blob IDs and bytes, assuming Git object hash collision resistance. It does not pin loose-object file handles, prove physical repository custody at a common instant, prevent a transient object-store swap between separate subprocess reads, or prevent later mutation. The existing no-follow path checks are still point-in-time checks, and the 1 MiB tree cap may refuse valid large trees. A caller-selected commit ID is not producer authority. `ObservedAgreement` and the optional preview remain non-authorizing. #1017 physical custody, #1018 verification/staging/rollback, Windows and installed parity remain open. No generation output, publication, receiver pin, Authority write, protected effect or merge occurs.
