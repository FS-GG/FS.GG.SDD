namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open FS.GG.SDD.Commands.WorkModelSourceBundle
open FS.GG.SDD.Commands.WorkModelBlockingDiagnosticsPreview
open Xunit

module WorkModelDuplicateIdBoundaryTests =
    [<Fact>]
    let ``red-before physical source preview passes while duplicate logical work id blocks producer`` () =
        if OperatingSystem.IsLinux() then
            let id = "002-normalized-work-model"
            let outputPath = $"readiness/{id}/work-model.json"
            let sourceRoot =
                Path.Combine(TestSupport.repoRoot, "tests/fixtures/normalized-work-model/valid-work-item")
            let root = Path.Combine(Path.GetTempPath(), "sdd-duplicate-id-" + Guid.NewGuid().ToString("N"))
            let sourcePaths =
                [ ".fsgg/project.yml"; ".fsgg/sdd.yml"; ".fsgg/agents.yml"
                  $"work/{id}/spec.md"; $"work/{id}/tasks.yml"; $"work/{id}/evidence.yml" ]
            Directory.CreateDirectory root |> ignore
            try
                let selected: FileSnapshot list =
                    sourcePaths |> List.map (fun (relative: string) ->
                        let target = Path.Combine(root, relative)
                        Directory.CreateDirectory(
                            Path.GetDirectoryName target
                            |> Option.ofObj
                            |> Option.defaultWith (fun () -> failwith "fixture path has no parent")) |> ignore
                        File.Copy(Path.Combine(sourceRoot, relative), target)
                        let body = File.ReadAllText target
                        { Path = relative
                          Text = if relative.EndsWith("/evidence.yml", StringComparison.Ordinal) then
                                     ViewGeneration.evidenceTextForWorkModel body
                                 else body
                          RawBytes = None })
                let candidate: Candidate =
                    { Version = 2; WorkId = id
                      Sources = selected |> List.map (fun source ->
                          { Path = source.Path; Digest = SchemaVersion.sha256Text source.Text }) }
                let generated =
                    Serialization.generateWorkModel
                        { WorkId = id; Snapshots = selected
                          GeneratorVersion = SchemaVersion.currentGeneratorVersion ()
                          ExpectedOutputPath = Some outputPath }
                Assert.Empty(WorkModel.blockingDiagnostics generated.Model)

                let otherPath = "work/other/spec.md"
                let otherText = TestSupport.validSpec id "Duplicate"
                Directory.CreateDirectory(Path.Combine(root, "work/other")) |> ignore
                File.WriteAllText(Path.Combine(root, otherPath), otherText)
                let request = TestSupport.request Analyze root
                let initial, _ = FS.GG.SDD.Commands.CommandWorkflow.init request
                let otherSnapshot: FileSnapshot =
                    { Path = otherPath; Text = otherText; RawBytes = None }
                let observed: CommandEffectResult =
                    { Effect = ReadFile otherPath
                      Succeeded = true
                      Read = Bytes otherSnapshot
                      Snapshot = Some otherSnapshot
                      Process = None; Confirmed = None; Diagnostic = None }
                let model = { initial with InterpretedEffects = [ observed ] }
                let commandDiagnostics = EarlyStageAuthoring.duplicateWorkIdDiagnostics id model
                Assert.Contains(commandDiagnostics, fun diagnostic ->
                    diagnostic.Id = "duplicateWorkId")
                let spec = selected |> List.find (fun source -> source.Path = $"work/{id}/spec.md")
                let tasks = selected |> List.find (fun source -> source.Path = $"work/{id}/tasks.yml")
                let evidence = selected |> List.find (fun source -> source.Path = $"work/{id}/evidence.yml")
                let _, _, effects, _ =
                    ViewGeneration.generatedViewPlan request id None
                        (Some spec.Text) None None None (Some tasks.Text) (Some evidence.Text)
                        commandDiagnostics model
                Assert.Empty effects

                match verifyPhysical root id outputPath generated.Json selected candidate with
                | Error reason -> failwithf "source-only preview refused: %A" reason
                | Ok preview -> Assert.Equal(outputPath, preview.OutputPath)

                // An unrelated work ID in the same sibling slot is not a duplicate.
                let unrelatedText = TestSupport.validSpec "other" "Unrelated"
                File.WriteAllText(Path.Combine(root, otherPath), unrelatedText)
                let unrelatedSnapshot = { otherSnapshot with Text = unrelatedText }
                let unrelated =
                    { model with
                        InterpretedEffects =
                            [ { observed with Read = Bytes unrelatedSnapshot
                                              Snapshot = Some unrelatedSnapshot } ] }
                Assert.Empty(EarlyStageAuthoring.duplicateWorkIdDiagnostics id unrelated)
            finally Directory.Delete(root, true)
