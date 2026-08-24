open System
open FS.GG.SDD.Artifacts.TypedSpecifications

let identifier value =
    match SpecificationId.create value with
    | Ok id -> id
    | Error message -> failwith message

let extension =
    RequirementsDraft.empty
    |> RequirementsDraft.addAcceptance
        { Id = identifier "AC-001"
          StoryIds = [ identifier "US-001" ]
          RequirementIds = [ identifier "FR-001" ]
          Statement = "Compilation succeeds." }
    |> RequirementsDraft.addRequirement
        { Id = identifier "FR-001"
          Statement = "The model compiles."
          AcceptanceIds = [ identifier "AC-001" ]
          EvidenceObligationIds = [] }
    |> RequirementsDraft.addStory
        { Id = identifier "US-001"
          Priority = "P1"
          Statement = "A consumer compiles a typed model." }
    |> RequirementsDraft.addScope
        { Id = identifier "SB-001"
          Statement = "Typed requirements." }
    |> RequirementsDraft.withUserValue "A clean consumer uses the preview."
    |> RequirementsDraft.build

let model =
    { Identity = identifier "SPEC-001"
      SchemaVersion = 1
      Provenance =
        { Agent = "consumer"
          Session = "clean"
          SourcePath = "spec.md"
          SourceRevision = String.replicate 64 "a"
          AuthoredAtUtc = "2026-08-24T12:00:00Z" }
      Intent = "Exercise the public package."
      EvidenceObligations = []
      Extension = extension }

match SpecificationCompiler.compile RequirementsExtension.contract model with
| Error findings -> failwithf "compile failed: %A" findings
| Ok compiled ->
    let json = SpecificationCodec.serialize RequirementsExtension.contract model |> Result.defaultWith (failwithf "%A")
    let decoded = SpecificationCodec.deserialize RequirementsExtension.contract json |> Result.defaultWith (failwithf "%A")
    let projection = SpecificationProjection.generate RequirementsExtension.contract decoded |> Result.defaultWith (failwithf "%A")
    if compiled.Fingerprint.Length <> 64 || String.IsNullOrWhiteSpace projection.Markdown then
        failwith "public kernel output is incomplete"

let forbidden = [ "FS.GG.SIR"; "FS.GG.Coord" ]
let references =
    typeof<SpecificationModel<RequirementsExtension>>.Assembly.GetReferencedAssemblies()
    |> Array.choose (fun assembly -> assembly.Name |> Option.ofObj)

for prefix in forbidden do
    if references |> Array.exists (fun name -> name.StartsWith(prefix, StringComparison.Ordinal)) then
        failwithf "forbidden dependency: %s" prefix

printfn "typed-specification-consumer: ok"
