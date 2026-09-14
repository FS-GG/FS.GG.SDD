namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open Xunit

module AudioSkillsTests =
    let private parameters template =
        Map.ofList [ "profile", "app"; "template", template; "bundle", "player" ]

    [<Fact>]
    let ``concrete template identity maps to the registry predicate vocabulary`` () =
        Assert.Equal("fable-game", AudioSkills.templatePredicateValue "fs-gg-fable-game")
        Assert.Equal("custom-template", AudioSkills.templatePredicateValue "custom-template")

        let forged = Map.ofList [ "template", "rendering-app"; "bundle", "player" ]
        let effective = AudioSkills.ownerPredicateParameters "fs-gg-fable-game" forged
        Assert.Equal("fable-game", effective["template"])
        Assert.Equal<string list>([ "fs-gg-browser-audio" ], (AudioSkills.plan effective).MaterializedIds)

    [<Fact>]
    let ``fable game receives the exact Audio-owned browser guidance in every configured root`` () =
        let outcome = AudioSkills.plan (parameters "fable-game")

        Assert.Equal<string list>([ "fs-gg-browser-audio" ], outcome.MaterializedIds)
        Assert.Empty outcome.VerifyFailedIds
        Assert.Empty outcome.PredicateUnevaluatedIds
        Assert.Empty outcome.NamespaceCollisionIds
        Assert.Equal(None, outcome.ManifestError)

        let paths = outcome.ProvenancePaths |> List.map fst |> List.sort
        let expected =
            Fsgg.Schemas.agentSkillRoots
            |> List.map (fun root -> $"{root}/skills/fs-gg-browser-audio/SKILL.md")
            |> List.sort
        Assert.Equal<string list>(expected, paths)
        Assert.All(outcome.ProvenancePaths, fun (_, digest) ->
            Assert.Equal("07d9a0277044162611cffb20089577565fcc0ef802ac71f0cbc668cf70c0818c", digest))
        Assert.All(outcome.Writes, fun effect ->
            match effect with
            | WriteFile(_, _, AgentGuidanceTarget) -> ()
            | other -> failwithf "unexpected Audio materialization effect: %A" other)

    [<Fact>]
    let ``non fable template omits browser guidance without a predicate gap`` () =
        let outcome = AudioSkills.plan (parameters "rendering-app")
        Assert.Empty outcome.MaterializedIds
        Assert.Empty outcome.Writes
        Assert.Empty outcome.PredicateUnevaluatedIds

    [<Fact>]
    let ``Audio package failures retain Audio ownership in operator diagnostics`` () =
        let cases =
            [ { RenderingSkills.empty with ManifestError = Some "boom" },
              "scaffold.audioSkillManifestMalformed"
              { RenderingSkills.empty with NamespaceCollisionIds = [ "fs-gg-sdd-audio" ] },
              "scaffold.audioSkillNamespaceCollision"
              { RenderingSkills.empty with VerifyFailedIds = [ "fs-gg-browser-audio" ] },
              "scaffold.audioSkillVerifyFailed"
              { RenderingSkills.empty with PredicateUnevaluatedIds = [ "fs-gg-browser-audio" ] },
              "scaffold.audioSkillPredicateUnevaluated" ]

        for outcome, expected in cases do
            let diagnostics = HandlersScaffold.audioSkillDiagnostics outcome
            let diagnostic = Assert.Single diagnostics
            Assert.Equal(expected, diagnostic.Id)
            Assert.Contains("audio", diagnostic.Message, System.StringComparison.OrdinalIgnoreCase)
