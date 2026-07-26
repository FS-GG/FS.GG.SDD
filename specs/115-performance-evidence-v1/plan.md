# Plan — Independently verifiable performance evidence

## Contract

Add the additive `performance-evidence-v1` DTO family to `FS.GG.Contracts`. A sample set repeats
all binding facts deliberately: this makes each set independently auditable and lets SDD reject
mixed sets before concatenating samples. The JSON artifact has a contract version, optional
producer claim, and sample sets.

The evaluator groups sets by workload id, checks an exact binding tuple, concatenates only
identically bound sets, computes nearest-rank percentiles (`ceil(p*n)`, one-based), and uses the
maximum catch-up sample. It ignores the producer claim as authority and reports disagreement.

## Projection

Artifact loading hydrates cited performance artifacts into evidence declarations. Work-model JSON
retains the typed artifact and computed measurements. Governance handoff 1.2.0 projects the same
performance evidence entries independently of evidence-node state.

## Compatibility and release

The existing `performanceBudget` declaration is retained. Summary-only artifacts accepted by
0.24.0 become malformed because they cannot be independently checked; this behavioral tightening
is documented as a migration obligation. Public record additions require the repository's
coordinated Contracts/CLI release path and downstream Governance registry/pin update.

## Verification

Add focused parser/evaluator fixtures for raw failure, deterministic percentiles, mixed bindings,
headless/live mismatch, and valid normal/stress evidence. Extend work-model and handoff round-trip
goldens, public-surface baselines, and run the Release solution plus formatting gates.

