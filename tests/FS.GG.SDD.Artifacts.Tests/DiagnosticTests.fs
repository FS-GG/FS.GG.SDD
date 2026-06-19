namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module DiagnosticTests =
    let artifact =
        ArtifactRef.create "work/001-sdd-artifact-model/tasks.yml" ArtifactRef.ArtifactKind.Tasks ArtifactRef.ArtifactOwner.Sdd true
        |> Result.defaultWith failwith

    [<Fact>]
    let ``Diagnostic factories create actionable findings`` () =
        let diagnostic = Diagnostics.unknownReference artifact "FR-999" "Declare FR-999 or remove the reference."

        Assert.Equal("unknownReference", diagnostic.Id)
        Assert.Equal(Diagnostics.DiagnosticSeverity.DiagnosticError, diagnostic.Severity)
        Assert.Equal("Declare FR-999 or remove the reference.", diagnostic.Correction)
        Assert.Contains("FR-999", diagnostic.RelatedIds)

    [<Fact>]
    let ``Diagnostics sort by severity id artifact and location`` () =
        let warning = Diagnostics.proseStructuredMismatch artifact "Mismatch." "Update prose."
        let error = Diagnostics.missingArtifact artifact "Create the task file."
        let sorted = Diagnostics.sort [ warning; error ]

        Assert.Equal("missingArtifact", sorted.Head.Id)
        Assert.True(Diagnostics.hasBlocking sorted)
