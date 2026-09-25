# Blocking-Diagnostics Work-Model Preview

**Status:** source-only FSC-04 producer-policy prerequisite, stacked on draft #1023. **Owner:** FS.GG.SDD.

## Contract

- FR-001: Regenerate the model from independently selected snapshots using the referenced assembly's current generator identity.
- FR-002: Apply the producer's existing `WorkModel.blockingDiagnostics` rule before any physical capture. Return sorted distinct diagnostic IDs on refusal, not a generated body or write effect.
- FR-003: When there are no blocking diagnostics, delegate to #1023's exact-output, assembly-bound, twice-pinned physical source preview.

## Red-before control

A disposable fixture generates exact JSON from matching selected and physical sources, but its incomplete project configuration produces error diagnostics. #1023 accepts that byte-exact proposal, while current `ViewGeneration.generatedViewPlan` withholds `WriteFile` when `blockingModelDiagnostics` is nonempty. This preview refuses before capture. A separate normalized fixture with no blocking diagnostics succeeds through the pinned reader.

## Limit

This mirrors only the producer's model-diagnostic gate. Command diagnostics, verification-wave ordering and authored-source staging/rollback still require #1018's producer decision. #1017 physical custody, cross-root instant consistency, ABA/post-check and Windows concurrency, installed parity, publication, receiver pinning, and GS2-10 freeze remain holds. No live output or effect is authorized.
