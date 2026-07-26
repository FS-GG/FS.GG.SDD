---
title: FS.GG.Contracts 7.0.0 — early performance intent
---

# FS.GG.Contracts 7.0.0

`FS.GG.Contracts` moves `6.0.0` → **`7.0.0`**. This is a declared binary break:
`GovernanceHandoffPerformanceEvidence` gains an optional `Intent` member and the package publishes
`PerformanceIntentDeclaration`.

The new declaration is the single producer-owned shape for target FPS, representative workload
identities and definition digests, maximum expected scale, timing and structural limits,
measurement capability, live-compositor posture, evidence references, and typed disposition.
Consumers on 6.x must recompile. Legacy handoffs may carry `intent: null`; Governance must require
intent only for the governed interactive/render-loop profiles.

`CompatibilitySuppressions.xml` acknowledges the resulting `CP0002` against the published 6.0.0
baseline. It is intentionally narrow and must be removed after 7.0.0 becomes the feed baseline.

Release sequence: publish Contracts 7.0.0 first, update the dependency registry, then publish the
coherent FS.GG.SDD 0.27.0 package set.
