# Provisional Work-Model Source Bundle Contract

**Status:** source-only FSC-04 prerequisite, stacked on draft #1009. **Owner:** FS.GG.SDD.

## Outcome

A pure verifier binds a producer-selected work-model source list to separately captured physical bytes and candidate source digests. It refuses an omitted source, an extra captured or candidate source, a case alias, path escape, stale selected text or raw bytes, and a wrong digest before returning any captured set. It has no output or publication capability.

## Requirements

- FR-001: The caller supplies `ViewGeneration.workModelSnapshots` selection independently of candidate rows. Version 2 and the work ID are exact. This verifier does not infer source authority from the candidate.
- FR-002: The provisional allowed source profile contains `.fsgg/{project,sdd,agents}.yml`, available authored `work/<id>/{spec,clarifications,checklist,plan,tasks,evidence}` files, and optional paths below `readiness/<id>`. The three config paths and `spec.md` are required; a selected readiness source requires selected `evidence.yml`. `charter.md`, path aliases, escaping paths, and performance artifacts elsewhere refuse. The producer may later choose a different versioned profile.
- FR-003: Selected, captured, and candidate paths each have an exact set match with ordinal case-alias refusal. Captured digests must attest to exact raw bytes. A selected raw-byte field, when supplied, must match those bytes.
- FR-004: Decode captured bytes through the same strict `SkillMirror.decodeBody` used by the current command read edge. Compare decoded text to the selected text after applying the producer's evidence `sourceSnapshots` projection. Compare candidate digests to `SchemaVersion.sha256Text` of that selected text, including its CRLF normalization.
- FR-005: The verifier remains pure and read-only. It cannot write or authorize a generated view.

## Characterization and boundary

A disposable red-before fixture shows the v1 one-root contract accepting all three `.fsgg` sources while `work/<id>/spec.md` differs. This is a misuse of the v1 contract, not a defect in its one-root promise. The v2 bundle comparison requires the work path and optional readiness source in the same selected set. Independent tests cover missing and extra physical sources, candidate omission and case alias, wrong selected text and raw bytes, stale and malformed digests, unprojected evidence, disallowed charter, unsafe paths, work ID/version mismatch, and missing evidence for readiness.

This pure join does not prove that the caller selected every producer source or that a supplied capture covers every physical entry in each root. Each single-root capture must independently establish its own closed set; a producer-owned coordinator must define how these roots and optional artifacts are selected and captured. The current producer can select performance artifacts outside `readiness/<id>`, so this provisional profile refuses those rather than claiming production parity. Separate captures are not an atomic cross-root snapshot. ABA, timestamp-hidden writes, mutations after the final check, Windows concurrency, package and installed parity, output rollback, publication, receiver pinning, and the GS2-10 candidate freeze remain separate acceptance boundaries.
