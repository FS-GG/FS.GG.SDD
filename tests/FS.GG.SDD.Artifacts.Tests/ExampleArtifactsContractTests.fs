namespace FS.GG.SDD.Artifacts.Tests

open System.IO
open FS.GG.SDD.Artifacts
open Xunit

/// FR-004 drift guard (#106 §1.4): every shipped worked example under
/// docs/examples/lifecycle-artifacts/ is parsed through the LIVE public artifact
/// parser on each build. If an example ever contradicts the tool — a mistyped id,
/// a broken coverage line, an unsatisfiable evidence block — this fails, so the
/// copy-adaptable references can never rot.
///
/// PARSING IS NOT ENOUGH (#192). These tests reach only the FS.GG.SDD.Artifacts layer, so a
/// stage gate — `missingDisposition`, `stalePlanSnapshot` — is invisible here: both live in
/// FS.GG.SDD.Commands. The example once parsed cleanly while `fsgg-sdd analyze` blocked on it.
/// The complementary guard that drives the example through the real lifecycle stages is
/// ExampleLifecycleContractTests, in FS.GG.SDD.Commands.Tests. Keep both.
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

    /// FS.GG.SDD#265 / ADR-0003 — the decision-path fixpoint fence, tied to the example bytes.
    /// The example authors its decisions in the canonical `- **DEC-001** [tags]:` grammar; the parser
    /// that populates `work-model.json` (`RequirementModel.parseDecisions`) must actually read them.
    /// Before the fix this returned empty, the decisions never entered the work model, and any task
    /// referencing `DEC-001`/`DEC-002` raised `unknownReference`. Pinning the round-trip here means a
    /// regression that re-breaks the grammar fails on the shipped example, not in a user's workspace.
    [<Fact>]
    let ``Example clarifications.md decisions round-trip through the work-model decision parser`` () =
        let decisionIds =
            RequirementModel.parseDecisions (snapshot "clarifications.md")
            |> List.map (fun decision -> decision.Id.Value)

        Assert.Contains("DEC-001", decisionIds)
        Assert.Contains("DEC-002", decisionIds)

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
