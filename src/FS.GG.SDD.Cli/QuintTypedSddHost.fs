namespace FS.GG.SDD.Cli

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text
open FS.GG.SDD.Artifacts.TypedSpecifications

module internal QuintTypedSddHost =
    type Output =
        { Manifest: QuintAuthorityManifest
          Writes: (string * byte array) list }

    type Rollback =
        { ManifestPath: string
          ManifestBytes: byte array
          Writes: (string * byte array) list }

    let private diagnostic id message correction : TypedLifecycleDiagnostic =
        { Id = id
          Message = message
          Correction = correction }

    let private sha256 (value: byte array) =
        SHA256.HashData value |> Convert.ToHexString |> _.ToLowerInvariant()

    let private template =
        """# Requirements and evidence vertical slice

This document is both the reviewer-facing requirements package and the sole authored source for the
candidate model. Every semantic identity named in the prose appears in the executable catalogue below.
The slice asks one question: can `REQ-AUDIT-001` become accepted before its required `EV-VERIFY-001`
evidence is observed? The answer must remain no.

The catalogue is data, not a compiler-node naming convention. `RequirementEntry.id`,
`EvidenceEntry.id`, and their explicit relationship are the compiled-contract inputs.

```quint requirements.qnt +=
module RequirementsSlice {
  type RequirementEntry = { id: str, evidenceId: str, priority: int }
  type EvidenceEntry = { id: str, kind: str, required: bool }
  type ActionEntry = { id: str, reads: Set[str], writes: Set[str] }
  type PropertyEntry = { id: str, kind: str }

  pure val auditRequirement =
    { id: "REQ-AUDIT-001", evidenceId: "EV-VERIFY-001", priority: 1 }
  pure val requirements = Set(auditRequirement)

  pure val evidenceCatalogue = Set(
    { id: "EV-VERIFY-001", kind: "verification", required: true }
  )
  pure val actionCatalogue = Set(
    { id: "ObserveEvidence", reads: Set("EvidenceCatalogue"), writes: Set("ObservedEvidence") },
    { id: "AcceptRequirement", reads: Set("AuditRequirement", "ObservedEvidence"), writes: Set("AcceptedRequirements") }
  )
  pure val propertyCatalogue = Set(
    { id: "AcceptedOnlyWithEvidence", kind: "invariant" },
    { id: "RequirementCanBeAccepted", kind: "reachability" }
  )

  var observedEvidence: Set[str]
  var acceptedRequirements: Set[str]

  action init = all {
    observedEvidence' = Set(),
    acceptedRequirements' = Set(),
  }

  action observeEvidence(evidenceId: str): bool = all {
    evidenceCatalogue.exists(e => e.id == evidenceId),
    observedEvidence' = observedEvidence.union(Set(evidenceId)),
    acceptedRequirements' = acceptedRequirements,
  }

  action acceptRequirement(requirementId: str): bool =
    all {
      requirementId == auditRequirement.id,
      observedEvidence.contains(auditRequirement.evidenceId),
      observedEvidence' = observedEvidence,
      acceptedRequirements' = acceptedRequirements.union(Set(requirementId)),
    }

  action step = any {
    observeEvidence("EV-VERIFY-001"),
    acceptRequirement("REQ-AUDIT-001"),
  }

  val acceptedOnlyWithEvidence =
    acceptedRequirements.contains("REQ-AUDIT-001") implies
      observedEvidence.contains("EV-VERIFY-001")

  val requirementCanBeAccepted =
    not(acceptedRequirements.contains("REQ-AUDIT-001"))
}
```

The executable example traces the only accepted path: initialization, evidence observation, then
acceptance. The deliberately absent shortcut is also meaningful: calling `acceptRequirement` first is
disabled, so injected removal of its evidence guard must make the invariant red.

```quint requirements.qnt +=
module RequirementsSliceTests {
  import RequirementsSlice.*

  run evidenceBeforeAcceptance =
    init
      .then(observeEvidence("EV-VERIFY-001"))
      .then(acceptRequirement("REQ-AUDIT-001"))
      .expect(and {
        acceptedRequirements.contains("REQ-AUDIT-001"),
        acceptedOnlyWithEvidence,
      })
}
```

No prose-only requirement exists: the requirement, evidence obligation, relationship, invariant, and
example are all explicit in the embedded Quint source.
"""

    let private position line column : QuintSourcePosition = { Line = line; Column = column }

    let private range path startLine startColumn endLine endColumn : QuintSourceRange =
        { Path = path
          Start = position startLine startColumn
          End = position endLine endColumn }

    let private parseFences (source: QuintMarkdownSource) =
        let lines = source.Text.Split('\n')
        let mutable cursor = 0
        let mutable ordinal = 0
        let mutable generatedLines = Map.empty<string, int>
        let fences = ResizeArray<QuintFence>()
        let maps = ResizeArray<QuintSourceMapEntry>()

        while cursor < lines.Length do
            let header = lines[cursor]

            if
                header.StartsWith("```quint ", StringComparison.Ordinal)
                && header.EndsWith(" +=", StringComparison.Ordinal)
            then
                let target = header.Substring(9, header.Length - 12)

                let closing =
                    [ cursor + 1 .. lines.Length - 1 ]
                    |> List.tryFind (fun index -> lines[index] = "```")
                    |> Option.defaultWith (fun () -> invalidOp "unterminated Quint fence")

                let contentLines = lines[cursor + 1 .. closing - 1]
                let content = String.Join("\n", contentLines) + "\n"

                let moduleName =
                    contentLines
                    |> Array.tryPick (fun line ->
                        let trimmed = line.TrimStart()

                        if trimmed.StartsWith("module ", StringComparison.Ordinal) then
                            Some(trimmed.Substring(7).Split([| ' '; '{' |], StringSplitOptions.RemoveEmptyEntries)[0])
                        else
                            None)
                    |> Option.defaultWith (fun () -> invalidOp "Quint fence has no module declaration")

                let firstGeneratedLine = Map.tryFind target generatedLines |> Option.defaultValue 1
                let lastGeneratedLine = firstGeneratedLine + contentLines.Length - 1
                let lastColumn = max 1 contentLines[contentLines.Length - 1].Length

                fences.Add
                    { Ordinal = ordinal
                      Target = target
                      ModuleName = moduleName
                      SourceRange = range source.Path (cursor + 1) 1 (closing + 1) 3
                      ContentSha256 = sha256 (Encoding.UTF8.GetBytes content) }

                maps.Add
                    { Target = target
                      GeneratedRange = range target firstGeneratedLine 1 lastGeneratedLine lastColumn
                      Source =
                        { FenceOrdinal = ordinal
                          Range = range source.Path (cursor + 2) 1 closing lastColumn } }

                generatedLines <- Map.add target (lastGeneratedLine + 1) generatedLines
                ordinal <- ordinal + 1
                cursor <- closing + 1
            else
                cursor <- cursor + 1

        List.ofSeq fences, List.ofSeq maps

    let private binding path catalogue id kind line : QuintCatalogueSourceBinding =
        { ModuleName = "RequirementsSlice"
          CatalogueName = catalogue
          Id = id
          Kind = kind
          Source = range path line 1 line 200 }

    let private sourceBindings path =
        [ binding path "requirements" "REQ-AUDIT-001" Requirement 19
          binding path "evidenceCatalogue" "EV-VERIFY-001" Evidence 23
          binding path "actionCatalogue" "ObserveEvidence" Action 26
          binding path "actionCatalogue" "AcceptRequirement" Action 27
          binding path "propertyCatalogue" "AcceptedOnlyWithEvidence" Invariant 30
          binding path "propertyCatalogue" "RequirementCanBeAccepted" ReachabilityProperty 31 ]

    let private requirement id =
        QuintToolchain.q1.Components
        |> List.collect _.Objects
        |> List.find (fun item -> item.Id = id)

    let private cacheObjectPath cacheRoot id =
        let item = requirement id
        Path.Combine(cacheRoot, "objects", item.Sha256)

    let private observeCache cacheRoot id =
        let item = requirement id
        let path = cacheObjectPath cacheRoot id

        try
            if not (File.Exists path) then
                { Id = id
                  Kind = item.Kind
                  State = QuintCacheObjectState.Absent },
                None
            else
                let bytes = File.ReadAllBytes path

                { Id = id
                  Kind = item.Kind
                  State = QuintCacheObjectState.Present(sha256 bytes, Some(int64 bytes.Length), true) },
                Some bytes
        with ex ->
            { Id = id
              Kind = item.Kind
              State = QuintCacheObjectState.Unreadable ex.Message },
            None

    let private request step objectId arguments : QuintProcessRequest =
        { StepId = step
          ExecutableObjectId = objectId
          Arguments = arguments
          Environment = [ "LANG", "C.UTF-8"; "TZ", "UTC" ]
          WorkingDirectory = "isolated-run" }

    let private execute executable (request: QuintProcessRequest) workingDirectory =
        try
            if not (OperatingSystem.IsLinux()) then
                invalidOp "the qualified Quint backend requires Linux network-namespace isolation"

            let unshare = "/usr/bin/unshare"

            if not (File.Exists unshare) then
                invalidOp "the qualified Quint backend requires /usr/bin/unshare for network isolation"

            let start = ProcessStartInfo(unshare)
            start.WorkingDirectory <- workingDirectory
            start.RedirectStandardOutput <- true
            start.RedirectStandardError <- true
            start.UseShellExecute <- false
            start.Environment.Clear()

            for name, value in request.Environment do
                start.Environment[name] <- value

            start.ArgumentList.Add "--user"
            start.ArgumentList.Add "--map-root-user"
            start.ArgumentList.Add "--net"
            start.ArgumentList.Add "--"
            start.ArgumentList.Add executable
            request.Arguments |> List.iter start.ArgumentList.Add

            use child =
                Process.Start start
                |> Option.ofObj
                |> Option.defaultWith (fun () -> invalidOp "process did not start")

            let stdout = child.StandardOutput.ReadToEndAsync()
            let stderr = child.StandardError.ReadToEndAsync()

            if not (child.WaitForExit 60000) then
                child.Kill(true)
                Error(-1, "process timed out")
            elif child.ExitCode <> 0 then
                Error(child.ExitCode, stderr.Result.Trim())
            else
                let warnings =
                    [ stdout.Result.Trim(); stderr.Result.Trim() ]
                    |> List.filter (String.IsNullOrWhiteSpace >> not)

                Ok warnings
        with ex ->
            Error(-1, ex.Message)

    let private runOnce
        (lmtBytes: byte array)
        (quintBytes: byte array)
        logicalPath
        target
        (requests: QuintProcessRequest list)
        (markdownBytes: byte array)
        runRoot
        =
        Directory.CreateDirectory runRoot |> ignore
        let markdownPath = Path.Combine(runRoot, logicalPath)

        Path.GetDirectoryName markdownPath
        |> Option.ofObj
        |> Option.iter (Directory.CreateDirectory >> ignore)

        File.WriteAllBytes(markdownPath, markdownBytes)
        let tools = Path.Combine(runRoot, ".tools")
        Directory.CreateDirectory tools |> ignore
        let lmt = Path.Combine(tools, "lmt")
        let quint = Path.Combine(tools, "quint")
        File.WriteAllBytes(lmt, lmtBytes)
        File.WriteAllBytes(quint, quintBytes)
        File.SetUnixFileMode(lmt, UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute)
        File.SetUnixFileMode(quint, UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute)
        let extractRequest = requests |> List.find (fun item -> item.StepId = "extract")
        let typecheckRequest = requests |> List.find (fun item -> item.StepId = "typecheck")

        match execute lmt extractRequest runRoot with
        | Error(code, detail) -> Error("extract", code, detail)
        | Ok extractionOutput ->
            let generatedPath = Path.Combine(runRoot, target)

            if not (File.Exists generatedPath) then
                Error("extract", -1, $"lmt did not emit {target}")
            else
                match execute quint typecheckRequest runRoot with
                | Error(code, detail) -> Error("typecheck", code, detail)
                | Ok quintOutput ->
                    let typedPath = Path.Combine(runRoot, "typed.json")

                    if not (File.Exists typedPath) then
                        Error("typecheck", -1, "Quint did not emit typed.json")
                    else
                        Ok(File.ReadAllBytes generatedPath, File.ReadAllBytes typedPath, extractionOutput @ quintOutput)

    let author
        packageIdentity
        workId
        title
        agent
        session
        cacheRoot
        (rollback: Rollback option)
        (migrationPayload: byte array option)
        =
        let lmtObservation, lmtBytes = observeCache cacheRoot "lmt-binary"
        let quintObservation, quintBytes = observeCache cacheRoot "quint-binary"
        let cache = [ lmtObservation; quintObservation ]
        let logicalPath = $"work/{workId}/specification.md"

        let requests: QuintProcessRequest list =
            [ request "extract" "lmt-binary" [ logicalPath ]
              request "typecheck" "quint-binary" [ "typecheck"; "requirements.qnt"; "--out=typed.json" ] ]

        match QuintToolchain.plan QuintToolchain.q1 cache requests, lmtBytes, quintBytes with
        | Error findings, _, _ ->
            Error(
                findings
                |> List.map (fun finding ->
                    diagnostic
                        "typedSdd.v2.cacheInvalid"
                        finding.Message
                        "Preseed --cache/objects with the exact Q1 lmt and Quint objects.")
            )
        | Ok _, Some lmtObject, Some quintObject ->
            let migrationProjection =
                migrationPayload
                |> Option.map (fun bytes ->
                    "\n## Migrated manifest-v1 semantic payload\n\n"
                    + "fsgg.requirements-extension/v1+base64 "
                    + Convert.ToBase64String bytes
                    + "\n")
                |> Option.defaultValue ""

            let markdownText =
                template.Replace("# Requirements and evidence vertical slice", $"# {title}", StringComparison.Ordinal)
                + migrationProjection

            let markdownBytes = Encoding.UTF8.GetBytes markdownText

            match QuintSource.createMarkdown logicalPath markdownBytes with
            | Error findings ->
                Error(
                    findings
                    |> List.map (fun finding ->
                        diagnostic "typedSdd.v2.sourceInvalid" finding.Message "Use canonical LF UTF-8 Markdown.")
                )
            | Ok source ->
                let fences, sourceMaps = parseFences source

                let temporary =
                    Path.Combine(Path.GetTempPath(), "fsgg-quint-author-" + Guid.NewGuid().ToString("N"))

                try
                    match
                        runOnce
                            lmtObject
                            quintObject
                            logicalPath
                            fences.Head.Target
                            requests
                            markdownBytes
                            (Path.Combine(temporary, "first")),
                        runOnce
                            lmtObject
                            quintObject
                            logicalPath
                            fences.Head.Target
                            requests
                            markdownBytes
                            (Path.Combine(temporary, "second"))
                    with
                    | Error(step, code, detail), _
                    | _, Error(step, code, detail) ->
                        Error
                            [ diagnostic
                                  $"typedSdd.v2.{step}Failed"
                                  $"Exact tool step '{step}' failed ({code}): {detail}"
                                  "Correct the exact cache object or authored Quint input; no acquisition is attempted." ]
                    | Ok(firstGenerated, firstTyped, firstWarnings), Ok(secondGenerated, secondTyped, secondWarnings) when
                        firstGenerated <> secondGenerated || firstTyped <> secondTyped
                        ->
                        Error
                            [ diagnostic
                                  "typedSdd.v2.nondeterministicTool"
                                  "Two isolated exact-tool runs produced different bytes."
                                  "Refuse the toolchain and restore the qualified cache." ]
                    | Ok(firstGenerated, firstTyped, firstWarnings), Ok(_, _, secondWarnings) when
                        firstWarnings @ secondWarnings <> []
                        ->
                        Error
                            [ diagnostic
                                  "typedSdd.v2.toolWarning"
                                  "The exact tool emitted unexpected output or warnings."
                                  "Resolve all extractor and Quint output before authoring." ]
                    | Ok(generated, typed, _), Ok(_, _, _) ->
                        let generatedObservation =
                            [ { Target = fences.Head.Target
                                Sha256 = sha256 generated
                                Bytes = int64 generated.Length } ]

                        let input: QuintObservedCompilation =
                            { ModuleName = "RequirementsBindings"
                              Toolchain = QuintToolchain.q1
                              Cache = cache
                              ProcessRequests = requests
                              Endpoint = QuintEndpointState.Available
                              ProcessObservations =
                                [ { StepId = "extract"
                                    Outcome = QuintProcessOutcome.Succeeded }
                                  { StepId = "typecheck"
                                    Outcome = QuintProcessOutcome.Succeeded } ]
                              Source = source
                              FenceManifest =
                                { Schema = QuintSource.fenceManifestSchema
                                  SourcePath = source.Path
                                  SourceSha256 = source.Sha256
                                  Fences = fences }
                              Extraction =
                                { First = generatedObservation
                                  Second = generatedObservation
                                  Warnings = [] }
                              SourceMap =
                                { Schema = QuintSource.sourceMapSchema
                                  SourceSha256 = source.Sha256
                                  Entries = sourceMaps }
                              TypedEffect =
                                { Profile = QuintProfile.identity
                                  QuintVersion = QuintProfile.quintVersion
                                  TypedEffectJson = Encoding.UTF8.GetString typed
                                  SourceBindings = sourceBindings logicalPath }
                              Metadata =
                                { Specification = "Q1Requirements"
                                  Relationships = []
                                  VerificationProfiles = []
                                  Bounds = []
                                  Impacts = []
                                  Compatibility = []
                                  Digests =
                                    [ { Name = "sandbox-contract"
                                        Sha256 = sha256 QuintSandbox.contractBytes }
                                      { Name = "typed-effect"
                                        Sha256 = sha256 typed } ] } }

                        match QuintCompiler.compileObserved input with
                        | Error findings ->
                            Error(
                                findings
                                |> List.map (fun finding ->
                                    diagnostic
                                        "typedSdd.v2.compilationFailed"
                                        finding.Message
                                        "Correct the authored source or exact tool observations.")
                            )
                        | Ok output ->
                            let finalized =
                                match migrationPayload with
                                | None ->
                                    Ok(
                                        output.Contract,
                                        output.CanonicalContract,
                                        output.Bindings,
                                        output.Receipt,
                                        output.CanonicalReceipt
                                    )
                                | Some payload ->
                                    let payloadLine =
                                        let markdownLines = markdownText.Split('\n')

                                        markdownLines
                                        |> Array.findIndex (fun line ->
                                            line.StartsWith(
                                                "fsgg.requirements-extension/v1+base64 ",
                                                StringComparison.Ordinal
                                            ))
                                        |> (+) 1

                                    let markdownLines = markdownText.Split('\n')

                                    let payloadRange =
                                        range
                                            logicalPath
                                            payloadLine
                                            1
                                            payloadLine
                                            markdownLines[payloadLine - 1].Length

                                    QuintV1Migration.lower payload payloadRange output.Contract
                                    |> Result.bind (fun contract ->
                                        match
                                            QuintContract.serializeCanonical contract,
                                            QuintBindings.generate "RequirementsBindings" contract
                                        with
                                        | Ok canonical, Ok bindings ->
                                            match
                                                QuintContract.fingerprint
                                                    { SourceSha256 = output.Receipt.SourceSha256
                                                      FenceManifestSha256 = output.Receipt.FenceManifestSha256
                                                      GeneratedModulesSha256 = output.Receipt.GeneratedModulesSha256
                                                      ToolchainSha256 = output.Receipt.ToolchainSha256
                                                      Contract = contract }
                                            with
                                            | Ok fingerprint ->
                                                let receipt =
                                                    { output.Receipt with
                                                        ContractSha256 = sha256 (Encoding.UTF8.GetBytes canonical)
                                                        CompilationFingerprint = fingerprint }

                                                Ok(
                                                    contract,
                                                    canonical,
                                                    bindings,
                                                    receipt,
                                                    QuintCompiler.encodeReceipt receipt
                                                )
                                            | Error findings ->
                                                Error(
                                                    findings
                                                    |> List.map (fun finding ->
                                                        diagnostic
                                                            "typedSdd.v2.migrationCompilationFailed"
                                                            finding.Message
                                                            finding.Correction)
                                                )
                                        | Error findings, _ ->
                                            Error(
                                                findings
                                                |> List.map (fun finding ->
                                                    diagnostic
                                                        "typedSdd.v2.migrationCompilationFailed"
                                                        finding.Message
                                                        finding.Correction)
                                            )
                                        | _, Error findings ->
                                            Error(
                                                findings
                                                |> List.map (fun finding ->
                                                    diagnostic
                                                        "typedSdd.v2.migrationCompilationFailed"
                                                        finding.Message
                                                        "Correct the bounded v1 migration identities and references.")
                                            ))

                            match finalized with
                            | Error findings -> Error findings
                            | Ok(_, canonicalContract, bindings, _, canonicalReceipt) ->
                                let fenceBytes = QuintSource.encodeFenceManifest input.FenceManifest
                                let sourceMapBytes = QuintSource.encodeSourceMap input.SourceMap

                                let relative =
                                    [ "markdown", logicalPath, markdownBytes
                                      "fence-manifest", $"readiness/{workId}/quint/fences.json", fenceBytes
                                      "generated-modules", $"readiness/{workId}/quint/{fences.Head.Target}", generated
                                      "source-map", $"readiness/{workId}/quint/source-map.json", sourceMapBytes
                                      "typed-effect", $"readiness/{workId}/quint/typed-effect.json", typed
                                      "sandbox-contract",
                                      $"readiness/{workId}/quint/sandbox-contract.json",
                                      QuintSandbox.contractBytes
                                      "compiled-contract",
                                      $"readiness/{workId}/quint/contract.json",
                                      Encoding.UTF8.GetBytes canonicalContract
                                      "bindings",
                                      $"readiness/{workId}/quint/bindings.fs",
                                      Encoding.UTF8.GetBytes bindings.FSharpSource
                                      "compilation-receipt",
                                      $"readiness/{workId}/quint/receipt.json",
                                      Encoding.UTF8.GetBytes canonicalReceipt ]

                                let artifacts =
                                    relative
                                    |> List.map (fun (id, path, bytes) ->
                                        { Id = id
                                          Path = path
                                          Sha256 = sha256 bytes })

                                let manifest =
                                    { SchemaVersion = 2
                                      Lifecycle = "typed-sdd"
                                      Backend = "quint-specification-v1"
                                      ProfileIdentity = QuintProfile.identity
                                      ToolchainIdentity = QuintToolchain.fingerprint QuintToolchain.q1
                                      PackageIdentity = packageIdentity
                                      Artifacts = artifacts
                                      AuthoringAgent = agent
                                      AuthoringSession = session
                                      RollbackManifestPath = rollback |> Option.map _.ManifestPath
                                      RollbackManifestSha256 = rollback |> Option.map (_.ManifestBytes >> sha256) }

                                let observations =
                                    [ yield!
                                          relative
                                          |> List.map (fun (_, path, bytes) ->
                                              { Path = path
                                                State = QuintAuthorityArtifactState.Present bytes })
                                      match rollback with
                                      | Some value ->
                                          yield
                                              { Path = value.ManifestPath
                                                State = QuintAuthorityArtifactState.Present value.ManifestBytes }
                                      | None -> () ]

                                match TypedAuthority.validateQuintV2 packageIdentity observations manifest with
                                | [] ->
                                    let manifestPath = $"readiness/{workId}/typed-authority.json"
                                    let rollbackWrites = rollback |> Option.map _.Writes |> Option.defaultValue []

                                    Ok
                                        { Manifest = manifest
                                          Writes =
                                            rollbackWrites
                                            @ (relative |> List.map (fun (_, path, bytes) -> path, bytes))
                                            @ [ manifestPath,
                                                Encoding.UTF8.GetBytes(TypedAuthority.serializeQuintV2 manifest) ] }
                                | findings -> Error findings
                finally
                    if Directory.Exists temporary then
                        Directory.Delete(temporary, true)
        | _ ->
            Error
                [ diagnostic
                      "typedSdd.v2.cacheInvalid"
                      "The exact cache objects could not be retained for isolated execution."
                      "Restore the complete readable content-addressed cache." ]

    let authorGeneral
        packageIdentity
        workId
        title
        agent
        session
        cacheRoot
        logicalPath
        (markdownBytes: byte array)
        (selectors: QuintGeneralBindingManifest)
        (rollback: Rollback option)
        =
        let lmtObservation, lmtBytes = observeCache cacheRoot "lmt-binary"
        let quintObservation, quintBytes = observeCache cacheRoot "quint-binary"
        let cache = [ lmtObservation; quintObservation ]

        match QuintSource.createMarkdown logicalPath markdownBytes with
        | Error findings ->
            findings
            |> List.map (fun finding ->
                diagnostic "typedSdd.v2.sourceInvalid" finding.Message "Use canonical LF UTF-8 Markdown.")
            |> Error
        | Ok source ->
            let fences, sourceMaps = parseFences source
            let targets = fences |> List.map _.Target |> List.distinct
            let selectorSources =
                [ yield! selectors.Exports |> List.map _.Source.Path
                  yield! selectors.Actions |> List.map _.Source.Path ]
                |> List.distinct

            if fences.IsEmpty || targets.Length <> 1 then
                Error
                    [ diagnostic
                          "typedSdd.v2.fenceTargets"
                          "A general Quint authority must contain one or more fences for exactly one generated target."
                          "Append all literate Quint fences to one .qnt target." ]
            elif selectors.Profile <> QuintGeneralProfile.identity then
                Error
                    [ diagnostic
                          "typedSdd.v2.profileIdentityMismatch"
                          $"Selector profile '{selectors.Profile}' is unsupported."
                          $"Use {QuintGeneralProfile.identity}." ]
            elif selectorSources <> [ logicalPath ] then
                Error
                    [ diagnostic
                          "typedSdd.v2.selectorSourceMismatch"
                          "Every selector source range must name the selected literate Markdown authority."
                          "Bind all selectors to the exact --source project-relative path." ]
            else
                let target = targets.Head
                let requests: QuintProcessRequest list =
                    [ request "extract" "lmt-binary" [ logicalPath ]
                      request "typecheck" "quint-binary" [ "typecheck"; target; "--out=typed.json" ] ]

                match QuintToolchain.plan QuintToolchain.general cache requests, lmtBytes, quintBytes with
                | Error findings, _, _ ->
                    findings
                    |> List.map (fun finding ->
                        diagnostic
                            "typedSdd.v2.cacheInvalid"
                            finding.Message
                            "Preseed --cache/objects with the exact profile-2 lmt and Quint objects.")
                    |> Error
                | Ok _, Some lmtObject, Some quintObject ->
                    let temporary =
                        Path.Combine(Path.GetTempPath(), "fsgg-quint-general-author-" + Guid.NewGuid().ToString("N"))

                    try
                        match
                            runOnce
                                lmtObject
                                quintObject
                                logicalPath
                                target
                                requests
                                markdownBytes
                                (Path.Combine(temporary, "first")),
                            runOnce
                                lmtObject
                                quintObject
                                logicalPath
                                target
                                requests
                                markdownBytes
                                (Path.Combine(temporary, "second"))
                        with
                        | Error(step, code, detail), _
                        | _, Error(step, code, detail) ->
                            Error
                                [ diagnostic
                                      $"typedSdd.v2.{step}Failed"
                                      $"Exact tool step '{step}' failed ({code}): {detail}"
                                      "Correct the exact cache object or authored Quint input." ]
                        | Ok(firstGenerated, firstTyped, firstWarnings), Ok(secondGenerated, secondTyped, secondWarnings)
                            when firstGenerated <> secondGenerated || firstTyped <> secondTyped ->
                            Error
                                [ diagnostic
                                      "typedSdd.v2.nondeterministicTool"
                                      "Two isolated exact-tool runs produced different bytes."
                                      "Refuse the toolchain and restore the qualified cache." ]
                        | Ok(_, _, firstWarnings), Ok(_, _, secondWarnings) when firstWarnings @ secondWarnings <> [] ->
                            Error
                                [ diagnostic
                                      "typedSdd.v2.toolWarning"
                                      "The exact tool emitted unexpected output or warnings."
                                      "Resolve all extractor and Quint output before authoring." ]
                        | Ok(generated, typed, _), Ok(_, _, _) ->
                            let generatedObservation =
                                [ { Target = target
                                    Sha256 = sha256 generated
                                    Bytes = int64 generated.Length } ]

                            let input: QuintGeneralObservedCompilation =
                                { ModuleName = selectors.ModuleName
                                  Toolchain = QuintToolchain.general
                                  Cache = cache
                                  ProcessRequests = requests
                                  Endpoint = QuintEndpointState.Available
                                  ProcessObservations =
                                    [ { StepId = "extract"
                                        Outcome = QuintProcessOutcome.Succeeded }
                                      { StepId = "typecheck"
                                        Outcome = QuintProcessOutcome.Succeeded } ]
                                  Source = source
                                  FenceManifest =
                                    { Schema = QuintSource.fenceManifestSchema
                                      SourcePath = source.Path
                                      SourceSha256 = source.Sha256
                                      Fences = fences }
                                  Extraction =
                                    { First = generatedObservation
                                      Second = generatedObservation
                                      Warnings = [] }
                                  SourceMap =
                                    { Schema = QuintSource.sourceMapSchema
                                      SourceSha256 = source.Sha256
                                      Entries = sourceMaps }
                                  TypedEffect =
                                    { Profile = selectors.Profile
                                      QuintVersion = QuintGeneralProfile.quintVersion
                                      TypedEffectJson = Encoding.UTF8.GetString typed
                                      ExportBindings = selectors.Exports
                                      ActionBindings = selectors.Actions }
                                  Metadata =
                                    { Specification = title
                                      Relationships = []
                                      VerificationProfiles = []
                                      Bounds = []
                                      Impacts = []
                                      Compatibility = []
                                      Digests =
                                        [ { Name = "sandbox-contract"
                                            Sha256 = sha256 QuintSandbox.contractBytes }
                                          { Name = "typed-effect"
                                            Sha256 = sha256 typed } ] } }

                            match QuintCompiler.compileGeneralObserved input with
                            | Error findings ->
                                findings
                                |> List.map (fun finding ->
                                    diagnostic
                                        "typedSdd.v2.compilationFailed"
                                        finding.Message
                                        "Correct the authored source, selectors, or exact tool observations.")
                                |> Error
                            | Ok output ->
                                let fenceBytes = QuintSource.encodeFenceManifest input.FenceManifest
                                let sourceMapBytes = QuintSource.encodeSourceMap input.SourceMap
                                let relative =
                                    [ "markdown", logicalPath, markdownBytes
                                      "fence-manifest", $"readiness/{workId}/quint/fences.json", fenceBytes
                                      "generated-modules", $"readiness/{workId}/quint/{target}", generated
                                      "source-map", $"readiness/{workId}/quint/source-map.json", sourceMapBytes
                                      "typed-effect", $"readiness/{workId}/quint/typed-effect.json", typed
                                      "profile-bindings",
                                      $"readiness/{workId}/quint/profile-bindings.json",
                                      Encoding.UTF8.GetBytes output.CanonicalBindingManifest
                                      "sandbox-contract",
                                      $"readiness/{workId}/quint/sandbox-contract.json",
                                      QuintSandbox.contractBytes
                                      "compiled-contract",
                                      $"readiness/{workId}/quint/contract.json",
                                      Encoding.UTF8.GetBytes output.CanonicalContract
                                      "bindings",
                                      $"readiness/{workId}/quint/bindings.fs",
                                      Encoding.UTF8.GetBytes output.Bindings.FSharpSource
                                      "compilation-receipt",
                                      $"readiness/{workId}/quint/receipt.json",
                                      Encoding.UTF8.GetBytes output.CanonicalReceipt ]

                                let artifacts =
                                    relative
                                    |> List.map (fun (id, path, bytes) ->
                                        { Id = id
                                          Path = path
                                          Sha256 = sha256 bytes })

                                let manifest =
                                    { SchemaVersion = 2
                                      Lifecycle = "typed-sdd"
                                      Backend = "quint-specification-v1"
                                      ProfileIdentity = QuintGeneralProfile.identity
                                      ToolchainIdentity = QuintToolchain.fingerprint QuintToolchain.general
                                      PackageIdentity = packageIdentity
                                      Artifacts = artifacts
                                      AuthoringAgent = agent
                                      AuthoringSession = session
                                      RollbackManifestPath = rollback |> Option.map _.ManifestPath
                                      RollbackManifestSha256 = rollback |> Option.map (_.ManifestBytes >> sha256) }

                                let observations =
                                    [ yield!
                                          relative
                                          |> List.map (fun (_, path, bytes) ->
                                              { Path = path
                                                State = QuintAuthorityArtifactState.Present bytes })
                                      match rollback with
                                      | Some value ->
                                          yield
                                              { Path = value.ManifestPath
                                                State = QuintAuthorityArtifactState.Present value.ManifestBytes }
                                      | None -> () ]

                                match TypedAuthority.validateQuintV2 packageIdentity observations manifest with
                                | [] ->
                                    let manifestPath = $"readiness/{workId}/typed-authority.json"
                                    let rollbackWrites = rollback |> Option.map _.Writes |> Option.defaultValue []
                                    Ok
                                        { Manifest = manifest
                                          Writes =
                                            rollbackWrites
                                            @ (relative |> List.map (fun (_, path, bytes) -> path, bytes))
                                            @ [ manifestPath,
                                                Encoding.UTF8.GetBytes(TypedAuthority.serializeQuintV2 manifest) ] }
                                | findings -> Error findings
                    finally
                        if Directory.Exists temporary then Directory.Delete(temporary, true)
                | _ ->
                    Error
                        [ diagnostic
                              "typedSdd.v2.cacheInvalid"
                              "The exact cache objects could not be retained for isolated execution."
                              "Restore the complete readable content-addressed cache." ]
