# Feature 116 — Performance intent before implementation

**Issue:** FS.GG.SDD#700  
**Tier:** 1 (public lifecycle and Governance-handoff contract)

## Outcome

Interactive and render-loop work cannot become implementation-ready until its specification
declares one typed performance intent. The same declaration is bound by measured evidence and
carried to Governance.

## Requirements

- **FR-001:** `project.profile` values `interactive` and the provider render-loop profile require a
  `performanceIntent` mapping in specification front matter.
- **FR-002:** Active intent MUST name a stable id, target FPS, normal workload ids and current
  definition digests, maximum expected scale, p95/p99/catch-up thresholds, structural cost
  ceilings, measurement capability, and live-compositor posture.
- **FR-003:** Placeholder workload definitions and omitted required facts MUST block `analyze`.
- **FR-004:** Non-applicability MUST cite evidence and rationale. Deferral MUST cite decision
  evidence and an open blocking debt issue, and MUST remain blocking.
- **FR-005:** A later `performanceBudget` MUST bind the exact canonical intent; divergent or
  unbound declarations MUST block evidence, verify, and ship.
- **FR-006:** Work model and Governance handoff MUST carry the canonical intent without a
  hand-written mirror.
- **FR-007:** Profiles outside the governed set and legacy evidence without intent remain
  compatible.

## Acceptance

1. Missing interactive intent blocks before implementation.
2. Active intent with current bindings reaches evidence; passing/failing raw samples retain their
   existing recomputed result.
3. Placeholder workload definitions block.
4. Supported non-applicability passes the pre-implementation gate.
5. Deferral remains blocking and names its debt.
6. A non-interactive work item without intent is unchanged.
