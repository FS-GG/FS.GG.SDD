namespace FS.GG.SDD.Artifacts

open System
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions

module SchemaVersion =
    type SchemaVersion = { Major: int; Minor: int option; Raw: string }
    type SourceDigest = { Algorithm: string; Value: string }
    type OutputDigest = { Algorithm: string; Value: string }
    type GeneratorVersion = { Id: string; Version: string }

    type SchemaCompatibilityStatus =
        | Current
        | Deprecated
        | Unsupported
        | Malformed
        | Future

    type SchemaCompatibility =
        { RawValue: string
          Version: SchemaVersion option
          Status: SchemaCompatibilityStatus
          SupportedRange: string
          MigrationHint: string option }

    let create major = { Major = major; Minor = None; Raw = string major }

    let parse (value: string) =
        let value = if String.IsNullOrEmpty value then "" else value.Trim()
        let m = Regex.Match(value, @"^(\d+)(?:\.(\d+))?$")
        let malformed = Error "Schema version must be an integer or major.minor value."

        // The regex constrains each group to digits, so Int32.TryParse only fails
        // on overflow (e.g. schemaVersion: 99999999999999999999). Int32.Parse threw
        // OverflowException there instead of classifying the value as malformed.
        let tryInt (s: string) =
            match Int32.TryParse(s, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture) with
            | true, v -> Some v
            | _ -> None

        if not m.Success then
            malformed
        else
            match tryInt m.Groups[1].Value with
            | None -> malformed
            | Some major ->
                if not m.Groups[2].Success then
                    Ok { Major = major; Minor = None; Raw = value }
                else
                    match tryInt m.Groups[2].Value with
                    | None -> malformed
                    | Some minor -> Ok { Major = major; Minor = Some minor; Raw = value }

    let isSupported version = version.Major = 1

    let statusValue status =
        match status with
        | Current -> "current"
        | Deprecated -> "deprecated"
        | Unsupported -> "unsupported"
        | Malformed -> "malformed"
        | Future -> "future"

    let supportedRange = "1"

    let compatibility raw version status hint =
        { RawValue = raw
          Version = version
          Status = status
          SupportedRange = supportedRange
          MigrationHint = hint }

    let classifyRaw (value: string option) =
        let raw = value |> Option.map (fun value -> value.Trim()) |> Option.defaultValue ""

        if String.IsNullOrWhiteSpace raw then
            compatibility raw None Malformed (Some "Add schemaVersion: 1 to the structured artifact.")
        else
            match parse raw with
            | Error message -> compatibility raw None Malformed (Some message)
            | Ok version when version.Major = 1 ->
                compatibility raw (Some version) Current None
            | Ok version when version.Major = 0 ->
                compatibility raw (Some version) Deprecated (Some "Migrate the artifact to schemaVersion: 1.")
            | Ok version when version.Major = 2 ->
                compatibility raw (Some version) Unsupported (Some "Run the documented migration to schemaVersion: 1 before normalization.")
            | Ok version when version.Major > 2 ->
                compatibility raw (Some version) Future (Some "Use a newer FS.GG.SDD.Artifacts generator or downgrade the artifact schema.")
            | Ok version ->
                compatibility raw (Some version) Unsupported (Some "Use schemaVersion: 1 or add a documented migration path.")

    let isCurrent compatibility = compatibility.Status = Current
    let isDeprecated compatibility = compatibility.Status = Deprecated

    let isBlocking compatibility =
        match compatibility.Status with
        | Current
        | Deprecated -> false
        | Unsupported
        | Malformed
        | Future -> true

    let isSha256 (value: string) =
        Regex.IsMatch(value, @"^[a-f0-9]{64}$", RegexOptions.CultureInvariant)

    let createSourceDigest (algorithm: string) (value: string) =
        let algorithm = if String.IsNullOrEmpty algorithm then "" else algorithm.Trim().ToLowerInvariant()
        let value = if String.IsNullOrEmpty value then "" else value.Trim()

        if algorithm <> "sha256" then
            Error "Only sha256 source digests are supported."
        elif isSha256 value then
            Ok ({ Algorithm = algorithm; Value = value } : SourceDigest)
        else
            Error "SHA-256 digests must be lowercase hexadecimal."

    let createOutputDigest (algorithm: string) (value: string) =
        let algorithm = if String.IsNullOrEmpty algorithm then "" else algorithm.Trim().ToLowerInvariant()
        let value = if String.IsNullOrEmpty value then "" else value.Trim()

        if algorithm <> "sha256" then
            Error "Only sha256 output digests are supported."
        elif isSha256 value then
            Ok ({ Algorithm = algorithm; Value = value } : OutputDigest)
        else
            Error "SHA-256 digests must be lowercase hexadecimal."

    let hex bytes =
        bytes
        |> Array.map (fun (b: byte) -> b.ToString("x2"))
        |> String.concat ""

    let sha256Text (text: string) =
        let bytes = Encoding.UTF8.GetBytes(if String.IsNullOrEmpty text then "" else text.Replace("\r\n", "\n"))
        let digest = SHA256.HashData bytes |> hex
        ({ Algorithm = "sha256"; Value = digest } : SourceDigest)

    let outputSha256Text text =
        let digest = sha256Text text
        ({ Algorithm = digest.Algorithm; Value = digest.Value } : OutputDigest)

    let createGeneratorVersion (id: string) (version: string) =
        let id = if String.IsNullOrEmpty id then "" else id.Trim()
        let version = if String.IsNullOrEmpty version then "" else version.Trim()

        if String.IsNullOrWhiteSpace id then
            Error "Generator id is required."
        elif String.IsNullOrWhiteSpace version then
            Error "Generator version is required."
        else
            Ok { Id = id; Version = version }

    // Derive the generator version from the single <Version> source in
    // Directory.Build.props (baked into the assembly informational version at
    // build time) so the generator version can never drift from the package
    // version (feature 018 / FR-003, T004). The informational version may carry a
    // source-control suffix (e.g. "0.2.0+<sha>"); strip it to the semantic core.
    let assemblyGeneratorVersion () =
        let assembly = typeof<SchemaVersion>.Assembly

        let informational =
            assembly.GetCustomAttributes(typeof<System.Reflection.AssemblyInformationalVersionAttribute>, false)
            |> Array.tryHead
            |> Option.map (fun attr -> (attr :?> System.Reflection.AssemblyInformationalVersionAttribute).InformationalVersion)

        match informational with
        | Some value when not (String.IsNullOrWhiteSpace value) ->
            let value = value.Trim()
            let plus = value.IndexOf('+')
            if plus >= 0 then value.Substring(0, plus) else value
        | _ -> "0.5.0"

    let currentGeneratorVersion () =
        let version = assemblyGeneratorVersion ()
        match createGeneratorVersion "FS.GG.SDD.Artifacts" version with
        | Ok value -> value
        | Error message ->
            failwithf "currentGeneratorVersion: invariant violated — generator version %s/%s rejected: %s" "FS.GG.SDD.Artifacts" version message
