# Data Model: Risk-Scaled SDD

## RiskProfile

- `name`: `small | normal | high`
- `reasons`: ordered, non-empty explanations
- `paths`: normalized changed paths used in the decision
- `testTier`: `none | fast | full`
- `protectedControls`: whether release/API/formal-model/build-policy checks are mandatory

The profile only promotes; one high path makes the whole candidate high. Empty, malformed, or unavailable input produces `high` with an indeterminate reason.

## CandidateEvidence

- `candidate`: immutable commit identity
- `kind`: execution or durable record
- `source`: inspectable artifact location
- `digest`: exact-byte binding when local
- `outcome`: pass, fail, or indeterminate
- `provenance`: optional metadata including synthetic/fixture notes

`provenance` never changes `outcome`. A pass satisfies a protected obligation only when its receipt is coherent and current for the candidate.

## CriticDecision

- `candidate`: immutable commit identity
- `critic`: identity distinct from implementer
- `verdict`: pass or changes required
- `findings`: concise review results
- `timestamp`: review instant

Any candidate change invalidates the decision. Coordination remains the authority for enforcing critic independence and merge authorization.

## Compatibility

Existing `synthetic`, `syntheticDisclosure`, synthetic disposition/count, and stage artifacts remain readable and renderable. New evaluations treat them as provenance; old consumers continue to see their known fields. No existing file must be rewritten merely to adopt the new lifecycle.
