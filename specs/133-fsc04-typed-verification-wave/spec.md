# Typed Work-Model Verification-Wave Preview

**Status:** pure FSC-04 source prerequisite, stacked on draft #1018. **Owner:** FS.GG.SDD.

## Contract

- FR-001: A proposed generated work-model body must name the exact work ID and `readiness/<id>/work-model.json` output path. Its root `sources[].sourceDigest` and its single `generatedViews[kind=workModel].sources[].digest` must each exactly match the independent v2 candidate rows. Malformed rows, duplicate or case-aliased paths, stale digests, wrong work ID, and wrong output path refuse.
- FR-002: `prepare` returns a private typed value only after the proposed output and candidate agree. `verifyCaptured` then runs the pure v2 bundle comparison over the same selected snapshots and supplied captures. Refusals cannot return a verified preview.
- FR-003: A successful result contains an output digest and source paths for inspection. It contains no JSON body or `CommandEffect` and cannot commit output. Physical recapture, staging, and the live command driver remain outside this pure module.

## Evidence

A red-before control changes the proposed work-model JSON's root source digest while keeping selected snapshots, physical captures, and candidate rows valid. The v2 source verifier still accepts those unchanged inputs because it does not receive proposed output bytes. The typed `prepare` step refuses. Independent controls cover stale candidate digest, wrong output path and work ID, a duplicate case alias, and changed supplied physical capture. A valid body and captured source set yield a read-only preview.

## Acceptance boundary

This is a preview of a future verification wave, not a live gate. #1018 proves the current effect batch cannot use a same-batch verifier as a failure barrier, and authoring commands may co-batch source writes with generated output. Producer owner must choose staged source bytes and rollback or a separate post-write verification transaction before adoption. Supplied captures still need #1017 physical custody and a temporal join. Cross-root instant consistency, ABA/timestamp-hidden/post-check changes, Windows concurrency, installed parity, publication, receiver pinning, output rollback, and GS2-10 freeze remain held.
