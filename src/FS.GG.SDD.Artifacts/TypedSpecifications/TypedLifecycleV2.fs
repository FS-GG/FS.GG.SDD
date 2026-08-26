namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.IO
open System.Text
open System.Text.Json

type QuintAuthorityArtifact =
    { Id: string
      Path: string
      Sha256: string }

type QuintAuthorityManifest =
    { SchemaVersion: int
      Lifecycle: string
      Backend: string
      ProfileIdentity: string
      ToolchainIdentity: string
      PackageIdentity: string
      Artifacts: QuintAuthorityArtifact list
      AuthoringAgent: string
      AuthoringSession: string
      RollbackManifestPath: string option
      RollbackManifestSha256: string option }

type TypedAuthority =
    | FsharpSpecificationV1 of TypedAuthorityManifest
    | QuintSpecificationV1 of QuintAuthorityManifest

type QuintVerificationRung =
    | ProseOnly
    | StructuralTypecheck
    | TestAndSimulation
    | ModelCheck
    | FullCorpus

type QuintVerificationSelection =
    { ChangedPaths: string list
      Impacts: QuintImpact list }

[<RequireQualifiedAccess>]
module TypedAuthority =
    let private diagnostic id message correction : TypedLifecycleDiagnostic =
        { Id = id
          Message = message
          Correction = correction }

    let private requiredArtifactIds =
        set
            [ "markdown"
              "fence-manifest"
              "generated-modules"
              "source-map"
              "compiled-contract"
              "bindings"
              "compilation-receipt" ]

    let private safeRelativePath (value: string) =
        not (String.IsNullOrWhiteSpace value)
        && not (Path.IsPathRooted value)
        && value.Replace('\\', '/').Split('/')
           |> Array.forall (fun segment -> segment <> "" && segment <> "." && segment <> "..")

    let private isSha256 (value: string) =
        value.Length = 64
        && value |> Seq.forall Uri.IsHexDigit

    let serializeQuintV2 manifest =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", manifest.SchemaVersion)
        writer.WriteString("lifecycle", manifest.Lifecycle)
        writer.WriteString("backend", manifest.Backend)
        writer.WriteString("profileIdentity", manifest.ProfileIdentity)
        writer.WriteString("toolchainIdentity", manifest.ToolchainIdentity)
        writer.WriteString("packageIdentity", manifest.PackageIdentity)
        writer.WriteStartArray("artifacts")

        manifest.Artifacts
        |> List.sortBy _.Id
        |> List.iter (fun artifact ->
            writer.WriteStartObject()
            writer.WriteString("id", artifact.Id)
            writer.WriteString("path", artifact.Path)
            writer.WriteString("sha256", artifact.Sha256)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteString("authoringAgent", manifest.AuthoringAgent)
        writer.WriteString("authoringSession", manifest.AuthoringSession)

        match manifest.RollbackManifestPath with
        | Some value -> writer.WriteString("rollbackManifestPath", value)
        | None -> writer.WriteNull("rollbackManifestPath")

        match manifest.RollbackManifestSha256 with
        | Some value -> writer.WriteString("rollbackManifestSha256", value)
        | None -> writer.WriteNull("rollbackManifestSha256")

        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray()) + "\n"

    let private readString (root: JsonElement) (name: string) =
        match root.TryGetProperty name with
        | true, value when value.ValueKind = JsonValueKind.String ->
            match value.GetString() |> Option.ofObj with
            | Some text when not (String.IsNullOrWhiteSpace text) -> Ok text
            | _ -> Error(diagnostic "typedSdd.v2.manifestMalformed" $"Field '{name}' is empty." "Regenerate the manifest-v2 authority.")
        | _ ->
            Error(diagnostic "typedSdd.v2.manifestMalformed" $"Missing string field '{name}'." "Regenerate the manifest-v2 authority.")

    let private readOptionalString (root: JsonElement) (name: string) =
        match root.TryGetProperty name with
        | true, value when value.ValueKind = JsonValueKind.String -> value.GetString() |> Option.ofObj |> Ok
        | true, value when value.ValueKind = JsonValueKind.Null -> Ok None
        | false, _ -> Error(diagnostic "typedSdd.v2.manifestMalformed" $"Missing field '{name}'." "Regenerate the manifest-v2 authority.")
        | _ -> Error(diagnostic "typedSdd.v2.manifestMalformed" $"Field '{name}' must be a string or null." "Regenerate the manifest-v2 authority.")

    let private requiredItemString (item: JsonElement) (name: string) =
        match item.GetProperty(name).GetString() |> Option.ofObj with
        | Some value when not (String.IsNullOrWhiteSpace value) -> value
        | _ -> raise (FormatException($"Artifact field '{name}' is empty."))

    let private decodeV2 (root: JsonElement) =
        let allowed =
            set
                [ "schemaVersion"; "lifecycle"; "backend"; "profileIdentity"; "toolchainIdentity"
                  "packageIdentity"; "artifacts"; "authoringAgent"; "authoringSession"
                  "rollbackManifestPath"; "rollbackManifestSha256" ]

        let unknown =
            root.EnumerateObject()
            |> Seq.map _.Name
            |> Seq.filter (allowed.Contains >> not)
            |> Seq.sort
            |> Seq.toList

        if not (List.isEmpty unknown) then
            let names = String.concat ", " unknown

            Error(
                diagnostic
                    "typedSdd.v2.manifestUnknownField"
                    $"Manifest-v2 contains unknown fields: {names}."
                    "Remove unknown fields or upgrade the CLI explicitly."
            )
        else
            let artifactsResult =
                match root.TryGetProperty "artifacts" with
                | true, value when value.ValueKind = JsonValueKind.Array ->
                    try
                        value.EnumerateArray()
                        |> Seq.map (fun item ->
                            let itemAllowed = set [ "id"; "path"; "sha256" ]
                            let itemUnknown = item.EnumerateObject() |> Seq.map _.Name |> Seq.filter (itemAllowed.Contains >> not) |> Seq.toList

                            if not (List.isEmpty itemUnknown) then
                                raise (FormatException("Artifact contains unknown fields."))

                            { Id = requiredItemString item "id"
                              Path = requiredItemString item "path"
                              Sha256 = requiredItemString item "sha256" })
                        |> Seq.toList
                        |> Ok
                    with ex ->
                        Error(diagnostic "typedSdd.v2.manifestMalformed" ex.Message "Regenerate the manifest-v2 authority.")
                | _ -> Error(diagnostic "typedSdd.v2.manifestMalformed" "Missing array field 'artifacts'." "Regenerate the manifest-v2 authority.")

            match
                readString root "lifecycle",
                readString root "backend",
                readString root "profileIdentity",
                readString root "toolchainIdentity",
                readString root "packageIdentity",
                artifactsResult,
                readString root "authoringAgent",
                readString root "authoringSession",
                readOptionalString root "rollbackManifestPath",
                readOptionalString root "rollbackManifestSha256"
            with
            | Ok lifecycle, Ok backend, Ok profile, Ok toolchain, Ok package, Ok artifacts, Ok agent, Ok session, Ok rollbackPath, Ok rollbackSha ->
                Ok(
                    QuintSpecificationV1
                        { SchemaVersion = 2
                          Lifecycle = lifecycle
                          Backend = backend
                          ProfileIdentity = profile
                          ToolchainIdentity = toolchain
                          PackageIdentity = package
                          Artifacts = artifacts
                          AuthoringAgent = agent
                          AuthoringSession = session
                          RollbackManifestPath = rollbackPath
                          RollbackManifestSha256 = rollbackSha }
                )
            | results ->
                let firstError =
                    [ match results with
                      | Error error, _, _, _, _, _, _, _, _, _ -> yield error
                      | _, Error error, _, _, _, _, _, _, _, _ -> yield error
                      | _, _, Error error, _, _, _, _, _, _, _ -> yield error
                      | _, _, _, Error error, _, _, _, _, _, _ -> yield error
                      | _, _, _, _, Error error, _, _, _, _, _ -> yield error
                      | _, _, _, _, _, Error error, _, _, _, _ -> yield error
                      | _, _, _, _, _, _, Error error, _, _, _ -> yield error
                      | _, _, _, _, _, _, _, Error error, _, _ -> yield error
                      | _, _, _, _, _, _, _, _, Error error, _ -> yield error
                      | _, _, _, _, _, _, _, _, _, Error error -> yield error
                      | _ -> () ]
                    |> List.head

                Error firstError

    let deserialize (text: string) =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement

            if root.ValueKind <> JsonValueKind.Object then
                Error(diagnostic "typedSdd.v2.manifestMalformed" "Authority manifest must be a JSON object." "Regenerate the authority manifest.")
            else
                match root.TryGetProperty "schemaVersion" with
                | true, value when value.ValueKind = JsonValueKind.Number && value.GetInt32() = 1 ->
                    TypedAuthorityManifest.deserialize text |> Result.map FsharpSpecificationV1
                | true, value when value.ValueKind = JsonValueKind.Number && value.GetInt32() = 2 -> decodeV2 root
                | true, value when value.ValueKind = JsonValueKind.Number ->
                    Error(
                        diagnostic
                            "typedSdd.authorityCompatibility"
                            $"Authority schema {value.GetInt32()} is unsupported."
                            "Upgrade the CLI or use manifest schemaVersion 1 or 2."
                    )
                | _ -> Error(diagnostic "typedSdd.v2.manifestMalformed" "Missing numeric schemaVersion." "Regenerate the authority manifest.")
        with ex ->
            Error(diagnostic "typedSdd.v2.manifestMalformed" ex.Message "Regenerate the authority manifest.")

    let validateQuintV2 expectedPackageIdentity artifactBytes manifest =
        let artifactMap = artifactBytes |> Map.ofList
        let offeredIds = manifest.Artifacts |> List.map _.Id
        let duplicates = offeredIds |> List.countBy id |> List.filter (fun (_, count) -> count > 1) |> List.map fst
        let offered = Set.ofList offeredIds

        [ if manifest.SchemaVersion <> 2 || manifest.Lifecycle <> "typed-sdd" || manifest.Backend <> "quint-specification-v1" then
              yield diagnostic "typedSdd.v2.wrongAuthority" "Manifest does not select typed-sdd/quint-specification-v1 schema v2." "Use the explicit manifest-v2 Quint backend."
          if manifest.ProfileIdentity <> QuintProfile.identity then
              yield diagnostic "typedSdd.v2.profileIdentityMismatch" $"Profile identity '{manifest.ProfileIdentity}' is unsupported." $"Use {QuintProfile.identity}."
          if manifest.ToolchainIdentity <> QuintToolchain.fingerprint QuintToolchain.q1 then
              yield diagnostic "typedSdd.v2.toolchainIdentityMismatch" "Toolchain identity differs from the Q1-qualified manifest." "Provision and select the exact Q1/Q2 toolchain cache."
          if manifest.PackageIdentity <> expectedPackageIdentity then
              yield diagnostic "typedSdd.v2.packageIdentityMismatch" "Authority package identity differs from the installed producer." "Install the exact recorded coherent package set."
          if String.IsNullOrWhiteSpace manifest.AuthoringAgent || String.IsNullOrWhiteSpace manifest.AuthoringSession then
              yield diagnostic "typedSdd.v2.authoringReceiptMissing" "Manifest-v2 lacks a complete authoring receipt." "Re-author with --agent and --session."
          if not (List.isEmpty duplicates) then
              let names = String.concat ", " duplicates
              yield diagnostic "typedSdd.v2.artifactDuplicate" $"Duplicate artifact ids: {names}." "Keep exactly one entry for each required artifact."
          if offered <> requiredArtifactIds then
              let missing = Set.difference requiredArtifactIds offered |> String.concat ", "
              let extra = Set.difference offered requiredArtifactIds |> String.concat ", "
              yield diagnostic "typedSdd.v2.artifactInventory" $"Artifact inventory differs (missing: {missing}; extra: {extra})." "Regenerate the complete closed manifest-v2 inventory."
          for artifact in manifest.Artifacts |> List.sortBy _.Id do
              if not (safeRelativePath artifact.Path) then
                  yield diagnostic "typedSdd.v2.artifactPath" $"Artifact '{artifact.Id}' has unsafe path '{artifact.Path}'." "Use one contained project-relative path."
              elif not (isSha256 artifact.Sha256) then
                  yield diagnostic "typedSdd.v2.artifactDigest" $"Artifact '{artifact.Id}' has an invalid SHA-256." "Regenerate the manifest from staged bytes."
              else
                  match Map.tryFind artifact.Path artifactMap with
                  | None
                  | Some None ->
                      yield diagnostic "typedSdd.v2.artifactMissing" $"Artifact '{artifact.Id}' is missing." "Restore or re-author the complete v2 authority."
                  | Some(Some bytes) when TypedAuthorityManifest.sha256 bytes <> artifact.Sha256 ->
                      yield diagnostic "typedSdd.v2.artifactMismatch" $"Artifact '{artifact.Id}' differs from its manifest digest." "Re-author instead of editing generated authority artifacts."
                  | _ -> ()
          match manifest.RollbackManifestPath, manifest.RollbackManifestSha256 with
          | None, None -> ()
          | Some path, Some sha when safeRelativePath path && isSha256 sha ->
              match Map.tryFind path artifactMap with
              | Some(Some bytes) when TypedAuthorityManifest.sha256 bytes = sha -> ()
              | Some(Some _) -> yield diagnostic "typedSdd.v2.rollbackMismatch" "Rollback manifest differs from its recorded digest." "Restore the authenticated rollback inventory before rollback."
              | _ -> yield diagnostic "typedSdd.v2.rollbackMissing" "The recorded rollback manifest is missing." "Restore the authenticated rollback inventory."
          | _ -> yield diagnostic "typedSdd.v2.rollbackBinding" "Rollback path and digest must both be present or both be null." "Regenerate the migration receipt." ]

[<RequireQualifiedAccess>]
module QuintVerificationSelector =
    let value rung =
        match rung with
        | ProseOnly -> "prose-only"
        | StructuralTypecheck -> "structural-typecheck"
        | TestAndSimulation -> "test-and-simulation"
        | ModelCheck -> "model-check"
        | FullCorpus -> "full-corpus"

    let private rank = function
        | ProseOnly -> 0
        | StructuralTypecheck -> 1
        | TestAndSimulation -> 2
        | ModelCheck -> 3
        | FullCorpus -> 4

    let private strongest left right = if rank left >= rank right then left else right

    let private category (value: string) =
        match value.Trim().ToLowerInvariant() with
        | "prose"
        | "documentation"
        | "docs" -> ProseOnly
        | "catalogue"
        | "metadata"
        | "relationship" -> StructuralTypecheck
        | "action"
        | "simulation"
        | "test" -> TestAndSimulation
        | "invariant"
        | "temporal"
        | "bounds"
        | "reachability"
        | "model-check" -> ModelCheck
        | "adapter"
        | "runtime"
        | "compiler"
        | "profile"
        | "toolchain"
        | "schema"
        | "selector" -> FullCorpus
        | _ -> FullCorpus

    let private pathRung (path: string) =
        let normalized = path.Replace('\\', '/').ToLowerInvariant()

        if normalized.Contains("quintverificationselector")
           || normalized.Contains("quintcompiler")
           || normalized.Contains("quintprofile")
           || normalized.Contains("quinttoolchain")
           || normalized.Contains("typedlifecyclev2")
           || normalized.EndsWith(".schema.json", StringComparison.Ordinal) then
            FullCorpus
        elif normalized.EndsWith(".md", StringComparison.Ordinal) then
            ProseOnly
        else
            FullCorpus

    let select (selection: QuintVerificationSelection) =
        let impactRung = selection.Impacts |> List.map (fun impact -> category impact.Category)
        let pathRungs = selection.ChangedPaths |> List.map pathRung

        match impactRung @ pathRungs with
        | [] -> FullCorpus
        | values -> values |> List.fold strongest ProseOnly
