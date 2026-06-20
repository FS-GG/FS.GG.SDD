# Artifact Traceability — Ship Command

| Requirement | Implementation | Tests / Evidence |
|---|---|---|
| FR-001 native `ship` command | CommandTypes (`SddCommand.Ship`, parse/stage/next), CommandWorkflow `plan`/`shipReadEffects` | prelude (`parse ship`), `ship CLI JSON smoke` |
| FR-002 require init + verify-ready prerequisites | CommandWorkflow.computeShipPlan (project/work-id/prereq/verify gate) | `ship missing verification blocks`, `ship outside project blocks`, `ship missing analysis blocks` |
| FR-003 load & validate sources | computeShipPlan prerequisite helpers + shipVerificationPrerequisite | `ship creates generated ship view` |
| FR-004 generate/refresh ship.json (non-dry-run) | computeShipPlan shipEffects | `ship creates generated ship view`, `ship dry run` |
| FR-005 ship view contents | shipJson serializer; LifecycleArtifacts.ShipView/parseShipView | ShipViewTests, `ship creates generated ship view` |
| FR-006 stable finding ids + links | shipFindings (`SF###`), disposition links | ShipViewTests, ship.json findings |
| FR-007 aggregate not re-derive | computeShipPlan aggregates verify view EvidenceDispositions | `ship aggregates verification view without regenerating it` |
| FR-008 disposition mapping | computeShipPlan disposition (shipReady/blocked/advisory) | `ship creates...` (advisory), blocked tests |
| FR-009 no authored mutation | shipEffects writes only ship.json + work-model.json | `ship preserves authored lifecycle sources and verification view` |
| FR-010 stale/blocked on drift | staleVerificationView + generated-view currency | `ship not-verification-ready blocks` |
| FR-011 block conditions | ship + reused prerequisite diagnostics | blocked ShipCommandTests + fixtures |
| FR-012 ship-ready only when resolved | hasBlocking gate → readiness shipReady | `ship creates...`, blocked tests |
| FR-013 next action protected boundary (null command) | CommandReports.nextAction Ship branch | `ship next action lists ship and work-model artifacts with null command` |
| FR-014 blocked next actions | shipCorrectionCommand | blocked tests, nextAction routing |
| FR-015 report fields | ShipSummary + CommandSerialization.writeShip | `ship report shape exposes ship summary` |
| FR-016 deterministic | shipJson/serializeReport deterministic ordering | `ship deterministic JSON report is byte stable` |
| FR-017 human summary projection | CommandRendering ship block | `ship text projection uses report facts` |
| FR-018 refresh work-model / diagnose analysis+verify currency | computeShipPlan generatedViews (work-model refresh; analysis/verify diagnosed) | `ship aggregates verification view without regenerating it` |
| FR-019 generated-view diagnostics | generatedViewPlan currency + view states | `ship blocks malformed existing ship view` |
| FR-020 stable diagnostic ids | CommandReports ship diagnostics | prelude shipDiagnostics line |
| FR-021 dry-run | DryRun suppresses effects | `ship dry run reports generated change without mutation` |
| FR-022 works without Governance | analysisBoundaryFacts advisory | `ship does not require Governance files` |
| FR-023 governance advisory | governanceCompatibility notEvaluated | `ship does not require Governance files`, sdd-governance-boundary.md |
| FR-024 no Governance behavior | no freshness/route/profile/gate/audit/release code | `ship does not require Governance files` |

Tests: 4 artifact (ShipViewTests) + 19 command (ShipCommandTests incl. 3 CLI smoke).
Full suite: 258 passed / 0 failed. FSI surface: fsi-public-surface.txt.
