namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

module LifecycleRuleContracts =
    type GovernanceCompatibility =
        { RouteAware: bool
          ProfileAware: bool
          FreshnessAware: bool
          EnforceableBySdd: bool }

    type RuleInput = { Artifact: ArtifactRef; Required: bool }

    type LifecycleRuleContract =
        { SchemaVersion: SchemaVersion
          Id: string
          Owner: ArtifactOwner
          Stage: LifecycleStage
          Inputs: RuleInput list
          FindingShape: string
          DiagnosticIds: string list
          Evidence: string list
          TestObligations: string list
          GovernanceCompatibility: GovernanceCompatibility }

    let sddOnlyCompatibility () =
        { RouteAware = false
          ProfileAware = false
          FreshnessAware = false
          EnforceableBySdd = false }

    let artifact path kind =
        match ArtifactRef.create path kind Sdd true with
        | Ok value -> value
        | Error message -> invalidArg (nameof path) message

    let input path kind = { Artifact = artifact path kind; Required = true }

    let contract id stage inputs diagnostics evidence obligations =
        { SchemaVersion = SchemaVersion.create 1
          Id = id
          Owner = ArtifactOwner.Sdd
          Stage = stage
          Inputs = inputs
          FindingShape = "diagnostic"
          DiagnosticIds = diagnostics
          Evidence = evidence
          TestObligations = obligations
          GovernanceCompatibility = sddOnlyCompatibility () }

    let initialContracts () =
        [ contract
              "requiredSpecSections"
              LifecycleStage.Specify
              [ input "work/{workId}/spec.md" ArtifactKind.Spec ]
              [ "missingArtifact"; "requirementNotTyped"; "malformedSchemaVersion"; "proseStructuredMismatch" ]
              [ "specificationFixture" ]
              [ "schemaValidationFixture"; "semanticPublicSurfaceTest" ]
          contract
              "planObligations"
              LifecycleStage.Plan
              [ input "work/{workId}/plan.md" ArtifactKind.Plan ]
              [ "missingArtifact"; "unknownReference"; "proseStructuredMismatch" ]
              [ "planFixture" ]
              [ "semanticPublicSurfaceTest" ]
          contract
              "taskGraphShape"
              LifecycleStage.Tasks
              [ input "work/{workId}/tasks.yml" ArtifactKind.Tasks ]
              [ "duplicateIdentifier"; "unknownReference"; "workModelInconsistent"; "proseStructuredMismatch" ]
              [ "taskFixture" ]
              [ "taskGraphFixture" ]
          contract
              "evidenceDeclarations"
              LifecycleStage.Evidence
              [ input "work/{workId}/evidence.yml" ArtifactKind.Evidence ]
              [ "missingArtifact"; "unknownReference"; "malformedSchemaVersion"; "workModelInconsistent" ]
              [ "evidenceFixture" ]
              [ "evidenceDeclarationFixture" ]
          contract
              "loadedAgentSkills"
              LifecycleStage.Implement
              [ input ".fsgg/agents.yml" ArtifactKind.AgentsConfig ]
              [ "unknownReference"; "missingArtifact"; "staleGeneratedView" ]
              [ "agentGuidanceFixture" ]
              [ "agentGuidanceReview" ]
          contract
              "testObligations"
              LifecycleStage.Verify
              [ input "work/{workId}/evidence.yml" ArtifactKind.Evidence ]
              [ "missingArtifact"; "unknownReference"; "workModelInconsistent"; "staleGeneratedView" ]
              [ "testEvidenceFixture" ]
              [ "semanticPublicSurfaceTest"; "fixtureValidation" ] ]

    let contractIds () = initialContracts () |> List.map (fun contract -> contract.Id)
