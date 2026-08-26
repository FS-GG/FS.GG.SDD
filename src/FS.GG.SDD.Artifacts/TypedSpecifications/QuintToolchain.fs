namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.Globalization
open System.Security.Cryptography
open System.Text
open System.Text.Json

type QuintCacheObjectKind =
    | File
    | Tree
    | Closure
    | Source

type QuintCacheRequirement =
    { Id: string
      Kind: QuintCacheObjectKind
      Sha256: string
      Bytes: int64 option }

type QuintToolComponent =
    { Id: string
      Version: string
      Source: string
      Objects: QuintCacheRequirement list }

type QuintGuidanceIdentity =
    { Source: string
      License: string
      LicenseSha256: string
      TrackedTreeSha256: string }

type QuintToolchainManifest =
    { Schema: string
      Profile: string
      Platform: string
      Components: QuintToolComponent list
      Guidance: QuintGuidanceIdentity option }

type QuintCacheObjectState =
    | Absent
    | Unreadable of detail: string
    | Present of sha256: string * bytes: int64 option * complete: bool

type QuintCacheObservation =
    { Id: string
      Kind: QuintCacheObjectKind
      State: QuintCacheObjectState }

type QuintEndpointState =
    | Available
    | Occupied of detail: string

type QuintProcessRequest =
    { StepId: string
      ExecutableObjectId: string
      Arguments: string list
      Environment: (string * string) list
      WorkingDirectory: string }

type QuintCompilationPlan =
    { ManifestSha256: string
      RequiredObjects: QuintCacheRequirement list
      Requests: QuintProcessRequest list }

type QuintProcessOutcome =
    | Succeeded
    | Failed of exitCode: int * detail: string

type QuintProcessObservation =
    { StepId: string
      Outcome: QuintProcessOutcome }

module private QuintToolchainInternal =
    let diagnostic code path message : SpecificationDiagnostic =
        { Code = code
          Path = path
          Message = message
          Location = None }

    let sortDiagnostics (diagnostics: SpecificationDiagnostic list) =
        diagnostics
        |> List.distinct
        |> List.sortBy (fun item -> item.Path, item.Code, item.Message)

    let isSha256 (value: string) =
        not (String.IsNullOrWhiteSpace value)
        && value.Length = 64
        && value
           |> Seq.forall (fun character ->
               (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))

    let isSafeToken (value: string) =
        not (String.IsNullOrWhiteSpace value)
        && not (IO.Path.IsPathRooted value)
        && not (value.Contains('/'))
        && not (value.Contains('\\'))
        && value <> "."
        && value <> ".."

    let kindText =
        function
        | File -> "file"
        | Tree -> "tree"
        | Closure -> "closure"
        | Source -> "source"

    let writeManifest (writer: Utf8JsonWriter) (manifest: QuintToolchainManifest) =
        writer.WriteStartObject()
        writer.WriteString("schema", manifest.Schema)
        writer.WriteString("profile", manifest.Profile)
        writer.WriteString("platform", manifest.Platform)
        writer.WritePropertyName("components")
        writer.WriteStartArray()

        for toolComponent in manifest.Components |> List.sortBy (fun item -> item.Id) do
            writer.WriteStartObject()
            writer.WriteString("id", toolComponent.Id)
            writer.WriteString("version", toolComponent.Version)
            writer.WriteString("source", toolComponent.Source)
            writer.WritePropertyName("objects")
            writer.WriteStartArray()

            for item in toolComponent.Objects |> List.sortBy (fun item -> item.Id) do
                writer.WriteStartObject()
                writer.WriteString("id", item.Id)
                writer.WriteString("kind", kindText item.Kind)
                writer.WriteString("sha256", item.Sha256)

                match item.Bytes with
                | Some bytes -> writer.WriteNumber("bytes", bytes)
                | None -> ()

                writer.WriteEndObject()

            writer.WriteEndArray()
            writer.WriteEndObject()

        writer.WriteEndArray()

        match manifest.Guidance with
        | Some guidance ->
            writer.WritePropertyName("guidance")
            writer.WriteStartObject()
            writer.WriteString("source", guidance.Source)
            writer.WriteString("license", guidance.License)
            writer.WriteString("licenseSha256", guidance.LicenseSha256)
            writer.WriteString("trackedTreeSha256", guidance.TrackedTreeSha256)
            writer.WriteEndObject()
        | None -> ()

        writer.WriteEndObject()

    let encode manifest =
        use stream = new IO.MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = false))
        writeManifest writer manifest
        writer.Flush()
        stream.ToArray()

    let sha256 (bytes: byte array) =
        SHA256.HashData bytes
        |> Array.map (fun value -> value.ToString("x2", CultureInfo.InvariantCulture))
        |> String.concat ""

    let req id kind sha bytes =
        { Id = id
          Kind = kind
          Sha256 = sha
          Bytes = bytes }

    let toolComponent id version source objects =
        { Id = id
          Version = version
          Source = source
          Objects = objects }

    let exact =
        { Schema = "fsgg.quint.toolchain-manifest/v1"
          Profile = "fsgg-quint-profile/1"
          Platform = "linux/amd64"
          Components =
            [ toolComponent
                  "ajv"
                  "8.17.1"
                  "npm:ajv@8.17.1"
                  [ req "ajv-package-lock" File "8f52d263544de67504b2d103b6321f32a34a821cf9f54170f4f75d77e136f691" None
                    req
                        "ajv-closure"
                        Closure
                        "e14d4bfc96cce335d1d370f844294c8c6eeced38c61da0f5ae224e26f74d5007"
                        (Some 1289583L) ]
              toolComponent
                  "apalache"
                  "0.56.1"
                  "github:apalache-mc/apalache@v0.56.1"
                  [ req "apalache-archive" File "a61c07569d7195ddc589f01037fa10fafef4fb0796af2f1c9cb45226375dfbfc" None
                    req
                        "apalache-tree"
                        Tree
                        "3466d07f06d7ac80ee0f171a96383183cee9d91bf1b5995d897d4f15c004569f"
                        (Some 136014794L)
                    req "apalache-jar" File "4753c0ebb2cbb266e2c6ac19ab5ca3827d726cc80fd1fc5d7c1eeb64736cd60b" None ]
              toolComponent
                  "go"
                  "1.24.1"
                  "go1.24.1.linux-amd64"
                  [ req "go-archive" File "cb2396bae64183cdccf81a9a6df0aea3bce9511fc21469fb89a0c00470088073" None ]
              toolComponent
                  "java"
                  "Eclipse Temurin 21.0.9+10"
                  "adoptium:temurin-21.0.9+10-jre-linux-x64"
                  [ req "java-archive" File "aeab55d064a1a27a3744b0880b9b414077b4ed2b1790817eea3df60aec946431" None
                    req "java-binary" File "e865867065e48928c58293f30e7ae26a79c842f8607fa51d7e2e9fb90b602786" None ]
              toolComponent
                  "lmt"
                  "62fe18f2f6a6e11c158ff2b2209e1082a4fcd59c"
                  "github:driusan/lmt@62fe18f2f6a6e11c158ff2b2209e1082a4fcd59c"
                  [ req
                        "lmt-binary"
                        File
                        "37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10"
                        (Some 2787745L) ]
              toolComponent
                  "node"
                  "26.7.0"
                  "nodejs:v26.7.0-linux-x64"
                  [ req
                        "node-binary"
                        File
                        "d51d79e0e04abfe366345496a8e1379d56493271af4e0d6f27dd6ba76be628ea"
                        (Some 62822072L) ]
              toolComponent
                  "quint"
                  "0.32.0"
                  "github:informalsystems/quint@v0.32.0"
                  [ req
                        "quint-binary"
                        File
                        "939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f"
                        (Some 125661253L) ]
              toolComponent
                  "rust-evaluator"
                  "0.6.0"
                  "github:informalsystems/quint@evaluator-v0.6.0"
                  [ req
                        "rust-evaluator-archive"
                        File
                        "61755a09d5052d93a4e75e840059edfd0d3674aeda164b9d2464be3d6e21b1c2"
                        None
                    req
                        "rust-evaluator-binary"
                        File
                        "b2efdeac5713d153e41bf2143b94ed75d888fdd5637f4a5d61a04c695313510a"
                        (Some 2628304L) ] ]
          Guidance =
            Some
                { Source = "quint-co/quint-llm-kit@cc75369f741af7d490936f82002c2d28e3b3d78d"
                  License = "Apache-2.0"
                  LicenseSha256 = "5cc84061e5937535827c4fd3446c7609ad87065b55733b1874b2ddc67df04bf0"
                  TrackedTreeSha256 = "68a11d403846de3af26759eef97f4a35eff5e71d561d41ea17d96e535c171556" } }

    let compareRequirements componentIndex (expected: QuintCacheRequirement list) (actual: QuintCacheRequirement list) =
        let basePath = $"/components/%d{componentIndex}/objects"

        [ let duplicateIds =
              actual
              |> List.countBy (fun item -> item.Id)
              |> List.filter (fun (_, count) -> count > 1)

          for id, _ in duplicateIds do
              yield diagnostic "QUINT-TOOLCHAIN-OBJECT-DUPLICATE" basePath $"Cache object id '%s{id}' is duplicated."

          let expectedIds = expected |> List.map (fun item -> item.Id) |> Set.ofList

          for item in actual |> List.filter (fun item -> not (Set.contains item.Id expectedIds)) do
              yield
                  diagnostic
                      "QUINT-TOOLCHAIN-OBJECT-UNDECLARED"
                      basePath
                      $"Cache object '%s{item.Id}' is not part of the exact Q1-qualified closure."

          for expectedItem in expected |> List.sortBy (fun item -> item.Id) do
              let path = $"%s{basePath}/%s{expectedItem.Id}"

              match actual |> List.tryFind (fun item -> item.Id = expectedItem.Id) with
              | None ->
                  yield
                      diagnostic
                          "QUINT-TOOLCHAIN-OBJECT-MISSING"
                          path
                          $"Required cache object '%s{expectedItem.Id}' is missing from the manifest."
              | Some actualItem ->
                  if actualItem.Kind <> expectedItem.Kind then
                      yield
                          diagnostic
                              "QUINT-TOOLCHAIN-OBJECT-KIND-MISMATCH"
                              (path + "/kind")
                              $"Cache object '%s{expectedItem.Id}' has the wrong object kind."

                  if actualItem.Sha256 <> expectedItem.Sha256 then
                      yield
                          diagnostic
                              "QUINT-TOOLCHAIN-OBJECT-DIGEST-MISMATCH"
                              (path + "/sha256")
                              $"Expected SHA-256 '%s{expectedItem.Sha256}' but found '%s{actualItem.Sha256}'."

                  if actualItem.Bytes <> expectedItem.Bytes then
                      yield
                          diagnostic
                              "QUINT-TOOLCHAIN-OBJECT-SIZE-MISMATCH"
                              (path + "/bytes")
                              $"Cache object '%s{expectedItem.Id}' does not match the exact Q1 byte-count identity." ]

[<RequireQualifiedAccess>]
module QuintToolchain =
    let schema = "fsgg.quint.toolchain-manifest/v1"
    let profile = "fsgg-quint-profile/1"
    let q1 = QuintToolchainInternal.exact
    let encodeCanonical manifest = QuintToolchainInternal.encode manifest

    let fingerprint manifest =
        manifest |> encodeCanonical |> QuintToolchainInternal.sha256

    let validateManifest (manifest: QuintToolchainManifest) =
        let expected = q1
        let components = manifest.Components |> List.sortBy (fun item -> item.Id)
        let expectedComponents = expected.Components |> List.sortBy (fun item -> item.Id)

        [ if manifest.Schema <> expected.Schema then
              yield
                  QuintToolchainInternal.diagnostic
                      "QUINT-TOOLCHAIN-SCHEMA-MISMATCH"
                      "/schema"
                      $"Expected '%s{expected.Schema}' but found '%s{manifest.Schema}'."

          if manifest.Profile <> expected.Profile then
              yield
                  QuintToolchainInternal.diagnostic
                      "QUINT-TOOLCHAIN-PROFILE-MISMATCH"
                      "/profile"
                      $"Expected '%s{expected.Profile}' but found '%s{manifest.Profile}'."

          if manifest.Platform <> expected.Platform then
              yield
                  QuintToolchainInternal.diagnostic
                      "QUINT-TOOLCHAIN-PLATFORM-MISMATCH"
                      "/platform"
                      $"Expected '%s{expected.Platform}' but found '%s{manifest.Platform}'."

          for id, _ in
              components
              |> List.countBy (fun item -> item.Id)
              |> List.filter (fun (_, count) -> count > 1) do
              yield
                  QuintToolchainInternal.diagnostic
                      "QUINT-TOOLCHAIN-COMPONENT-DUPLICATE"
                      "/components"
                      $"Tool component id '%s{id}' is duplicated."

          if List.length components <> List.length expectedComponents then
              yield
                  QuintToolchainInternal.diagnostic
                      "QUINT-TOOLCHAIN-COMPONENT-SET-MISMATCH"
                      "/components"
                      "Tool components must match the exact Q1-qualified closure."

          for index, expectedComponent in expectedComponents |> List.indexed do
              match components |> List.tryFind (fun item -> item.Id = expectedComponent.Id) with
              | None ->
                  yield
                      QuintToolchainInternal.diagnostic
                          "QUINT-TOOLCHAIN-COMPONENT-MISSING"
                          "/components"
                          $"Required tool component '%s{expectedComponent.Id}' is missing."
              | Some actual ->
                  if
                      actual.Version <> expectedComponent.Version
                      || actual.Source <> expectedComponent.Source
                  then
                      yield
                          QuintToolchainInternal.diagnostic
                              "QUINT-TOOLCHAIN-COMPONENT-MISMATCH"
                              $"/components/%d{index}"
                              $"Tool component '%s{expectedComponent.Id}' does not match its exact Q1 identity."

                  yield! QuintToolchainInternal.compareRequirements index expectedComponent.Objects actual.Objects

          match manifest.Guidance with
          | None -> ()
          | Some actual when Some actual = expected.Guidance -> ()
          | Some _ ->
              yield
                  QuintToolchainInternal.diagnostic
                      "QUINT-GUIDANCE-IDENTITY-MISMATCH"
                      "/guidance"
                      "Optional guidance must match the exact reviewed Apache-2.0 snapshot." ]
        |> QuintToolchainInternal.sortDiagnostics

    let validateCache (manifest: QuintToolchainManifest) (observations: QuintCacheObservation list) =
        let required =
            manifest.Components |> List.collect (fun toolComponent -> toolComponent.Objects)

        [ yield! validateManifest manifest

          for id, _ in
              observations
              |> List.countBy (fun item -> item.Id)
              |> List.filter (fun (_, count) -> count > 1) do
              yield
                  QuintToolchainInternal.diagnostic
                      "QUINT-CACHE-OBSERVATION-DUPLICATE"
                      "/cache"
                      $"Cache object '%s{id}' has more than one observation."

          for requirement in required |> List.sortBy (fun item -> item.Id) do
              let path = $"/cache/%s{requirement.Id}"

              match observations |> List.tryFind (fun item -> item.Id = requirement.Id) with
              | None
              | Some { State = Absent } ->
                  yield
                      QuintToolchainInternal.diagnostic
                          "QUINT-CACHE-OBJECT-ABSENT"
                          path
                          $"Required cache object '%s{requirement.Id}' is absent; preseed the exact content-addressed object."
              | Some { State = Unreadable detail } ->
                  yield
                      QuintToolchainInternal.diagnostic
                          "QUINT-CACHE-OBJECT-UNREADABLE"
                          path
                          $"Required cache object '%s{requirement.Id}' could not be read: %s{detail}"
              | Some observation when observation.Kind <> requirement.Kind ->
                  yield
                      QuintToolchainInternal.diagnostic
                          "QUINT-CACHE-OBJECT-KIND-MISMATCH"
                          path
                          $"Cache object '%s{requirement.Id}' has the wrong object kind."
              | Some { State = Present(_, _, false) } ->
                  yield
                      QuintToolchainInternal.diagnostic
                          "QUINT-CACHE-OBJECT-INCOMPLETE"
                          path
                          $"Cache object '%s{requirement.Id}' is incomplete."
              | Some { State = Present(sha, bytes, true) } ->
                  if not (QuintToolchainInternal.isSha256 sha) || sha <> requirement.Sha256 then
                      yield
                          QuintToolchainInternal.diagnostic
                              "QUINT-CACHE-OBJECT-DIGEST-MISMATCH"
                              path
                              $"Cache object '%s{requirement.Id}' expected SHA-256 '%s{requirement.Sha256}' but found '%s{sha}'."

                  match requirement.Bytes, bytes with
                  | Some expectedBytes, Some actualBytes when expectedBytes <> actualBytes ->
                      yield
                          QuintToolchainInternal.diagnostic
                              "QUINT-CACHE-OBJECT-SIZE-MISMATCH"
                              path
                              $"Cache object '%s{requirement.Id}' expected %d{expectedBytes} bytes but found %d{actualBytes}."
                  | Some _, None ->
                      yield
                          QuintToolchainInternal.diagnostic
                              "QUINT-CACHE-OBJECT-SIZE-UNOBSERVED"
                              path
                              $"Cache object '%s{requirement.Id}' requires an observed byte count."
                  | _ -> ()

          let requiredIds = required |> List.map (fun item -> item.Id) |> Set.ofList

          for observation in observations |> List.filter (fun item -> not (Set.contains item.Id requiredIds)) do
              yield
                  QuintToolchainInternal.diagnostic
                      "QUINT-CACHE-OBJECT-UNDECLARED"
                      $"/cache/%s{observation.Id}"
                      $"Cache observation '%s{observation.Id}' is not declared by the toolchain manifest." ]
        |> QuintToolchainInternal.sortDiagnostics

    let plan
        (manifest: QuintToolchainManifest)
        (observations: QuintCacheObservation list)
        (requests: QuintProcessRequest list)
        =
        let cacheDiagnostics = validateCache manifest observations

        let requestDiagnostics =
            [ for id, _ in
                  requests
                  |> List.countBy (fun item -> item.StepId)
                  |> List.filter (fun (_, count) -> count > 1) do
                  yield
                      QuintToolchainInternal.diagnostic
                          "QUINT-PLAN-STEP-DUPLICATE"
                          "/requests"
                          $"Process step id '%s{id}' is duplicated."

              let objectIds =
                  manifest.Components
                  |> List.collect (fun toolComponent -> toolComponent.Objects)
                  |> List.map (fun item -> item.Id)
                  |> Set.ofList

              for index, request in requests |> List.indexed do
                  let path = $"/requests/%d{index}"

                  if String.IsNullOrWhiteSpace request.StepId then
                      yield
                          QuintToolchainInternal.diagnostic
                              "QUINT-PLAN-STEP-ID-REQUIRED"
                              (path + "/stepId")
                              "Step id is required."

                  if not (Set.contains request.ExecutableObjectId objectIds) then
                      yield
                          QuintToolchainInternal.diagnostic
                              "QUINT-PLAN-EXECUTABLE-UNDECLARED"
                              (path + "/executableObjectId")
                              "The executable must be a verified object declared by the manifest."

                  if not (QuintToolchainInternal.isSafeToken request.WorkingDirectory) then
                      yield
                          QuintToolchainInternal.diagnostic
                              "QUINT-PLAN-WORKDIR-UNSAFE"
                              (path + "/workingDirectory")
                              "Working directory must be a non-empty relative isolated-directory token."

                  if requests <> (requests |> List.sortBy (fun item -> item.StepId)) then
                      yield
                          QuintToolchainInternal.diagnostic
                              "QUINT-PLAN-STEP-ORDER-MISMATCH"
                              "/requests"
                              "Process requests must be ordered by stable step id."

                  if
                      request.Arguments
                      |> List.exists (fun value ->
                          value.Contains("http://", StringComparison.OrdinalIgnoreCase)
                          || value.Contains("https://", StringComparison.OrdinalIgnoreCase)
                          || value.Contains("@latest", StringComparison.OrdinalIgnoreCase))
                  then
                      yield
                          QuintToolchainInternal.diagnostic
                              "QUINT-PLAN-ACQUISITION-REFUSED"
                              (path + "/arguments")
                              "Compilation cannot express a network URI or moving installer."

                  if request.Environment <> (request.Environment |> List.sortBy fst) then
                      yield
                          QuintToolchainInternal.diagnostic
                              "QUINT-PLAN-ENVIRONMENT-ORDER-MISMATCH"
                              (path + "/environment")
                              "Environment bindings must be ordered by name."

                  for name, _ in
                      request.Environment
                      |> List.countBy fst
                      |> List.filter (fun (_, count) -> count > 1) do
                      yield
                          QuintToolchainInternal.diagnostic
                              "QUINT-PLAN-ENVIRONMENT-DUPLICATE"
                              (path + "/environment")
                              $"Environment variable '%s{name}' is duplicated."

                  for name, value in request.Environment do
                      if String.IsNullOrWhiteSpace name then
                          yield
                              QuintToolchainInternal.diagnostic
                                  "QUINT-PLAN-ENVIRONMENT-NAME-REQUIRED"
                                  (path + "/environment")
                                  "Environment variable names must be non-empty."
                      elif
                          name.Contains("PROXY", StringComparison.OrdinalIgnoreCase)
                          || value.Contains("http://", StringComparison.OrdinalIgnoreCase)
                          || value.Contains("https://", StringComparison.OrdinalIgnoreCase)
                          || value.Contains("@latest", StringComparison.OrdinalIgnoreCase)
                      then
                          yield
                              QuintToolchainInternal.diagnostic
                                  "QUINT-PLAN-NETWORK-ENVIRONMENT-REFUSED"
                                  (path + "/environment")
                                  "Compilation cannot express proxy, network URI, or moving-install environment bindings." ]
            |> QuintToolchainInternal.sortDiagnostics

        match QuintToolchainInternal.sortDiagnostics (cacheDiagnostics @ requestDiagnostics) with
        | [] ->
            Ok
                { ManifestSha256 = fingerprint manifest
                  RequiredObjects =
                    manifest.Components
                    |> List.collect (fun toolComponent -> toolComponent.Objects)
                    |> List.sortBy (fun item -> item.Id)
                  Requests = requests }
        | diagnostics -> Error diagnostics

    let validateExecution
        (plan: QuintCompilationPlan)
        (endpoint: QuintEndpointState)
        (observations: QuintProcessObservation list)
        =
        [ match endpoint with
          | Available -> ()
          | Occupied detail ->
              yield
                  QuintToolchainInternal.diagnostic
                      "QUINT-EXECUTION-ENDPOINT-OCCUPIED"
                      "/endpoint"
                      $"The dedicated local server endpoint is already occupied: %s{detail}"

          for id, _ in
              observations
              |> List.countBy (fun item -> item.StepId)
              |> List.filter (fun (_, count) -> count > 1) do
              yield
                  QuintToolchainInternal.diagnostic
                      "QUINT-EXECUTION-OBSERVATION-DUPLICATE"
                      "/observations"
                      $"Process step '%s{id}' has more than one observation."

          for request in plan.Requests do
              let path = $"/observations/%s{request.StepId}"

              match observations |> List.tryFind (fun item -> item.StepId = request.StepId) with
              | None ->
                  yield
                      QuintToolchainInternal.diagnostic
                          "QUINT-EXECUTION-OBSERVATION-ABSENT"
                          path
                          $"Process step '%s{request.StepId}' has no effect-edge observation."
              | Some { Outcome = Failed(exitCode, detail) } ->
                  yield
                      QuintToolchainInternal.diagnostic
                          "QUINT-EXECUTION-PROCESS-FAILED"
                          path
                          $"Process step '%s{request.StepId}' failed with exit code %d{exitCode}: %s{detail}"
              | Some { Outcome = Succeeded } -> ()

          let stepIds = plan.Requests |> List.map (fun item -> item.StepId) |> Set.ofList

          for observation in observations |> List.filter (fun item -> not (Set.contains item.StepId stepIds)) do
              yield
                  QuintToolchainInternal.diagnostic
                      "QUINT-EXECUTION-OBSERVATION-UNDECLARED"
                      $"/observations/%s{observation.StepId}"
                      $"Observation '%s{observation.StepId}' does not bind a planned process step." ]
        |> QuintToolchainInternal.sortDiagnostics
