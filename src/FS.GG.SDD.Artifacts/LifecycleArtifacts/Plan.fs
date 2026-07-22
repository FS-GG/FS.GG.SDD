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

    /// FS.GG.SDD#569 (feature 105). Whether a framework-API reference is a USE (the plan intends to
    /// build against it, cited on a Contract Impact line) or an ABSENCE claim (a deferral asserts it
    /// is missing, cited as `blocked-on-framework:`). The two carry opposite plan-time verdicts when
    /// resolved against the pinned package's real surface (ADR-0004 D3).
    type FrameworkReferenceKind =
        | FrameworkUse
        | FrameworkBlockedOn

    /// FS.GG.SDD#569 (feature 105). A structured framework-API reference parsed from a
    /// `framework:` / `blocked-on-framework:` token: `<PackageId>[@<version>]#<symbol>`. `Version` is
    /// `None` when the token omits `@<version>`, in which case the pinned package version is the
    /// resolved version (ADR-0004 D1). This is authored data only — it is not resolved here.
    type FrameworkApiReference =
        { PackageId: string
          Version: string option
          Symbol: string
          Kind: FrameworkReferenceKind
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
          FrameworkApiReferences: FrameworkApiReference list
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

    // The `<PREFIX>-<NNN>` family a plan line can reference. Kept as one constant so the reference
    // scan (`planSourceIdsInLine`) and the accepted-deferral declaration scan stay in lockstep.
    let private planSourceIdPattern =
        @"\b(?:FR|US|AC|SB|AMB|CQ|DEC|CHK|CR|PD|PC|VO|PM|GV)-\d{3,}\b"

    /// The family ids a plan line REFERENCES — the ids it carries in `[...]` bracket tags
    /// (`- PD-001 [FR-002] [AC-003]: …`, `[AMB:AMB-001]`), the citation grammar the plan artifact
    /// already emits (`lineRefs`) and the one `missingDisposition` tells authors to tag with. An id
    /// that appears only in a line's PROSE — a decision citing an inherited or prior-milestone id
    /// ("… extending the SB-008 seam", "… inherited from M2 DEC-006") — is a citation, not a source
    /// reference, so it is NOT returned and does not read as a dangling `Plan reference '…' does not
    /// resolve` at tasks/analyze (FS.GG.SDD#648). This is the reference-position sibling of the
    /// list-leading declaration anchor #541/#647 (`Internal.listLeadingIdMatch`) established for the
    /// specification and clarification stable-id scans.
    let planSourceIdsInLine line =
        Regex.Matches(line, @"\[[^\]]*\]")
        |> Seq.cast<Match>
        |> Seq.collect (fun bracket ->
            Regex.Matches(bracket.Value, planSourceIdPattern, RegexOptions.IgnoreCase)
            |> Seq.cast<Match>)
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

    // The authored status of a decision is a bare keyword at the DECLARATION position of the
    // line — `- <PD-###> [tag]… <status>: <description>` — sitting immediately before the first
    // colon, after the id and any `[…]` bracket tags. It is NEVER read from the free prose that
    // follows the colon. Scanning the whole line for the word `stale` (etc.) misread a decision
    // whose prose merely *discussed* staleness ("… so a stale prior frame is never re-fired.") as a
    // `stale`-flagged decision, wrongly raising `stalePlanDecision` and blocking `tasks` even with
    // every `## Source Snapshot` digest current (FS.GG.SDD#653). This is the declaration-position-
    // vs-prose fix the #541/#645/#648 (`Internal.listLeadingIdMatch`) family applied to id tokens,
    // now for the decision status marker: the marker must sit before the colon, alone.
    let planDecisionStatus (line: string) =
        let marker =
            let colon = line.IndexOf(':')

            if colon < 0 then
                // No `<status>:` marker at all → the default, unmarked status.
                ""
            else
                // The header is everything before the first colon; drop the leading bullet, the
                // leading `PD-###` id token, and any `[…]` bracket tags. What remains is the
                // authored status marker alone (or empty for an unmarked `- PD-###: …` decision).
                let header = line.Substring(0, colon).Trim().TrimStart('-', '*').Trim()

                let withoutId =
                    match planDecisionIdsInLine header |> List.tryHead with
                    | Some decisionId -> cleanAfterId decisionId.Value header
                    | None -> header

                Regex.Replace(withoutId, @"\[[^\]]*\]", "").Trim().ToLowerInvariant()

        // A prose colon before the descriptive one can leave arbitrary words in `marker`, so the
        // marker must match a known status keyword EXACTLY — never as a contained substring/word.
        match marker with
        | "accepteddeferral"
        | "accepted deferral" -> "acceptedDeferral"
        | "stale"
        | "needs review" -> "stale"
        | "incomplete" -> "incomplete"
        | "advisory" -> "advisory"
        | "complete"
        | "planned" -> "complete"
        | _ -> "complete"

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
            // A deferral is DECLARED by the id at the list-leading position — `- CR-002 acceptedDeferral: …`
            // (the upstream deferral it keeps visible). That id is the deferral's identity AND a genuine
            // source reference, so it is captured even though it is unbracketed; bracket-tagged ids on the
            // line are additional references. A family id appearing only later in the line's prose is a
            // citation, not a reference, and `planSourceIdsInLine` already excludes it (FS.GG.SDD#648).
            match listLeadingIdMatch planSourceIdPattern line with
            | None -> None
            | Some m ->
                let declaredId = m.Value.ToUpperInvariant()
                let sourceIds = (declaredId :: planSourceIdsInLine line) |> List.distinct

                Some
                    { Id = declaredId
                      Text = line.Trim().TrimStart('-', '*').Trim()
                      SourceIds = sourceIds
                      SourceLocation = sourceLocation lineNumber })

    // FS.GG.SDD#569 (feature 105). The framework-API reference grammar (ADR-0004 D1):
    //   framework: <PackageId>[@<version>]#<symbol>            on a Contract Impact line (a USE)
    //   blocked-on-framework: <PackageId>[@<version>]#<symbol> on an Accepted Deferral (an ABSENCE claim)
    // A keyword present with a token that is NOT this grammar is a blocking diagnostic (FR-003), never a
    // silent non-match — a mis-typed reference reading as "no reference" is exactly the RM2 failure mode
    // one level up. The reference is authored data; nothing is resolved against a package here.
    let private frameworkTokenRegex =
        Regex(@"^(?<pkg>[^@#\s]+)(?:@(?<ver>[^#\s]+))?#(?<sym>[^\s]+)$", RegexOptions.Compiled)

    // Find "<keyword>: <token>" in a line. `guardLeadingHyphen` keeps the bare `framework:` from
    // matching the tail of `blocked-on-framework:` (the char before "framework" there is '-').
    let private frameworkKeywordToken (keyword: string) (guardLeadingHyphen: bool) (line: string) =
        let lookbehind = if guardLeadingHyphen then "(?<![\\w-])" else ""

        let m =
            Regex.Match(line, lookbehind + Regex.Escape(keyword) + @"\s*:\s*(\S+)", RegexOptions.IgnoreCase)

        if m.Success then Some(m.Groups.[1].Value) else None

    let private parseFrameworkReferencesInSection
        artifact
        (section: string)
        (keyword: string)
        (guardLeadingHyphen: bool)
        (kind: FrameworkReferenceKind)
        text
        =
        sectionLines section text
        |> List.collect (fun (lineNumber, line) ->
            match frameworkKeywordToken keyword guardLeadingHyphen line with
            | None -> []
            | Some token ->
                let m = frameworkTokenRegex.Match token

                if m.Success then
                    [ Ok
                          { PackageId = m.Groups.["pkg"].Value
                            Version =
                              (if m.Groups.["ver"].Success then
                                   Some m.Groups.["ver"].Value
                               else
                                   None)
                            Symbol = m.Groups.["sym"].Value
                            Kind = kind
                            SourceIds = planSourceIdsInLine line
                            SourceLocation = sourceLocation lineNumber } ]
                else
                    [ Error(Diagnostics.malformedFrameworkReference artifact token) ])

    let parseFrameworkApiReferences artifact text =
        let results =
            parseFrameworkReferencesInSection artifact "Contract Impact" "framework" true FrameworkUse text
            @ parseFrameworkReferencesInSection
                artifact
                "Accepted Deferrals"
                "blocked-on-framework"
                false
                FrameworkBlockedOn
                text

        let references =
            results
            |> List.choose (function
                | Ok reference -> Some reference
                | Error _ -> None)

        let diagnostics =
            results
            |> List.choose (function
                | Error diagnostic -> Some diagnostic
                | Ok _ -> None)

        references, diagnostics

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

            let frameworkReferences, frameworkReferenceDiagnostics =
                parseFrameworkApiReferences artifact text

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
                          [ heading ])
                  frameworkReferenceDiagnostics ]
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
                  FrameworkApiReferences =
                    frameworkReferences
                    |> List.sortBy (fun reference -> reference.PackageId, reference.Version, reference.Symbol)
                  BlockingFindings = blockingFindings |> List.sort
                  AdvisoryNotes = advisoryNotes |> List.sort
                  LifecycleNotes = lifecycleNotes
                  StaleDecisionCount =
                    decisions
                    |> List.filter (fun decision -> decision.Status = "stale")
                    |> List.length
                  Diagnostics = diagnostics }
