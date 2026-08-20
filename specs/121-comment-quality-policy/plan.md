# Implementation Plan: Durable Comment-Quality Policy

**Spec**: `specs/121-comment-quality-policy/spec.md` · **Item**: FS.GG.SDD#886

## Design

Add one `Comment Quality` section to the generic product constitution literal and
to its authoritative content contract. Keep those two bodies byte-identical.
Adopt the same complete obligations in the SDD producer constitution and active
Spec Kit constitution, using their existing local structure.

The init write path remains unchanged. Its existing `AgentGuidanceTarget`
safe-write behavior continues to preserve an authored constitution, so existing
workspaces receive no implicit migration.

## Verification

1. Assert that a real init emits bytes identical to the fenced body in the
   authoritative constitution contract.
2. Assert the load-bearing policy clauses explicitly so accidental semantic
   weakening remains visible even if both projections move together.
3. Invert one required seed clause and observe the focused test fail, then restore
   it and rerun the test green.
4. Run all init tests and the full solution suite; re-pin only the deterministic
   constitution digest in the full-shape command-report golden.

## Compatibility and Migration

No public F# API, command-report schema, or persisted artifact schema changes.
New workspaces receive the policy; existing authored constitutions adopt it
explicitly. Publication precedes any downstream fleet pin.
