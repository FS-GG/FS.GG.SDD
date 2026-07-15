namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion
open YamlDotNet.RepresentationModel

[<AutoOpen>]
module Plan =
    type PlanFrontMatter =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Title: string
          Stage: LifecycleStage
          ChangeTier: string
          Status: string
          SourceSpec: string
          SourceClarifications: string
          SourceChecklist: string
          PublicOrToolFacingImpact: bool option }

    type PlanSourceSnapshot =
        { Label: string
          Path: string
          Digest: string option
          SchemaVersion: int option
          SourceLocation: SourceLocation option }

    type PlanDecision =
        { DecisionId: PlanDecisionId
          Title: string
          Status: string
          Text: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type PlanContractReference =
        { ContractId: PlanContractReferenceId
          Kind: string
          Target: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type VerificationObligation =
        { ObligationId: VerificationObligationId
          Title: string
          EvidenceKind: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type PlanMigrationNote =
        { MigrationId: PlanMigrationNoteId
          Posture: string
          Text: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type GeneratedViewImpact =
        { ImpactId: GeneratedViewImpactId
          Target: string
          CurrencyBehavior: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type AcceptedPlanDeferral =
        { Id: string
          Text: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type PlanFacts =
        { FrontMatter: PlanFrontMatter
          StandardSections: string list
          MissingStandardSections: string list
          SourceSnapshots: PlanSourceSnapshot list
          Decisions: PlanDecision list
          ContractReferences: PlanContractReference list
          VerificationObligations: VerificationObligation list
          MigrationNotes: PlanMigrationNote list
          GeneratedViewImpacts: GeneratedViewImpact list
          AcceptedDeferrals: AcceptedPlanDeferral list
          BlockingFindings: string list
          AdvisoryNotes: string list
          LifecycleNotes: string list
          StaleDecisionCount: int
          Diagnostics: Diagnostic list }

    let planStandardSections () =
        [ "Source Snapshot"
          "Plan Scope"
          "Plan Decisions"
          "Contract Impact"
          "Verification Obligations"
          "Migration Posture"
          "Generated View Impact"
          "Accepted Deferrals"
          "Planning Findings"
          "Advisory Notes"
          "Lifecycle Notes" ]

    let parsePlanFrontMatter (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Plan

        match frontMatter snapshot with
        | None ->
            Error [ Diagnostics.malformedSchemaVersion artifact "Plan artifact is missing structured front matter." ]
        | Some(yaml, body) ->
            match yamlRoot artifact "Plan front matter is empty." 1 yaml with
            | Error diagnostics -> Error diagnostics
            | Ok root ->
                let version, versionDiagnostics = schemaVersion artifact root

                let workId =
                    tryScalarAt [ "workId" ] root
                    |> Option.bind (Identifiers.createWorkId >> Result.toOption)

                let stage =
                    tryScalarAt [ "stage" ] root
                    |> Option.bind (Identifiers.parseStage >> Result.toOption)

                let sourceSpec = tryScalarAt [ "sourceSpec" ] root
                let sourceClarifications = tryScalarAt [ "sourceClarifications" ] root
                let sourceChecklist = tryScalarAt [ "sourceChecklist" ] root

                match version, workId, stage, sourceSpec, sourceClarifications, sourceChecklist, versionDiagnostics with
                | Some schema,
                  Some workId,
                  Some stage,
                  Some sourceSpec,
                  Some sourceClarifications,
                  Some sourceChecklist,
                  [] ->
                    Ok(
                        { SchemaVersion = schema
                          WorkId = workId
                          Title =
                            tryScalarAt [ "title" ] root
                            |> Option.defaultValue (Identifiers.workIdValue workId)
                          Stage = stage
                          ChangeTier = tryScalarAt [ "changeTier" ] root |> Option.defaultValue "tier1"
                          Status = tryScalarAt [ "status" ] root |> Option.defaultValue "planned"
                          SourceSpec = sourceSpec
                          SourceClarifications = sourceClarifications
                          SourceChecklist = sourceChecklist
                          PublicOrToolFacingImpact = boolScalarAt [ "publicOrToolFacingImpact" ] root },
                        body
                    )
                | _ ->
                    Error(
                        versionDiagnostics
                        @ [ Diagnostics.workModelInconsistent
                                artifact
                                "Plan front matter is incomplete."
                                "Add schemaVersion, workId, title, stage: plan, changeTier, status, sourceSpec, sourceClarifications, and sourceChecklist to plan.md."
                                []
                            |> Diagnostics.withDefectTag Diagnostics.DefectTags.FrontMatterIncomplete ]
                    )

    let planDecisionIdsInLine line =
        Regex.Matches(line, @"\bPD-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createPlanDecisionId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let planContractReferenceIdsInLine line =
        Regex.Matches(line, @"\bPC-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createPlanContractReferenceId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let verificationObligationIdsInLine line =
        Regex.Matches(line, @"\bVO-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createVerificationObligationId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let planMigrationNoteIdsInLine line =
        Regex.Matches(line, @"\bPM-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createPlanMigrationNoteId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let generatedViewImpactIdsInLine line =
        Regex.Matches(line, @"\bGV-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createGeneratedViewImpactId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let planSourceIdsInLine line =
        Regex.Matches(line, @"\b(?:FR|US|AC|SB|AMB|CQ|DEC|CHK|CR|PD|PC|VO|PM|GV)-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.map (fun m -> m.Value.ToUpperInvariant())
        |> Seq.distinct
        |> Seq.toList

    let parsePlanSourceSnapshots text : PlanSourceSnapshot list =
        sectionLines "Source Snapshot" text
        |> List.choose (fun (lineNumber, line) ->
            let m =
                Regex.Match(
                    line,
                    @"^\s*-\s*([A-Za-z][A-Za-z0-9_-]*)\s*:\s*(\S+)(?:\s+sha256:([a-fA-F0-9]{64}))?(?:\s+schemaVersion:(\d+))?",
                    RegexOptions.IgnoreCase
                )

            if m.Success then
                let schema =
                    if m.Groups.[4].Success then
                        match Int32.TryParse m.Groups.[4].Value with
                        | true, value -> Some value
                        | _ -> None
                    else
                        None

                Some
                    { Label = m.Groups.[1].Value
                      Path = normalizePath m.Groups.[2].Value
                      Digest =
                        if m.Groups.[3].Success then
                            Some(m.Groups.[3].Value.ToLowerInvariant())
                        else
                            None
                      SchemaVersion = schema
                      SourceLocation = sourceLocation lineNumber }
            else
                None)

    let planDecisionStatus (line: string) =
        let lowered = line.ToLowerInvariant()

        if
            containsWord "accepteddeferral" lowered
            || containsWord "accepted deferral" lowered
        then
            "acceptedDeferral"
        elif containsWord "stale" lowered || containsWord "needs review" lowered then
            "stale"
        elif containsWord "incomplete" lowered then
            "incomplete"
        elif containsWord "advisory" lowered then
            "advisory"
        elif containsWord "complete" lowered || containsWord "planned" lowered then
            "complete"
        else
            "complete"

    let parsePlanDecisions text =
        sectionLines "Plan Decisions" text
        |> List.choose (fun (lineNumber, line) ->
            match planDecisionIdsInLine line |> List.tryHead with
            | Some decisionId ->
                Some
                    { DecisionId = decisionId
                      Title = cleanAfterId decisionId.Value line
                      Status = planDecisionStatus line
                      Text = cleanAfterId decisionId.Value line
                      SourceIds = planSourceIdsInLine line |> List.filter ((<>) decisionId.Value)
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parsePlanContractReferences text =
        sectionLines "Contract Impact" text
        |> List.choose (fun (lineNumber, line) ->
            match planContractReferenceIdsInLine line |> List.tryHead with
            | Some contractId ->
                let text = cleanAfterId contractId.Value line

                let kind =
                    let lowered = text.ToLowerInvariant()

                    if containsWord "command" lowered then "command"
                    elif containsWord "report" lowered then "report"
                    elif containsWord "schema" lowered then "schema"
                    elif containsWord "generated" lowered then "generatedView"
                    else "artifact"

                Some
                    { ContractId = contractId
                      Kind = kind
                      Target = text
                      SourceIds = planSourceIdsInLine line |> List.filter ((<>) contractId.Value)
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parseVerificationObligations text =
        sectionLines "Verification Obligations" text
        |> List.choose (fun (lineNumber, line) ->
            match verificationObligationIdsInLine line |> List.tryHead with
            | Some obligationId ->
                let text = cleanAfterId obligationId.Value line
                let lowered = text.ToLowerInvariant()

                let evidenceKind =
                    if containsWord "cli" lowered || containsWord "smoke" lowered then
                        "smoke"
                    elif containsWord "fsi" lowered then
                        "fsi"
                    elif containsWord "semantic" lowered then
                        "semanticTest"
                    elif containsWord "golden" lowered || containsWord "json" lowered then
                        "golden"
                    else
                        "test"

                Some
                    { ObligationId = obligationId
                      Title = text
                      EvidenceKind = evidenceKind
                      SourceIds = planSourceIdsInLine line |> List.filter ((<>) obligationId.Value)
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parsePlanMigrationNotes text =
        sectionLines "Migration Posture" text
        |> List.choose (fun (lineNumber, line) ->
            match planMigrationNoteIdsInLine line |> List.tryHead with
            | Some migrationId ->
                let text = cleanAfterId migrationId.Value line
                let lowered = text.ToLowerInvariant()

                let posture =
                    if lowered.Contains("diagnoseonly") || lowered.Contains("diagnose-only") then
                        "diagnoseOnly"
                    elif lowered.Contains("breaking") then
                        "breaking"
                    elif lowered.Contains("compatible") then
                        "compatible"
                    else
                        "none"

                Some
                    { MigrationId = migrationId
                      Posture = posture
                      Text = text
                      SourceIds = planSourceIdsInLine line |> List.filter ((<>) migrationId.Value)
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parseGeneratedViewImpacts text =
        sectionLines "Generated View Impact" text
        |> List.choose (fun (lineNumber, line) ->
            match generatedViewImpactIdsInLine line |> List.tryHead with
            | Some impactId ->
                let text = cleanAfterId impactId.Value line
                let lowered = text.ToLowerInvariant()

                let currency =
                    if lowered.Contains("stale") then "staleDiagnostic"
                    elif lowered.Contains("refresh") then "refresh"
                    else "diagnostic"

                Some
                    { ImpactId = impactId
                      Target = text
                      CurrencyBehavior = currency
                      SourceIds = planSourceIdsInLine line |> List.filter ((<>) impactId.Value)
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parseAcceptedPlanDeferrals text =
        sectionLines "Accepted Deferrals" text
        |> List.choose (fun (lineNumber, line) ->
            let sourceIds = planSourceIdsInLine line

            if List.isEmpty sourceIds then
                None
            else
                Some
                    { Id = sourceIds.Head
                      Text = line.Trim().TrimStart('-', '*').Trim()
                      SourceIds = sourceIds
                      SourceLocation = sourceLocation lineNumber })

    let parsePlanFacts (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Plan

        match parsePlanFrontMatter snapshot with
        | Error diagnostics -> Error diagnostics
        | Ok(frontMatter, _) ->
            let text =
                (if String.IsNullOrEmpty snapshot.Text then
                     ""
                 else
                     snapshot.Text)
                    .Replace("\r\n", "\n")

            let standardSections = planStandardSections ()

            let missingStandardSections =
                standardSections |> List.filter (fun heading -> not (hasHeading heading text))

            let snapshots = parsePlanSourceSnapshots text
            let decisions = parsePlanDecisions text
            let contracts = parsePlanContractReferences text
            let obligations = parseVerificationObligations text
            let migrations = parsePlanMigrationNotes text
            let impacts = parseGeneratedViewImpacts text
            let deferrals = parseAcceptedPlanDeferrals text
            let blockingFindings = parseNonEmptySectionLines "Planning Findings" text
            let advisoryNotes = parseNonEmptySectionLines "Advisory Notes" text
            let lifecycleNotes = parseNonEmptySectionLines "Lifecycle Notes" text

            let diagnostics =
                [ duplicateScopedDiagnostics
                      artifact
                      (fun (id: PlanDecisionId) -> id.Value)
                      (decisions
                       |> List.map (fun decision -> decision.DecisionId, decision.SourceLocation))
                  duplicateScopedDiagnostics
                      artifact
                      (fun (id: PlanContractReferenceId) -> id.Value)
                      (contracts
                       |> List.map (fun contract -> contract.ContractId, contract.SourceLocation))
                  duplicateScopedDiagnostics
                      artifact
                      (fun (id: VerificationObligationId) -> id.Value)
                      (obligations
                       |> List.map (fun obligation -> obligation.ObligationId, obligation.SourceLocation))
                  duplicateScopedDiagnostics
                      artifact
                      (fun (id: PlanMigrationNoteId) -> id.Value)
                      (migrations
                       |> List.map (fun migration -> migration.MigrationId, migration.SourceLocation))
                  duplicateScopedDiagnostics
                      artifact
                      (fun (id: GeneratedViewImpactId) -> id.Value)
                      (impacts |> List.map (fun impact -> impact.ImpactId, impact.SourceLocation))
                  missingStandardSections
                  |> List.map (fun heading ->
                      Diagnostics.workModelInconsistent
                          artifact
                          $"Plan artifact is missing the '{heading}' section."
                          $"Add a '## {heading}' section to plan.md before relying on parsed planning facts."
                          [ heading ]) ]
                |> List.concat
                |> Diagnostics.sort

            Ok
                { FrontMatter = frontMatter
                  StandardSections = standardSections
                  MissingStandardSections = missingStandardSections
                  SourceSnapshots = snapshots |> List.sortBy (fun snapshot -> snapshot.Label, snapshot.Path)
                  Decisions = decisions |> List.sortBy (fun decision -> decision.DecisionId.Value)
                  ContractReferences = contracts |> List.sortBy (fun contract -> contract.ContractId.Value)
                  VerificationObligations = obligations |> List.sortBy (fun obligation -> obligation.ObligationId.Value)
                  MigrationNotes = migrations |> List.sortBy (fun migration -> migration.MigrationId.Value)
                  GeneratedViewImpacts = impacts |> List.sortBy (fun impact -> impact.ImpactId.Value)
                  AcceptedDeferrals = deferrals |> List.sortBy (fun deferral -> deferral.Id)
                  BlockingFindings = blockingFindings |> List.sort
                  AdvisoryNotes = advisoryNotes |> List.sort
                  LifecycleNotes = lifecycleNotes
                  StaleDecisionCount =
                    decisions
                    |> List.filter (fun decision -> decision.Status = "stale")
                    |> List.length
                  Diagnostics = diagnostics }
