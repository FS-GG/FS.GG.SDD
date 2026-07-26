# Implementation plan

1. Publish `PerformanceIntentDeclaration` from `FS.GG.Contracts` and advance the handoff contract.
2. Parse the declaration from specification front matter and project it through work-model JSON.
3. Enforce profile-dependent readiness in the work-model validation used by `analyze`.
4. Bind evidence budgets to the same typed declaration and reject drift.
5. Carry the optional declaration beside raw samples in Governance handoff, preserving legacy
   non-interactive evidence.
6. Add contract, lifecycle, compatibility, and release fixtures.
