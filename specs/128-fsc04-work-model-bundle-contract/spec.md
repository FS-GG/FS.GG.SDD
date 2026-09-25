# Provisional Work-Model Source Bundle Contract

**Status:** source-only FSC-04 prerequisite, stacked on draft #1009. **Owner:** FS.GG.SDD.

## Outcome

A pure verifier binds a producer-selected work-model source list to separately captured physical bytes and candidate source digests. It refuses an omitted source, an extra captured or candidate source, a case alias, path escape, stale selected text or raw bytes, and a wrong digest before returning any captured set. It has no output or publication capability.

## Requirements

- FR-001: The caller supplies `ViewGeneration.workModelSnapshots` selection independently of candidate rows. Version 2 and the work ID are exact. This verifier does not infer source authority from the candidate.
- FR-002: The provisional allowed source profile contains `.fsgg/{project,sdd,agents}.yml`, available authored `work/<id>/{spec,clarifications,checklist,plan,tasks,evidence}` files, and repository-relative performance paths declared by the captured physical `evidence.yml`. The three config paths and `spec.md` are required. A selected performance path requires selected evidence. `charter.md`, unbound performance paths, aliases, and escaping paths refuse.
- FR-003: Selected, captured, and candidate paths each have an exact set match with ordinal case-alias refusal. Captured digests must attest to exact raw bytes. A selected raw-byte field, when supplied, must match those bytes.
- FR-004: Decode captured bytes through the same strict `SkillMirror.decodeBody` used by the current command read edge. Compare decoded text to the selected text after applying the producer's evidence `sourceSnapshots` projection. Compare candidate digests to `SchemaVersion.sha256Text` of that selected text, including its CRLF normalization.
- FR-005: The verifier remains pure and read-only. It cannot write or authorize a generated view.
- FR-006: The physical evidence bytes must parse without diagnostics and match the selected work ID. Every declared performance artifact must be in the selected source set, even if the current producer would omit it after a missing or failed read. This stricter omission rule is a provisional fail-closed contract, not current production parity.

## Characterization and boundary

A disposable red-before fixture shows the v1 one-root contract accepting all three `.fsgg` sources while `work/<id>/spec.md` differs. This is a misuse of the v1 contract, not a defect in its one-root promise. The v2 bundle comparison requires the work path and declared performance sources in the same selected set. Independent tests cover missing and extra physical sources, candidate omission and case alias, wrong selected text and raw bytes, stale and malformed digests, unprojected evidence, disallowed charter, unsafe paths, work ID/version mismatch, and missing evidence for performance selection.

Default-branch `ViewGeneration.performanceEvidenceSnapshots` reads `budget.ArtifactPath` without restricting it to `readiness/<id>`, while the evidence cited-path rule accepts any contained repository-relative path. A direct producer test selects `tests/performance.txt`. Before this correction, the provisional comparator refused that valid selection and accepted a bundle that omitted a declared performance artifact from both candidate and captured lists. The corrected comparator derives the allowed path set from captured evidence and refuses that omission. It also refuses malformed physical evidence and selected performance paths absent from the declaration.

This pure join does not prove that the caller selected every producer source or that a supplied capture covers every physical entry in each root. Each single-root capture must independently establish its own closed set; a producer-owned coordinator must define how arbitrary contained performance paths are captured without admitting unrelated files. The current producer omits an artifact when its read is absent or failed; the provisional verifier instead refuses a declared omission, so adoption needs an explicit producer decision. Separate captures are not an atomic cross-root snapshot. ABA, timestamp-hidden writes, mutations after the final check, Windows concurrency, package and installed parity, output rollback, publication, receiver pinning, and the GS2-10 candidate freeze remain separate acceptance boundaries.
