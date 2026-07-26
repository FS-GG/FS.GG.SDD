# Performance budgets

An evidence declaration may activate a normal-play performance gate:

```yaml
performanceBudget:
  artifactPath: readiness/013-map-motion/performance-evidence.json
  targetFps: 60
  workloadIds: [idle-play, sustained-movement, firing]
  stressWorkloadIds: [pointer-1000hz]
  workloadDefinitionDigests:
    [idle-play=sha256:…, sustained-movement=sha256:…, firing=sha256:…, pointer-1000hz=sha256:…]
  currencyToken: commit:abc123
  capturedAfterUtc: 2026-07-25T00:00:00Z
  maxP95Ms: 16.67
  maxP99Ms: 25
  maxCatchUpFrames: 0
  measurementScope: normal-60-fps-play
  requiredCapability: bounded-headless-update-render
  liveCompositorRequired: false
  deferralIssue: FS-GG/Game#123
```

Omit the mapping when an artifact is only a baseline or stress report with no active target. When
present, `workloadDefinitionDigests` must bind every normal and stress workload exactly once. The
declaration-owned digest, currency token, and capture cutoff are authoritative: a sample set with a
different digest/token or a capture time before the cutoff is malformed.

The artifact must use `performance-evidence-v1` JSON and carry raw duration and catch-up samples.
SDD recomputes nearest-rank p95/p99 and maximum catch-up; `claimedBudgetPassed` is never
authoritative. Sample sets for one workload cannot be combined across host, package, measurement,
capability, policy, capture-time, currency, or contamination bindings. Stress sets remain
diagnostic and cannot replace a normal-play set.

Headless evidence is disclosure by default. It blocks when `liveCompositorRequired: true`, as does
probe/readback contamination.

A `deferralIssue` records unresolved debt; it does not convert a failed target into a pass. Evidence,
verify, and ship remain blocked until fresh evidence satisfies every active normal workload.
