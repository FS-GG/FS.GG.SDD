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

    let create major = { Major = major; Minor = None; Raw = string major }

    let parse (value: string) =
        let value = if isNull value then "" else value.Trim()
        let m = Regex.Match(value, @"^(\d+)(?:\.(\d+))?$")

        if m.Success then
            let minor =
                if m.Groups[2].Success then
                    Some(Int32.Parse m.Groups[2].Value)
                else
                    None

            Ok { Major = Int32.Parse m.Groups[1].Value; Minor = minor; Raw = value }
        else
            Error "Schema version must be an integer or major.minor value."

    let isSupported version = version.Major = 1

    let isSha256 (value: string) =
        Regex.IsMatch(value, @"^[a-f0-9]{64}$", RegexOptions.CultureInvariant)

    let createSourceDigest (algorithm: string) (value: string) =
        let algorithm = if isNull algorithm then "" else algorithm.Trim().ToLowerInvariant()
        let value = if isNull value then "" else value.Trim()

        if algorithm <> "sha256" then
            Error "Only sha256 source digests are supported."
        elif isSha256 value then
            Ok ({ Algorithm = algorithm; Value = value } : SourceDigest)
        else
            Error "SHA-256 digests must be lowercase hexadecimal."

    let createOutputDigest (algorithm: string) (value: string) =
        let algorithm = if isNull algorithm then "" else algorithm.Trim().ToLowerInvariant()
        let value = if isNull value then "" else value.Trim()

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
        let bytes = Encoding.UTF8.GetBytes(if isNull text then "" else text.Replace("\r\n", "\n"))
        let digest = SHA256.HashData bytes |> hex
        ({ Algorithm = "sha256"; Value = digest } : SourceDigest)

    let outputSha256Text text =
        let digest = sha256Text text
        ({ Algorithm = digest.Algorithm; Value = digest.Value } : OutputDigest)

    let createGeneratorVersion (id: string) (version: string) =
        let id = if isNull id then "" else id.Trim()
        let version = if isNull version then "" else version.Trim()

        if String.IsNullOrWhiteSpace id then
            Error "Generator id is required."
        elif String.IsNullOrWhiteSpace version then
            Error "Generator version is required."
        else
            Ok { Id = id; Version = version }
