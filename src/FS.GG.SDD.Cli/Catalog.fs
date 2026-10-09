namespace FS.GG.SDD.Cli

open System
open System.IO
open System.Text
open System.Text.Json
open Spectre.Console
open Fsgg.ProviderCatalog
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CatalogScaffoldWorkflow
open FS.GG.SDD.Commands.CommandTypes

module Catalog =
    let diagnostic code path message : Diagnostic =
        {
            Code = code
            Path = path
            Message = message
        }

    let encode write =
        use stream = new MemoryStream()
        use w = new Utf8JsonWriter(stream)
        write w
        w.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    let project (result: Result<CatalogPreview, Diagnostic list>) =
        encode (fun w ->
            w.WriteStartObject()
            w.WriteString("schema", "fsgg.catalog-preview/v1")

            match result with
            | Error diagnostics ->
                w.WriteString("status", "refused")
                w.WriteStartArray "diagnostics"

                for d in diagnostics do
                    w.WriteStartObject()
                    w.WriteString("code", d.Code)
                    w.WriteString("path", d.Path)
                    w.WriteString("message", d.Message)
                    w.WriteEndObject()

                w.WriteEndArray()
            | Ok(p: CatalogPreview) ->
                w.WriteString("status", p.Status)
                w.WriteString("catalogId", p.Catalog.Id)
                w.WriteString("catalogRevision", p.Catalog.Revision)
                w.WriteString("catalogDigest", p.Catalog.Digest)
                w.WriteStartArray "providers"

                for d in
                    p.Catalog.Providers
                    |> List.sortWith (fun a b -> StringComparer.Ordinal.Compare(a.Id, b.Id)) do
                    use descriptor = JsonDocument.Parse(ProviderCatalogIntegrity.descriptorJson d)
                    descriptor.RootElement.WriteTo w

                w.WriteEndArray()

                match p.Selected with
                | None -> w.WriteNull "selected"
                | Some s ->
                    w.WriteStartObject "selected"
                    w.WriteString("providerId", s.Descriptor.Id)
                    w.WriteString("rawProductName", s.RawProductName)
                    w.WriteString("packageIdentity", s.PackageIdentity)
                    w.WriteString("codeIdentifier", s.CodeIdentifier)
                    w.WriteStartArray "effectiveParameters"

                    for key, value in s.EffectiveParameters do
                        w.WriteStartObject()
                        w.WriteString("key", key)
                        w.WriteString("value", value)
                        w.WriteEndObject()

                    w.WriteEndArray()
                    w.WriteEndObject()

                w.WriteNull "observations"

            w.WriteEndObject())

    let print args result =
        let json = project result
        let format = Rendering.selectFormat args
        let text = "Catalog inspection\n" + json

        match format with
        | Json -> Console.Out.WriteLine json
        | Text -> Console.Out.WriteLine text
        | Rich ->
            let capabilities =
                Rendering.detectCapabilities (Rendering.forceColorRequested args) Console.IsOutputRedirected

            if capabilities.ColorEnabled && capabilities.IsInteractive then
                let console, writer = Rendering.createCappedConsole capabilities
                use writer = writer
                console.Write(Panel(Markup.Escape(json)).Header("Catalog inspection"))
                Console.Out.Write(writer.ToString())
            else
                Console.Out.WriteLine text

        match result with
        | Ok _ -> 0
        | Error _ -> 1

    let parseOptions (args: string list) =
        let flags = [ "--json"; "--text"; "--rich"; "--force-color" ]
        let valued = [ "--catalog"; "--catalog-sha256"; "--provider"; "--param" ]

        let rec parse options rest =
            match rest with
            | [] -> Ok(List.rev options)
            | name :: tail when List.contains name flags -> parse options tail
            | name :: value :: tail when
                List.contains name valued
                && not (value.StartsWith("--", StringComparison.Ordinal))
                ->
                if name <> "--param" && List.exists (fun (k, _) -> k = name) options then
                    Error [ diagnostic "catalog.duplicateOption" name "Option must occur exactly once." ]
                else
                    parse ((name, value) :: options) tail
            | name :: _ when List.contains name valued ->
                Error [ diagnostic "catalog.missingOptionValue" name "Option requires a value." ]
            | name :: _ ->
                Error
                    [
                        diagnostic "catalog.unknownOption" name "Unsupported catalog inspection option."
                    ]

        parse [] args

    let unavailable args =
        print
            args
            (Error
                [
                    diagnostic
                        "catalog.scaffoldUnavailable"
                        "$.catalog"
                        "Catalog scaffold invocation is not available; use catalog inspect for read-only preparation."
                ])

    type ScaffoldOptions =
        { CatalogPath: string
          CatalogDigest: string
          Provider: string
          TargetRoot: string
          TemplateArchive: string
          TemplateDigest: string
          PolicyPath: string
          PolicyDigest: string
          Platform: string
          TransportExecutable: string
          PreflightSeconds: int
          ScaffoldSeconds: int
          Overrides: (string * string) list
          DryRun: bool }

    let parseScaffoldOptions (args: string list) : Result<ScaffoldOptions, Diagnostic list> =
        let flags = ["--json"; "--text"; "--rich"; "--force-color"; "--dry-run"]
        let required =
            ["--catalog"; "--catalog-sha256"; "--provider"; "--root"; "--template-archive"
             "--template-sha256"; "--admission-policy"; "--admission-policy-sha256"; "--platform"
             "--transport-executable"; "--preflight-timeout-seconds"; "--scaffold-timeout-seconds"]
        let rec parse seen options parameters rest =
            match rest with
            | [] -> Ok(options, List.rev parameters, seen)
            | name :: tail when List.contains name flags ->
                if List.contains name seen then
                    Error [diagnostic "catalog.duplicateOption" name "Option must occur exactly once."]
                else parse (name::seen) options parameters tail
            | "--param" :: value :: tail when not(value.StartsWith("--", StringComparison.Ordinal)) ->
                let split = value.IndexOf '='
                if split <= 0 then
                    Error [diagnostic "catalog.parameterMalformed" "$.parameters" "Expected key=value; empty values are preserved."]
                else
                    let key = value.Substring(0,split)
                    if parameters |> List.exists(fun (existing,_) -> existing = key) then
                        Error [diagnostic "catalog.duplicateParameter" "$.parameters" "Parameter keys must be distinct."]
                    else parse seen options ((key,value.Substring(split+1))::parameters) tail
            | name :: value :: tail when List.contains name required && not(value.StartsWith("--", StringComparison.Ordinal)) ->
                if List.contains name seen then
                    Error [diagnostic "catalog.duplicateOption" name "Option must occur exactly once."]
                elif String.IsNullOrWhiteSpace value then
                    Error [diagnostic "catalog.missingOptionValue" name "Option requires a nonempty value."]
                else parse (name::seen) ((name,value)::options) parameters tail
            | name :: _ when name = "--param" || List.contains name required ->
                Error [diagnostic "catalog.missingOptionValue" name "Option requires a value."]
            | name :: _ -> Error [diagnostic "catalog.unknownOption" name "Unsupported catalog scaffold option."]
        match parse [] [] [] args with
        | Error errors -> Error errors
        | Ok(options,parameters,seen) ->
            let missing = required |> List.filter(fun name -> not(List.contains name seen))
            if not missing.IsEmpty then
                Error(missing |> List.map(fun name -> diagnostic "catalog.selectionMissing" name "Scaffold requires this explicit selection."))
            else
                let value name = options |> List.find(fun (key,_) -> key = name) |> snd
                let positive name =
                    match Int32.TryParse(value name, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture) with
                    | true, number when number > 0 -> Ok number
                    | _ -> Error [diagnostic "catalog.invalidBudget" name "Budget must be a positive integral number of seconds."]
                let invalidPaths =
                    ["--root"; "--template-archive"; "--transport-executable"]
                    |> List.filter(fun name ->
                        let selected = value name
                        not(Path.IsPathFullyQualified selected) || selected.Contains(char 0) || selected.Contains('\\'))
                if not invalidPaths.IsEmpty then
                    Error(invalidPaths |> List.map(fun name -> diagnostic "catalog.invalidPath" name "An explicit fully qualified Linux path is required."))
                else
                    match positive "--preflight-timeout-seconds", positive "--scaffold-timeout-seconds" with
                    | Ok preflight, Ok scaffold ->
                        Ok {CatalogPath=value "--catalog"; CatalogDigest=value "--catalog-sha256"
                            Provider=value "--provider"; TargetRoot=value "--root"
                            TemplateArchive=value "--template-archive"; TemplateDigest=value "--template-sha256"
                            PolicyPath=value "--admission-policy"; PolicyDigest=value "--admission-policy-sha256"
                            Platform=value "--platform"; TransportExecutable=value "--transport-executable"
                            PreflightSeconds=preflight; ScaffoldSeconds=scaffold; Overrides=parameters
                            DryRun=List.contains "--dry-run" seen}
                    | Error first, Error second -> Error(first @ second)
                    | Error errors, _ | _, Error errors -> Error errors
    let private readSelected (path: string) =
        // Only explicitly selected metadata is read here; native workspace acquisition
        // belongs to the preheld production Operation, not the CLI parser.
        use stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
        let maximum = 1024 * 1024
        use retained = new MemoryStream()
        let buffer = Array.zeroCreate<byte> 8192
        let mutable reading = true
        while reading do
            let count = stream.Read(buffer,0,min buffer.Length (maximum + 1 - int retained.Length))
            if count = 0 then reading <- false
            elif retained.Length + int64 count > int64 maximum then
                raise (IOException("Selected metadata exceeds the 1MiB capture limit."))
            else retained.Write(buffer,0,count)
        retained.ToArray()

    let private printScaffold (args: string list) (status: string) (ownership: string) (diagnostics: Diagnostic list) (model: Model option) =
        let json = encode(fun w ->
            w.WriteStartObject()
            w.WriteString("schema", "fsgg.catalog-scaffold/v1")
            w.WriteString("status", status)
            w.WriteString("ownership", ownership)
            w.WriteStartArray "diagnostics"
            for (d: Diagnostic) in diagnostics do
                w.WriteStartObject()
                w.WriteString("code", d.Code)
                w.WriteString("path", d.Path)
                w.WriteString("message", d.Message)
                w.WriteEndObject()
            w.WriteEndArray()
            match model with
            | None -> w.WriteNull "observations"
            | Some actual ->
                match preflightObservation actual, templateObservation actual with
                | None, None -> w.WriteNull "observations"
                | preflight, template ->
                    w.WriteStartObject "observations"
                    w.WriteBoolean("preflightObserved", preflight.IsSome)
                    w.WriteBoolean("templateObserved", template.IsSome)
                    w.WriteEndObject()
            w.WriteEndObject())
        match Rendering.selectFormat args with
        | Json -> Console.Out.WriteLine json
        | Text -> Console.Out.WriteLine("Catalog scaffold\n" + json)
        | Rich ->
            let capabilities = Rendering.detectCapabilities (Rendering.forceColorRequested args) Console.IsOutputRedirected
            if capabilities.ColorEnabled && capabilities.IsInteractive then
                let console, writer = Rendering.createCappedConsole capabilities
                use writer = writer
                console.Write(Panel(Markup.Escape(json)).Header("Catalog scaffold"))
                Console.Out.Write(writer.ToString())
            else Console.Out.WriteLine("Catalog scaffold\n" + json)

    let private retainOwner operation =
        let pause () = try System.Threading.Thread.Sleep 1000 with _ -> ()
        let mutable released = false
        while not released do
            try
                FS.GG.SDD.Commands.CatalogScaffoldEffects.waitForSettled operation
                |> fun pending -> Async.RunSynchronously(pending, cancellationToken=System.Threading.CancellationToken.None)
                |> ignore
                match FS.GG.SDD.Commands.CatalogScaffoldEffects.release operation with
                | Ok () -> released <- true
                | Error _ -> pause ()
            with _ -> pause ()
            GC.KeepAlive operation

    let scaffold args =
        match parseScaffoldOptions args with
        | Error errors -> printScaffold args "refused" "not-started" errors None; 1
        | Ok selected ->
            let request: Result<CatalogScaffoldRequest, Diagnostic list> =
                try
                    Ok {Selection={CatalogBytes=readSelected selected.CatalogPath; ExpectedRawDigest=selected.CatalogDigest
                                   Provider=Some selected.Provider; Overrides=selected.Overrides}
                        TargetRoot=selected.TargetRoot; TemplateArchive=selected.TemplateArchive
                        ExpectedArchiveDigest=selected.TemplateDigest; PolicyBytes=readSelected selected.PolicyPath
                        ExpectedPolicyDigest=selected.PolicyDigest; SelectedPlatform=selected.Platform
                        PreflightTimeoutSeconds=selected.PreflightSeconds; ScaffoldTimeoutSeconds=selected.ScaffoldSeconds
                        DryRun=selected.DryRun}
                with
                | :? IOException | :? UnauthorizedAccessException | :? ArgumentException ->
                    Error [diagnostic "catalog.unreadable" "$.selection" "Selected catalog or policy metadata is unreadable or exceeds its capture limit."]
            match request with
            | Error errors -> printScaffold args "refused" "not-started" errors None; 1
            | Ok request ->
                let host: FS.GG.SDD.Commands.CatalogScaffoldEffects.HostSelection =
                    {Mode=FS.GG.SDD.Commands.CatalogScaffoldEffects.LocalLinux; TransportExecutable=selected.TransportExecutable}
                use cancellation = new System.Threading.CancellationTokenSource()
                let cancel = ConsoleCancelEventHandler(fun _ event ->
                    event.Cancel <- true
                    try cancellation.Cancel()
                    with :? ObjectDisposedException -> ())
                Console.CancelKeyPress.AddHandler cancel
                try
                    match FS.GG.SDD.Commands.CatalogScaffoldEffects.prepare host request cancellation.Token with
                    | Error errors -> printScaffold args "refused" "not-started" errors None; 1
                    | Ok operation ->
                        let mutable safelyReleased = false
                        try
                            // No catch may let the runtime exit while this same Operation is
                            // unresolved. Projection failure changes reporting, never custody.
                            let actual =
                                try Ok(Async.RunSynchronously(FS.GG.SDD.Commands.CatalogScaffoldEffects.run operation,
                                                              cancellationToken=System.Threading.CancellationToken.None))
                                with _ -> Error [diagnostic "catalog.runtimeFailure" "$" "The original scaffold runner failed; its owner remains retained until release is observed."]
                            let status, errors, model, code =
                                match actual with
                                | Error errors -> "failed",errors,None,2
                                | Ok model ->
                                    let status, code =
                                        match outcome model with
                                        | FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Prepared -> "prepared",0
                                        | FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Succeeded -> "succeeded",0
                                        | FS.GG.SDD.Commands.CatalogScaffoldWorkflow.CleanupUnknown -> "cleanup-unknown",1
                                        | FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Failed -> "failed",1
                                        | FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Pending -> "pending",1
                                    status,diagnostics model,Some model,code
                            let releaseResult =
                                try FS.GG.SDD.Commands.CatalogScaffoldEffects.release operation
                                with _ -> Error [diagnostic "catalog.releaseUnknown" "$" "Original operation release has no accepted return; the same owner is retained."]
                            let released, retainedDiagnostics =
                                match releaseResult with
                                | Ok () -> true,errors
                                | Error releaseErrors -> false,errors @ releaseErrors
                            let observed =
                                try Some(FS.GG.SDD.Commands.CatalogScaffoldEffects.observe operation)
                                with _ -> None
                            let released =
                                released && (observed |> Option.exists(fun value ->
                                    value.Ownership = FS.GG.SDD.Commands.CatalogScaffoldEffects.Settled
                                    || value.Ownership = FS.GG.SDD.Commands.CatalogScaffoldEffects.NotStarted))
                            let ownership = observed |> Option.map(fun value -> string value.Ownership) |> Option.defaultValue "unknown"
                            let mutable reportingFailed = false
                            try printScaffold args status ownership retainedDiagnostics model
                            with _ -> reportingFailed <- true
                            if not released then retainOwner operation
                            safelyReleased <- true
                            GC.KeepAlive operation
                            if reportingFailed then 2 else code
                        finally
                            if not safelyReleased then retainOwner operation
                            GC.KeepAlive operation

                finally
                    // The operation's own cancellation path settles custody before this
                    // handler and token retire; framework cancellation must not abort cleanup.
                    Console.CancelKeyPress.RemoveHandler cancel

    let run args =
        match args with
        | "inspect" :: rest ->
            let result =
                match parseOptions rest with
                | Error d -> Error d
                | Ok options ->
                    let option name =
                        options
                        |> List.tryPick (fun (key, value) -> if key = name then Some value else None)

                    match option "--catalog", option "--catalog-sha256" with
                    | Some path, Some expected ->
                        let inputs =
                            options |> List.filter (fun (key, _) -> key = "--param") |> List.map snd

                        let parsed =
                            inputs
                            |> List.map (fun value ->
                                let split = value.IndexOf '='

                                if split <= 0 then
                                    Error
                                        [
                                            diagnostic
                                                "catalog.parameterMalformed"
                                                "$.parameters"
                                                "Expected key=value; empty values are preserved."
                                        ]
                                else
                                    Ok(value.Substring(0, split), value.Substring(split + 1)))

                        match
                            parsed
                            |> List.tryPick (function
                                | Error e -> Some e
                                | _ -> None)
                        with
                        | Some errors -> Error errors
                        | None ->
                            try
                                let selection =
                                    {
                                        CatalogBytes = File.ReadAllBytes path
                                        ExpectedRawDigest = expected
                                        Provider = option "--provider"
                                        Overrides =
                                            parsed
                                            |> List.choose (function
                                                | Ok p -> Some p
                                                | _ -> None)
                                    }

                                prepare selection
                            with
                            | :? IOException ->
                                Error
                                    [
                                        diagnostic
                                            "catalog.unreadable"
                                            "$.catalog"
                                            "Selected catalog file is unreadable."
                                    ]
                            | :? UnauthorizedAccessException ->
                                Error
                                    [
                                        diagnostic
                                            "catalog.unreadable"
                                            "$.catalog"
                                            "Selected catalog file is inaccessible."
                                    ]
                    | _ ->
                        Error
                            [
                                diagnostic
                                    "catalog.selectionMissing"
                                    "$.catalog"
                                    "Inspection requires explicit --catalog and --catalog-sha256."
                            ]

            print rest result
        | _ ->
            print
                args
                (Error
                    [
                        diagnostic
                            "catalog.commandMissing"
                            "$"
                            "Use catalog inspect --catalog <file> --catalog-sha256 <sha256:digest>."
                    ])
