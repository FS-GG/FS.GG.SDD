open System
open System.IO
open System.Security.Cryptography
open System.Text
open FS.GG.SDD.Artifacts.TypedSpecifications

let fail message =
    failwith ("PROFILE-2-SIR-REFUSAL: " + message)

let expect label =
    function
    | Ok value -> value
    | Error findings -> fail (sprintf "%s: %A" label findings)

let sha256 (text: string) =
    text
    |> Encoding.UTF8.GetBytes
    |> SHA256.HashData
    |> Convert.ToHexString
    |> _.ToLowerInvariant()

match fsi.CommandLineArgs |> Array.skip 1 with
| [| typedEffectPath; selectorPath; outputRoot |] ->
    let selectors =
        File.ReadAllText selectorPath
        |> QuintGeneralBindingManifest.deserialize
        |> expect "selector manifest"

    let typedEffect = File.ReadAllText typedEffectPath

    let catalogue =
        QuintGeneralProfile.adaptTypedEffectJson
            { Profile = selectors.Profile
              QuintVersion = QuintGeneralProfile.quintVersion
              TypedEffectJson = typedEffect
              ExportBindings = selectors.Exports
              ActionBindings = selectors.Actions }
        |> expect "typed/effect adaptation"

    let rules =
        catalogue.Catalogue |> List.filter (fun row -> row.ExportId = "EXPORT-Rules")

    let properties =
        catalogue.Catalogue
        |> List.filter (fun row -> row.ExportId = "EXPORT-Properties")

    if
        rules.Length <> 16
        || properties.Length <> 7
        || catalogue.ActionEffects.Length <> 5
    then
        fail (
            sprintf
                "expected 16 rules, 7 properties, and 5 actions; got %d/%d/%d"
                rules.Length
                properties.Length
                catalogue.ActionEffects.Length
        )

    let contract: QuintCompiledContractV2 =
        { Schema = QuintContractV2.schema
          Profile = catalogue.Profile
          Specification = "SirCombat"
          Exports = catalogue.Exports
          Catalogue = catalogue.Catalogue
          ActionEffects = catalogue.ActionEffects
          Relationships = []
          VerificationProfiles = []
          Bounds = []
          Impacts = []
          Compatibility = []
          Digests =
            [ { Name = "typed-effect"
                Sha256 = sha256 typedEffect } ] }

    let canonical = QuintContractV2.serializeCanonical contract |> expect "contract"

    let bindings =
        QuintBindingsV2.generate selectors.ModuleName contract |> expect "bindings"

    Directory.CreateDirectory outputRoot |> ignore
    File.WriteAllText(Path.Combine(outputRoot, "contract.json"), canonical)
    File.WriteAllText(Path.Combine(outputRoot, "bindings.fs"), bindings.FSharpSource)
    File.WriteAllText(Path.Combine(outputRoot, "bindings.fable.fs"), bindings.FableSource)

    File.WriteAllText(
        Path.Combine(outputRoot, "native.txt"),
        String.concat
            "\n"
            [ bindings.ContractFingerprint
              String.concat "," (rules |> List.map _.Id)
              canonical ]
        + "\n"
    )

    printfn "PROFILE-2-SIR-ACCEPTED: rules=16 properties=7 actions=5 fingerprint=%s" bindings.ContractFingerprint
| _ -> fail "expected <typed-effect.json> <profile-bindings.json> <output-root>"
