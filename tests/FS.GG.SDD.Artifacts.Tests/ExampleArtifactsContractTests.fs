namespace FS.GG.SDD.Artifacts.Tests

open System.IO
open FS.GG.SDD.Artifacts
open Xunit

/// FR-004 drift guard (#106 §1.4): every shipped worked example under
/// docs/examples/lifecycle-artifacts/ is parsed through the LIVE public artifact
/// parser on each build. If an example ever contradicts the tool — a mistyped id,
/// a broken coverage line, an unsatisfiable evidence block — this fails, so the
/// copy-adaptable references can never rot.
module ExampleArtifactsContractTests =

    let private examplesDir =
        Path.Combine(TestSupport.repoRoot, "docs", "examples", "lifecycle-artifacts")

    let private snapshot name : FileSnapshot =
        let path = Path.Combine(examplesDir, name)

        { Path = $"work/001-example/{name}"
          Text = File.ReadAllText path }

    /// A diagnostic that would block the stage (error severity). Advisory
    /// missing-section notes on an isolated artifact are acceptable in an example.
    let private blocking (diagnostics: Diagnostics.Diagnostic list) =
        diagnostics
        |> List.filter (fun diagnostic -> diagnostic.Severity = Diagnostics.DiagnosticSeverity.DiagnosticError)

    [<Fact>]
    let ``Example clarifications.md parses with no blocking diagnostics and zero blocking ambiguities`` () =
        match Clarification.parseClarificationFacts (snapshot "clarifications.md") with
        | Error diagnostics -> failwith $"Example clarifications.md did not parse: {diagnostics}"
        | Ok facts ->
            Assert.Empty(blocking facts.Diagnostics)
            Assert.Equal(0, facts.BlockingAmbiguityCount)

    [<Fact>]
    let ``Example checklist.md parses with no blocking diagnostics and no blocking findings`` () =
        match Checklist.parseChecklistFacts (snapshot "checklist.md") with
        | Error diagnostics -> failwith $"Example checklist.md did not parse: {diagnostics}"
        | Ok facts ->
            Assert.Empty(blocking facts.Diagnostics)
            Assert.Empty(facts.BlockingFindings)
            Assert.Equal("checklistReady", facts.FrontMatter.Status)

    [<Fact>]
    let ``Example tasks.yml parses into a non-empty task graph`` () =
        match Task.parseTaskFacts (snapshot "tasks.yml") with
        | Error diagnostics -> failwith $"Example tasks.yml did not parse: {diagnostics}"
        | Ok facts ->
            Assert.Empty(blocking facts.Diagnostics)
            Assert.NotEmpty facts.Tasks

    [<Fact>]
    let ``Example spec.md parses with no blocking diagnostics and declares stable requirement ids`` () =
        match Specification.parseSpecificationFacts (snapshot "spec.md") with
        | Error diagnostics -> failwith $"Example spec.md did not parse: {diagnostics}"
        | Ok facts ->
            Assert.Empty(blocking facts.Diagnostics)
            Assert.NotEmpty facts.RequirementIds

    [<Fact>]
    let ``Example plan.md parses with no blocking diagnostics and records plan decisions`` () =
        match Plan.parsePlanFacts (snapshot "plan.md") with
        | Error diagnostics -> failwith $"Example plan.md did not parse: {diagnostics}"
        | Ok facts ->
            Assert.Empty(blocking facts.Diagnostics)
            Assert.NotEmpty facts.Decisions

    [<Fact>]
    let ``Example evidence.yml declarations all satisfy (result pass, non-synthetic)`` () =
        match Evidence.parseEvidence (snapshot "evidence.yml") with
        | Error diagnostics -> failwith $"Example evidence.yml did not parse: {diagnostics}"
        | Ok declarations ->
            Assert.NotEmpty declarations

            Assert.All(
                declarations,
                fun declaration ->
                    Assert.True(
                        declaration.Result.Trim().ToLowerInvariant() = "pass"
                        && not declaration.Synthetic,
                        $"Example evidence declaration {declaration.Id.Value} should model a satisfying declaration."
                    )
            )
