# Repeated Physical Work-Model Capture Preview

**Status:** read-only FSC-04 temporal characterization, stacked on draft #1020. **Owner:** FS.GG.SDD.

## Contract

- FR-001: Run the #1017 pinned physical source capture and v2 bundle verification twice against the same independently selected snapshots and candidate. A refusal on either pass is blocking.
- FR-002: Compare exact raw bytes by path across successful passes. A change refuses even when both passes decode to the same selected text and pass the v2 candidate digest check.
- FR-003: A successful result contains only work ID and source path/digest pairs for inspection. It contains no captured bytes, generated output body, or `CommandEffect`.

## Evidence

A deterministic red-before fixture starts with `.fsgg/project.yml=A0` and `.fsgg/sdd.yml=B0`. It captures A0, changes project to A1 and then sdd to B1, and captures B1. The selected/candidate pair A0/B1 never existed simultaneously, but the single-pass v2 verifier accepts the individually pinned mixed captures. The repeated preview refuses on its second pass because project now reads A1. A separate fixture changes the project's raw bytes by adding a UTF-8 BOM; both individual v2 passes accept the same decoded text and candidate, while the repeated preview refuses `RawChanged`. Stable sources pass.

## Limit

Two sequential passes cannot prove a simultaneous cross-file or cross-root instant. ABA, timestamp-hidden mutation, changes after the final pass, and a schedule that recreates the same mixed set across passes can escape. The #1018 verification wave and authored-source staging/rollback decision remain unresolved, and #1017 physical custody remains a prerequisite. Windows concurrency, installed parity, output rollback, publication, receiver pinning, and GS2-10 freeze remain held.
