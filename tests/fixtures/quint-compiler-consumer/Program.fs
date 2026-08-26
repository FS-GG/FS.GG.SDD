open System
open System.IO
open System.Security.Cryptography
open System.Text
open FS.GG.SDD.Artifacts.TypedSpecifications

let fail message =
    raise (InvalidOperationException message)

let expectOk label =
    function
    | Ok value -> value
    | Error findings -> fail $"{label}: {findings}"

let sha256Bytes (bytes: byte array) =
    SHA256.HashData bytes |> Convert.ToHexString |> _.ToLowerInvariant()

let sha256Text (text: string) =
    text |> Encoding.UTF8.GetBytes |> sha256Bytes

let position line column : QuintSourcePosition = { Line = line; Column = column }

let range path startLine startColumn endLine endColumn : QuintSourceRange =
    { Path = path
      Start = position startLine startColumn
      End = position endLine endColumn }

let parseFences (source: QuintMarkdownSource) =
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
                |> Option.defaultWith (fun () -> fail $"unterminated Quint fence in {source.Path}")

            let contentLines = lines[cursor + 1 .. closing - 1]
            let content = String.Join("\n", contentLines) + "\n"

            let moduleName =
                contentLines
                |> Array.tryPick (fun line ->
                    let trimmed = line.TrimStart()

                    if trimmed.StartsWith("module ", StringComparison.Ordinal) then
                        let suffix = trimmed.Substring(7)
                        Some(suffix.Split([| ' '; '{' |], StringSplitOptions.RemoveEmptyEntries)[0])
                    else
                        None)
                |> Option.defaultWith (fun () -> fail $"fence {ordinal} has no module declaration")

            let sourceRange = range source.Path (cursor + 1) 1 (closing + 1) 3

            let firstGeneratedLine =
                (Map.tryFind target generatedLines |> Option.defaultValue 1)

            let lastGeneratedLine = firstGeneratedLine + contentLines.Length - 1
            let lastColumn = max 1 contentLines[contentLines.Length - 1].Length

            fences.Add
                { Ordinal = ordinal
                  Target = target
                  ModuleName = moduleName
                  SourceRange = sourceRange
                  ContentSha256 = sha256Text content }

            maps.Add
                { Target = target
                  GeneratedRange = range target firstGeneratedLine 1 lastGeneratedLine lastColumn
                  Source =
                    { FenceOrdinal = ordinal
                      Range =
                        range source.Path (cursor + 2) 1 closing (max 1 contentLines[contentLines.Length - 1].Length) } }

            generatedLines <- Map.add target (lastGeneratedLine + 1) generatedLines
            ordinal <- ordinal + 1
            cursor <- closing + 1
        else
            cursor <- cursor + 1

    List.ofSeq fences, List.ofSeq maps

let binding path moduleName catalogue id kind line =
    { ModuleName = moduleName
      CatalogueName = catalogue
      Id = id
      Kind = kind
      Source = range path line 1 line 200 }

let bindings (logicalPath: string) =
    match Path.GetFileName logicalPath with
    | "requirements-and-evidence.md" ->
        [ binding logicalPath "RequirementsSlice" "requirements" "REQ-AUDIT-001" Requirement 19
          binding logicalPath "RequirementsSlice" "evidenceCatalogue" "EV-VERIFY-001" Evidence 23
          binding logicalPath "RequirementsSlice" "actionCatalogue" "ObserveEvidence" Action 26
          binding logicalPath "RequirementsSlice" "actionCatalogue" "AcceptRequirement" Action 27
          binding logicalPath "RequirementsSlice" "propertyCatalogue" "AcceptedOnlyWithEvidence" Invariant 30
          binding logicalPath "RequirementsSlice" "propertyCatalogue" "RequirementCanBeAccepted" ReachabilityProperty 31 ],
        "RequirementsBindings",
        "Q1Requirements"
    | "sir-damage-rule.md" ->
        [ binding logicalPath "SirDamageSlice" "actions" "Initialize" Action 16
          binding logicalPath "SirDamageSlice" "actions" "ApplyDamage" Action 17
          binding logicalPath "SirDamageSlice" "propertyCatalogue" "NonNegativeHitPoints" Invariant 20
          binding logicalPath "SirDamageSlice" "propertyCatalogue" "KnownLastAction" Invariant 21
          binding logicalPath "SirDamageSlice" "propertyCatalogue" "DamageCanReachZero" ReachabilityProperty 22 ],
        "SirBindings",
        "Q1SirDamage"
    | "coordination-process.md" ->
        let actionBindings =
            [ "Prepare", 19
              "Interfere", 20
              "Apply", 21
              "RefuseStale", 22
              "LoseResponse", 23
              "Retry", 24
              "Refresh", 25
              "Complete", 26 ]
            |> List.map (fun (id, line) -> binding logicalPath "CoordinationSlice" "actionCatalogue" id Action line)

        let propertyBindings =
            [ "AtMostOneApply", Invariant, 29
              "ReceiptMatchesApply", Invariant, 30
              "CompleteHasReceipt", Invariant, 31
              "StaleNeverApplies", Invariant, 32
              "StaleRefusalNeverApplies", Invariant, 33
              "KnownPhase", Invariant, 34
              "EventualCompletion", TemporalProperty, 35 ]
            |> List.map (fun (id, kind, line) ->
                binding logicalPath "CoordinationSlice" "propertyCatalogue" id kind line)

        actionBindings @ propertyBindings, "CoordinationBindings", "Q1Coordination"
    | name -> fail $"unsupported Q1 slice: {name}"

let requirement id =
    QuintToolchain.q1.Components
    |> List.collect _.Objects
    |> List.find (fun item -> item.Id = id)

let cacheObservation id =
    let item = requirement id

    { Id = item.Id
      Kind = item.Kind
      State = Present(item.Sha256, item.Bytes, true) }

let request step objectId arguments =
    { StepId = step
      ExecutableObjectId = objectId
      Arguments = arguments
      Environment = []
      WorkingDirectory = "isolated-run" }

match Environment.GetCommandLineArgs() |> Array.skip 1 with
| [| logicalPath; markdownPath; generatedPath; typedJsonPath; outputDirectory |] ->
    let source =
        File.ReadAllBytes markdownPath
        |> QuintSource.createMarkdown logicalPath
        |> expectOk "source"

    let fences, sourceMaps = parseFences source
    let generatedBytes = File.ReadAllBytes generatedPath

    let generated =
        [ { Target = fences.Head.Target
            Sha256 = sha256Bytes generatedBytes
            Bytes = int64 generatedBytes.Length } ]

    let sourceBindings, moduleName, specification = bindings logicalPath

    let input =
        { ModuleName = moduleName
          Toolchain = QuintToolchain.q1
          Cache = [ cacheObservation "lmt-binary"; cacheObservation "quint-binary" ]
          ProcessRequests =
            [ request "extract" "lmt-binary" [ logicalPath ]
              request "typecheck" "quint-binary" [ "typecheck"; fences.Head.Target; "--out=typed.json" ] ]
          Endpoint = Available
          ProcessObservations =
            [ { StepId = "extract"
                Outcome = Succeeded }
              { StepId = "typecheck"
                Outcome = Succeeded } ]
          Source = source
          FenceManifest =
            { Schema = QuintSource.fenceManifestSchema
              SourcePath = source.Path
              SourceSha256 = source.Sha256
              Fences = fences }
          Extraction =
            { First = generated
              Second = generated
              Warnings = [] }
          SourceMap =
            { Schema = QuintSource.sourceMapSchema
              SourceSha256 = source.Sha256
              Entries = sourceMaps }
          TypedEffect =
            { Profile = QuintProfile.identity
              QuintVersion = QuintProfile.quintVersion
              TypedEffectJson = File.ReadAllText typedJsonPath
              SourceBindings = sourceBindings }
          Metadata =
            { Specification = specification
              Relationships = []
              VerificationProfiles = []
              Bounds = []
              Impacts = []
              Compatibility = []
              Digests = [] } }

    let output = QuintCompiler.compileObserved input |> expectOk "compileObserved"
    Directory.CreateDirectory outputDirectory |> ignore
    File.WriteAllText(Path.Combine(outputDirectory, "contract.json"), output.CanonicalContract)
    File.WriteAllText(Path.Combine(outputDirectory, "receipt.json"), output.CanonicalReceipt)
    File.WriteAllText(Path.Combine(outputDirectory, "bindings.fs"), output.Bindings.FSharpSource)
    File.WriteAllText(Path.Combine(outputDirectory, "bindings.fable.fs"), output.Bindings.FableSource)

    File.WriteAllText(
        Path.Combine(outputDirectory, "native.txt"),
        String.concat
            "\n"
            [ output.Bindings.ContractFingerprint
              output.Contract.Catalogue
              |> List.sortBy _.Id
              |> List.map _.Id
              |> String.concat ","
              output.Bindings.CanonicalJson ]
        + "\n"
    )

    printfn "%s %s" specification output.CompilationFingerprint
| _ -> fail "expected: logical-path markdown generated-qnt typed-json output-directory"
