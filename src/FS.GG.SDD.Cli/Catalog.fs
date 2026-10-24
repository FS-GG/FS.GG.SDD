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
        {
            CatalogPath: string
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
            DryRun: bool
        }

    let parseScaffoldOptions (args: string list) : Result<ScaffoldOptions, Diagnostic list> =
        let flags = [ "--json"; "--text"; "--rich"; "--force-color"; "--dry-run" ]

        let required =
            [
                "--catalog"
                "--catalog-sha256"
                "--provider"
                "--root"
                "--template-archive"
                "--template-sha256"
                "--admission-policy"
                "--admission-policy-sha256"
                "--platform"
                "--transport-executable"
                "--preflight-timeout-seconds"
                "--scaffold-timeout-seconds"
            ]

        let rec parse seen options parameters rest =
            match rest with
            | [] -> Ok(options, List.rev parameters, seen)
            | name :: tail when List.contains name flags ->
                if List.contains name seen then
                    Error [ diagnostic "catalog.duplicateOption" name "Option must occur exactly once." ]
                else
                    parse (name :: seen) options parameters tail
            | "--param" :: value :: tail when not (value.StartsWith("--", StringComparison.Ordinal)) ->
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
                    let key = value.Substring(0, split)

                    if parameters |> List.exists (fun (existing, _) -> existing = key) then
                        Error
                            [
                                diagnostic
                                    "catalog.duplicateParameter"
                                    "$.parameters"
                                    "Parameter keys must be distinct."
                            ]
                    else
                        parse seen options ((key, value.Substring(split + 1)) :: parameters) tail
            | name :: value :: tail when
                List.contains name required
                && not (value.StartsWith("--", StringComparison.Ordinal))
                ->
                if List.contains name seen then
                    Error [ diagnostic "catalog.duplicateOption" name "Option must occur exactly once." ]
                elif String.IsNullOrWhiteSpace value then
                    Error
                        [
                            diagnostic "catalog.missingOptionValue" name "Option requires a nonempty value."
                        ]
                else
                    parse (name :: seen) ((name, value) :: options) parameters tail
            | name :: _ when name = "--param" || List.contains name required ->
                Error [ diagnostic "catalog.missingOptionValue" name "Option requires a value." ]
            | name :: _ ->
                Error
                    [
                        diagnostic "catalog.unknownOption" name "Unsupported catalog scaffold option."
                    ]

        match parse [] [] [] args with
        | Error errors -> Error errors
        | Ok(options, parameters, seen) ->
            let missing = required |> List.filter (fun name -> not (List.contains name seen))

            if not missing.IsEmpty then
                Error(
                    missing
                    |> List.map (fun name ->
                        diagnostic "catalog.selectionMissing" name "Scaffold requires this explicit selection.")
                )
            else
                let value name =
                    options |> List.find (fun (key, _) -> key = name) |> snd

                let positive name =
                    match
                        Int32.TryParse(
                            value name,
                            Globalization.NumberStyles.None,
                            Globalization.CultureInfo.InvariantCulture
                        )
                    with
                    | true, number when number > 0 -> Ok number
                    | _ ->
                        Error
                            [
                                diagnostic
                                    "catalog.invalidBudget"
                                    name
                                    "Budget must be a positive integral number of seconds."
                            ]

                let invalidPaths =
                    [ "--root"; "--template-archive"; "--transport-executable" ]
                    |> List.filter (fun name ->
                        let selected = value name

                        not (Path.IsPathFullyQualified selected)
                        || selected.Contains(char 0)
                        || selected.Contains('\\'))

                if not invalidPaths.IsEmpty then
                    Error(
                        invalidPaths
                        |> List.map (fun name ->
                            diagnostic "catalog.invalidPath" name "An explicit fully qualified Linux path is required.")
                    )
                else
                    match positive "--preflight-timeout-seconds", positive "--scaffold-timeout-seconds" with
                    | Ok preflight, Ok scaffold ->
                        Ok
                            {
                                CatalogPath = value "--catalog"
                                CatalogDigest = value "--catalog-sha256"
                                Provider = value "--provider"
                                TargetRoot = value "--root"
                                TemplateArchive = value "--template-archive"
                                TemplateDigest = value "--template-sha256"
                                PolicyPath = value "--admission-policy"
                                PolicyDigest = value "--admission-policy-sha256"
                                Platform = value "--platform"
                                TransportExecutable = value "--transport-executable"
                                PreflightSeconds = preflight
                                ScaffoldSeconds = scaffold
                                Overrides = parameters
                                DryRun = List.contains "--dry-run" seen
                            }
                    | Error first, Error second -> Error(first @ second)
                    | Error errors, _
                    | _, Error errors -> Error errors

    let private readSelectedBound maximum (path: string) =
        // Only explicitly selected metadata is read here; native workspace acquisition
        // belongs to the preheld production Operation, not the CLI parser.
        use stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
        use retained = new MemoryStream()
        let buffer = Array.zeroCreate<byte> 8192
        let mutable reading = true

        while reading do
            let count =
                stream.Read(buffer, 0, min buffer.Length (maximum + 1 - int retained.Length))

            if count = 0 then
                reading <- false
            elif retained.Length + int64 count > int64 maximum then
                raise (IOException("Selected metadata exceeds its capture limit."))
            else
                retained.Write(buffer, 0, count)

        retained.ToArray()

    let private readSelected path = readSelectedBound (1024 * 1024) path

    let private printScaffold
        (args: string list)
        (status: string)
        (ownership: string)
        (diagnostics: Diagnostic list)
        (model: Model option)
        =
        let json =
            encode (fun w ->
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
            let capabilities =
                Rendering.detectCapabilities (Rendering.forceColorRequested args) Console.IsOutputRedirected

            if capabilities.ColorEnabled && capabilities.IsInteractive then
                let console, writer = Rendering.createCappedConsole capabilities
                use writer = writer
                console.Write(Panel(Markup.Escape(json)).Header("Catalog scaffold"))
                Console.Out.Write(writer.ToString())
            else
                Console.Out.WriteLine("Catalog scaffold\n" + json)

    let private retainOwner operation =
        let pause () =
            try
                System.Threading.Thread.Sleep 1000
            with _ ->
                ()

        let mutable released = false

        while not released do
            try
                FS.GG.SDD.Commands.CatalogScaffoldEffects.waitForSettled operation
                |> fun pending ->
                    Async.RunSynchronously(pending, cancellationToken = System.Threading.CancellationToken.None)
                |> ignore

                match FS.GG.SDD.Commands.CatalogScaffoldEffects.release operation with
                | Ok() -> released <- true
                | Error _ -> pause ()
            with _ ->
                pause ()

            GC.KeepAlive operation

    let private executeScaffold args host request source =
        use cancellation = new System.Threading.CancellationTokenSource()

        let cancel =
            ConsoleCancelEventHandler(fun _ event ->
                event.Cancel <- true

                try
                    cancellation.Cancel()
                with :? ObjectDisposedException ->
                    ())

        Console.CancelKeyPress.AddHandler cancel

        try
            let prepared =
                match source with
                | None -> FS.GG.SDD.Commands.CatalogScaffoldEffects.prepare host request cancellation.Token
                | Some selectedSource -> FS.GG.SDD.Commands.CatalogScaffoldEffects.prepareFromSource host selectedSource request cancellation.Token

            match prepared with
            | Error errors ->
                printScaffold args "refused" "not-started" errors None
                1
            | Ok operation ->
                let mutable safelyReleased = false

                try
                    // No catch may let the runtime exit while this same Operation is
                    // unresolved. Projection failure changes reporting, never custody.
                    let actual =
                        try
                            Ok(
                                Async.RunSynchronously(
                                    FS.GG.SDD.Commands.CatalogScaffoldEffects.run operation,
                                    cancellationToken = System.Threading.CancellationToken.None
                                )
                            )
                        with _ ->
                            Error
                                [
                                    diagnostic
                                        "catalog.runtimeFailure"
                                        "$"
                                        "The original scaffold runner failed; its owner remains retained until release is observed."
                                ]

                    let status, errors, model, code =
                        match actual with
                        | Error errors -> "failed", errors, None, 2
                        | Ok model ->
                            let status, code =
                                match outcome model with
                                | FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Prepared -> "prepared", 0
                                | FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Succeeded -> "succeeded", 0
                                | FS.GG.SDD.Commands.CatalogScaffoldWorkflow.CleanupUnknown ->
                                    "cleanup-unknown", 1
                                | FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Failed -> "failed", 1
                                | FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Pending -> "pending", 1

                            status, diagnostics model, Some model, code

                    let releaseResult =
                        try
                            FS.GG.SDD.Commands.CatalogScaffoldEffects.release operation
                        with _ ->
                            Error
                                [
                                    diagnostic
                                        "catalog.releaseUnknown"
                                        "$"
                                        "Original operation release has no accepted return; the same owner is retained."
                                ]

                    let released, retainedDiagnostics =
                        match releaseResult with
                        | Ok() -> true, errors
                        | Error releaseErrors -> false, errors @ releaseErrors

                    let observed =
                        try
                            Some(FS.GG.SDD.Commands.CatalogScaffoldEffects.observe operation)
                        with _ ->
                            None

                    let released =
                        released
                        && (observed
                            |> Option.exists (fun value ->
                                value.Ownership = FS.GG.SDD.Commands.CatalogScaffoldEffects.Settled
                                || value.Ownership = FS.GG.SDD.Commands.CatalogScaffoldEffects.NotStarted))

                    let ownership =
                        observed
                        |> Option.map (fun value -> string value.Ownership)
                        |> Option.defaultValue "unknown"

                    let mutable reportingFailed = false

                    try
                        printScaffold args status ownership retainedDiagnostics model
                    with _ ->
                        reportingFailed <- true

                    if not released then
                        retainOwner operation

                    safelyReleased <- true
                    GC.KeepAlive operation
                    if reportingFailed then 2 else code
                finally
                    if not safelyReleased then
                        retainOwner operation

                    GC.KeepAlive operation

        finally
            // The operation's own cancellation path settles custody before this
            // handler and token retire; framework cancellation must not abort cleanup.
            Console.CancelKeyPress.RemoveHandler cancel

    let private scaffoldDirect args =
        match parseScaffoldOptions args with
        | Error errors ->
            printScaffold args "refused" "not-started" errors None
            1
        | Ok selected ->
            let request: Result<CatalogScaffoldRequest, Diagnostic list> =
                try
                    Ok
                        {
                            Selection =
                                {
                                    CatalogBytes = readSelected selected.CatalogPath
                                    ExpectedRawDigest = selected.CatalogDigest
                                    Provider = Some selected.Provider
                                    Overrides = selected.Overrides
                                }
                            TargetRoot = selected.TargetRoot
                            TemplateArchive = selected.TemplateArchive
                            ExpectedArchiveDigest = selected.TemplateDigest
                            PolicyBytes = readSelected selected.PolicyPath
                            ExpectedPolicyDigest = selected.PolicyDigest
                            SelectedPlatform = selected.Platform
                            PreflightTimeoutSeconds = selected.PreflightSeconds
                            ScaffoldTimeoutSeconds = selected.ScaffoldSeconds
                            DryRun = selected.DryRun
                        }
                with
                | :? IOException
                | :? UnauthorizedAccessException
                | :? ArgumentException ->
                    Error
                        [
                            diagnostic
                                "catalog.unreadable"
                                "$.selection"
                                "Selected catalog or policy metadata is unreadable or exceeds its capture limit."
                        ]

            match request with
            | Error errors ->
                printScaffold args "refused" "not-started" errors None
                1
            | Ok request ->
                let host: FS.GG.SDD.Commands.CatalogScaffoldEffects.HostSelection =
                    {
                        Mode = FS.GG.SDD.Commands.CatalogScaffoldEffects.LocalLinux
                        TransportExecutable = selected.TransportExecutable
                    }

                executeScaffold args host request None

    let private runDirect args =
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

    module Sources = FS.GG.SDD.Commands.ProviderSourceEffects

    let private sourceSelectors =
        [ "--source-set"; "--source-set-sha256"; "--acquisition-policy"; "--acquisition-policy-sha256"; "--source-cache"; "--offline" ]

    let private usesSources args = args |> List.exists (fun arg -> List.contains arg sourceSelectors)

    let private parseSourceOptions scaffold args =
        let common = sourceSelectors |> List.filter ((<>) "--offline")
        let execution =
            [ "--provider"; "--root"; "--admission-policy"; "--admission-policy-sha256"; "--platform"
              "--transport-executable"; "--preflight-timeout-seconds"; "--scaffold-timeout-seconds" ]
        let required = common @ (if scaffold then execution else [])
        let valued = common @ (if scaffold then execution else [ "--provider" ])
        let flags = [ "--offline"; "--json"; "--text"; "--rich"; "--force-color" ] @ (if scaffold then [ "--dry-run" ] else [])
        let error code name message = Error [ diagnostic code name message ]
        let rec parse (seen: Set<string>) (values: (string * string) list) (parameters: (string * string) list) (rest: string list) =
            match rest with
            | [] ->
                match required |> List.tryFind (fun name -> not (Set.contains name seen)) with
                | Some missing -> error "source.selectionMissing" missing "Source ingress requires every explicit source, policy and applicable execution selection."
                | None when not scaffold && not parameters.IsEmpty && not (Set.contains "--provider" seen) ->
                    error "source.providerMissing" "--param" "Literal parameter selection requires an explicit provider."
                | None -> Ok(Map.ofList values, List.rev parameters, seen)
            | name :: tail when List.contains name flags ->
                if Set.contains name seen then error "source.duplicateOption" name "Option must occur once."
                else parse (Set.add name seen) values parameters tail
            | "--param" :: value :: tail when not (value.StartsWith("--", StringComparison.Ordinal)) ->
                let split = value.IndexOf '='
                if split <= 0 then error "source.parameterMalformed" "--param" "Expected key=value; literal empty values are preserved."
                else
                    let key = value[..split - 1]
                    if parameters |> List.exists (fun (existing, _) -> existing = key) then
                        error "source.duplicateParameter" "--param" "Parameter keys must be distinct."
                    else parse seen values ((key, value[(split + 1)..]) :: parameters) tail
            | name :: value :: tail when List.contains name valued && not (value.StartsWith("--", StringComparison.Ordinal)) ->
                if Set.contains name seen then error "source.duplicateOption" name "Option must occur once."
                elif String.IsNullOrWhiteSpace value then error "source.missingOptionValue" name "A nonempty explicit value is required."
                else parse (Set.add name seen) ((name, value) :: values) parameters tail
            | name :: _ when name = "--param" || List.contains name valued -> error "source.missingOptionValue" name "Option requires a value."
            | name :: _ -> error "source.unknownOption" name "Unsupported or mixed direct-catalog/source option."
        parse Set.empty [] [] args

    let private printSources args result =
        let json =
            encode (fun w ->
                w.WriteStartObject()
                w.WriteString("schema", "fsgg.provider-source-preview/1")
                match result with
                | Error errors ->
                    w.WriteString("status", "refused")
                    w.WriteStartArray "diagnostics"
                    for (error: Diagnostic) in errors do
                        w.WriteStartObject()
                        w.WriteString("code", error.Code)
                        w.WriteString("path", error.Path)
                        w.WriteString("message", error.Message)
                        w.WriteEndObject()
                    w.WriteEndArray()
                | Ok(prepared, selected) ->
                    w.WriteString("status", "prepared")
                    w.WriteStartArray "sources"
                    for view in Sources.inspect prepared do
                        w.WriteStartObject()
                        w.WriteString("id", view.Source.Id)
                        w.WriteString("origin", string view.Source.Origin)
                        w.WriteString("revision", view.Revision)
                        w.WriteString("manifestSha256", view.Source.ManifestSha256)
                        w.WriteString("sourceSetSha256", view.SourceSetSha256)
                        w.WriteString("acquisitionPolicySha256", view.AcquisitionPolicySha256)
                        w.WriteString("acquisition", string view.Acquisition)
                        match view.ResolvedUri with Some uri -> w.WriteString("resolvedUri", uri) | None -> w.WriteNull "resolvedUri"
                        w.WriteStartArray "redirects"
                        for uri in view.Redirects do w.WriteStringValue uri
                        w.WriteEndArray()
                        w.WriteStartArray "networkHops"
                        for hop in view.NetworkHops do
                            w.WriteStartObject()
                            w.WriteString("uri", hop.Uri)
                            w.WriteString("address", hop.Address)
                            w.WriteNumber("statusCode", hop.StatusCode)
                            w.WriteEndObject()
                        w.WriteEndArray()
                        w.WritePropertyName "catalog"
                        use catalog = JsonDocument.Parse(project (Ok view.Catalog))
                        catalog.RootElement.WriteTo w
                        w.WriteEndObject()
                    w.WriteEndArray()
                    match selected with
                    | None -> w.WriteNull "selected"
                    | Some source ->
                        w.WritePropertyName "selected"
                        use catalog = JsonDocument.Parse(project (prepare (Sources.catalogSelection source)))
                        catalog.RootElement.WriteTo w
                        match ProviderSource.serializeObservation (Sources.sourceObservation source) with
                        | Error errors -> invalidOp (sprintf "Captured source observation is invalid: %A" errors)
                        | Ok text ->
                            w.WritePropertyName "sourceObservation"
                            use observation = JsonDocument.Parse text
                            observation.RootElement.WriteTo w
                w.WriteEndObject())
        // Plain JSON/text projections carry the same facts; interactive decoration is optional.
        match Rendering.selectFormat args with
        | Json -> Console.Out.WriteLine json
        | Text | Rich -> Console.Out.WriteLine("Provider source inspection\n" + json)
        match result with Ok _ -> 0 | Error _ -> 1

    let internal sourceFailureDiagnostics original secondary =
        let size (item: Fsgg.ProviderCatalog.Diagnostic) =
            System.Text.Encoding.UTF8.GetByteCount(item.Code + item.Path + item.Message)
        let marker = diagnostic "source.cleanupUnknown" "$" "Original resource cleanup remains Unknown; the caller retains custody."
        let reserve = if List.isEmpty secondary then [] else [ marker ]
        let mutable bytes = reserve |> List.sumBy size
        let primary =
            original
            |> List.distinct
            |> List.truncate (64 - reserve.Length)
            |> List.takeWhile (fun item ->
                bytes <- bytes + size item
                bytes <= 65536)
        let kept = primary @ reserve
        bytes <- kept |> List.sumBy size
        let details =
            secondary
            |> List.distinct
            |> List.filter (fun item -> not (List.contains item kept))
            |> List.truncate (64 - kept.Length)
            |> List.takeWhile (fun item ->
                bytes <- bytes + size item
                bytes <= 65536)
        kept @ details

    let private holdSourceOwner operation =
        // Terminal Unknown cannot settle by polling the completed runner. Preserve
        // the same owner until the separately selected external process custodian
        // retires this invocation; process death is not semantic cleanup evidence.
        try
            while true do System.Threading.Thread.Sleep System.Threading.Timeout.Infinite
        finally GC.KeepAlive operation

    let private retainSourceOwner operation =
        try
            Sources.waitForSettled operation
            |> fun pending -> Async.RunSynchronously(pending, cancellationToken = System.Threading.CancellationToken.None)
            |> ignore
            match Sources.release operation with
            | Ok() -> ()
            | Error _ -> holdSourceOwner operation
        with _ -> holdSourceOwner operation

    let private sourceCommand scaffold args =
        let refuse errors =
            if scaffold then
                printScaffold args "refused" "not-started" errors None
                1
            else printSources args (Error errors)
        match parseSourceOptions scaffold args with
        | Error errors -> refuse errors
        | Ok(values, parameters, flags) ->
            let prepared =
                try
                    let absolute name =
                        let value = values[name]
                        if not (Path.IsPathFullyQualified value) || value.Contains(char 0) || value.Contains('\\') then
                            raise (ArgumentException("An explicit fully qualified Linux path is required: " + name))
                        value
                    let positive name =
                        match Int32.TryParse(values[name], Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture) with
                        | true, value when value > 0 -> value
                        | _ -> raise (ArgumentException("Positive integral seconds required: " + name))
                    let execution =
                        if scaffold then
                            let policy = readSelectedBound ProviderSource.limits.MaximumPolicyBytes values["--admission-policy"]
                            Some(absolute "--root", absolute "--transport-executable", positive "--preflight-timeout-seconds",
                                 positive "--scaffold-timeout-seconds", policy)
                        else None
                    let request: Sources.Request =
                        { SourceSetBytes = readSelectedBound ProviderSource.limits.MaximumSetBytes values["--source-set"]
                          ExpectedSourceSetSha256 = values["--source-set-sha256"]
                          PolicyBytes = readSelectedBound ProviderSource.limits.MaximumPolicyBytes values["--acquisition-policy"]
                          ExpectedPolicySha256 = values["--acquisition-policy-sha256"]
                          CacheRoot = absolute "--source-cache"
                          TargetRoot = execution |> Option.map (fun (target, _, _, _, _) -> target)
                          Offline = Set.contains "--offline" flags }
                    Ok(request, execution)
                with
                | :? IOException | :? UnauthorizedAccessException | :? ArgumentException ->
                    Error [ diagnostic "source.selectionUnreadable" "$" "Explicit source, policy, path or budget selection is invalid, inaccessible or exceeds its bound." ]
            match prepared with
            | Error errors -> refuse errors
            | Ok(request, execution) ->
                use cancellation = new System.Threading.CancellationTokenSource()
                let cancel = ConsoleCancelEventHandler(fun _ event -> event.Cancel <- true; cancellation.Cancel())
                Console.CancelKeyPress.AddHandler cancel
                try
                    match Sources.prepare request cancellation.Token with
                    | Error errors -> refuse errors
                    | Ok operation ->
                        let mutable released = false
                        try
                            let result = Async.RunSynchronously(Sources.run operation, cancellationToken = System.Threading.CancellationToken.None)
                            // Selection/execution only receives data after the source owner's actual retirement.
                            match Sources.release operation with
                            | Error errors ->
                                // Preserve the original diagnostic order. The release
                                // refusal is secondary, never a replacement first cause.
                                let original = match result with Error original -> original | Ok _ -> (Sources.observe operation).Diagnostics
                                let findings = sourceFailureDiagnostics original errors
                                let code = refuse findings
                                holdSourceOwner operation
                                code
                            | Ok() ->
                                released <- true
                                match result with
                                | Error errors -> refuse errors
                                | Ok sources ->
                                    let selected =
                                        match Map.tryFind "--provider" values with
                                        | None -> Ok None
                                        | Some provider -> Sources.select sources provider parameters |> Result.map Some
                                    match selected, execution with
                                    | Error errors, _ -> refuse errors
                                    | Ok selected, None -> printSources args (Ok(sources, selected))
                                    | Ok(Some source), Some(target, transport, preflight, scaffoldSeconds, policy) ->
                                        let host: FS.GG.SDD.Commands.CatalogScaffoldEffects.HostSelection =
                                            { Mode = FS.GG.SDD.Commands.CatalogScaffoldEffects.LocalLinux; TransportExecutable = transport }
                                        let actual: CatalogScaffoldRequest =
                                            { Selection = Sources.catalogSelection source; TargetRoot = target
                                              TemplateArchive = Sources.archivePath source; ExpectedArchiveDigest = Sources.archiveSha256 source
                                              PolicyBytes = policy; ExpectedPolicyDigest = values["--admission-policy-sha256"]
                                              SelectedPlatform = values["--platform"]; PreflightTimeoutSeconds = preflight
                                              ScaffoldTimeoutSeconds = scaffoldSeconds; DryRun = Set.contains "--dry-run" flags }
                                        executeScaffold args host actual (Some source)
                                    | _ -> refuse [ diagnostic "source.providerMissing" "--provider" "Scaffold requires one explicitly selected provider." ]
                        finally
                            if not released then retainSourceOwner operation
                            GC.KeepAlive operation
                finally Console.CancelKeyPress.RemoveHandler cancel

    let scaffold args = if usesSources args then sourceCommand true args else scaffoldDirect args

    let run args =
        match args with
        | "inspect" :: rest when usesSources rest -> sourceCommand false rest
        | _ -> runDirect args
