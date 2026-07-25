# Implementation Plan: Typed Performance-Budget Obligations

**Branch**: `item/680-typed-performance-budgets`  
**Spec**: [spec.md](./spec.md)

## Design

The authored `EvidenceDeclaration` is the durable join point: it already links charter/spec-derived
requirements, plan obligations, generated tasks, evidence, verify, ship, and governance handoff.
Add one optional `PerformanceBudgetDeclaration` to that record and its shared field-list codec.

The pure Artifacts layer parses the standard line-oriented scaffold performance artifact through an
injected text lookup. It binds only `WorkloadIds` to the normal gate; `StressWorkloadIds` are
explicitly disjoint reporting metadata. Commands inject snapshots already read by the existing cited
artifact effect wave, then turn non-passing evaluations into blocking diagnostics. Those diagnostics
flow through generated work-model and governance-handoff projections using the existing lifecycle
diagnostic path.

## Contract and migration

- Additive `evidence.yml` mapping; schema version remains 1 because omission preserves legacy meaning.
- Additive F# public types/evaluator; bump the FS.GG.SDD coherent set from 0.23.0 to 0.24.0.
- No Governance runtime dependency and no provider-specific identity.

## Verification

- Field-list round-trip property includes present and absent performance budgets.
- Artifact tests pin over-budget MiniTank-shaped evidence, stress separation, and deferred debt.
- Verify command tests prove blocking and passing lifecycle outcomes over real filesystem fixtures.
- Full Release build/test, API surface check, and repository gates must pass.

## Constitution check

Tier 1 is satisfied by this spec/plan, `.fsi` first, semantic and lifecycle tests, implementation,
surface baseline, version bump, authoring documentation, and migration note. The evaluator is pure;
filesystem sensing remains at the existing MVU effect edge.
