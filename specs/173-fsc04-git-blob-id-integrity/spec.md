# Optional Git Blob-ID Byte Integrity

**Status:** read-only FSC-04 selected-blob custody prerequisite, stacked on draft #1058. **Owner:** FS.GG.SDD.

## Red-before equal-length object substitution

The preview's `cat-file -s` and `cat-file blob` calls accepted an equal-length, valid compressed loose blob payload copied under a different selected blob ID's filename. Git returned the foreign bytes without rejecting the name/content mismatch. Disposable SHA-1 and SHA-256 repositories each demonstrated this for both `.fsgg/project.yml` and `.fsgg/sdd.yml`; the previous preview returned `Ok` with those bytes.

The read-only preview now independently computes the Git blob object ID from `blob <length>\0` and the actual bytes returned by `cat-file`. A mismatch refuses as `BlobIdMismatch` before an observation is returned. Clean selected blobs in both Git object formats remain accepted. The hash uses incremental input so it does not create another full-size payload copy.

## Remaining boundary

This check binds each returned selected blob's bytes to its reported blob ID at the read instant. It does not independently authenticate the commit or tree object bytes, pin Git's object-file handle, prove a common instant across the two files or other source roots, or defeat object-store ABA and later mutation. A caller-supplied commit ID is not producer authority. `ObservedAgreement` and the optional Git preview remain non-authorizing. #1017 physical custody and #1018 verification/staging/rollback decisions remain open. Windows and installed parity are unproven. No generation output, publication, receiver pin, Authority write, protected effect or merge occurs.
