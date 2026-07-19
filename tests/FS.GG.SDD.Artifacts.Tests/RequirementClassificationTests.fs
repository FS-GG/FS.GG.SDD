namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

/// ADR-0048 (WI-3): the opt-in functional-requirement classification facet. The coverage-line
/// `{gameplay}` annotation is a closed set, case-insensitive, additive, and non-blocking — an
/// unrecognized brace token is ignored so every pre-ADR-0048 specification stays valid.
module RequirementClassificationTests =

    let private classificationOf line = RequirementModel.requirementClassification line

    [<Fact>]
    let ``recognizedRequirementClasses is the closed set, initially just gameplay`` () =
        Assert.Equal<string list>([ "gameplay" ], RequirementModel.recognizedRequirementClasses)

    [<Fact>]
    let ``a recognized gameplay token classifies the line`` () =
        Assert.Equal<string list>(
            [ "gameplay" ],
            classificationOf "- FR-001: W/S move the paddle. {gameplay} (covers AC-002)"
        )

    [<Fact>]
    let ``the annotation is case-insensitive and normalizes to lowercase`` () =
        Assert.Equal<string list>([ "gameplay" ], classificationOf "- FR-001: text {GamePlay} (covers AC-002)")

    [<Fact>]
    let ``a repeated token is deduplicated`` () =
        Assert.Equal<string list>(
            [ "gameplay" ],
            classificationOf "- FR-001: {gameplay} text {gameplay} (covers AC-002)"
        )

    [<Fact>]
    let ``a line with no brace token is unclassified`` () =
        Assert.Empty(classificationOf "- FR-001: W/S move the paddle. (covers AC-002)")

    [<Fact>]
    let ``an unrecognized brace token is ignored, never blocking`` () =
        // A typo, or braces used incidentally in prose, must not classify — and must not error.
        // This is exactly what keeps every pre-ADR-0048 specification valid unchanged.
        Assert.Empty(classificationOf "- FR-001: difficulty is configurable {difficulty} and {p0} (covers AC-002)")

    [<Fact>]
    let ``parseRequirements populates Classification end to end`` () =
        let text =
            String.concat
                "\n"
                [ "## Functional Requirements"
                  ""
                  "- FR-001: W/S move the paddle. {gameplay} (covers AC-002)"
                  "- FR-002: The menu lists saved games. (covers AC-003)" ]

        let requirements =
            RequirementModel.parseRequirements
                { Path = "work/001-classification/spec.md"
                  Text = text }

        let classificationFor id =
            requirements |> List.find (fun r -> r.Id.Value = id) |> (fun r -> r.Classification)

        Assert.Equal<string list>([ "gameplay" ], classificationFor "FR-001")
        Assert.Empty(classificationFor "FR-002")
