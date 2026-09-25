# Provisional Performance Physical Join

**Status:** read-only FSC-04 prerequisite, stacked on draft #1015. **Owner:** FS.GG.SDD.

## Contract

- FR-001: A caller supplies its independently selected work-model snapshots and core physical captures. The join derives declared performance paths from the captured `work/<id>/evidence.yml` bytes, never candidate rows or an ambient directory scan.
- FR-002: Each declared performance path must be present in the producer selection and absent from the supplied core capture set. Re-capture it through the Linux pinned selected-file adapter. Linked files, aliases, nonregular files, missing files, and observed mutation must refuse.
- FR-003: Pass the core captures plus fresh performance captures through the v2 bundle verifier. Selected text and raw bytes, physical digests, candidate paths and digests, and parsed evidence declarations must match before any capture list is returned.
- FR-004: The join is read-only and has no generated output, publication, or receiver effect.

## Evidence and limit

A disposable red-before control changes `tests/performance.txt` after an earlier physical capture; the pure v2 verifier still accepts that stale supplied capture, as its contract allows. The new read-only join re-captures the file and refuses the changed bytes. Independent controls cover a crowded parent with unrelated regular and linked siblings, a linked selected file, a case alias, and an omitted selection.

The join does not re-capture or prove complete closure of supplied `.fsgg` and `work/<id>` core captures. A stale core `evidence.yml` can therefore select declarations from a different instant than current work and performance files. It is a prerequisite for a producer-owned complete-source coordinator, not a generation authorization. Cross-root atomicity, ABA, timestamp-hidden writes, changes after final checks, Windows concurrency, installed parity, output rollback, GS2-10 freeze, publication, and receiver pinning remain separate.
