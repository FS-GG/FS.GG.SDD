# Artifact Traceability — Verify Command

| Spec area | Plan decision | Implementation | Tests | Readiness |
|---|---|---|---|---|
| US1 Verify evidence-ready work | Generate `verify.json`, refresh work-model, point to ship | `CommandWorkflow.computeVerifyPlan`, `verifyJson`, `CommandReports` `verify.next.ship` | `verify creates generated verification view…`, `verify next action lists verify and work-model artifacts`, `verify report shape…` | `command-verify-tests.txt` |
| US2 Find blocking readiness gaps | Block on missing prerequisites, malformed view, missing test/skill | prerequisite chain + `existingVerifyDiagnostic` + test/skill diagnostics | `verify missing evidence blocks…`, `verify missing analysis blocks…`, `verify blocks malformed existing verification view…`, `verify outside project blocks` | `command-verify-tests.txt` |
| US3 Preserve authored sources | Generated-only writes; dry-run mutates nothing | effects restricted to `verify.json` + `work-model.json`; dry-run suppression | `verify preserves authored lifecycle sources`, `verify dry run reports generated change without mutation`, `verify rerun over unchanged sources reports no change` | `command-verify-tests.txt` |
| US4 Traceable output | One report → JSON + text projections, deterministic, no Governance | `CommandSerialization.writeVerification`, `CommandRendering`, byte-stable JSON | `verify deterministic JSON…`, `verify text projection…`, `verify does not require Governance files`, CLI smokes | `output-boundary-tests.txt`, `cli-*-smoke.txt` |
| Verification view contract | Schema v1 `verify.json` with source digests + generator | `LifecycleArtifacts.parseVerificationView` + `VerificationView` types | `parseVerificationView reads schema version 1 shape`, `…recovers evidence and test disposition states`, `…reports malformed generated JSON` | `artifact-verify-tests.txt` |
| Public surface | `.fsi`-first additions | `CommandTypes`, `CommandReports`, `LifecycleArtifacts` `.fsi` | `SurfaceBaselineTests` (both projects) + `PublicSurface.baseline` updates | `full-suite.txt` |
| Agent/human contract | Shared report → FSI evidence | `scripts/prelude.fsx` verify block | `dotnet fsi scripts/prelude.fsx` | `fsi-public-surface.txt` |

Full suite: 235 passed (66 artifacts + 169 commands) — `full-suite.txt`.
