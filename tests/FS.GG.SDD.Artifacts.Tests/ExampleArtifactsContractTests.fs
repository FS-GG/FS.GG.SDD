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

    /// Feature 093 / FS.GG.SDD#164, FR-021. The shipped example authors the typed `requirements:` and
    /// `decisions:` fields and **no `sourceIds:` at all**. `evidence` and `verify` read only a task's
    /// `SourceIds`, so before the derivation landed, every task in the documented authoring shape was
    /// invisible to both — the tool's own worked example contradicted two of its stages.
    ///
    /// This asserts the property those stages depend on, at the parser: the derived `SourceIds` carries
    /// the typed refs. It is the guard that keeps the example and the consumers from diverging again.
    [<Fact>]
    let ``Example tasks.yml authors no sourceIds yet every task derives its typed refs`` () =
        // Comment lines explain what `sourceIds` *is*; only the YAML body must be free of the key.
        let body =
            File.ReadAllLines(Path.Combine(examplesDir, "tasks.yml"))
            |> Array.filter (fun line -> not (line.TrimStart().StartsWith "#"))
            |> String.concat "\n"

        Assert.DoesNotContain("sourceIds:", body)

        match Task.parseTaskFacts (snapshot "tasks.yml") with
        | Error diagnostics -> failwith $"Example tasks.yml did not parse: {diagnostics}"
        | Ok facts ->
            for task in facts.Tasks do
                let typed =
                    (task.Requirements |> List.map _.Value) @ (task.Decisions |> List.map _.Value)

                Assert.NotEmpty typed

                for id in typed do
                    Assert.Contains(id, task.SourceIds)

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
    let ``Example evidence.yml declarations are each a satisfying pass or a well-formed deferral`` () =
        match Evidence.parseEvidence (snapshot "evidence.yml") with
        | Error diagnostics -> failwith $"Example evidence.yml did not parse: {diagnostics}"
        | Ok declarations ->
            Assert.NotEmpty declarations

            // Feature 081 (#142): the corpus now also carries the canonical deferral shape.
            // Each declaration must be EITHER a satisfying pass (result: pass, non-synthetic)
            // OR a well-formed deferral carrying all four gate-required fields — so the
            // copyable evidence example can never teach a deferral the gate would reject.
            Assert.All(
                declarations,
                fun declaration ->
                    let result = declaration.Result.Trim().ToLowerInvariant()
                    let isSatisfyingPass = result = "pass" && not declaration.Synthetic

                    let isWellFormedDeferral =
                        (result = "deferred" || declaration.Kind = Evidence.EvidenceKind.Deferral)
                        && Option.isSome declaration.Rationale
                        && Option.isSome declaration.Owner
                        && Option.isSome declaration.Scope
                        && Option.isSome declaration.LaterLifecycleVisibility

                    Assert.True(
                        isSatisfyingPass || isWellFormedDeferral,
                        $"Example evidence declaration {declaration.Id.Value} should model a satisfying pass or a four-field deferral."
                    )
            )
