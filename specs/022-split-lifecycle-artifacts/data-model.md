# Phase 1 Data Model: family decomposition & compile order

This refactor introduces **no new types and changes no field**. It redistributes
the existing definitions in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs`
(types + parsers) across per-family modules. Every record/DU and every public
`parse*` value below already exists today; the table records its **new home**.

## Entities (refactor concepts)

- **Artifact family** — a cohesive group of lifecycle-artifact types + their
  parser(s); the unit of the split. One `.fs` + `.fsi` per family.
- **Shared lifecycle-artifacts core** — definitions used by more than one family;
  compiled before dependents. Split into `Internal` (private helpers) and `Core`
  (public shared types).
- **Public lifecycle-artifacts signature** — preserved as the *union* of the new
  per-family `.fsi` files (the old 722-line `.fsi` sliced by family, verbatim).
- **Compile-order manifest** — the `<Compile Include>` ordering in
  `FS.GG.SDD.Artifacts.fsproj`.

## Module map (new home for every member)

| Module (file) | Kind | Public types | Public parsers / values |
|---|---|---|---|
| `Internal` | `module internal`, no `.fsi` | — | YAML helpers (`parseYaml`, `tryMapping`, `tryScalar`, `tryChild`, `scalarList`, `schemaVersion`, `requiredScalar`, `combine`, `normalizePath`, `artifact`, `sourceArtifact`), Markdown helpers (`frontMatter`, `proseStatus`, `sourceLocation`, `hasHeading`, `sectionLines`, scoped-ID helpers), JSON helpers (`tryJsonProperty`, `jsonString`, `jsonInt`, `jsonBool`, `jsonArray`, `jsonStringList`, `parseJsonDigest`, `jsonDigest`, `diagnosticSeverityFromJson`, `artifactFromJsonPath`) |
| `Core` | public + `.fsi` | `FileSnapshot`, `LifecycleArtifactContract`, `AnalysisSourceRecord`, `AnalysisGeneratedViewRecord`, `AnalysisOptionalBoundaryFact` | `standardArtifactContracts` |
| `Config` | public + `.fsi` | `ProjectLifecycleConfig`, `SddLifecyclePolicy`, `AgentGuidanceTarget`, `AgentGuidanceConfig` | `parseProjectConfig`, `parseSddLifecyclePolicy`, `parseAgentGuidanceConfig` |
| `WorkItemMetadata` | public + `.fsi` | `WorkItemMetadata` | `parseWorkItemMetadata` |
| `Specification` | public + `.fsi` | `SpecificationFrontMatter`, `SpecificationRequirementReference`, `SpecificationFacts` | `specificationStandardSections`, `parseSpecificationFacts` |
| `Clarification` | public + `.fsi` | `ClarificationDecisionKind`, `ClarificationAnswerKind`, `ClarificationFrontMatter`, `ClarificationQuestion`, `ClarificationAnswer`, `ClarificationDecisionFact`, `RemainingAmbiguity`, `ClarificationFacts` | `clarificationStandardSections`, `parseClarificationFacts` |
| `Checklist` | public + `.fsi` | `ChecklistFrontMatter`, `ChecklistSourceSnapshot`, `ChecklistItem`, `ChecklistReviewResult`, `ChecklistFacts` | `checklistStandardSections`, `parseChecklistFacts` |
| `Plan` | public + `.fsi` | `PlanFrontMatter`, `PlanSourceSnapshot`, `PlanDecision`, `PlanContractReference`, `VerificationObligation`, `PlanMigrationNote`, `GeneratedViewImpact`, `AcceptedPlanDeferral`, `PlanFacts` | `planStandardSections`, `parsePlanFacts` |
| `RequirementModel` | public + `.fsi` | `Requirement`, `Decision`, `MarkdownRequirementMention` | `parseRequirements`, `parseDecisions`, `parseMarkdownRequirementMentions` |
| `Task` | public + `.fsi` | `TaskFrontMatter`, `TaskSourceSnapshot`, `TaskGraphFinding`, `TaskStatus`, `WorkTask`, `TaskFacts` | `parseTaskFacts`, `parseTasks` |
| `Analysis` | public + `.fsi` | `AnalysisSourceRelationship`, `AnalysisFinding`, `AnalysisReadiness`, `AnalysisNextAction`, `AnalysisView` | `parseAnalysisView` |
| `Evidence` | public + `.fsi` | `EvidenceKind`, `EvidenceSubject`, `EvidenceSourceSnapshot`, `EvidenceSourceReference`, `SyntheticDisclosure`, `EvidenceDeclaration`, `EvidenceObligation`†, `EvidenceArtifact` | `parseEvidenceArtifact`, `parseEvidence` |
| `Verify` | public + `.fsi` | `EvidenceDispositionState`, `EvidenceDisposition`, `RequiredTestDispositionState`, `RequiredTestDisposition`, `SkillVisibilityState`, `SkillVisibilityFact`, `VerificationFinding`, `VerificationStageReadiness`, `VerificationLifecycleReadiness`, `VerificationTaskGraphReadiness`, `VerificationView` | `parseVerificationView` |
| `Ship` | public + `.fsi` | `ShipReadinessFinding`, `ShipLifecycleStageReadiness`, `ShipVerificationReadinessSummary`, `ShipView` | `parseShipView` |
| `Guidance` | public + `.fsi` | `GuidanceCommandEntry`, `GuidanceSkillEntry`, `GeneratedGuidanceFileRef`, `GeneratedAgentGuidance` | `parseGeneratedAgentGuidance` |
| `WorkItem` | public + `.fsi` | `ParsedWorkItem` | `loadWorkItemFromSnapshots` |

† `EvidenceObligation` home (Evidence vs Verify) is compiler-confirmed during
implementation; it is referenced by verification logic but is evidence-shaped.
Disposition types (`EvidenceDisposition` etc.) live in **Verify** because they are
referenced only by `VerificationView`, breaking the apparent Evidence↔Verify
coupling.

All new family modules are declared `[<AutoOpen>] module FS.GG.SDD.Artifacts.<Name>`
so a single `open FS.GG.SDD.Artifacts` exposes the full surface (records labels and
DU cases included) — the property a re-export facade could not provide.

## Compile-order manifest (`FS.GG.SDD.Artifacts.fsproj`)

Replace the two lines

```xml
<Compile Include="LifecycleArtifacts.fsi" />
<Compile Include="LifecycleArtifacts.fs" />
```

with (each public family is `.fsi` then `.fs`; `Internal` has no `.fsi`):

```xml
<Compile Include="LifecycleArtifacts/Internal.fs" />
<Compile Include="LifecycleArtifacts/Core.fsi" />
<Compile Include="LifecycleArtifacts/Core.fs" />
<Compile Include="LifecycleArtifacts/Config.fsi" />
<Compile Include="LifecycleArtifacts/Config.fs" />
<Compile Include="LifecycleArtifacts/WorkItemMetadata.fsi" />
<Compile Include="LifecycleArtifacts/WorkItemMetadata.fs" />
<Compile Include="LifecycleArtifacts/Specification.fsi" />
<Compile Include="LifecycleArtifacts/Specification.fs" />
<Compile Include="LifecycleArtifacts/Clarification.fsi" />
<Compile Include="LifecycleArtifacts/Clarification.fs" />
<Compile Include="LifecycleArtifacts/Checklist.fsi" />
<Compile Include="LifecycleArtifacts/Checklist.fs" />
<Compile Include="LifecycleArtifacts/Plan.fsi" />
<Compile Include="LifecycleArtifacts/Plan.fs" />
<Compile Include="LifecycleArtifacts/RequirementModel.fsi" />
<Compile Include="LifecycleArtifacts/RequirementModel.fs" />
<Compile Include="LifecycleArtifacts/Task.fsi" />
<Compile Include="LifecycleArtifacts/Task.fs" />
<Compile Include="LifecycleArtifacts/Analysis.fsi" />
<Compile Include="LifecycleArtifacts/Analysis.fs" />
<Compile Include="LifecycleArtifacts/Evidence.fsi" />
<Compile Include="LifecycleArtifacts/Evidence.fs" />
<Compile Include="LifecycleArtifacts/Verify.fsi" />
<Compile Include="LifecycleArtifacts/Verify.fs" />
<Compile Include="LifecycleArtifacts/Ship.fsi" />
<Compile Include="LifecycleArtifacts/Ship.fs" />
<Compile Include="LifecycleArtifacts/Guidance.fsi" />
<Compile Include="LifecycleArtifacts/Guidance.fs" />
<Compile Include="LifecycleArtifacts/WorkItem.fsi" />
<Compile Include="LifecycleArtifacts/WorkItem.fs" />
```

(ordered before `LifecycleRuleContracts.fsi`, which remains where it is.)

## Dependency rules (forward references)

- `Internal` and `Core` precede all families.
- Analysis/Verify/Ship/Guidance depend on `Core`'s `AnalysisSourceRecord` /
  `AnalysisGeneratedViewRecord` / `AnalysisOptionalBoundaryFact`.
- `Verify` depends on its own disposition types (relocated there) and `Core`.
- `WorkItem` is last (aggregates `ProjectLifecycleConfig`, `SddLifecyclePolicy`,
  `AgentGuidanceConfig`, `WorkItemMetadata`, `Requirement`, `Decision`, `WorkTask`,
  `EvidenceDeclaration`, `MarkdownRequirementMention`, …).
- No family imports another family's *parser*; only `Core`/`Internal` are shared.
  If implementation surfaces a genuine cross-family **type** need, hoist that type
  into `Core` (never duplicate — FR-006).

## Size expectation (FR-009 / SC-001)

Largest resulting family file is expected well under the ~700-line target (the
biggest current clusters — Plan ≈ 300, Evidence ≈ 200, the JSON view parsers ≈
130 each). No file approaches the original 3,161 lines.
