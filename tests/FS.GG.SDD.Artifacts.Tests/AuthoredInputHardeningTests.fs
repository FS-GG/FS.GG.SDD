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

    // --- parseYamlDocument: malformed authored YAML diagnoses, does not crash ---
    // A tab-indented document makes YamlDotNet throw YamlException; the parser must
    // route it to a positioned malformedYaml diagnostic rather than crashing.

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

    // --- #303: a malformed document and an empty one are different diagnostics ---
    // `parseYaml` returned `option`, so every loader could only say "file is empty" —
    // including for a syntax error whose line and column the parser already knew.

    let private expectDiagnostics label result =
        match result with
        | Error diagnostics -> diagnostics
        | Ok _ -> failwith $"Expected {label} to be diagnosed, but it parsed."

    let private theDiagnostic label result =
        match expectDiagnostics label result with
        | [ diagnostic ] -> diagnostic
        | diagnostics -> failwith $"Expected {label} to yield exactly one diagnostic, got {List.length diagnostics}."

    /// The reporter's fixture: a nested double-quote inside a double-quoted scalar.
    /// YamlDotNet places it on the `notes:` line, which is line 3 of the document.
    let private nestedQuoteEvidence =
        "schemaVersion: 1\nworkId: 001-demo\nnotes: \"he said \"hi\" loudly\"\n"

    [<Fact>]
    let ``a malformed evidence document reports the YAML error with its line and column`` () =
        let snapshot: FileSnapshot =
            { Path = "work/001-demo/evidence.yml"
              Text = nestedQuoteEvidence }

        let diagnostic =
            theDiagnostic "a nested double-quote" (parseEvidenceArtifact snapshot)

        Assert.Equal("malformedYaml", diagnostic.Id)
        Assert.Equal(Some 3, diagnostic.Location |> Option.bind (fun location -> location.Line))

        Assert.True(
            diagnostic.Location
            |> Option.bind (fun location -> location.Column)
            |> Option.isSome
        )

        Assert.Contains("line 3", diagnostic.Message)
        Assert.DoesNotContain("is empty", diagnostic.Message)

    [<Fact>]
    let ``a genuinely empty evidence document still reports that it is empty`` () =
        let snapshot: FileSnapshot =
            { Path = "work/001-demo/evidence.yml"
              Text = "" }

        let diagnostic = theDiagnostic "an empty document" (parseEvidenceArtifact snapshot)

        Assert.Equal("malformedSchemaVersion", diagnostic.Id)
        Assert.Equal("Evidence file is empty.", diagnostic.Message)
        Assert.Equal(None, diagnostic.Location)

    [<Fact>]
    let ``a comment-only evidence document is empty, not malformed`` () =
        let snapshot: FileSnapshot =
            { Path = "work/001-demo/evidence.yml"
              Text = "# no evidence yet\n" }

        let diagnostic =
            theDiagnostic "a comment-only document" (parseEvidenceArtifact snapshot)

        Assert.Equal("malformedSchemaVersion", diagnostic.Id)
        Assert.Equal("Evidence file is empty.", diagnostic.Message)

    [<Fact>]
    let ``a malformed front matter line is reported at its line in the file, not in the front matter`` () =
        // The `notes:` line is line 3 of the file and line 2 of the front matter; the
        // author is looking at the file, so the diagnostic must say 3.
        let snapshot: FileSnapshot =
            { Path = "work/001-demo/spec.md"
              Text = "---\nschemaVersion: 1\nnotes: \"he said \"hi\" loudly\"\n---\n\n# Spec\n" }

        let diagnostic =
            theDiagnostic "a malformed front matter" (parseSpecificationFacts snapshot)

        Assert.Equal("malformedYaml", diagnostic.Id)
        Assert.Equal(Some 3, diagnostic.Location |> Option.bind (fun location -> location.Line))

    [<Fact>]
    let ``a malformed tasks document is not reported as empty`` () =
        let snapshot: FileSnapshot =
            { Path = "work/001-demo/tasks.yml"
              Text = "schemaVersion: 1\ntasks:\n\t- id: T001\n" }

        let diagnostic = theDiagnostic "a tab-indented tasks file" (parseTaskFacts snapshot)

        Assert.Equal("malformedYaml", diagnostic.Id)
        Assert.DoesNotContain("is empty", diagnostic.Message)

    // --- §2.1 of the 2026-07-15 review: deeply-nested / over-sized YAML must diagnose,
    // not abort. YamlDotNet parses nested flow collections recursively with no depth
    // limit, so a `[[[[…` document overflows the CLR stack — an *uncatchable* crash
    // (empirically exit 134/SIGABRT) that bypasses the parser's `try/with`. The
    // pre-scan in `parseYamlDocument` must turn both vectors into a clean diagnostic.

    [<Fact>]
    let ``deeply nested flow collections diagnose instead of aborting the process`` () =
        // ~50 KB of open brackets — the empirically-verified overflow input. Before the
        // pre-scan this aborted the process; it must now be a positioned diagnostic.
        let bomb = String.replicate 50_000 "[" + String.replicate 50_000 "]"

        let snapshot: FileSnapshot =
            { Path = "work/001-demo/evidence.yml"
              Text = $"schemaVersion: 1\nnotes: {bomb}\n" }

        let diagnostic = theDiagnostic "a deeply-nested document" (parseEvidenceArtifact snapshot)

        Assert.Equal("malformedYaml", diagnostic.Id)
        Assert.Contains("nesting depth", diagnostic.Message)

    [<Fact>]
    let ``an over-sized YAML document diagnoses instead of parsing`` () =
        // Past the char budget: refused before `stream.Load`, so no unbounded work runs.
        let huge = "schemaVersion: 1\nnotes: " + String.replicate 2_100_000 "a" + "\n"

        let snapshot: FileSnapshot =
            { Path = "work/001-demo/evidence.yml"
              Text = huge }

        let diagnostic = theDiagnostic "an over-sized document" (parseEvidenceArtifact snapshot)

        Assert.Equal("malformedYaml", diagnostic.Id)
        Assert.Contains("exceeding", diagnostic.Message)

    [<Fact>]
    let ``an ordinarily nested document still parses`` () =
        // The bounds sit far above any real artifact: a normally-nested evidence file
        // (flow sequences, a handful of levels) must be unaffected by the pre-scan.
        let snapshot: FileSnapshot =
            { Path = "work/001-demo/evidence.yml"
              Text =
                "schemaVersion: 1\nworkId: 001-demo\n"
                + "obligations:\n  - id: OB-001\n    tags: [a, b, [c, d]]\n    result: pass\n" }

        match parseEvidenceArtifact snapshot with
        // Either it parses, or it is rejected for an ordinary reason — never for depth/size.
        | Ok _ -> ()
        | Error diagnostics ->
            for diagnostic in diagnostics do
                Assert.DoesNotContain("nesting depth", diagnostic.Message)
                Assert.DoesNotContain("exceeding", diagnostic.Message)
