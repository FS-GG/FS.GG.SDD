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
    let ``Normalized work-model diagnostic factories use stable ids and severities`` () =
        let deprecated = Diagnostics.deprecatedSchemaVersion artifact "0"
        let future = Diagnostics.futureSchemaVersion artifact "3"
        let missing = Diagnostics.missingGeneratedWorkModel artifact "readiness/002-normalized-work-model/work-model.json"
        let untyped = Diagnostics.requirementNotTyped artifact "FR-999" "Declare FR-999."

        Assert.Equal("deprecatedSchemaVersion", deprecated.Id)
        Assert.Equal(Diagnostics.DiagnosticSeverity.DiagnosticWarning, deprecated.Severity)
        Assert.Equal("futureSchemaVersion", future.Id)
        Assert.Equal(Diagnostics.DiagnosticSeverity.DiagnosticError, future.Severity)
        Assert.Equal("missingGeneratedWorkModel", missing.Id)
        Assert.Equal("requirementNotTyped", untyped.Id)

    [<Fact>]
    let ``Diagnostics sort by severity id artifact and location`` () =
        let warning = Diagnostics.proseStructuredMismatch artifact "Mismatch." "Update prose."
        let error = Diagnostics.missingArtifact artifact "Create the task file."
        let sorted = Diagnostics.sort [ warning; error ]

        Assert.Equal("missingArtifact", sorted.Head.Id)
        Assert.True(Diagnostics.hasBlocking sorted)
