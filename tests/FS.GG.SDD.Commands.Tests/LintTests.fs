namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Text.RegularExpressions
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandSerialization
open Xunit

/// Feature 076 — the read-only pre-flight `fsgg-sdd lint <artifact>` engine + handler,
/// exercised through the real MVU workflow (`init`/`update`/interpret loop), the same path
/// the CLI drives. Fixtures live under `tests/fixtures/lint/`.
module LintTests =

    let private root = TestSupport.repoRoot

    // Drive the real workflow for a lint over `artifact`, returning the final model (so the
    // effect log is inspectable) — mirrors CommandEffects.driveToReport.
    let private driveLint (artifact: string option) =
        let request =
            { TestSupport.request Lint root with
                Artifact = artifact }

        let model, effects = init request

        let rec loop state pending =
            match pending with
            | [] -> state
            | current ->
                let results = interpretAll request.ProjectRoot request.DryRun current

                let nextState, nextEffects =
                    results
                    |> List.fold
                        (fun (s, acc) result ->
                            let s', produced = update (EffectInterpreted result) s
                            s', acc @ produced)
                        (state, [])

                loop nextState nextEffects

        loop model effects |> fun s -> update BuildReport s |> fst

    let private lintOf (relativePath: string) =
        let model = driveLint (Some relativePath)
        model, (model.Report |> Option.defaultWith (fun () -> buildReport model)).Lint

    let private fixture name = $"tests/fixtures/lint/broken-all/{name}"
    let private example name = $"docs/examples/lifecycle-artifacts/{name}"

    // ---- SC-001: each defect class is caught (4/4) ----

    [<Theory>]
    [<InlineData("checklist.md", "duplicateId")>]
    [<InlineData("checklist-frontmatter.md", "frontMatter")>]
    [<InlineData("clarifications.md", "missingDecisionTag")>]
    [<InlineData("spec.md", "coverageLine")>]
    let ``SC-001 broken fixture surfaces its defect class`` (file: string) (expectedClass: string) =
        let _, lint = lintOf (fixture file)
        let summary = Option.get lint
        Assert.Equal(DefectsFound, summary.Outcome)

        let classes =
            summary.Defects |> List.map (fun d -> lintDefectClassValue d.Class)

        Assert.Contains(expectedClass, classes)

    [<Fact>]
    let ``SC-001 the four load-bearing defect classes are all reachable`` () =
        let caught =
            [ "checklist.md"; "checklist-frontmatter.md"; "clarifications.md"; "spec.md" ]
            |> List.collect (fun f -> (lintOf (fixture f) |> snd |> Option.get).Defects)
            |> List.map (fun d -> lintDefectClassValue d.Class)
            |> Set.ofList

        for expected in [ "coverageLine"; "missingDecisionTag"; "frontMatter"; "duplicateId" ] do
            Assert.Contains(expected, caught)

    // ---- SC-002 / FR-013: canonical examples lint clean (no false positives) ----

    [<Theory>]
    [<InlineData("checklist.md")>]
    [<InlineData("clarifications.md")>]
    [<InlineData("evidence.yml")>]
    [<InlineData("tasks.yml")>]
    let ``SC-002 canonical example lints clean`` (file: string) =
        let _, lint = lintOf (example file)
        let summary = Option.get lint
        Assert.Equal(Clean, summary.Outcome)
        Assert.Empty summary.Defects

    // ---- FR-007 / SC-003: fix hint + resolvable grammar pointer on every grammar defect ----

    [<Fact>]
    let ``SC-003 every grammar-class defect carries a fix hint and grammar pointer`` () =
        let grammarClasses = set [ CoverageLine; MissingDecisionTag; FrontMatter; DuplicateId ]

        let defects =
            [ "checklist.md"; "checklist-frontmatter.md"; "clarifications.md"; "spec.md" ]
            |> List.collect (fun f -> (lintOf (fixture f) |> snd |> Option.get).Defects)
            |> List.filter (fun d -> Set.contains d.Class grammarClasses)

        Assert.NotEmpty defects

        for d in defects do
            Assert.False(System.String.IsNullOrWhiteSpace d.Diagnostic.Correction)
            Assert.True(Option.isSome d.GrammarPointer)

    // ---- FR-007 drift guard (T008): every pointer anchor resolves in the grammar-of-record ----

    let private slug (heading: string) =
        let lowered = heading.TrimStart('#', ' ').Trim().ToLowerInvariant()
        let hyphenated = Regex.Replace(lowered, @"[^a-z0-9]+", "-")
        hyphenated.Trim('-')

    [<Fact>]
    let ``FR-007 every grammar pointer anchor is a real heading in authoring-contracts`` () =
        let doc =
            File.ReadAllText(Path.Combine(root, "docs", "reference", "authoring-contracts.md"))

        let headingSlugs =
            doc.Split('\n')
            |> Array.filter (fun l -> l.TrimStart().StartsWith "#")
            |> Array.map slug
            |> Set.ofArray

        for cls in [ CoverageLine; MissingDecisionTag; FrontMatter; DuplicateId ] do
            match LintEngine.grammarPointer cls with
            | Some pointer -> Assert.Contains(pointer.Anchor, headingSlugs)
            | None -> failwith $"grammar class {lintDefectClassValue cls} must carry a pointer"

    // ---- FR-017: every reported defect is an Error ----

    [<Fact>]
    let ``FR-017 every reported defect is an error`` () =
        let defects =
            [ fixture "checklist.md"; fixture "clarifications.md"; fixture "spec.md" ]
            |> List.collect (fun f -> (lintOf f |> snd |> Option.get).Defects)

        Assert.NotEmpty defects

        for d in defects do
            Assert.Equal(FS.GG.SDD.Artifacts.Diagnostics.DiagnosticError, d.Diagnostic.Severity)

    // ---- FR-008 / T015: read-only — no mutating effect is ever emitted ----

    [<Fact>]
    let ``FR-008 lint emits only reads and never a write effect`` () =
        let model = driveLint (Some(fixture "checklist.md"))

        let mutating =
            model.InterpretedEffects
            |> List.map (fun r -> r.Effect)
            |> List.filter (function
                | WriteFile _
                | CreateDirectory _
                | RunProcess _
                | SetExecutable _ -> true
                | _ -> false)

        Assert.Empty mutating
        // and at least the artifact read happened
        Assert.Contains(model.InterpretedEffects, (fun r -> match r.Effect with | ReadFile _ -> true | _ -> false))

    // ---- FR-015: unusable input (missing / unrecognized) ----

    [<Fact>]
    let ``FR-011 missing artifact is unusable input`` () =
        let _, lint = lintOf "tests/fixtures/lint/does-not-exist.md"
        Assert.Equal(UnusableInput, (Option.get lint).Outcome)

    [<Fact>]
    let ``FR-002 unrecognized artifact kind is unusable input`` () =
        let _, lint = lintOf "README.md"
        Assert.Equal(UnusableInput, (Option.get lint).Outcome)

    // ---- SC-005: determinism — identical bytes ⇒ byte-identical JSON ----

    [<Fact>]
    let ``SC-005 lint json is deterministic across runs`` () =
        let jsonOf () =
            let model = driveLint (Some(fixture "checklist.md"))
            serializeReport (model.Report |> Option.defaultWith (fun () -> buildReport model))

        Assert.Equal(jsonOf (), jsonOf ())

    // ---- FR-016 / US3: `<stage> --explain` — same checks, non-blocking, no mutation ----

    // Drive a stage command with --explain over a temp work item, returning the final model.
    let private driveExplain (command: SddCommand) (workId: string) (projectRoot: string) =
        let request =
            { TestSupport.request command projectRoot with
                WorkId = Some workId
                Explain = true }

        let model, effects = init request

        let rec loop state pending =
            match pending with
            | [] -> state
            | current ->
                let results = interpretAll request.ProjectRoot request.DryRun current

                let nextState, nextEffects =
                    results
                    |> List.fold
                        (fun (s, acc) result ->
                            let s', produced = update (EffectInterpreted result) s
                            s', acc @ produced)
                        (state, [])

                loop nextState nextEffects

        loop model effects |> fun s -> update BuildReport s |> fst

    [<Fact>]
    let ``FR-016 clarify --explain surfaces the same defect and mutates nothing`` () =
        let tmp = TestSupport.tempDirectory ()
        let broken = File.ReadAllText(Path.Combine(root, fixture "clarifications.md"))
        TestSupport.writeRelative tmp "work/x/clarifications.md" broken

        let model = driveExplain Clarify "x" tmp
        let summary = (model.Report |> Option.defaultWith (fun () -> buildReport model)).Lint |> Option.get

        Assert.Equal(DefectsFound, summary.Outcome)
        Assert.Contains("missingDecisionTag", summary.Defects |> List.map (fun d -> lintDefectClassValue d.Class))

        // Non-blocking dry run: no mutating effect, and no state advanced.
        let mutating =
            model.InterpretedEffects
            |> List.filter (fun r ->
                match r.Effect with
                | WriteFile _
                | CreateDirectory _ -> true
                | _ -> false)

        Assert.Empty mutating

    [<Fact>]
    let ``FR-016 checklist --explain on a clean artifact is clean`` () =
        let tmp = TestSupport.tempDirectory ()
        let clean = File.ReadAllText(Path.Combine(root, example "checklist.md"))
        TestSupport.writeRelative tmp "work/x/checklist.md" clean

        let model = driveExplain Checklist "x" tmp
        let summary = (model.Report |> Option.defaultWith (fun () -> buildReport model)).Lint |> Option.get
        Assert.Equal(Clean, summary.Outcome)
