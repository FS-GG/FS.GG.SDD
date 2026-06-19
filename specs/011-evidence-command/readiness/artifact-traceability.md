# Artifact Traceability

Traceability for `011-evidence-command`.

| Source | Implementation and Evidence |
|---|---|
| `spec.md` FR-001 evidence command creates/updates authored evidence | `CommandTypes.Evidence`, `CommandWorkflow.computeEvidencePlan`, `EvidenceCommandTests`, `cli-json-smoke.txt` |
| `spec.md` FR-002 preserve existing declarations and block unsafe updates | `mergeEvidenceArtifacts`, `unsafeEvidenceUpdate`, `EvidenceCommandTests.evidence blocks undisclosed synthetic evidence without mutation` |
| `spec.md` FR-003 source snapshots and generated work-model refresh | `currentEvidenceSourceSnapshots`, `generatedViewPlan` evidence input, `cli-json-smoke.txt`, `output-boundary-tests.txt` |
| `spec.md` FR-004 deterministic JSON/text output | `CommandSerialization.writeEvidence`, `CommandRendering.renderText`, `EvidenceCommandTests.evidence deterministic JSON is byte stable`, `cli-text-smoke.txt` |
| `spec.md` FR-005 no required Governance runtime | `evidence does not require Governance files`, `sdd-governance-boundary.md` |
| `contracts/evidence-artifact.md` schema-versioned evidence artifact | `parseEvidenceArtifact`, `EvidenceArtifactTests`, `artifact-evidence-tests.txt` |
| `contracts/evidence-command.md` public command/report surface | `CommandReport.Evidence`, `EvidenceSummary`, `command-evidence-tests.txt`, `fsi-public-surface.txt` |
| `plan.md` verification strategy | `build-release.txt`, `full-suite.txt`, focused test transcripts, CLI smoke transcripts |
