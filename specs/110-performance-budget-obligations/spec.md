# Feature Specification: Typed Performance-Budget Obligations

**Issue**: FS.GG.SDD#680  
**Change tier**: Tier 1

## Outcome

Interactive products can declare an active normal-play performance target as typed evidence data.
SDD binds that declaration to the standard scaffold performance artifact and refuses evidence,
verify, and ship readiness when a required workload is absent, malformed, capability-mismatched,
or over its p95, p99, or catch-up threshold.

## Requirements

- FR-001: `evidence.yml` MUST support an optional typed `performanceBudget` mapping carrying the
  artifact path, target FPS, normal and stress workload ids, p95/p99/catch-up thresholds,
  measurement scope, required capability, live-compositor posture, and optional debt issue.
- FR-002: Absence of `performanceBudget` MUST mean baseline/stress information with no active target;
  existing evidence remains backward compatible.
- FR-003: Every declared normal workload MUST bind to one measured `scenario=` row in the standard
  performance artifact. Missing/malformed targets or workload facts MUST block.
- FR-004: Normal workloads MUST satisfy p95, p99, and catch-up thresholds. Stress workloads MUST be
  reported separately and MUST neither satisfy nor fail the normal gate.
- FR-005: `live-compositor-proof=false` MUST be disclosure unless the declaration explicitly
  requires live-compositor proof.
- FR-006: An over-budget declaration with `deferralIssue` MUST remain blocking typed debt; a link is
  not a pass.
- FR-007: The declaration MUST round-trip through the authored-artifact codec and the additive
  public surface MUST receive a minor package version bump.

## Acceptance

- AC-001: p95 38.262 ms against 16.67 ms produces
  `evidence.performanceBudgetExceeded`, blocks verify, and writes no verify view.
- AC-002: passing normal workloads remain verification-ready even when a separately declared stress
  workload reports p99 88.632 ms.
- AC-003: over-budget evidence carrying a debt issue evaluates as `PerformanceDeferred`, not passed.
- AC-004: legacy evidence with no active performance budget is unchanged.
