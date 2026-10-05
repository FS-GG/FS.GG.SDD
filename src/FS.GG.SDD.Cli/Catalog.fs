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
