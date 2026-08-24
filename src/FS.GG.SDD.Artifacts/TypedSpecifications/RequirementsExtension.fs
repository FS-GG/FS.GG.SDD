namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.RegularExpressions

type AmbiguityState =
    | Open
    | Resolved
    | Deferred

type ScopeBoundary =
    { Id: SpecificationId
      Statement: string }

type RequirementStory =
    { Id: SpecificationId
      Priority: string
      Statement: string }

type Requirement =
    { Id: SpecificationId
      Statement: string
      AcceptanceIds: SpecificationId list
      EvidenceObligationIds: SpecificationId list }

type AcceptanceCriterion =
    { Id: SpecificationId
      StoryIds: SpecificationId list
      RequirementIds: SpecificationId list
      Statement: string }

type RequirementAmbiguity =
    { Id: SpecificationId
      Question: string
      State: AmbiguityState
      Decision: string option }

type RequirementsExtension =
    { UserValue: string
      Scope: ScopeBoundary list
      NonGoals: ScopeBoundary list
      Stories: RequirementStory list
      Requirements: Requirement list
      Acceptance: AcceptanceCriterion list
      Ambiguities: RequirementAmbiguity list
      PublicImpact: string list
      LifecycleNotes: string list }

type RequirementsDraft =
    private
        { UserValue: string
          Scope: ScopeBoundary list
          NonGoals: ScopeBoundary list
          Stories: RequirementStory list
          Requirements: Requirement list
          Acceptance: AcceptanceCriterion list
          Ambiguities: RequirementAmbiguity list
          PublicImpact: string list
          LifecycleNotes: string list }

[<RequireQualifiedAccess>]
module RequirementsDraft =
    let empty: RequirementsDraft =
        { UserValue = ""
          Scope = []
          NonGoals = []
          Stories = []
          Requirements = []
          Acceptance = []
          Ambiguities = []
          PublicImpact = []
          LifecycleNotes = [] }

    let withUserValue value (draft: RequirementsDraft) = { draft with UserValue = value }

    let addScope boundary (draft: RequirementsDraft) =
        { draft with
            Scope = boundary :: draft.Scope }

    let addNonGoal boundary (draft: RequirementsDraft) =
        { draft with
            NonGoals = boundary :: draft.NonGoals }

    let addStory story (draft: RequirementsDraft) =
        { draft with
            Stories = story :: draft.Stories }

    let addRequirement requirement (draft: RequirementsDraft) =
        { draft with
            Requirements = requirement :: draft.Requirements }

    let addAcceptance acceptance (draft: RequirementsDraft) =
        { draft with
            Acceptance = acceptance :: draft.Acceptance }

    let addAmbiguity ambiguity (draft: RequirementsDraft) =
        { draft with
            Ambiguities = ambiguity :: draft.Ambiguities }

    let addPublicImpact impact (draft: RequirementsDraft) =
        { draft with
            PublicImpact = impact :: draft.PublicImpact }

    let addLifecycleNote note (draft: RequirementsDraft) =
        { draft with
            LifecycleNotes = note :: draft.LifecycleNotes }

    let build (draft: RequirementsDraft) : RequirementsExtension =
        { UserValue = draft.UserValue
          Scope = List.rev draft.Scope
          NonGoals = List.rev draft.NonGoals
          Stories = List.rev draft.Stories
          Requirements = List.rev draft.Requirements
          Acceptance = List.rev draft.Acceptance
          Ambiguities = List.rev draft.Ambiguities
          PublicImpact = List.rev draft.PublicImpact
          LifecycleNotes = List.rev draft.LifecycleNotes }

module private Requirements =
    let diagnostic code path message : SpecificationDiagnostic =
        { Code = code
          Path = path
          Message = message
          Location = None }

    let sortDiagnostics (diagnostics: SpecificationDiagnostic list) =
        diagnostics
        |> List.distinct
        |> List.sortBy (fun item -> item.Path, item.Code, item.Message)

    let blank code path name (value: string) =
        if String.IsNullOrWhiteSpace value then
            [ diagnostic code path $"%s{name} is required." ]
        else
            []

    let idText id = SpecificationId.value id
    let sortedById rows (getId: 'row -> SpecificationId) = rows |> List.sortBy (getId >> idText)

    let sortedIds (ids: SpecificationId list) =
        ids |> List.distinct |> List.sortBy idText

    let duplicates path ids =
        ids
        |> List.countBy id
        |> List.choose (fun (identifier, count) ->
            if count > 1 then
                Some(
                    diagnostic
                        "REQ-ID-DUPLICATE"
                        path
                        $"Identifier '%s{idText identifier}' is declared more than once."
                )
            else
                None)

    let duplicateReferences code path ids =
        ids
        |> List.countBy id
        |> List.choose (fun (identifier, count) ->
            if count > 1 then
                Some(diagnostic code path $"Reference '%s{idText identifier}' appears more than once.")
            else
                None)

    let validateWithEvidence (obligations: EvidenceObligation list) (extension: RequirementsExtension) =
        let scopeIds = extension.Scope |> List.map _.Id
        let nonGoalIds = extension.NonGoals |> List.map _.Id
        let storyIds = extension.Stories |> List.map _.Id
        let requirementIds = extension.Requirements |> List.map _.Id
        let acceptanceIds = extension.Acceptance |> List.map _.Id
        let ambiguityIds = extension.Ambiguities |> List.map _.Id

        let allIds =
            scopeIds @ nonGoalIds @ storyIds @ requirementIds @ acceptanceIds @ ambiguityIds

        let stories = Set.ofList storyIds
        let requirements = Set.ofList requirementIds
        let acceptance = Set.ofList acceptanceIds
        let evidence = obligations |> List.map _.Id |> Set.ofList

        [ yield! blank "REQ-USER-VALUE-REQUIRED" "/extension/userValue" "User value" extension.UserValue
          yield! duplicates "/extension" allIds

          for index, boundary in extension.Scope |> List.indexed do
              yield!
                  blank
                      "REQ-SCOPE-STATEMENT-REQUIRED"
                      $"/extension/scope/%d{index}/statement"
                      "Scope statement"
                      boundary.Statement

          for index, boundary in extension.NonGoals |> List.indexed do
              yield!
                  blank
                      "REQ-NON-GOAL-STATEMENT-REQUIRED"
                      $"/extension/nonGoals/%d{index}/statement"
                      "Non-goal statement"
                      boundary.Statement

          for index, story in extension.Stories |> List.indexed do
              yield!
                  blank
                      "REQ-STORY-PRIORITY-REQUIRED"
                      $"/extension/stories/%d{index}/priority"
                      "Story priority"
                      story.Priority

              yield!
                  blank
                      "REQ-STORY-STATEMENT-REQUIRED"
                      $"/extension/stories/%d{index}/statement"
                      "Story statement"
                      story.Statement

          for index, requirement in extension.Requirements |> List.indexed do
              yield!
                  blank
                      "REQ-STATEMENT-REQUIRED"
                      $"/extension/requirements/%d{index}/statement"
                      "Requirement statement"
                      requirement.Statement

              yield!
                  duplicateReferences
                      "REQ-ACCEPTANCE-DUPLICATE"
                      $"/extension/requirements/%d{index}/acceptanceIds"
                      requirement.AcceptanceIds

              yield!
                  duplicateReferences
                      "REQ-EVIDENCE-DUPLICATE"
                      $"/extension/requirements/%d{index}/evidenceObligationIds"
                      requirement.EvidenceObligationIds

              if List.isEmpty requirement.AcceptanceIds then
                  yield
                      diagnostic
                          "REQ-ACCEPTANCE-REQUIRED"
                          $"/extension/requirements/%d{index}/acceptanceIds"
                          "Requirement must reference at least one acceptance criterion."

              for referenced in requirement.AcceptanceIds do
                  if not (Set.contains referenced acceptance) then
                      yield
                          diagnostic
                              "REQ-ACCEPTANCE-UNRESOLVED"
                              $"/extension/requirements/%d{index}/acceptanceIds"
                              $"Acceptance reference '%s{idText referenced}' does not resolve."

              for referenced in requirement.EvidenceObligationIds do
                  if not (Set.contains referenced evidence) then
                      yield
                          diagnostic
                              "REQ-EVIDENCE-UNRESOLVED"
                              $"/extension/requirements/%d{index}/evidenceObligationIds"
                              $"Evidence reference '%s{idText referenced}' does not resolve."

          for index, criterion in extension.Acceptance |> List.indexed do
              yield!
                  blank
                      "REQ-ACCEPTANCE-STATEMENT-REQUIRED"
                      $"/extension/acceptance/%d{index}/statement"
                      "Acceptance statement"
                      criterion.Statement

              yield!
                  duplicateReferences
                      "REQ-STORY-REFERENCE-DUPLICATE"
                      $"/extension/acceptance/%d{index}/storyIds"
                      criterion.StoryIds

              yield!
                  duplicateReferences
                      "REQ-REQUIREMENT-REFERENCE-DUPLICATE"
                      $"/extension/acceptance/%d{index}/requirementIds"
                      criterion.RequirementIds

              for referenced in criterion.StoryIds do
                  if not (Set.contains referenced stories) then
                      yield
                          diagnostic
                              "REQ-STORY-UNRESOLVED"
                              $"/extension/acceptance/%d{index}/storyIds"
                              $"Story reference '%s{idText referenced}' does not resolve."

              for referenced in criterion.RequirementIds do
                  if not (Set.contains referenced requirements) then
                      yield
                          diagnostic
                              "REQ-REQUIREMENT-UNRESOLVED"
                              $"/extension/acceptance/%d{index}/requirementIds"
                              $"Requirement reference '%s{idText referenced}' does not resolve."

          for index, ambiguity in extension.Ambiguities |> List.indexed do
              yield!
                  blank
                      "REQ-AMBIGUITY-QUESTION-REQUIRED"
                      $"/extension/ambiguities/%d{index}/question"
                      "Ambiguity question"
                      ambiguity.Question

              match ambiguity.State, ambiguity.Decision with
              | Open, None -> ()
              | Open, Some _ ->
                  yield
                      diagnostic
                          "REQ-AMBIGUITY-OPEN-DECISION"
                          $"/extension/ambiguities/%d{index}/decision"
                          "Open ambiguity cannot carry a resolved decision."
              | (Resolved | Deferred), Some decision when not (String.IsNullOrWhiteSpace decision) -> ()
              | Resolved, _ ->
                  yield
                      diagnostic
                          "REQ-AMBIGUITY-DECISION-REQUIRED"
                          $"/extension/ambiguities/%d{index}/decision"
                          "Resolved ambiguity requires a decision."
              | Deferred, _ ->
                  yield
                      diagnostic
                          "REQ-AMBIGUITY-DEFERRAL-REQUIRED"
                          $"/extension/ambiguities/%d{index}/decision"
                          "Deferred ambiguity requires a visible deferral decision."

          for index, impact in extension.PublicImpact |> List.indexed do
              yield! blank "REQ-PUBLIC-IMPACT-REQUIRED" $"/extension/publicImpact/%d{index}" "Public impact" impact

          for index, note in extension.LifecycleNotes |> List.indexed do
              yield! blank "REQ-LIFECYCLE-NOTE-REQUIRED" $"/extension/lifecycleNotes/%d{index}" "Lifecycle note" note ]
        |> sortDiagnostics

    let stateName =
        function
        | Open -> "open"
        | Resolved -> "resolved"
        | Deferred -> "deferred"

    let parseState =
        function
        | "open" -> Ok Open
        | "resolved" -> Ok Resolved
        | "deferred" -> Ok Deferred
        | value -> Error $"Unknown ambiguity state '%s{value}'."

    let writeIdArray (writer: Utf8JsonWriter) (name: string) (ids: SpecificationId list) =
        writer.WriteStartArray name
        sortedIds ids |> List.iter (idText >> writer.WriteStringValue)
        writer.WriteEndArray()

    let writeBoundary (writer: Utf8JsonWriter) (boundary: ScopeBoundary) =
        writer.WriteStartObject()
        writer.WriteString("id", idText boundary.Id)
        writer.WriteString("statement", boundary.Statement)
        writer.WriteEndObject()

    let writeExtension (writer: Utf8JsonWriter) (extension: RequirementsExtension) =
        writer.WriteStartObject()
        writer.WriteString("schema", "fsgg.requirements-extension/v1")
        writer.WriteNumber("schemaVersion", 1)
        writer.WriteString("userValue", extension.UserValue)

        writer.WriteStartArray("scope")
        sortedById extension.Scope _.Id |> List.iter (writeBoundary writer)
        writer.WriteEndArray()

        writer.WriteStartArray("nonGoals")
        sortedById extension.NonGoals _.Id |> List.iter (writeBoundary writer)
        writer.WriteEndArray()

        writer.WriteStartArray("stories")

        sortedById extension.Stories _.Id
        |> List.iter (fun story ->
            writer.WriteStartObject()
            writer.WriteString("id", idText story.Id)
            writer.WriteString("priority", story.Priority)
            writer.WriteString("statement", story.Statement)
            writer.WriteEndObject())

        writer.WriteEndArray()

        writer.WriteStartArray("requirements")

        sortedById extension.Requirements _.Id
        |> List.iter (fun requirement ->
            writer.WriteStartObject()
            writer.WriteString("id", idText requirement.Id)
            writer.WriteString("statement", requirement.Statement)
            writeIdArray writer "acceptanceIds" requirement.AcceptanceIds
            writeIdArray writer "evidenceObligationIds" requirement.EvidenceObligationIds
            writer.WriteEndObject())

        writer.WriteEndArray()

        writer.WriteStartArray("acceptance")

        sortedById extension.Acceptance _.Id
        |> List.iter (fun criterion ->
            writer.WriteStartObject()
            writer.WriteString("id", idText criterion.Id)
            writeIdArray writer "storyIds" criterion.StoryIds
            writeIdArray writer "requirementIds" criterion.RequirementIds
            writer.WriteString("statement", criterion.Statement)
            writer.WriteEndObject())

        writer.WriteEndArray()

        writer.WriteStartArray("ambiguities")

        sortedById extension.Ambiguities _.Id
        |> List.iter (fun ambiguity ->
            writer.WriteStartObject()
            writer.WriteString("id", idText ambiguity.Id)
            writer.WriteString("question", ambiguity.Question)
            writer.WriteString("state", stateName ambiguity.State)

            match ambiguity.Decision with
            | Some decision -> writer.WriteString("decision", decision)
            | None -> writer.WriteNull("decision")

            writer.WriteEndObject())

        writer.WriteEndArray()

        writer.WriteStartArray("publicImpact")
        extension.PublicImpact |> List.sort |> List.iter writer.WriteStringValue
        writer.WriteEndArray()
        writer.WriteStartArray("lifecycleNotes")
        extension.LifecycleNotes |> List.sort |> List.iter writer.WriteStringValue
        writer.WriteEndArray()
        writer.WriteEndObject()

    let canonicalBytes (extension: RequirementsExtension) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        writeExtension writer extension
        writer.Flush()
        stream.ToArray()

    let tryProperty (name: string) (element: JsonElement) =
        let mutable value = Unchecked.defaultof<JsonElement>

        if element.TryGetProperty(name, &value) then
            Some value
        else
            None

    let stringProperty (name: string) (element: JsonElement) =
        match tryProperty name element with
        | Some value when value.ValueKind = JsonValueKind.String ->
            match value.GetString() |> Option.ofObj with
            | Some text -> Ok text
            | None -> Error $"Property '%s{name}' cannot be null."
        | _ -> Error $"Property '%s{name}' must be a string."

    let arrayProperty name element =
        match tryProperty name element with
        | Some value when value.ValueKind = JsonValueKind.Array -> Ok(value.EnumerateArray() |> List.ofSeq)
        | _ -> Error $"Property '%s{name}' must be an array."

    let parseId value = SpecificationId.create value

    let parseIdArray name element =
        arrayProperty name element
        |> Result.bind (fun values ->
            values
            |> List.map (fun value ->
                if value.ValueKind = JsonValueKind.String then
                    match value.GetString() |> Option.ofObj with
                    | Some text -> parseId text
                    | None -> Error $"Array '%s{name}' cannot contain null."
                else
                    Error $"Array '%s{name}' must contain strings.")
            |> fun results ->
                let errors =
                    results
                    |> List.choose (function
                        | Error error -> Some error
                        | _ -> None)

                if List.isEmpty errors then
                    Ok(
                        results
                        |> List.choose (function
                            | Ok value -> Some value
                            | _ -> None)
                    )
                else
                    Error(String.concat "; " errors))

    let parseRecord idField parser element =
        stringProperty idField element
        |> Result.bind parseId
        |> Result.bind (fun identifier -> parser identifier element)

    let parseList name parser root =
        arrayProperty name root
        |> Result.bind (fun values ->
            values
            |> List.map parser
            |> fun results ->
                let errors =
                    results
                    |> List.choose (function
                        | Error error -> Some error
                        | _ -> None)

                if List.isEmpty errors then
                    Ok(
                        results
                        |> List.choose (function
                            | Ok value -> Some value
                            | _ -> None)
                    )
                else
                    Error(String.concat "; " errors))

    let parseExtension (element: JsonElement) : Result<RequirementsExtension, SpecificationDiagnostic list> =
        try
            let schema = stringProperty "schema" element

            let version =
                match tryProperty "schemaVersion" element with
                | Some value when value.ValueKind = JsonValueKind.Number ->
                    match value.TryGetInt32() with
                    | true, number -> Ok number
                    | _ -> Error "schemaVersion must be an integer."
                | _ -> Error "schemaVersion must be an integer."

            let boundaries name =
                parseList
                    name
                    (parseRecord "id" (fun identifier item ->
                        stringProperty "statement" item
                        |> Result.map (fun statement ->
                            { Id = identifier
                              Statement = statement })))
                    element

            let stories =
                parseList
                    "stories"
                    (parseRecord "id" (fun identifier item ->
                        match stringProperty "priority" item, stringProperty "statement" item with
                        | Ok priority, Ok statement ->
                            Ok
                                { Id = identifier
                                  Priority = priority
                                  Statement = statement }
                        | Error error, _
                        | _, Error error -> Error error))
                    element

            let requirements =
                parseList
                    "requirements"
                    (parseRecord "id" (fun identifier item ->
                        match
                            stringProperty "statement" item,
                            parseIdArray "acceptanceIds" item,
                            parseIdArray "evidenceObligationIds" item
                        with
                        | Ok statement, Ok acceptanceIds, Ok evidenceIds ->
                            Ok
                                { Id = identifier
                                  Statement = statement
                                  AcceptanceIds = acceptanceIds
                                  EvidenceObligationIds = evidenceIds }
                        | values ->
                            [ match values with
                              | Error error, _, _ -> yield error
                              | _ -> ()
                              match values with
                              | _, Error error, _ -> yield error
                              | _ -> ()
                              match values with
                              | _, _, Error error -> yield error
                              | _ -> () ]
                            |> String.concat "; "
                            |> Error))
                    element

            let acceptance =
                parseList
                    "acceptance"
                    (parseRecord "id" (fun identifier item ->
                        match
                            parseIdArray "storyIds" item,
                            parseIdArray "requirementIds" item,
                            stringProperty "statement" item
                        with
                        | Ok storyIds, Ok requirementIds, Ok statement ->
                            Ok
                                { Id = identifier
                                  StoryIds = storyIds
                                  RequirementIds = requirementIds
                                  Statement = statement }
                        | values ->
                            [ match values with
                              | Error error, _, _ -> yield error
                              | _ -> ()
                              match values with
                              | _, Error error, _ -> yield error
                              | _ -> ()
                              match values with
                              | _, _, Error error -> yield error
                              | _ -> () ]
                            |> String.concat "; "
                            |> Error))
                    element

            let ambiguities =
                parseList
                    "ambiguities"
                    (parseRecord "id" (fun identifier item ->
                        match stringProperty "question" item, stringProperty "state" item with
                        | Ok question, Ok stateText ->
                            parseState stateText
                            |> Result.map (fun state ->
                                let decision =
                                    match tryProperty "decision" item with
                                    | Some value when value.ValueKind = JsonValueKind.String ->
                                        value.GetString() |> Option.ofObj
                                    | _ -> None

                                { Id = identifier
                                  Question = question
                                  State = state
                                  Decision = decision })
                        | Error error, _
                        | _, Error error -> Error error))
                    element

            let strings name =
                arrayProperty name element
                |> Result.bind (fun values ->
                    if
                        values
                        |> List.forall (fun item ->
                            item.ValueKind = JsonValueKind.String && not (isNull (item.GetString())))
                    then
                        Ok(values |> List.choose (fun item -> item.GetString() |> Option.ofObj))
                    else
                        Error $"Array '%s{name}' must contain strings.")

            match
                schema,
                version,
                stringProperty "userValue" element,
                boundaries "scope",
                boundaries "nonGoals",
                stories,
                requirements,
                acceptance,
                ambiguities,
                strings "publicImpact",
                strings "lifecycleNotes"
            with
            | Ok schemaValue,
              Ok versionValue,
              Ok userValue,
              Ok scope,
              Ok nonGoals,
              Ok storyRows,
              Ok requirementRows,
              Ok acceptanceRows,
              Ok ambiguityRows,
              Ok publicImpact,
              Ok lifecycleNotes when schemaValue = "fsgg.requirements-extension/v1" && versionValue = 1 ->
                let decoded: RequirementsExtension =
                    { UserValue = userValue
                      Scope = scope
                      NonGoals = nonGoals
                      Stories = storyRows
                      Requirements = requirementRows
                      Acceptance = acceptanceRows
                      Ambiguities = ambiguityRows
                      PublicImpact = publicImpact
                      LifecycleNotes = lifecycleNotes }

                Ok decoded
            | values ->
                let message =
                    [ match values with
                      | Error e, _, _, _, _, _, _, _, _, _, _ -> yield e
                      | _ -> ()
                      match values with
                      | _, Error e, _, _, _, _, _, _, _, _, _ -> yield e
                      | _ -> ()
                      match values with
                      | _, _, Error e, _, _, _, _, _, _, _, _ -> yield e
                      | _ -> ()
                      match values with
                      | _, _, _, Error e, _, _, _, _, _, _, _ -> yield e
                      | _ -> ()
                      match values with
                      | _, _, _, _, Error e, _, _, _, _, _, _ -> yield e
                      | _ -> ()
                      match values with
                      | _, _, _, _, _, Error e, _, _, _, _, _ -> yield e
                      | _ -> ()
                      match values with
                      | _, _, _, _, _, _, Error e, _, _, _, _ -> yield e
                      | _ -> ()
                      match values with
                      | _, _, _, _, _, _, _, Error e, _, _, _ -> yield e
                      | _ -> ()
                      match values with
                      | _, _, _, _, _, _, _, _, Error e, _, _ -> yield e
                      | _ -> ()
                      match values with
                      | _, _, _, _, _, _, _, _, _, Error e, _ -> yield e
                      | _ -> ()
                      match values with
                      | _, _, _, _, _, _, _, _, _, _, Error e -> yield e
                      | _ -> ()
                      match schema, version with
                      | Ok schemaValue, Ok versionValue when
                          schemaValue <> "fsgg.requirements-extension/v1" || versionValue <> 1
                          ->
                          yield "Requirements extension schema is unsupported."
                      | _ -> () ]
                    |> String.concat "; "

                Error [ diagnostic "REQ-CODEC-MALFORMED" "/extension" message ]
        with error ->
            Error [ diagnostic "REQ-CODEC-MALFORMED" "/extension" error.Message ]

    let markdown (extension: RequirementsExtension) =
        let boundaryRows (rows: ScopeBoundary list) =
            sortedById rows (fun item -> item.Id)
            |> List.map (fun item -> $"- %s{idText item.Id}: %s{item.Statement}")
            |> function
                | [] -> [ "- None." ]
                | values -> values

        let storyRows =
            sortedById extension.Stories (fun item -> item.Id)
            |> List.map (fun item -> $"- %s{idText item.Id} (%s{item.Priority}): %s{item.Statement}")
            |> function
                | [] -> [ "- None." ]
                | values -> values

        let acceptanceRows =
            sortedById extension.Acceptance (fun item -> item.Id)
            |> List.map (fun item ->
                let tags =
                    (item.StoryIds @ item.RequirementIds)
                    |> sortedIds
                    |> List.map (fun id -> $"[%s{idText id}]")
                    |> String.concat " "

                $"- %s{idText item.Id} %s{tags}: %s{item.Statement}")
            |> function
                | [] -> [ "- None." ]
                | values -> values

        let requirementRows =
            sortedById extension.Requirements (fun item -> item.Id)
            |> List.map (fun item ->
                let refs = item.AcceptanceIds |> sortedIds |> List.map idText |> String.concat ", "
                $"- %s{idText item.Id}: %s{item.Statement} (Acceptance: %s{refs})")
            |> function
                | [] -> [ "- None." ]
                | values -> values

        let ambiguityRows =
            sortedById extension.Ambiguities (fun item -> item.Id)
            |> List.map (fun item ->
                let decision =
                    item.Decision
                    |> Option.map (fun value -> " — " + value)
                    |> Option.defaultValue ""

                $"- %s{idText item.Id} %s{stateName item.State}: %s{item.Question}%s{decision}")
            |> function
                | [] -> [ "- None." ]
                | values -> values

        [ "## User value"; ""; extension.UserValue; ""; "## Scope"; "" ]
        @ boundaryRows extension.Scope
        @ [ ""; "## Non-goals"; "" ]
        @ boundaryRows extension.NonGoals
        @ [ ""; "## User stories"; "" ]
        @ storyRows
        @ [ ""; "## Acceptance criteria"; "" ]
        @ acceptanceRows
        @ [ ""; "## Requirements"; "" ]
        @ requirementRows
        @ [ ""; "## Ambiguities"; "" ]
        @ ambiguityRows
        @ [ ""; "## Public impact"; "" ]
        @ (if List.isEmpty extension.PublicImpact then
               [ "- None." ]
           else
               extension.PublicImpact |> List.sort |> List.map (fun value -> "- " + value))
        @ [ ""; "## Lifecycle notes"; "" ]
        @ (if List.isEmpty extension.LifecycleNotes then
               [ "- None." ]
           else
               extension.LifecycleNotes |> List.sort |> List.map (fun value -> "- " + value))

[<RequireQualifiedAccess>]
module RequirementsExtension =
    let validate (extension: RequirementsExtension) =
        Requirements.validateWithEvidence [] extension

    let contract: ExtensionContract<RequirementsExtension> =
        { Kind = "requirements"
          SchemaVersion = 1
          Validate = Requirements.validateWithEvidence
          EncodeCanonical = Requirements.canonicalBytes
          WriteJson = Requirements.writeExtension
          DecodeJson = Requirements.parseExtension
          ProjectMarkdown = Requirements.markdown }

[<RequireQualifiedAccess>]
module RequirementsMigration =
    let private heading = Regex(@"^##\s+(.+?)\s*$", RegexOptions.Compiled)

    let private scopeRow =
        Regex(@"^-\s+(SB-\d{3,}):\s+(.+)$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    let private storyRow =
        Regex(@"^-\s+(US-\d{3,})\s+\(([^)]+)\):\s+(.+)$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    let private acceptanceRow =
        Regex(@"^-\s+(AC-\d{3,})\s+(.+?):\s+(.+)$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    let private requirementRow =
        Regex(@"^-\s+(FR-\d{3,}):\s+(.+)$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    let private ambiguityRow =
        Regex(
            @"^-\s+(AMB-\d{3,})(?:\s+(open|resolved|deferred))?:\s+(.+)$",
            RegexOptions.Compiled ||| RegexOptions.IgnoreCase
        )

    let private idReference =
        Regex(@"\b(?:US|FR|AC|EV)\-\d{3,}\b", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    let private location line column = { Line = line; Column = column }

    let private finding code reason message line column =
        { Code = code
          Reason = reason
          Message = message
          Location = location line column }

    let private identifier (value: string) line =
        match SpecificationId.create (value.ToUpperInvariant()) with
        | Ok id -> Ok id
        | Error message -> Error(finding "REQ-MIGRATION-ID" MalformedConstruct message line 1)

    let private sectionRows (sections: Map<string, (int * string) list>) name =
        sections |> Map.tryFind name |> Option.defaultValue []

    let private meaningful rows =
        rows |> List.filter (fun (_, text) -> not (String.IsNullOrWhiteSpace text))

    let private logicalRows rows =
        rows
        |> meaningful
        |> List.fold
            (fun accumulated (line, text) ->
                let trimmed = text.Trim()
                let continuation = text.Length > 0 && Char.IsWhiteSpace text[0]

                match accumulated with
                | (firstLine, current) :: tail when continuation -> (firstLine, current + " " + trimmed) :: tail
                | _ -> (line, trimmed) :: accumulated)
            []
        |> List.rev

    let analyzeMarkdown (markdown: string) =
        let lines = markdown.Replace("\r\n", "\n").Split('\n')
        let mutable current = ""
        let mutable sections: Map<string, (int * string) list> = Map.empty
        let unknown = ResizeArray<MigrationFinding>()

        let supported =
            set
                [ "User Value"
                  "Scope"
                  "Non-Goals"
                  "User Stories"
                  "Acceptance Scenarios"
                  "Functional Requirements"
                  "Ambiguities"
                  "Public Or Tool-Facing Impact"
                  "Lifecycle Notes" ]

        for index, line in lines |> Array.indexed do
            let matched = heading.Match line

            if matched.Success then
                current <- matched.Groups[1].Value

                if not (Set.contains current supported) then
                    unknown.Add(
                        finding
                            "REQ-MIGRATION-UNKNOWN-HEADING"
                            UnknownSemanticHeading
                            $"Heading '%s{current}' has no P2 requirements representation."
                            (index + 1)
                            1
                    )
            elif not (String.IsNullOrWhiteSpace current) then
                let existing = Map.tryFind current sections |> Option.defaultValue []
                sections <- Map.add current (existing @ [ index + 1, line ]) sections

        let schemaLine =
            lines
            |> Array.indexed
            |> Array.tryFind (fun (_, line) ->
                line.TrimStart().StartsWith("schemaVersion:", StringComparison.OrdinalIgnoreCase))

        let schemaFindings =
            match schemaLine with
            | Some(index, line) when (line.Split([| ':' |], 2)[1]).Trim() = "1" -> []
            | Some(index, _) ->
                [ finding
                      "REQ-MIGRATION-SCHEMA"
                      UnsupportedSchemaVersion
                      "Only Standard SDD schemaVersion 1 can migrate."
                      (index + 1)
                      1 ]
            | None ->
                [ finding "REQ-MIGRATION-SCHEMA" UnsupportedSchemaVersion "Standard SDD schemaVersion is required." 1 1 ]

        let nonEmptyUnknown =
            unknown
            |> Seq.filter (fun item ->
                let headingName =
                    lines[item.Location.Line - 1]
                    |> fun value -> heading.Match(value).Groups[1].Value

                sectionRows sections headingName |> meaningful |> List.isEmpty |> not)
            |> List.ofSeq

        let parseBoundaries section =
            sectionRows sections section
            |> logicalRows
            |> List.map (fun (line, text) ->
                let matched = scopeRow.Match text

                if matched.Success then
                    identifier matched.Groups[1].Value line
                    |> Result.map (fun id ->
                        { Id = id
                          Statement = matched.Groups[2].Value })
                else
                    Error(
                        finding
                            "REQ-MIGRATION-SCOPE"
                            MalformedConstruct
                            $"%s{section} row is not a stable SB-### list item."
                            line
                            1
                    ))

        let parsedScope = parseBoundaries "Scope"
        let parsedNonGoals = parseBoundaries "Non-Goals"

        let parsedStories =
            sectionRows sections "User Stories"
            |> logicalRows
            |> List.map (fun (line, text) ->
                let matched = storyRow.Match text

                if matched.Success then
                    identifier matched.Groups[1].Value line
                    |> Result.map (fun id ->
                        { Id = id
                          Priority = matched.Groups[2].Value
                          Statement = matched.Groups[3].Value })
                else
                    Error(
                        finding
                            "REQ-MIGRATION-STORY"
                            MalformedConstruct
                            "User story is not a stable US-### (priority) list item."
                            line
                            1
                    ))

        let parsedAcceptance =
            sectionRows sections "Acceptance Scenarios"
            |> logicalRows
            |> List.map (fun (line, text) ->
                let matched = acceptanceRow.Match text

                if matched.Success then
                    let references =
                        idReference.Matches(text)
                        |> Seq.cast<Match>
                        |> Seq.map _.Value.ToUpperInvariant()
                        |> List.ofSeq

                    match identifier matched.Groups[1].Value line with
                    | Error error -> Error error
                    | Ok id ->
                        let ids =
                            references
                            |> List.choose (fun value -> SpecificationId.create value |> Result.toOption)

                        Ok
                            { Id = id
                              StoryIds =
                                ids
                                |> List.filter (SpecificationId.value >> _.StartsWith("US-", StringComparison.Ordinal))
                              RequirementIds =
                                ids
                                |> List.filter (SpecificationId.value >> _.StartsWith("FR-", StringComparison.Ordinal))
                              Statement = matched.Groups[3].Value }
                else
                    Error(
                        finding
                            "REQ-MIGRATION-ACCEPTANCE"
                            MalformedConstruct
                            "Acceptance scenario is not a stable AC-### list item."
                            line
                            1
                    ))

        let parsedRequirements =
            sectionRows sections "Functional Requirements"
            |> logicalRows
            |> List.map (fun (line, text) ->
                let matched = requirementRow.Match text

                if matched.Success then
                    match identifier matched.Groups[1].Value line with
                    | Error error -> Error error
                    | Ok id ->
                        let references =
                            idReference.Matches(text)
                            |> Seq.cast<Match>
                            |> Seq.map _.Value.ToUpperInvariant()
                            |> List.ofSeq

                        let ids =
                            references
                            |> List.choose (fun value -> SpecificationId.create value |> Result.toOption)

                        Ok
                            { Id = id
                              Statement = matched.Groups[2].Value
                              AcceptanceIds =
                                ids
                                |> List.filter (SpecificationId.value >> _.StartsWith("AC-", StringComparison.Ordinal))
                              EvidenceObligationIds =
                                ids
                                |> List.filter (SpecificationId.value >> _.StartsWith("EV", StringComparison.Ordinal)) }
                else
                    Error(
                        finding
                            "REQ-MIGRATION-REQUIREMENT"
                            MalformedConstruct
                            "Requirement is not a stable FR-### list item."
                            line
                            1
                    ))

        let parsedAmbiguities =
            sectionRows sections "Ambiguities"
            |> logicalRows
            |> List.filter (fun (_, text) ->
                not (text.Trim().Equals("No material ambiguities recorded.", StringComparison.OrdinalIgnoreCase)))
            |> List.map (fun (line, text) ->
                let matched = ambiguityRow.Match text

                if matched.Success then
                    match identifier matched.Groups[1].Value line with
                    | Error error -> Error error
                    | Ok id ->
                        let state =
                            match matched.Groups[2].Value.ToLowerInvariant() with
                            | "resolved" -> Resolved
                            | "deferred" -> Deferred
                            | _ -> Open

                        let body = matched.Groups[3].Value
                        let separator = body.IndexOf(" — ", StringComparison.Ordinal)

                        match state, separator with
                        | Open, _ ->
                            Ok
                                { Id = id
                                  Question = body
                                  State = state
                                  Decision = None }
                        | (Resolved | Deferred), index when index > 0 && index + 3 < body.Length ->
                            Ok
                                { Id = id
                                  Question = body.Substring(0, index)
                                  State = state
                                  Decision = Some(body.Substring(index + 3)) }
                        | _ ->
                            Error(
                                finding
                                    "REQ-MIGRATION-AMBIGUITY-DECISION"
                                    MalformedConstruct
                                    "Resolved or deferred ambiguity requires an explicit decision after an em dash."
                                    line
                                    1
                            )
                else
                    Error(
                        finding
                            "REQ-MIGRATION-AMBIGUITY"
                            MalformedConstruct
                            "Ambiguity is not a stable AMB-### list item."
                            line
                            1
                    ))

        let errors results =
            results
            |> List.choose (function
                | Error error -> Some error
                | _ -> None)

        let parseFindings =
            errors parsedScope
            @ errors parsedNonGoals
            @ errors parsedStories
            @ errors parsedAcceptance
            @ errors parsedRequirements
            @ errors parsedAmbiguities

        let valueRows = sectionRows sections "User Value" |> meaningful

        let missingRequired =
            [ if List.isEmpty valueRows then
                  yield finding "REQ-MIGRATION-USER-VALUE" MalformedConstruct "User Value section is required." 1 1
              if List.isEmpty (sectionRows sections "Scope" |> meaningful) then
                  yield finding "REQ-MIGRATION-SCOPE" MalformedConstruct "Scope section is required." 1 1
              if List.isEmpty (sectionRows sections "User Stories" |> meaningful) then
                  yield finding "REQ-MIGRATION-STORY" MalformedConstruct "User Stories section is required." 1 1
              if List.isEmpty (sectionRows sections "Acceptance Scenarios" |> meaningful) then
                  yield
                      finding
                          "REQ-MIGRATION-ACCEPTANCE"
                          MalformedConstruct
                          "Acceptance Scenarios section is required."
                          1
                          1
              if List.isEmpty (sectionRows sections "Functional Requirements" |> meaningful) then
                  yield
                      finding
                          "REQ-MIGRATION-REQUIREMENT"
                          MalformedConstruct
                          "Functional Requirements section is required."
                          1
                          1 ]

        let unsupported = schemaFindings @ nonEmptyUnknown @ parseFindings @ missingRequired

        if not (List.isEmpty unsupported) then
            Unsupported(
                unsupported
                |> List.sortBy (fun item -> item.Location.Line, item.Location.Column, item.Code)
            )
        else
            let take results =
                results
                |> List.choose (function
                    | Ok value -> Some value
                    | _ -> None)

            let extension: RequirementsExtension =
                { UserValue = valueRows |> List.map snd |> String.concat "\n"
                  Scope = take parsedScope
                  NonGoals = take parsedNonGoals
                  Stories = take parsedStories
                  Requirements = take parsedRequirements
                  Acceptance = take parsedAcceptance
                  Ambiguities = take parsedAmbiguities
                  PublicImpact =
                    sectionRows sections "Public Or Tool-Facing Impact"
                    |> logicalRows
                    |> List.map (snd >> _.TrimStart('-', ' '))
                  LifecycleNotes =
                    sectionRows sections "Lifecycle Notes"
                    |> logicalRows
                    |> List.map (snd >> _.TrimStart('-', ' ')) }

            let validation = Requirements.validateWithEvidence [] extension

            let unresolved =
                validation
                |> List.filter (fun item -> item.Code.EndsWith("-UNRESOLVED", StringComparison.Ordinal))
                |> List.map (fun item ->
                    let referenced = Regex.Match(item.Message, @"'([^']+)'")

                    let idText =
                        if referenced.Success then
                            referenced.Groups[1].Value
                        else
                            ""

                    let line =
                        lines
                        |> Array.tryFindIndex (fun text -> text.Contains(idText, StringComparison.OrdinalIgnoreCase))
                        |> Option.map ((+) 1)
                        |> Option.defaultValue 1

                    finding "REQ-MIGRATION-UNRESOLVED" UnresolvedReference item.Message line 1)

            let invalid =
                validation
                |> List.filter (fun item -> not (item.Code.EndsWith("-UNRESOLVED", StringComparison.Ordinal)))
                |> List.map (fun item -> finding "REQ-MIGRATION-INVALID" MalformedConstruct item.Message 1 1)

            if not (List.isEmpty invalid) then
                Unsupported(invalid |> List.sortBy (fun item -> item.Location.Line, item.Code, item.Message))
            elif List.isEmpty unresolved then
                Migrated extension
            else
                Ambiguous(unresolved |> List.sortBy (fun item -> item.Location.Line, item.Code))
