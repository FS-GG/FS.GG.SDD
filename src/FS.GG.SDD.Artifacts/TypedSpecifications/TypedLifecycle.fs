namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json

type LifecycleLane =
    | NoLifecycle
    | StandardSdd
    | TypedSdd
    | LegacySpecKit

type TypedAuthorityManifest =
    { SchemaVersion: int
      Lifecycle: string
      Backend: string
      CompilerIdentity: string
      PackageIdentity: string
      ExtensionIdentity: string
      CanonicalPath: string
      CanonicalSha256: string
      NormalizedPath: string
      NormalizedSha256: string
      MarkdownPath: string
      MarkdownSha256: string
      AuthoringAgent: string
      AuthoringSession: string
      RollbackSourceSha256: string option }

type TypedLifecycleDiagnostic =
    { Id: string
      Message: string
      Correction: string }

[<RequireQualifiedAccess>]
module LifecycleLane =
    let value (lane: LifecycleLane) =
        match lane with
        | NoLifecycle -> "none"
        | StandardSdd -> "sdd"
        | TypedSdd -> "typed-sdd"
        | LegacySpecKit -> "spec-kit"

    let backend (lane: LifecycleLane) =
        match lane with
        | TypedSdd -> "fsharp-specification-v1"
        | StandardSdd -> "markdown-sdd-v1"
        | NoLifecycle -> "none"
        | LegacySpecKit -> "legacy-spec-kit"

    let resolve (value: string option) =
        match value |> Option.map (fun item -> item.Trim().ToLowerInvariant()) with
        | None
        | Some "" -> Ok StandardSdd
        | Some "none" -> Ok NoLifecycle
        | Some "sdd" -> Ok StandardSdd
        | Some "typed-sdd" -> Ok TypedSdd
        | Some "spec-kit" -> Ok LegacySpecKit
        | Some unsupported ->
            Error
                { Id = "typedSdd.lifecycleUnsupported"
                  Message = $"Lifecycle '{unsupported}' is not supported."
                  Correction = "Choose one of: none, sdd, typed-sdd, spec-kit." }

[<RequireQualifiedAccess>]
module TypedAuthorityManifest =
    let path (workId: string) = $"readiness/{workId}/typed-authority.json"

    let sha256 (bytes: byte array) =
        let digest: byte array = SHA256.HashData(bytes)
        Convert.ToHexString(digest).ToLowerInvariant()

    let serialize (manifest: TypedAuthorityManifest) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", manifest.SchemaVersion)
        writer.WriteString("lifecycle", manifest.Lifecycle)
        writer.WriteString("backend", manifest.Backend)
        writer.WriteString("compilerIdentity", manifest.CompilerIdentity)
        writer.WriteString("packageIdentity", manifest.PackageIdentity)
        writer.WriteString("extensionIdentity", manifest.ExtensionIdentity)
        writer.WriteString("canonicalPath", manifest.CanonicalPath)
        writer.WriteString("canonicalSha256", manifest.CanonicalSha256)
        writer.WriteString("normalizedPath", manifest.NormalizedPath)
        writer.WriteString("normalizedSha256", manifest.NormalizedSha256)
        writer.WriteString("markdownPath", manifest.MarkdownPath)
        writer.WriteString("markdownSha256", manifest.MarkdownSha256)
        writer.WriteString("authoringAgent", manifest.AuthoringAgent)
        writer.WriteString("authoringSession", manifest.AuthoringSession)
        match manifest.RollbackSourceSha256 with
        | Some value -> writer.WriteString("rollbackSourceSha256", value)
        | None -> writer.WriteNull("rollbackSourceSha256")
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray()) + "\n"

    let private diagnostic id message correction =
        { Id = id; Message = message; Correction = correction }

    let deserialize (text: string) =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement
            let required (name: string) =
                match root.TryGetProperty name with
                | true, value when value.ValueKind = JsonValueKind.String ->
                    value.GetString() |> Option.ofObj |> Option.defaultWith (fun () -> raise (FormatException($"Null string field '{name}'.")))
                | _ -> raise (FormatException($"Missing string field '{name}'."))
            let rollback =
                match root.TryGetProperty "rollbackSourceSha256" with
                | true, value when value.ValueKind = JsonValueKind.String -> value.GetString() |> Option.ofObj
                | _ -> None
            let schemaVersion = root.GetProperty("schemaVersion").GetInt32()
            if schemaVersion <> 1 then
                Error(diagnostic "typedSdd.authorityCompatibility" $"Authority schema {schemaVersion} is unsupported." "Upgrade the CLI or regenerate with schemaVersion 1.")
            else
                Ok
                    { SchemaVersion = schemaVersion
                      Lifecycle = required "lifecycle"
                      Backend = required "backend"
                      CompilerIdentity = required "compilerIdentity"
                      PackageIdentity = required "packageIdentity"
                      ExtensionIdentity = required "extensionIdentity"
                      CanonicalPath = required "canonicalPath"
                      CanonicalSha256 = required "canonicalSha256"
                      NormalizedPath = required "normalizedPath"
                      NormalizedSha256 = required "normalizedSha256"
                      MarkdownPath = required "markdownPath"
                      MarkdownSha256 = required "markdownSha256"
                      AuthoringAgent = required "authoringAgent"
                      AuthoringSession = required "authoringSession"
                      RollbackSourceSha256 = rollback }
        with ex ->
            Error(diagnostic "typedSdd.authorityMalformed" ex.Message "Regenerate the Typed SDD authority manifest.")

    let validate expectedPackageIdentity compilerAvailable canonicalBytes normalizedBytes markdownBytes manifest =
        [ if manifest.Lifecycle <> "typed-sdd" || manifest.Backend <> "fsharp-specification-v1" then
              yield diagnostic "typedSdd.wrongLifecycle" "Provenance does not select the Typed SDD F# backend." "Select lifecycle=typed-sdd and refresh provenance."
          if not compilerAvailable then
              yield diagnostic "typedSdd.compilerUnavailable" "The recorded F# compiler is unavailable." "Install a compatible .NET SDK and restore the pinned tool manifest."
          if manifest.PackageIdentity <> expectedPackageIdentity then
              yield diagnostic "typedSdd.identityMismatch" "The authority package identity differs from the installed producer." "Install the exact package identity recorded by provenance, or upgrade explicitly."
          match canonicalBytes with
          | None -> yield diagnostic "typedSdd.canonicalMissing" "The canonical F# specification is missing." "Restore or author the canonical specification.fsx."
          | Some bytes when sha256 bytes <> manifest.CanonicalSha256 ->
              yield diagnostic "typedSdd.directCanonicalEdit" "The canonical F# source changed outside an authoring receipt." "Run the Typed SDD author operation to accept and recompile the edit."
          | _ -> ()
          match normalizedBytes, markdownBytes with
          | None, _
          | _, None -> yield diagnostic "typedSdd.projectionMissing" "A required Typed SDD projection is missing." "Run typed-sdd inspect --refresh."
          | Some normalized, Some markdown when sha256 normalized <> manifest.NormalizedSha256 || sha256 markdown <> manifest.MarkdownSha256 ->
              yield diagnostic "typedSdd.staleProjection" "A Typed SDD projection is stale or was edited." "Regenerate projections from the canonical F# source."
          | _ -> () ]
