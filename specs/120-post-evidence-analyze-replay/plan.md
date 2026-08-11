# Implementation Plan: Convergent Post-Evidence Analyze Replay

**Spec**: `specs/120-post-evidence-analyze-replay/spec.md` · **Item**: FS.GG.SDD#857

## Design

The common work-model generator obtains the on-disk evidence snapshot when it
exists. It canonicalises only the `sourceSnapshots` block for the evidence source
digest. `evidence` still parses and validates the untouched
artifact, so its stale-source check remains an honest comparison.

The same canonicalisation is used both for generation and generated-view currency
checking. Therefore the work model sees evidence meaning, not its own recursively
recorded provenance, and the two paths cannot disagree about currency.

## Verification

1. Drive a disposable project to ship-ready.
2. Replay `analyze -> evidence -> verify -> ship -> refresh -> agents` once to
   migrate its prior pre-evidence analysis representation.
3. Repeat that exact sequence and assert `NoChange` plus byte-identical readiness,
   summary, and agent-guidance views.
4. Run the Commands test tier and the full lifecycle package replay.

## Compatibility

No public API or persisted schema version changes. Existing evidence files retain
their full `sourceSnapshots` on disk; only the internal source digest used by the
derived work model is canonicalised.
