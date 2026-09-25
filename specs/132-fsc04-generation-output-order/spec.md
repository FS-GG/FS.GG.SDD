# Work-Model Generation Output Ordering Characterization

**Status:** FSC-04 read-only design prerequisite, stacked on draft #1017. **Owner:** FS.GG.SDD.

## Observed contract

- FR-001: A source verification failure must prevent `readiness/<id>/work-model.json` from being committed. A verifier placed before `WriteFile` in the same `CommandEffect` batch does not satisfy this requirement: `CommandEffects.interpretAll` evaluates every effect via `List.map` before folding any result through `CommandWorkflow.update`.
- FR-002: A later effect failure does not roll back an earlier generated-view write. `WriteFile` is atomic for its own destination but the effect batch is not a transaction.
- FR-003: A producer integration must carry the selected work-model source set and candidate digests to a read-only verification wave, consume its result, and only then schedule generated-view output. Because lifecycle handlers currently co-batch authored writes before generated writes, the design must also decide whether the verified source set describes pre-write physical files, staged authored bytes, or a post-authored-write state with rollback.

## Evidence

Disposable interpreter controls show both directions. A failing authored `spec.md` write is followed by a successful generated work-model write in the same batch. In reverse order, a successful generated work-model write remains committed after a later directory effect fails. The controls run only under a temporary test root and leave no production output.

`Prerequisites.runHandlerWithBlockedSeed` concatenates `writeEffects @ generatedEffects` for an unblocked stage; `computeSpecifyPlan` and the other authoring handlers can therefore schedule authored and generated writes together. The #1015 pre-output read-state gate acts at pure planning time but does not establish physical source custody. The #1017 verifier is read-only and is not wired into the live generation wave.

## Unresolved boundary

Adding a verifier as one more effect in the current batch would not stop subsequent writes on refusal. Inserting physical verification before authored writes can disagree with a newly authored source that the generated view already includes. Inserting it after authored writes requires a rollback or staging policy if verification fails. This draft records that design choice for the producer owner; it does not authorize a live gate flip. Cross-root instant consistency, ABA/timestamp-hidden/post-check changes, Windows concurrency, installed parity, publication, receiver pinning, and GS2-10 freeze remain held.
