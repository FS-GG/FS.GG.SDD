namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System

[<RequireQualifiedAccess>]
module WorkspaceReducerAdapter =
    let private diagnostic code path message : QuintReplayDiagnostic =
        { Code = code; Path = path; Message = message }

    let private hash character = String(character, 64)

    let private proposal model =
        let target =
            model.Modules
            |> List.find (fun item -> item.Kind = WorkspaceModuleKind.ProductSpecification)
        let changedDigest = if target.ContentSha256 = hash 'd' then hash 'e' else hash 'd'
        let proposed : ChangeProposal =
            { SchemaVersion = 1
              IssueRef = "FS-GG/FS.GG.SDD#1164"
              ProseSha256 = hash 'c'
              BaseFingerprint = WorkspaceLifecycle.fingerprint model |> Result.defaultWith (sprintf "%A" >> failwith)
              AuthoringDepth = AuthoringDepth.DirectQuint
              Changes = [ WorkspaceChange.Upsert { target with ContentSha256 = changedDigest } ]
              Disposition = ProposalDisposition.CoherentDelta
              EvidenceFingerprint = None }
        proposed, { target with ContentSha256 = changedDigest }

    let private acceptance =
        { AcceptedBy = "sync-adapter@example.invalid"
          EvidenceRefs = [ "model:coherentDeltaCanReachAcceptance" ]
          AcceptedAtUtc = "2026-11-02T00:00:00Z" }

    let private field name (row: (string * FsQuint.ItfValue) list) =
        row |> List.tryFind (fst >> ((=) name)) |> Option.map snd

    let private expectedValue name row =
        match field name row with
        | Some(FsQuint.ItfValue.Integer value) -> Ok(QuintReplayValue.Integer(value.ToString()))
        | Some(FsQuint.ItfValue.Boolean value) -> Ok(QuintReplayValue.Boolean value)
        | Some(FsQuint.ItfValue.Text value) -> Ok(QuintReplayValue.Text value)
        | Some(FsQuint.ItfValue.Record fields) when name = "proposalPhase" ->
            match field "tag" fields with
            | Some(FsQuint.ItfValue.Text tag) -> Ok(QuintReplayValue.Text tag)
            | _ -> Error [ diagnostic "SYNC-ITF-PHASE" name "Model phase tag is missing." ]
        | _ -> Error [ diagnostic "SYNC-ITF-FIELD" name "Model trace field is missing or has the wrong type." ]

    let private names =
        [ "acceptedRevision"; "acceptedFingerprint"; "acceptedCoherent"; "proposalPhase"
          "proposalBaseRevision"; "proposalDisposition"; "authorityAtFiling"; "lastAction" ]

    let private state bindings =
        let draft : QuintReplayState = { Identity = ""; Bindings = bindings }
        QuintReplay.stateFingerprint draft
        |> Result.map (fun identity -> { draft with Identity = identity })

    let private expected row =
        let values = names |> List.map (fun name -> expectedValue name row |> Result.map (fun value -> name, value))
        let errors = values |> List.collect (function Error findings -> findings | _ -> [])
        if List.isEmpty errors then
            values |> List.choose (function Ok value -> Some value | _ -> None) |> state
        else Error errors

    // The Quint trace abstracts an accepted base to zero and its one accepted
    // transition to one. The full module delta is checked separately below.
    let private observed originRevision model phase baseRevision disposition filing action =
        let relativeRevision = model.Revision - originRevision
        [ "acceptedRevision", QuintReplayValue.Integer(string relativeRevision)
          "acceptedFingerprint", QuintReplayValue.Integer(string relativeRevision)
          "acceptedCoherent", QuintReplayValue.Boolean(List.isEmpty (WorkspaceLifecycle.validate model))
          "proposalPhase", QuintReplayValue.Text phase
          "proposalBaseRevision", QuintReplayValue.Integer(string baseRevision)
          "proposalDisposition", QuintReplayValue.Text disposition
          "authorityAtFiling", QuintReplayValue.Integer(string filing)
          "lastAction", QuintReplayValue.Text action ]
        |> state

    let private replayCoherentKnown accepted itfBytes environment =
        let limits = { FsQuint.Itf.defaultLimits with MaxBytes = 1048576; MaxStates = 4 }
        match FsQuint.Itf.read limits itfBytes with
        | Error findings -> Error(findings |> List.map (fun item -> diagnostic item.Code item.Path item.Message))
        | Ok itf when itf.States.Length <> 4 ->
            Error [ diagnostic "SYNC-ITF-COVERAGE" "/states" "Expected exactly four model-produced states for the selected acceptance witness." ]
        | Ok itf ->
            let projected = itf.States |> List.map expected
            let errors = projected |> List.collect (function Error findings -> findings | _ -> [])
            if not (List.isEmpty errors) then Error errors
            else
                let expectedStates = projected |> List.choose (function Ok value -> Some value | _ -> None)
                let source = { Path = "workspace-lifecycle.md"; Line = 1; Column = 1 }
                let actions = [ "FileIssue"; "Classify"; "Accept" ]
                let draft : QuintReplayTrace =
                    { SchemaVersion = 1; TraceIdentity = ""; Environment = environment; Initial = expectedStates.Head
                      Steps =
                        List.zip actions expectedStates.Tail
                        |> List.mapi (fun index (action, expectedState) ->
                            { Index = index + 1; Action = action; Source = source; Expected = expectedState }) }
                match QuintReplay.traceFingerprint draft with
                | Error findings -> Error findings
                | Ok traceIdentity ->
                    let trace = { draft with TraceIdentity = traceIdentity }
                    let model = accepted
                    let candidate, changed = proposal model
                    match WorkspaceLifecycle.reduce model candidate acceptance with
                    | Error findings ->
                        Error [ diagnostic "SYNC-REDUCER-REFUSED" "/adapter/accept" $"Production reducer refused coherent acceptance: %A{findings}" ]
                    | Ok reduced ->
                        let expectedModules =
                            model.Modules
                            |> List.map (fun item -> if item.Id = changed.Id then changed else item)
                            |> List.sortBy (fun item -> SpecificationId.value item.Id)
                        let actualModules =
                            reduced.Modules |> List.sortBy (fun item -> SpecificationId.value item.Id)
                        if reduced.SchemaVersion <> model.SchemaVersion
                           || reduced.Revision <> model.Revision + 1L
                           || actualModules <> expectedModules then
                            Error [ diagnostic "SYNC-REDUCER-CONTENT" "/adapter/accept" "Production reducer lost or altered the accepted module delta." ]
                        else
                            let actual =
                                    [ observed model.Revision model "NoProposal" -1L "" -1L "Init"
                                      observed model.Revision model "Filed" 0L "" 0L "FileIssue"
                                      observed model.Revision model "Classified" 0L "CoherentDelta" 0L "Classify"
                                      observed model.Revision reduced "Accepted" 0L "CoherentDelta" 0L "Accept" ]
                            let actualErrors = actual |> List.collect (function Error findings -> findings | _ -> [])
                            if not (List.isEmpty actualErrors) then Error actualErrors
                            else
                                let actualStates = actual |> List.choose (function Ok value -> Some value | _ -> None)
                                if actualStates.Head <> trace.Initial then
                                    Error [ diagnostic "SYNC-INITIAL-DIVERGENCE" "/adapter/init" "The production initial state differs from the model trace." ]
                                else
                                    let observations =
                                        List.zip trace.Steps actualStates.Tail
                                        |> List.map (fun (step, actualState) ->
                                            { Index = step.Index; Action = step.Action; Source = step.Source; Actual = actualState })
                                    QuintReplay.compare trace observations

    let replayCoherent accepted itfBytes environment =
        if accepted.Modules |> List.exists (fun item -> item.Kind = WorkspaceModuleKind.ProductSpecification) then
            replayCoherentKnown accepted itfBytes environment
        else
            Error [ diagnostic "SYNC-ADAPTER-SUBJECT" "/adapter" "Accepted workspace has no product specification for coherent replay." ]

    let checkStaleBase accepted =
        if not (accepted.Modules |> List.exists (fun item -> item.Kind = WorkspaceModuleKind.ProductSpecification)) then
            Error [ diagnostic "SYNC-ADAPTER-SUBJECT" "/adapter" "Accepted workspace has no product specification for stale-base replay." ]
        else
            let stale, _ = proposal accepted
            match WorkspaceLifecycle.reduce accepted stale acceptance with
            | Error findings -> Error [ diagnostic "SYNC-SETUP" "/adapter" $"Cannot advance accepted base: %A{findings}" ]
            | Ok advanced ->
                match WorkspaceLifecycle.reduce advanced stale acceptance with
                | Error findings when findings |> List.exists (fun item -> item.Code = "WORKSPACE-PROPOSAL-STALE") -> Ok()
                | Error findings -> Error [ diagnostic "SYNC-STALE-DIAGNOSTIC" "/adapter/stale" $"Unexpected stale refusal: %A{findings}" ]
                | Ok _ -> Error [ diagnostic "SYNC-STALE-ACCEPTED" "/adapter/stale" "Production reducer accepted a stale proposal." ]
