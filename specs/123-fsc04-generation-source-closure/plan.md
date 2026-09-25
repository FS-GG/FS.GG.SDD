# Plan: Generation Source Closure

**Source:** [spec.md](spec.md). **FSC lane:** 04, provisional source-only. **Owner:** FS.GG.SDD.

## Design

Reuse the existing `GenerationManifest.isStale` public `.fsi` surface. Compare sorted source-path/digest pairs after rejecting empty or duplicate path lists. Exact path equality makes a wrong `readiness/<work-id>/` root stale; no new path format or schema is introduced. The function remains pure and deterministic.

## Constitution and implementation order

The existing `.fsi` already fixes the public shape, so no new signature or API baseline is needed. Add semantic tests first, observe the missing-producer and duplicate cases fail, then change the `.fs` body. This is a Tier 1 behavior correction because the currency result changes; the data model and serialized bytes do not. MVU is unnecessary for a pure comparator. Both agent runtimes use the same library; no agent-skill text changes.

## Verification and join

Run focused `FS.GG.SDD.Artifacts.Tests` and the normal project build. Keep the PR draft; no producer package release, receiver pin, gate flip or protected V2 merge follows from local green. A later FSC-04 owner must qualify installed producer bytes, consumers and wrong-root filesystem effects before adoption.
