# Pure Work-Candidate Inventory Preview

**Status:** source-only FSC-04 candidate-identity prerequisite, stacked on draft #1025. **Owner:** FS.GG.SDD.

## Contract

- FR-001: Interpret a separately supplied `work/` file capture set without filesystem reads or output effects. Verify each captured file's raw digest and reject invalid or case-aliased paths.
- FR-002: Require the selected work item's exact `work/<id>/spec.md` path. Parse immediate-child `spec.md` and `charter.md` candidate files from captured bytes; reject malformed candidate text instead of silently dropping its identity.
- FR-003: Refuse any candidate in another work directory whose authored logical work ID equals the selected ID. Return only candidate paths on success, never bytes or an output effect.

## Evidence

#1025 showed the prior physical source preview accepting a selected work model while a separately read sibling spec caused the producer's `duplicateWorkId` diagnostic and suppressed output. This pure adapter refuses the same duplicate when both candidates are supplied. Independent controls cover an unrelated candidate, unrelated noncandidate file, malformed candidate, case alias, exact selected-path omission, and raw-digest mismatch.

## Limits

The caller may omit a physical candidate from the supplied set. This module cannot prove completeness, no-follow traversal, roster stability, a common capture instant, or that a `CapturedFile` came from the repository. A producer-owned physical `work/` candidate inventory remains required before this preview could gate output. Rejecting malformed foreign candidates is a conservative policy stronger than the current `duplicateWorkIdDiagnostics` reader, which ignores parse failures; adoption needs an explicit owner decision. #1017 selected-source custody, #1018 verification-wave/staging/rollback decision, ABA/post-check, Windows concurrency, installed parity, publication, receiver pinning, merge and GS2-10 freeze remain held.
