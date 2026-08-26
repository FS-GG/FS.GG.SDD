#load "../../src/FS.GG.SDD.Artifacts/TypedSpecifications/QuintProfile.fs"

open System
open System.IO
open System.Text.Json.Nodes
open FS.GG.SDD.Artifacts.TypedSpecifications

let fail message = raise (InvalidOperationException message)
let expectCode code = function
    | Error findings when findings |> List.exists (fun finding -> finding.Code = code) -> ()
    | Error findings -> fail $"Expected {code}, got {findings |> List.map _.Code}"
    | Ok _ -> fail $"Expected refusal {code}."

let source line =
    { Path = "docs/experiments/quint-q1/slices/requirements-and-evidence.md"
      Start = { Line = line; Column = 5 }
      End = { Line = line; Column = 100 } }

let binding catalogue id kind line =
    { ModuleName = "RequirementsSlice"; CatalogueName = catalogue; Id = id; Kind = kind; Source = source line }

let bindings =
    [ binding "requirements" "REQ-AUDIT-001" Requirement 19
      binding "evidenceCatalogue" "EV-VERIFY-001" Evidence 23
      binding "actionCatalogue" "ObserveEvidence" Action 26
      binding "actionCatalogue" "AcceptRequirement" Action 27
      binding "propertyCatalogue" "AcceptedOnlyWithEvidence" Invariant 30
      binding "propertyCatalogue" "RequirementCanBeAccepted" ReachabilityProperty 31 ]

let fixture =
    if fsi.CommandLineArgs.Length <> 2 then fail "Pass the exact requirements.qnt typecheck --out JSON path."
    File.ReadAllText fsi.CommandLineArgs[1]

let observation text =
    { Profile = QuintProfile.identity
      QuintVersion = QuintProfile.quintVersion
      TypedEffectJson = text
      SourceBindings = bindings }

match QuintProfile.adaptTypedEffectJson (observation fixture) with
| Error findings -> fail $"Exact capture was refused: {findings}"
| Ok catalogue ->
    if catalogue.Entries.Length <> 6 then fail $"Expected 6 rows, got {catalogue.Entries.Length}."
    let accept = catalogue.ActionEffects |> List.find (fun effect -> effect.ActionId = "AcceptRequirement")
    if accept.Reads <> [ "AuditRequirement"; "ObservedEvidence" ] || accept.Writes <> [ "AcceptedRequirements" ] then
        fail $"Unexpected stable effect projection: {accept}"

expectCode "QUINT-PROFILE-VERSION" (QuintProfile.adaptTypedEffectJson { observation fixture with QuintVersion = "0.32.1" })
expectCode "QUINT-PROFILE-VERSION-MISSING" (QuintProfile.adaptTypedEffectJson { observation fixture with QuintVersion = "" })
expectCode "QUINT-PROFILE-IDENTITY" (QuintProfile.adaptTypedEffectJson { observation fixture with Profile = "fsgg-quint-profile/2" })
expectCode "QUINT-IR-SOURCE-BINDING-REQUIRED" (QuintProfile.adaptTypedEffectJson { observation fixture with SourceBindings = bindings.Tail })

let mutate (edit: JsonObject -> unit) =
    let root = JsonNode.Parse(fixture).AsObject()
    edit root
    root.ToJsonString()

expectCode "QUINT-IR-UNSUPPORTED-FIELD" (QuintProfile.adaptTypedEffectJson (observation (mutate (fun root -> root["future"] <- 1))))
expectCode "QUINT-IR-STAGE" (QuintProfile.adaptTypedEffectJson (observation (mutate (fun root -> root["stage"] <- "parsing"))))
expectCode "QUINT-IR-COMPILER-WARNING" (QuintProfile.adaptTypedEffectJson (observation (mutate (fun root -> root["warnings"].AsArray().Add("warning")))))

let catalogueExpression name (root: JsonObject) =
    let modules = root["modules"].AsArray()
    let firstModule = modules[0].AsObject()
    let declarations = firstModule["declarations"].AsArray()
    declarations
    |> Seq.map _.AsObject()
    |> Seq.find (fun declaration -> declaration["name"] <> null && declaration["name"].GetValue<string>() = name)
    |> fun declaration -> declaration["expr"].AsObject()

expectCode "QUINT-IR-UNSUPPORTED-OPCODE" (QuintProfile.adaptTypedEffectJson (observation (mutate (fun root -> (catalogueExpression "actionCatalogue" root)["opcode"] <- "List"))))
expectCode "QUINT-IR-EXPRESSION-KIND" (QuintProfile.adaptTypedEffectJson (observation (mutate (fun root -> (catalogueExpression "actionCatalogue" root)["kind"] <- "name"))))

let propertyKind (root: JsonObject) =
    let expression = catalogueExpression "propertyCatalogue" root
    let rows = expression["args"].AsArray()
    let firstRecord = rows[0].AsObject()
    let fields = firstRecord["args"].AsArray()
    (fields[3].AsObject())["value"] <- JsonValue.Create("safety")

expectCode "QUINT-IR-PROPERTY-KIND" (QuintProfile.adaptTypedEffectJson (observation (mutate propertyKind)))
printfn "Exact Quint 0.32.0 IR capture and 9 fail-closed mutations passed."
