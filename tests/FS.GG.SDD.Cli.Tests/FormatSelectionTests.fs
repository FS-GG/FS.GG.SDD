namespace FS.GG.SDD.Cli.Tests

open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Cli.Rendering
open Xunit

module FormatSelectionTests =
    let sample = RichRenderingTests.sampleReport
    let caps = { IsInteractive = true; ColorEnabled = true; Width = Some 100 }

    [<Fact>]
    let ``T020 no flag selects Json`` () =
        Assert.Equal(Json, selectFormat [])
        Assert.Equal(Json, selectFormat [ "--work"; "x" ])

    [<Fact>]
    let ``T020 explicit single flags select their format`` () =
        Assert.Equal(Json, selectFormat [ "--json" ])
        Assert.Equal(Text, selectFormat [ "--text" ])
        Assert.Equal(Rich, selectFormat [ "--rich" ])

    [<Fact>]
    let ``T020 precedence is rich over text over json over default`` () =
        Assert.Equal(Rich, selectFormat [ "--json"; "--text"; "--rich" ])
        Assert.Equal(Text, selectFormat [ "--json"; "--text" ])
        Assert.Equal(Json, selectFormat [ "--json" ])

    [<Fact>]
    let ``T021 all three formats project the same report consistently`` () =
        let jsonResult = resolve Json caps sample
        let textResult = resolve Text caps sample
        let richResult = resolve Rich caps sample
        // Each is the matching projection of the same report.
        Assert.Equal(serializeReport sample, jsonResult.Text)
        Assert.Equal(renderText sample, textResult.Text)
        Assert.True(richResult.UsedRichRendering)
        // Outcome is consistent across projections.
        Assert.Contains(outcomeValue sample.Outcome, jsonResult.Text)
        Assert.Contains(outcomeValue sample.Outcome, textResult.Text)
        Assert.Contains(outcomeValue sample.Outcome, richResult.Text)

    [<Fact>]
    let ``T021 explicit json equals default json bytes`` () =
        let explicitJson = resolve (selectFormat [ "--json" ]) caps sample
        let defaultJson = resolve (selectFormat []) caps sample
        Assert.Equal(defaultJson.Text, explicitJson.Text)
        Assert.Equal(serializeReport sample, explicitJson.Text)
