# Contract: Evidence Confidence v2

1. `result: pass` is a claim, not proof.
2. A coherent current `observedRun` or applicable durable `recordReceipt` establishes observation.
3. Candidate-bound observed outcomes and critic decisions are the primary confidence inputs.
4. `synthetic` and `syntheticDisclosure` are optional provenance metadata. They do not override a coherent observed outcome.
5. Missing, malformed, stale, failed, or candidate-mismatched receipts remain unsatisfied.
6. Protected boundaries require observation and independent critic authority. A compatibility opt-out may remain for old packages but is not the default.
7. Existing serialized fields and counts remain available during the compatibility window.
