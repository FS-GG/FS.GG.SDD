namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Commands.CommandTypes
open Xunit

/// T015 drift-guard (FR-007 / SC-003): every command, heading, stable-id format, path,
/// and authoring rule named in the seeded `.fsgg/early-stage-guidance.md` resolves
/// against the LIVE SDD contract. Modeled on `AuthoringDocsContractTests`. A dangling
/// reference is a build failure — reproducing the original failure mode this feature
/// exists to remove.
module EarlyStageGuidanceContractTests =

    // The guidance is produced by `init`; read the freshly-seeded copy, not a checked-in
    // file, so the test pins what authors actually receive.
    let private guidanceDoc =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.readRelative root ".fsgg/early-stage-guidance.md"

    /// Content lines of every fenced block whose info string contains the given label.
    let private taggedBlocks (label: string) (markdown: string) : string list list =
        let lines = markdown.Replace("\r\n", "\n").Split('\n')
        let mutable blocks = []
        let mutable current: string list option = None

        for line in lines do
            let trimmed = line.TrimStart()

            if trimmed.StartsWith("```") then
                match current with
                | Some collected ->
                    blocks <- (List.rev collected) :: blocks
                    current <- None
                | None ->
                    let info = trimmed.Substring(3)

                    if info.Contains(label) then
                        current <- Some []
            else
                match current with
                | Some collected -> current <- Some(line :: collected)
                | None -> ()

        List.rev blocks

    let private nonBlankLines (block: string list) =
        block |> List.map (fun l -> l.Trim()) |> List.filter (fun l -> l <> "")

    let private headingsFor (stage: string) =
        match taggedBlocks $"headings:{stage}" guidanceDoc with
        | [ block ] -> nonBlankLines block
        | other -> failwith $"Expected exactly one headings:{stage} block, found {List.length other}."

    let private idPrefixesFor (stage: string) =
        match taggedBlocks $"ids:{stage}" guidanceDoc with
        | [ block ] -> nonBlankLines block
        | other -> failwith $"Expected exactly one ids:{stage} block, found {List.length other}."

    // --- 1. Commands resolve and order matches nextLifecycleCommand ---

    [<Fact>]
    let ``every pre-work-model stage the guidance names is a real lifecycle stage`` () =
        for stage in [ "charter"; "specify"; "clarify"; "checklist" ] do
            Assert.Contains($"fsgg-sdd {stage}", guidanceDoc)

            match parseStage stage with
            | Ok _ -> ()
            | Error message -> failwith $"Guidance names stage '{stage}' but it is not a lifecycle stage: {message}"

    [<Fact>]
    let ``the pre-work-model stage order matches the live lifecycle`` () =
        Assert.Equal(Some Specify, nextLifecycleCommand Charter)
        Assert.Equal(Some Clarify, nextLifecycleCommand Specify)
        Assert.Equal(Some Checklist, nextLifecycleCommand Clarify)
        Assert.Equal(Some Plan, nextLifecycleCommand Checklist)

    // --- 2. Heading lists equal the live standard-section lists ---

    [<Fact>]
    let ``charter headings equal the live charter standard sections`` () =
        // The charter standard sections are owned by the early-parsing layer.
        let live =
            [ "Identity"
              "Principles"
              "Scope Boundaries"
              "Policy Pointers"
              "Lifecycle Notes" ]

        Assert.Equal<string list>(live, headingsFor "charter")

    [<Fact>]
    let ``specify headings equal the live specification standard sections`` () =
        Assert.Equal<string list>(Specification.specificationStandardSections (), headingsFor "specify")

    [<Fact>]
    let ``clarify headings equal the live clarification standard sections`` () =
        Assert.Equal<string list>(Clarification.clarificationStandardSections (), headingsFor "clarify")

    [<Fact>]
    let ``checklist headings equal the live checklist standard sections`` () =
        Assert.Equal<string list>(Checklist.checklistStandardSections (), headingsFor "checklist")

    // --- 3. Stable-id prefixes resolve with the right ^PREFIX-\d{3,}$ shape ---

    // Each prefix the guidance names maps to a live Identifiers constructor; the
    // constructor must accept PREFIX-001 and reject a too-short / non-numeric tail,
    // proving the documented ^PREFIX-\d{3,}$ shape.
    let private idValidator =
        dict
            [ "FR", (createRequirementId >> Result.isOk)
              "US", (createUserStoryId >> Result.isOk)
              "AC", (createAcceptanceScenarioId >> Result.isOk)
              "SB", (createScopeBoundaryId >> Result.isOk)
              "AMB", (createAmbiguityId >> Result.isOk)
              "CQ", (createClarificationQuestionId >> Result.isOk)
              "DEC", (createDecisionId >> Result.isOk)
              "CHK", (createChecklistItemId >> Result.isOk)
              "CR", (createChecklistResultId >> Result.isOk) ]

    [<Fact>]
    let ``every stable-id prefix the guidance names is a real Identifiers prefix`` () =
        let allPrefixes =
            [ "specify"; "clarify"; "checklist" ]
            |> List.collect idPrefixesFor
            |> List.distinct

        Assert.NotEmpty allPrefixes

        for prefix in allPrefixes do
            Assert.True(
                idValidator.ContainsKey prefix,
                $"Guidance names id prefix '{prefix}' with no live Identifiers constructor."
            )

            let validate = idValidator[prefix]
            Assert.True(validate $"{prefix}-001", $"Live constructor rejects the documented {prefix}-001 form.")

            Assert.False(
                validate $"{prefix}-1",
                $"Live constructor accepts {prefix}-1 but the guidance documents three-or-more digits."
            )

            Assert.False(validate $"{prefix}-abc", $"Live constructor accepts a non-numeric {prefix}-abc tail.")

    // --- 4. Paths resolve ---

    [<Fact>]
    let ``every path the guidance references exists or is lifecycle-produced`` () =
        // The guidance itself is produced by init at .fsgg/early-stage-guidance.md (read
        // above), so that path resolves by construction. The other referenced paths:
        // A real reference doc in the repo.
        Assert.Contains("docs/reference/authoring-contracts.md", guidanceDoc)

        Assert.True(
            File.Exists(Path.Combine(TestSupport.repoRoot, "docs", "reference", "authoring-contracts.md")),
            "Guidance references docs/reference/authoring-contracts.md but it does not exist."
        )
        // The generated-view location, by lifecycle convention.
        Assert.Contains("readiness/<id>/agent-commands/<target>/", guidanceDoc)

    // --- 5. §1.1 / §1.2 rules are consistent with the LIVE parsers ---

    [<Fact>]
    let ``the documented accepted coverage line establishes coverage in the live parser`` () =
        let lines =
            taggedBlocks "coverage:accepted" guidanceDoc |> List.collect nonBlankLines

        Assert.NotEmpty lines

        for line in lines do
            let text =
                String.concat
                    "\n"
                    [ "---"
                      "schemaVersion: 1"
                      "workId: 001-early-stage-guard"
                      "stage: specify"
                      "---"
                      ""
                      "## Functional Requirements"
                      ""
                      line ]

            match
                Specification.parseSpecificationFacts
                    { Path = "work/001-early-stage-guard/spec.md"
                      Text = text }
            with
            | Ok facts ->
                Assert.True(
                    facts.RequirementReferences
                    |> List.exists (fun reference -> not (List.isEmpty reference.AcceptanceScenarioIds)),
                    $"Guidance lists this as ACCEPTED coverage but the live parser does not cover it: {line}"
                )
            | Error diagnostics -> failwith $"Documented coverage line did not parse: {line}\n{diagnostics}"

    [<Fact>]
    let ``the documented satisfied evidence block satisfies under the live parser`` () =
        let blocks = taggedBlocks "evidence:satisfied" guidanceDoc
        Assert.NotEmpty blocks

        for block in blocks do
            let text = String.concat "\n" block

            match
                Evidence.parseEvidence
                    { Path = "work/001-early-stage-guard/evidence.yml"
                      Text = text }
            with
            | Ok declarations ->
                Assert.NotEmpty declarations

                Assert.All(
                    declarations,
                    fun declaration ->
                        Assert.True(
                            declaration.Result.Trim().ToLowerInvariant() = "pass"
                            && not declaration.Synthetic,
                            $"Guidance marks this declaration SATISFIED but the non-synthetic-pass rule rejects it: {declaration.Id.Value}"
                        )
                )
            | Error diagnostics -> failwith $"Documented evidence block did not parse:\n{text}\n{diagnostics}"

    // --- Determinism: two init runs seed byte-identical guidance ---

    [<Fact>]
    let ``two init runs seed byte-identical early-stage guidance`` () =
        let mk () =
            let dir =
                Path.Combine(Path.GetTempPath(), "fsgg-sdd-" + Guid.NewGuid().ToString("N"), "es-det")

            Directory.CreateDirectory dir |> ignore
            TestSupport.initializeProject dir
            File.ReadAllBytes(Path.Combine(dir, ".fsgg", "early-stage-guidance.md"))

        Assert.Equal<byte[]>(mk (), mk ())
