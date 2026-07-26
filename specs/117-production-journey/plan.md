# Plan

1. Extend requirement classification and deterministically derive a separate journey task/capability.
2. Add a lossless schema-v1 journey receipt codec to `evidence.yml`.
3. Centralize fail-closed journey validation and consume it in evidence and verify dispositions.
4. Carry the separate journey classification and counts through persisted views and the handoff.
5. Cover compatibility, malformed receipts, report mismatch, and the positive Game reference shape.

Verification uses focused Artifacts and Commands tests followed by the repository gate.
