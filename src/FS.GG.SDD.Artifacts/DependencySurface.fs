namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Reflection
open System.Text
open System.Text.Json
open FS.GG.SDD.Artifacts.SchemaVersion

module DependencySurface =

    type CapturedSymbol = string

    type DependencySurfaceCapture =
        { SchemaVersion: int
          PackageId: string
          Version: string
          CapturedFrom: string
          Sha256: string
          Symbols: CapturedSymbol list }

    let schemaVersion = 1

    let defaultBaselineRoot = "docs/dependency-surface"

    // Canonical symbol set: sorted, deduplicated. Every derivation (the digest, the
    // serialized bytes, the constructor) funnels through this so a capture's identity is
    // independent of the read order the edge happened to enumerate members in.
    let private canonicalSymbols (symbols: CapturedSymbol list) =
        symbols
        |> List.filter (String.IsNullOrWhiteSpace >> not)
        |> List.distinct
        |> List.sort

    let symbolDigest (symbols: CapturedSymbol list) =
        (canonicalSymbols symbols |> String.concat "\n" |> sha256Text).Value

    let create packageId version capturedFrom symbols =
        let canonical = canonicalSymbols symbols

        { SchemaVersion = schemaVersion
          PackageId = packageId
          Version = version
          CapturedFrom = capturedFrom
          Sha256 = symbolDigest canonical
          Symbols = canonical }

    let capturePath (baselineRoot: string) (packageId: string) (version: string) =
        // Structural only — no package/feed literal in generic SDD (FR-009). Kept relative
        // and forward-slashed so the committed path is platform-stable and diffable.
        let root = baselineRoot.Replace('\\', '/').TrimEnd('/')
        $"{root}/{packageId}/{version}.json"

    let serialize (capture: DependencySurfaceCapture) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", capture.SchemaVersion)
        writer.WriteString("packageId", capture.PackageId)
        writer.WriteString("version", capture.Version)
        writer.WriteString("capturedFrom", capture.CapturedFrom)
        writer.WriteString("sha256", capture.Sha256)
        writer.WriteStartArray("symbols")

        // Sorted + deduplicated so the emitted bytes are deterministic and reconcilable.
        canonicalSymbols capture.Symbols
        |> List.iter (fun symbol -> writer.WriteStringValue symbol)

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()

        // Trailing LF so the committed artifact is POSIX-clean; Utf8JsonWriter emits `\n`
        // for indentation (not Environment.NewLine), so the bytes are platform-stable.
        Encoding.UTF8.GetString(stream.ToArray()) + "\n"

    let tryParse (text: string) : Result<DependencySurfaceCapture, string> =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement

            // One schema-version policy for every artifact: gate through the canonical
            // classifier so the capture shares the accept/reject grammar of every other
            // committed artifact rather than a bespoke Major-only check.
            match jsonInt "schemaVersion" root with
            | Some version when SchemaVersion.isBlocking (SchemaVersion.classifyRaw (Some(string version))) ->
                Error $"dependency-surface capture: unsupported schemaVersion {version}."
            | Some version ->
                let packageId = jsonString "packageId" root |> Option.defaultValue ""
                let packageVersion = jsonString "version" root |> Option.defaultValue ""
                let capturedFrom = jsonString "capturedFrom" root |> Option.defaultValue ""
                let sha256 = jsonString "sha256" root |> Option.defaultValue ""
                // `jsonStringList` already sorts, deduplicates, and drops blanks — the same
                // canonical order `serialize` writes, so a parsed capture round-trips.
                let symbols = jsonStringList "symbols" root

                if String.IsNullOrWhiteSpace packageId then
                    Error "dependency-surface capture: missing required field 'packageId'."
                elif String.IsNullOrWhiteSpace packageVersion then
                    Error "dependency-surface capture: missing required field 'version'."
                elif String.IsNullOrWhiteSpace sha256 then
                    Error "dependency-surface capture: missing required field 'sha256'."
                else
                    Ok
                        { SchemaVersion = version
                          PackageId = packageId
                          Version = packageVersion
                          CapturedFrom = capturedFrom
                          Sha256 = sha256
                          Symbols = symbols }
            | None -> Error "dependency-surface capture: missing required field 'schemaVersion'."
        with :? JsonException as ex ->
            Error $"dependency-surface capture: malformed JSON ({ex.Message})."

    let symbolSet (capture: DependencySurfaceCapture) : Set<CapturedSymbol> = Set.ofList capture.Symbols

    let symbolsFromAssembly (assembly: Assembly) : CapturedSymbol list =
        // Reflection-tolerant type load: a package whose transitive dependencies are not all
        // present still yields the types that resolved (mirrors the internal PublicSurface
        // capture's tolerance). A hard failure yields no symbols — the caller (edge) then
        // reports the surface as unavailable, never a false drift.
        let types =
            try
                assembly.GetTypes()
            with :? ReflectionTypeLoadException as ex ->
                ex.Types |> Array.choose Option.ofObj

        let visibleTypes =
            types
            |> Array.filter (fun t -> (t.IsPublic || t.IsNestedPublic) && not (String.IsNullOrEmpty t.Name))

        // An F# module compiles to an abstract sealed type; its `val`s are public static members.
        let isModule (t: Type) = t.IsAbstract && t.IsSealed

        let typeSymbols = visibleTypes |> Array.map (fun t -> t.Name)

        let safeMembers (extract: Type -> string seq) (t: Type) =
            try
                extract t |> Seq.toArray
            with _ ->
                [||]

        let moduleMembers =
            visibleTypes
            |> Array.filter isModule
            |> Array.collect (
                safeMembers (fun t ->
                    let methods =
                        t.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
                        |> Array.filter (fun m -> not m.IsSpecialName)
                        |> Array.map (fun m -> $"{t.Name}.{m.Name}")

                    let properties =
                        t.GetProperties(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
                        |> Array.map (fun p -> $"{t.Name}.{p.Name}")

                    Array.append methods properties |> Array.toSeq)
            )

        let recordMembers =
            visibleTypes
            |> Array.filter (isModule >> not)
            |> Array.collect (
                safeMembers (fun t ->
                    t.GetProperties(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
                    |> Array.map (fun p -> $"{t.Name}.{p.Name}")
                    |> Array.toSeq)
            )

        Array.concat [ typeSymbols; moduleMembers; recordMembers ]
        |> Array.toList
        |> canonicalSymbols
