#load "../../src/FS.GG.SDD.Artifacts/TypedSpecifications/QuintProfile.fs"

open System
open System.IO
open System.Text.Json.Nodes
open FS.GG.SDD.Artifacts.TypedSpecifications

let fail message =
    raise (InvalidOperationException message)

let expectCode code =
    function
    | Error findings when findings |> List.exists (fun finding -> finding.Code = code) -> ()
    | Error findings -> fail $"Expected {code}, got {findings |> List.map _.Code}"
    | Ok _ -> fail $"Expected refusal {code}."

let source path line =
    { Path = path
      Start = { Line = line; Column = 5 }
      End = { Line = line; Column = 100 } }

let binding sourcePath moduleName catalogue id kind line =
    { ModuleName = moduleName
      CatalogueName = catalogue
      Id = id
      Kind = kind
      Source = source sourcePath line }

let requirementsPath =
    "docs/experiments/quint-q1/slices/requirements-and-evidence.md"

let requirementsBindings =
    [ binding requirementsPath "RequirementsSlice" "requirements" "REQ-AUDIT-001" Requirement 19
      binding requirementsPath "RequirementsSlice" "evidenceCatalogue" "EV-VERIFY-001" Evidence 23
      binding requirementsPath "RequirementsSlice" "actionCatalogue" "ObserveEvidence" Action 26
      binding requirementsPath "RequirementsSlice" "actionCatalogue" "AcceptRequirement" Action 27
      binding requirementsPath "RequirementsSlice" "propertyCatalogue" "AcceptedOnlyWithEvidence" Invariant 30
      binding
          requirementsPath
          "RequirementsSlice"
          "propertyCatalogue"
          "RequirementCanBeAccepted"
          ReachabilityProperty
          31 ]

let fixture =
    if fsi.CommandLineArgs.Length <> 2 && fsi.CommandLineArgs.Length <> 4 then
        fail "Pass requirements, and optionally S.I.R. plus coordination, exact typecheck JSON paths."

    File.ReadAllText fsi.CommandLineArgs[1]

let observation sourceBindings text =
    { Profile = QuintProfile.identity
      QuintVersion = QuintProfile.quintVersion
      TypedEffectJson = text
      SourceBindings = sourceBindings }

match QuintProfile.adaptTypedEffectJson (observation requirementsBindings fixture) with
| Error findings -> fail $"Exact capture was refused: {findings}"
| Ok catalogue ->
    if catalogue.Entries.Length <> 6 then
        fail $"Expected 6 rows, got {catalogue.Entries.Length}."

    let accept =
        catalogue.ActionEffects
        |> List.find (fun effect -> effect.ActionId = "AcceptRequirement")

    if
        accept.Reads <> [ "AuditRequirement"; "ObservedEvidence" ]
        || accept.Writes <> [ "AcceptedRequirements" ]
    then
        fail $"Unexpected stable effect projection: {accept}"

expectCode
    "QUINT-PROFILE-VERSION"
    (QuintProfile.adaptTypedEffectJson
        { observation requirementsBindings fixture with
            QuintVersion = "0.32.1" })

expectCode
    "QUINT-PROFILE-VERSION-MISSING"
    (QuintProfile.adaptTypedEffectJson
        { observation requirementsBindings fixture with
            QuintVersion = "" })

expectCode
    "QUINT-PROFILE-IDENTITY"
    (QuintProfile.adaptTypedEffectJson
        { observation requirementsBindings fixture with
            Profile = "fsgg-quint-profile/2" })

expectCode
    "QUINT-IR-SOURCE-BINDING-REQUIRED"
    (QuintProfile.adaptTypedEffectJson
        { observation requirementsBindings fixture with
            SourceBindings = requirementsBindings.Tail })

let mutate (edit: JsonObject -> unit) =
    let root = JsonNode.Parse(fixture).AsObject()
    edit root
    root.ToJsonString()

expectCode
    "QUINT-IR-UNSUPPORTED-FIELD"
    (QuintProfile.adaptTypedEffectJson (observation requirementsBindings (mutate (fun root -> root["future"] <- 1))))

expectCode
    "QUINT-IR-STAGE"
    (QuintProfile.adaptTypedEffectJson (
        observation requirementsBindings (mutate (fun root -> root["stage"] <- "parsing"))
    ))

expectCode
    "QUINT-IR-COMPILER-WARNING"
    (QuintProfile.adaptTypedEffectJson (
        observation requirementsBindings (mutate (fun root -> root["warnings"].AsArray().Add("warning")))
    ))

let catalogueExpression name (root: JsonObject) =
    let modules = root["modules"].AsArray()
    let firstModule = modules[0].AsObject()
    let declarations = firstModule["declarations"].AsArray()

    declarations
    |> Seq.map _.AsObject()
    |> Seq.find (fun declaration -> declaration["name"] <> null && declaration["name"].GetValue<string>() = name)
    |> fun declaration -> declaration["expr"].AsObject()

expectCode
    "QUINT-IR-UNSUPPORTED-OPCODE"
    (QuintProfile.adaptTypedEffectJson (
        observation
            requirementsBindings
            (mutate (fun root -> (catalogueExpression "actionCatalogue" root)["opcode"] <- "List"))
    ))

expectCode
    "QUINT-IR-EXPRESSION-KIND"
    (QuintProfile.adaptTypedEffectJson (
        observation
            requirementsBindings
            (mutate (fun root -> (catalogueExpression "actionCatalogue" root)["kind"] <- "name"))
    ))

let propertyKind (root: JsonObject) =
    let expression = catalogueExpression "propertyCatalogue" root
    let rows = expression["args"].AsArray()
    let firstRecord = rows[0].AsObject()
    let fields = firstRecord["args"].AsArray()
    (fields[3].AsObject())["value"] <- JsonValue.Create("safety")

expectCode
    "QUINT-IR-PROPERTY-KIND"
    (QuintProfile.adaptTypedEffectJson (observation requirementsBindings (mutate propertyKind)))

if fsi.CommandLineArgs.Length = 4 then
    let sirPath = "docs/experiments/quint-q1/slices/sir-damage-rule.md"

    let sirBindings =
        [ binding sirPath "SirDamageSlice" "actions" "Initialize" Action 16
          binding sirPath "SirDamageSlice" "actions" "ApplyDamage" Action 17
          binding sirPath "SirDamageSlice" "propertyCatalogue" "NonNegativeHitPoints" Invariant 20
          binding sirPath "SirDamageSlice" "propertyCatalogue" "KnownLastAction" Invariant 21
          binding sirPath "SirDamageSlice" "propertyCatalogue" "DamageCanReachZero" ReachabilityProperty 22 ]

    let coordinationPath = "docs/experiments/quint-q1/slices/coordination-process.md"

    let coordinationBindings =
        [ for id, line in
              [ "Prepare", 19
                "Interfere", 20
                "Apply", 21
                "RefuseStale", 22
                "LoseResponse", 23
                "Retry", 24
                "Refresh", 25
                "Complete", 26 ] do
              binding coordinationPath "CoordinationSlice" "actionCatalogue" id Action line

          for id, kind, line in
              [ "AtMostOneApply", Invariant, 29
                "ReceiptMatchesApply", Invariant, 30
                "CompleteHasReceipt", Invariant, 31
                "StaleNeverApplies", Invariant, 32
                "StaleRefusalNeverApplies", Invariant, 33
                "KnownPhase", Invariant, 34
                "EventualCompletion", TemporalProperty, 35 ] do
              binding coordinationPath "CoordinationSlice" "propertyCatalogue" id kind line ]

    let assertCorpus name expectedRows sourceBindings path =
        let text = File.ReadAllText path

        match QuintProfile.adaptTypedEffectJson (observation sourceBindings text) with
        | Error findings -> fail $"{name} exact capture was refused: {findings}"
        | Ok catalogue when catalogue.Entries.Length <> expectedRows ->
            fail $"{name}: expected {expectedRows} rows, got {catalogue.Entries.Length}."
        | Ok _ -> ()

    assertCorpus "S.I.R." 5 sirBindings fsi.CommandLineArgs[2]
    assertCorpus "coordination" 15 coordinationBindings fsi.CommandLineArgs[3]

printfn "Exact Quint 0.32.0 Q1 IR corpus and 9 fail-closed mutations passed."
