namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

// Feature 059 (#66): authored-input parsing must diagnose, never crash.
// A malformed authored YAML document, an overflowing schemaVersion integer, and
// a loose version string all used to throw or slip through; each must now yield a
// clean Error/None so the CLI exits 1 with a diagnostic instead of a stack trace.
module AuthoredInputHardeningTests =

    // --- SchemaVersion.parse: overflow classifies as malformed, does not throw ---

    [<Fact>]
    let ``parse classifies an overflowing integer as malformed instead of throwing`` () =
        match SchemaVersion.parse "99999999999999999999" with
        | Error _ -> ()
        | Ok version -> failwith $"Expected an overflowing schemaVersion to be malformed, got {version}."

    [<Fact>]
    let ``parse classifies an overflowing minor as malformed instead of throwing`` () =
        match SchemaVersion.parse "1.99999999999999999999" with
        | Error _ -> ()
        | Ok version -> failwith $"Expected an overflowing minor to be malformed, got {version}."

    [<Theory>]
    [<InlineData("1")>]
    [<InlineData("1.2")>]
    let ``parse still accepts valid integer and major.minor values`` (text: string) =
        match SchemaVersion.parse text with
        | Ok version -> Assert.Equal(text, version.Raw)
        | Error message -> failwith $"Expected {text} to parse, got {message}."

    // --- parseYaml: malformed authored YAML diagnoses, does not crash ---
    // A tab-indented document makes YamlDotNet throw YamlException; the parser must
    // route it to the existing "empty/unparseable -> diagnostic" path.

    [<Fact>]
    let ``parseProjectConfig diagnoses tab-indented YAML instead of crashing`` () =
        let snapshot: FileSnapshot =
            { Path = ".fsgg/project.yml"
              Text = "project:\n\tid: fs-gg-sdd\n" }

        match parseProjectConfig snapshot with
        | Error diagnostics -> Assert.NotEmpty diagnostics
        | Ok config -> failwith $"Expected malformed YAML to be diagnosed, parsed {config}."

    [<Fact>]
    let ``parseProjectConfig diagnoses a duplicate mapping key instead of crashing`` () =
        let snapshot: FileSnapshot =
            { Path = ".fsgg/project.yml"
              Text = "schemaVersion: 1\nproject:\n  id: a\n  id: b\n" }

        match parseProjectConfig snapshot with
        | Error diagnostics -> Assert.NotEmpty diagnostics
        | Ok config -> failwith $"Expected a duplicate key to be diagnosed, parsed {config}."
