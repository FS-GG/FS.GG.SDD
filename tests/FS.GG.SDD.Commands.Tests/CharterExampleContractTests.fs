namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Commands.Internal.EarlyStageAuthoring
open Xunit

/// Feature 078 (#125) drift guard: the shipped charter example under
/// docs/examples/lifecycle-artifacts/charter.md is parsed through the LIVE charter front-matter
/// parser on each build. Charter has no `FS.GG.SDD.Artifacts` module (it is identity-only, parsed
/// in `EarlyStageAuthoring`), so — unlike spec/plan (ExampleArtifactsContractTests) — its example
/// is validated here in Commands.Tests. If the example ever contradicts the tool, this fails, so
/// the pointer target citing charter.md can never send an author to a broken example.
module CharterExampleContractTests =

    [<Fact>]
    let ``Example charter.md front matter parses through the live charter parser`` () =
        let path = "work/001-example/charter.md"

        let text =
            File.ReadAllText(
                Path.Combine(TestSupport.repoRoot, "docs", "examples", "lifecycle-artifacts", "charter.md")
            )

        match parseCharterFrontMatter path text with
        | Error diagnostic -> failwith $"Example charter.md did not parse: {diagnostic.Message}"
        | Ok frontMatter ->
            Assert.Equal("001-example", frontMatter.WorkId)
            Assert.Equal("charter", frontMatter.Stage)
            Assert.Equal("chartered", frontMatter.Status)
