# Feature 115 — Independently verifiable performance evidence

**Tier:** 1 (public evidence and governance-handoff contract change)

## Outcome

SDD accepts a versioned `performance-evidence-v1` artifact containing raw measurements and the
facts that bind those measurements to a workload, budget, host, package set, and measurement
environment. SDD recomputes the result; a producer-authored verdict is never sufficient.

## Requirements

- **FR-001:** A performance artifact MUST identify `performance-evidence-v1` and carry one or more
  raw duration and catch-up sample sets.
- **FR-002:** Every sample set MUST bind its workload id and definition digest, normal/stress
  class, target and thresholds, warmup and sample policy, host profile, package versions,
  measurement scope and mode, capability facts, capture time/currency token, and probe/readback
  contamination state. Capture times MUST use ISO-8601 with an explicit offset, and the
  contamination state MUST be an explicitly present JSON boolean.
- **FR-002a:** The declaration MUST independently name the expected digest for every workload,
  the current currency token, and a capture-not-before timestamp. Artifact values MUST match
  those declaration-owned inputs.
- **FR-003:** SDD MUST recompute p95 and p99 with the documented nearest-rank algorithm and
  sustained catch-up as the maximum raw catch-up sample.
- **FR-004:** A claimed pass MUST be rejected when recomputed measurements fail.
- **FR-005:** Sets for one workload MUST NOT be combined across workload-definition digests,
  hosts, package versions, modes, scopes, capabilities, policies, capture times, contamination
  states, or currency tokens. This applies equally to normal-play and present stress sets.
- **FR-006:** Every declared normal workload MUST have bound samples; stress workloads remain
  diagnostic context and MUST NOT replace a missing normal workload.
- **FR-007:** Headless or probe/readback-contaminated measurements MUST NOT satisfy a declaration
  requiring live-compositor evidence.
- **FR-008:** Parsed measurement facts and recomputed statistics MUST survive work-model and
  governance-handoff projection without being reduced to a verdict summary.
- **FR-009:** Missing, malformed, unbound, stale/mixed, or capability-inadequate evidence MUST fail
  closed with actionable reasons.

## Acceptance scenarios

1. A producer says `claimedBudgetPassed: true`, but a raw p99 sample exceeds the declaration;
   evaluation is failed and reports the recomputed percentile.
2. Reordering identical raw samples produces identical nearest-rank p95/p99 and maximum catch-up.
3. Two sets for one workload with different host profiles or definition digests are malformed and
   are not combined.
4. Headless samples cannot satisfy `liveCompositorRequired: true`.
5. A well-bound M5/M6 artifact passes and Governance receives raw samples, bindings, and computed
   statistics; an M0-style summary-only artifact is malformed.

## Non-goals

- Trusting the provenance of the machine that wrote the artifact; CI/Governance owns provenance.
- Choosing product workloads or performance thresholds.
- Treating stress-throughput measurements as normal-play acceptance.
