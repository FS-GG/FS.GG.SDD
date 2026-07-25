# Performance budgets

An evidence declaration may activate a normal-play performance gate:

```yaml
performanceBudget:
  artifactPath: readiness/013-map-motion/performance-baseline.txt
  targetFps: 60
  workloadIds: [idle-play, sustained-movement, firing]
  stressWorkloadIds: [pointer-1000hz]
  maxP95Ms: 16.67
  maxP99Ms: 25
  maxCatchUpFrames: 0
  measurementScope: normal-60-fps-play
  requiredCapability: bounded-headless-update-render
  liveCompositorRequired: false
  deferralIssue: FS-GG/Game#123
```

Omit the mapping when an artifact is only a baseline or stress report with no active target. When
present, every `workloadIds` entry must have a `scenario=<id>` row with numeric `p95-ms`, `p99-ms`,
and `catch-up-frames` facts. The artifact's target, scope, and `measurement-mode` values must equal
the typed declaration. A stress row is deliberately outside the normal gate unless its id is also
declared as normal (overlap is malformed).

`live-compositor-proof=false` is disclosure by default. It blocks only when
`liveCompositorRequired: true`.

A `deferralIssue` records unresolved debt; it does not convert a failed target into a pass. Evidence,
verify, and ship remain blocked until fresh evidence satisfies every active normal workload.
