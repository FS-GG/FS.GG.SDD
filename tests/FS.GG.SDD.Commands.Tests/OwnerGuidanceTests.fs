namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module OwnerGuidanceTests =
    [<System.Runtime.InteropServices.DllImport("libc", SetLastError = true)>]
    extern int private mkfifo(string path, uint32 mode)

    let descriptor =
        """{"schema":"fsgg.owner-guidance/1","guidance":[{"id":"atlas","sourcePath":".fsgg/body.md","targets":["claude","codex"],"requiredPaths":[".fsgg/policy.json"]}]}"""

    [<Fact>]
    let ``existing command effects retain published union tag ordinals`` () =
        let effects =
            [
                ReadFile "file"
                EnumerateDirectory "directory"
                CreateDirectory "directory"
                WriteFile("file", "body", AgentGuidanceTarget)
                RunProcess("tool", [], "")
                ReadPackageSurface("package", "version")
                SetExecutable "file"
                Confirm("step", "prompt")
            ]

        effects
        |> List.iteri (fun expected effect ->
            let case, _ =
                Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(effect, typeof<CommandEffect>)

            Assert.Equal(expected, case.Tag))

    [<Fact>]
    let ``descriptor accepts body references and rejects schema duplicate and escape mutations`` () =
        Assert.True(OwnerGuidance.parse descriptor |> Result.isOk)

        for mutated in
            [
                descriptor.Replace("fsgg.owner-guidance/1", "wrong")
                descriptor.Replace(".fsgg/body.md", "../body.md")
                descriptor.Replace(".fsgg/body.md", " .fsgg/body.md")
                descriptor.Replace("\"claude\",\"codex\"", "\"codex\",\"codex\"")
                descriptor.Replace("\"schema\":", "\"extra\":0,\"schema\":")
            ] do
            Assert.True(OwnerGuidance.parse mutated |> Result.isError)

    [<Fact>]
    let ``merge preserves Unicode and surrounding CRLF prose and is idempotent`` () =
        let original =
            "# Authored café 🐱\r\n\r\n<!-- atlas:begin -->\r\nstale\r\n<!-- atlas:end -->\r\nTail α\r\n"

        let expected = original.Replace("stale", "read project knowledge")
        let result = OwnerGuidance.merge "atlas" "read project knowledge\n" original
        Assert.Equal(Ok expected, result)
        Assert.Equal(result, OwnerGuidance.merge "atlas" "read project knowledge\n" expected)

    [<Theory>]
    [<InlineData("<!-- atlas:end -->\n<!-- atlas:begin -->")>]
    [<InlineData("<!-- atlas:begin -->")>]
    [<InlineData("<!-- atlas:begin -->x<!-- other:begin -->y<!-- other:end --><!-- atlas:end -->")>]
    [<InlineData("<!-- atlas:begin -->x<!-- atlas:end --><!-- atlas:begin -->y<!-- atlas:end -->")>]
    let ``ambiguous authored markers refuse`` text =
        Assert.True(OwnerGuidance.merge "atlas" "body" text |> Result.isError)

    [<Fact>]
    let ``observed merge refuses an actual concurrent edit and preserves it`` () =
        let root = TestSupport.tempDirectory ()
        let path = Path.Combine(root, "AGENTS.md")
        File.WriteAllText(path, "observed")
        let observed = File.ReadAllBytes path
        File.WriteAllText(path, "concurrent authored edit")

        let effect =
            WriteFile("AGENTS.md", "merged", HybridArtifact(ObservedMerge(Some observed, [])))

        let result = CommandEffects.interpret root false effect
        Assert.False result.Succeeded
        Assert.Equal("concurrent authored edit", File.ReadAllText path)

    [<Fact>]
    let ``contained reads and observed writes refuse symlink targets`` () =
        let root = TestSupport.tempDirectory ()
        let outside = Path.Combine(TestSupport.tempDirectory (), "outside.md")
        File.WriteAllText(outside, "authored outside")
        File.CreateSymbolicLink(Path.Combine(root, "AGENTS.md"), outside) |> ignore

        let read =
            CommandEffects.interpret root false (ReadContainedFile("AGENTS.md", "fixture"))

        Assert.True(
            match read.Read with
            | Unreadable _ -> true
            | _ -> false
        )

        let write =
            CommandEffects.interpret
                root
                false
                (WriteFile("AGENTS.md", "merged", HybridArtifact(ObservedMerge(Some(File.ReadAllBytes outside), []))))

        Assert.False write.Succeeded
        Assert.Equal("authored outside", File.ReadAllText outside)

    [<Fact>]
    let ``contained read refuses a FIFO before opening it`` () =
        if OperatingSystem.IsLinux() then
            let root = TestSupport.tempDirectory ()
            Assert.Equal(0, mkfifo (Path.Combine(root, "AGENTS.md"), 0o600u))

            let result =
                CommandEffects.interpret root false (ReadContainedFile("AGENTS.md", "fixture"))

            Assert.True(
                match result.Read with
                | Unreadable _ -> true
                | _ -> false
            )

    let private fixture () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let files =
            [
                OwnerGuidance.descriptorPath, descriptor
                ".fsgg/body.md", "Use the owning project knowledge.\n"
                ".fsgg/policy.json", "{\"policy\":true}\n"
            ]
            |> List.map (fun (p, text) -> p, Encoding.UTF8.GetBytes text)
            |> Map.ofList

        files
        |> Map.iter (fun p bytes ->
            let path = Path.Combine(root, p)

            Directory.CreateDirectory(Path.GetDirectoryName path |> Option.ofObj |> Option.defaultValue root)
            |> ignore

            File.WriteAllBytes(path, bytes))

        let agentsSnapshot =
            (CommandEffects.interpret root false (ReadFile ".fsgg/agents.yml")).Snapshot
            |> Option.get

        let agents =
            FS.GG.SDD.Artifacts.Config.parseAgentGuidanceConfig agentsSnapshot
            |> Result.defaultWith (fun _ -> failwith "fixture agents")

        root, files, agents

    let private observe root paths =
        paths
        |> List.map (fun path ->
            let result =
                CommandEffects.interpret root false (ReadContainedFile(path, "fixture"))

            let observed =
                match result.Read with
                | Bytes snap -> Ok(Some snap)
                | Absent -> Ok None
                | Unreadable(_, e) -> Error e
                | Truncated _ -> Error "unexpected listing"

            path, observed)
        |> Map.ofList

    [<Fact>]
    let ``all targets are preflighted before any write and divergent owner policy is preserved`` () =
        let root, files, agents = fixture ()
        let paths = OwnerGuidance.requiredReads files agents |> Result.defaultWith failwith
        let before = File.ReadAllBytes(Path.Combine(root, "AGENTS.md"))
        File.WriteAllText(Path.Combine(root, ".fsgg/policy.json"), "custom policy")
        let plan = OwnerGuidance.plan files agents (observe root paths)
        Assert.Empty plan.Writes
        Assert.NotEmpty plan.Problems
        Assert.True((before = File.ReadAllBytes(Path.Combine(root, "AGENTS.md"))))
        Assert.Equal("custom policy", File.ReadAllText(Path.Combine(root, ".fsgg/policy.json")))

    [<Fact>]
    let ``filesystem activation preserves root prose Knowledge and Atlas history and reruns without writes`` () =
        let root, files, agents = fixture ()

        for path, text in
            [
                "AGENTS.md", "# café 🐱\r\nAuthored Codex text\r\n"
                "CLAUDE.md", "# Claude\nAuthored Claude text\n"
                ".fsgg/knowledge/custom.json", "knowledge history"
                "docs/atlas/records/prior.json", "immutable Atlas history"
            ] do
            TestSupport.writeRelative root path text

        let paths = OwnerGuidance.requiredReads files agents |> Result.defaultWith failwith
        let plan = OwnerGuidance.plan files agents (observe root paths)
        Assert.Empty plan.Problems
        Assert.Equal(2, plan.Writes.Length)

        for write in plan.Writes do
            let result =
                CommandEffects.interpret
                    root
                    false
                    (WriteFile(
                        write.Path,
                        write.Text,
                        HybridArtifact(ObservedMerge(write.ExpectedBytes, files |> Map.toList))
                    ))

            Assert.True result.Succeeded

        Assert.StartsWith("# café 🐱\r\nAuthored Codex text\r\n", File.ReadAllText(Path.Combine(root, "AGENTS.md")))
        Assert.Equal("knowledge history", TestSupport.readRelative root ".fsgg/knowledge/custom.json")
        Assert.Equal("immutable Atlas history", TestSupport.readRelative root "docs/atlas/records/prior.json")
        let second = OwnerGuidance.plan files agents (observe root paths)
        Assert.Empty second.Problems
        Assert.Empty second.Writes

    [<Fact>]
    let ``undeclared fragment absent descriptor and unreadable target have distinct outcomes`` () =
        let root, files, agents = fixture ()
        Assert.Empty (OwnerGuidance.plan Map.empty agents Map.empty).Problems

        Assert.True(
            OwnerGuidance.requiredReads (Map.remove ".fsgg/body.md" files) agents
            |> Result.isError
        )

        let targets =
            agents.Targets
            |> List.map (fun target ->
                { target with
                    GuidancePath = (if target.Id = "claude" then "AGENTS.md" else "agents.md")
                })

        let colliding = { agents with Targets = targets }
        Assert.True(OwnerGuidance.requiredReads files colliding |> Result.isError)

        let paths = OwnerGuidance.requiredReads files agents |> Result.defaultWith failwith

        let observed =
            observe root paths |> Map.add "CLAUDE.md" (Error "fixture unreadable")

        let plan = OwnerGuidance.plan files agents observed
        Assert.Empty plan.Writes
        Assert.NotEmpty plan.Problems
        Assert.Empty plan.MissingTargets

    [<Fact>]
    let ``observed merge rechecks prerequisite bytes and preserves a UTF8 BOM`` () =
        let root = TestSupport.tempDirectory ()

        let initial =
            Array.append [| 0xEFuy; 0xBBuy; 0xBFuy |] (Encoding.UTF8.GetBytes "# authored\r\n")

        File.WriteAllBytes(Path.Combine(root, "AGENTS.md"), initial)
        TestSupport.writeRelative root "policy.json" "expected"

        let effect =
            WriteFile(
                "AGENTS.md",
                "# authored\r\nmanaged\r\n",
                HybridArtifact(ObservedMerge(Some initial, [ "policy.json", Encoding.UTF8.GetBytes "expected" ]))
            )

        TestSupport.writeRelative root "policy.json" "concurrent policy"
        Assert.False (CommandEffects.interpret root false effect).Succeeded
        Assert.True((initial = File.ReadAllBytes(Path.Combine(root, "AGENTS.md"))))
        TestSupport.writeRelative root "policy.json" "expected"
        Assert.True (CommandEffects.interpret root false effect).Succeeded
        Assert.True([| 0xEFuy; 0xBBuy; 0xBFuy |] = File.ReadAllBytes(Path.Combine(root, "AGENTS.md")).[0..2])

/// Real MVU handlers and filesystem with an inert external provider transport.
/// The optional package-fixture build overrides the existing Drivers content roots;
/// ordinary pinned builds exercise neutral compatibility instead of claiming activation.
[<Collection("Console")>]
module OwnerGuidancePayloadTests =
    open FS.GG.SDD.Commands.CommandWorkflow
    open FS.GG.SDD.Commands.CommandReports
    open FS.GG.SDD.Commands.Internal

    let private hasPayload () =
        let files =
            DriverSkills.plan (Set.ofList SeededSkills.skillNames)
            |> Foundation.ownerGuidanceFiles

        let present = Map.containsKey OwnerGuidance.descriptorPath files

        if Environment.GetEnvironmentVariable "FSGG_OWNER_GUIDANCE_FIXTURE_REQUIRED" = "1" then
            Assert.True(present, "explicit candidate payload must be embedded")

        present

    let private runWithConfirmation root command assumeYes failSecondWrite input =
        let request =
            { TestSupport.request command root with
                Provider = (if command = Scaffold then Some "fixture" else None)
                AssumeYes = assumeYes
                IsInteractive = Option.isSome input
            }

        let model, effects = init request

        let interpret effect =
            match effect with
            | Confirm _ ->
                let prior = Console.In
                use reader = new StringReader(Option.defaultValue "" input)

                try
                    Console.SetIn reader
                    CommandEffects.interpret root false effect
                finally
                    Console.SetIn prior
            | RunProcess(_, args, _) ->
                // The provider stands in only for external transport; all SDD file effects and
                // progression remain production control flow. It creates one ordinary product file.
                if args |> List.contains "fsgg-fixture-app" then
                    TestSupport.writeRelative root "App.fs" "module App\n"

                {
                    Effect = effect
                    Succeeded = true
                    Read = Absent
                    Snapshot = None
                    Confirmed = None
                    Diagnostic = None
                    Process =
                        Some
                            {
                                Started = true
                                ExitCode = 0
                                Command = "inert fixture transport"
                                StandardOutput = ""
                                StandardOutputTruncated = false
                                StandardError = ""
                                StandardErrorTruncated = false
                            }
                }
            | WriteFile("CLAUDE.md", _, HybridArtifact(ObservedMerge _)) when
                File.Exists(Path.Combine(root, ".concurrent-guidance-edit"))
                ->
                // Perform a real filesystem edit after the MVU read and before the production write edge.
                File.Delete(Path.Combine(root, ".concurrent-guidance-edit"))
                TestSupport.writeRelative root "CLAUDE.md" "# Concurrent authored edit\n"
                CommandEffects.interpret root false effect
            | WriteFile("CLAUDE.md", _, HybridArtifact(ObservedMerge _)) when failSecondWrite ->
                let path = Path.Combine(root, "CLAUDE.md")
                File.Move(path, path + ".preserved")
                Directory.CreateDirectory path |> ignore
                CommandEffects.interpret root false effect
            | _ -> CommandEffects.interpret root false effect

        let rec loop remaining state pending =
            if remaining = 0 then
                failwith "MVU did not reach idle"

            match pending with
            | [] -> state
            | effects ->
                let results = effects |> List.map interpret

                let state, next =
                    results
                    |> List.fold
                        (fun (state, pending) result ->
                            let updated, produced = update (EffectInterpreted result) state
                            updated, pending @ produced)
                        (state, [])

                loop (remaining - 1) state next

        let final = loop 200 model effects |> fun m -> update BuildReport m |> fst
        final, final.Report |> Option.defaultWith (fun () -> buildReport final)

    let private run root command assumeYes failSecondWrite =
        runWithConfirmation root command assumeYes failSecondWrite None

    let private scaffoldFixture failSecond =
        let root = TestSupport.tempDirectory ()

        TestSupport.writeRelative
            root
            ".fsgg/providers.yml"
            "schemaVersion: 1\nproviders:\n  - name: fixture\n    contractVersion: \"1.0.0\"\n    templateId: fsgg-fixture-app\n    source: inert-fixture\n"

        let model, report = run root Scaffold false failSecond
        root, model, report

    [<Fact>]
    let ``ordinary scaffold activates verified owner guidance after materialization or stays neutral without a descriptor``
        ()
        =
        let declared = hasPayload ()
        let root, model, report = scaffoldFixture false
        Assert.Equal(0, exitCodeForReport report)

        for path in [ "AGENTS.md"; "CLAUDE.md" ] do
            let text = TestSupport.readRelative root path
            Assert.Contains("This file is an SDD lifecycle guidance target.", text)
            Assert.Equal(declared, text.Contains("<!-- atlas:begin -->", StringComparison.Ordinal))

        if declared then
            let index predicate =
                model.InterpretedEffects |> List.findIndex (fun r -> predicate r.Effect)

            let materialized =
                index (function
                    | WriteFile(path, _, AgentGuidanceTarget) -> path = OwnerGuidance.descriptorPath
                    | _ -> false)

            let activated =
                index (function
                    | WriteFile(_, _, HybridArtifact(ObservedMerge _)) -> true
                    | _ -> false)

            Assert.True(materialized < activated)

            Assert.DoesNotContain(
                model.PendingEffects,
                function
                | WriteFile(path, _, _) -> path.Contains("work-model")
                | _ -> false
            )

        // Optional qualification output retains the actual scaffold for the shipped owner's
        // independent helper/readback checks. It is not a package-install assertion.
        match Environment.GetEnvironmentVariable "FSGG_OWNER_GUIDANCE_READBACK_DIR" with
        | null
        | "" -> ()
        | destination ->
            for file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories) do
                let target = Path.Combine(destination, Path.GetRelativePath(root, file))

                Directory.CreateDirectory(
                    Path.GetDirectoryName target |> Option.ofObj |> Option.defaultValue destination
                )
                |> ignore

                File.Copy(file, target, false)

    [<Fact>]
    let ``retained doctor is read only and accepted upgrade preserves prose history and converges`` () =
        if hasPayload () then
            let root, _, initial = scaffoldFixture false
            Assert.Equal(0, exitCodeForReport initial)
            TestSupport.writeRelative root "AGENTS.md" "# Edited Codex café\n"
            TestSupport.writeRelative root "CLAUDE.md" "# Edited Claude α\n"
            TestSupport.writeRelative root ".fsgg/knowledge/custom.json" "knowledge history"
            TestSupport.writeRelative root "docs/atlas/records/prior.json" "immutable Atlas history"
            let before = File.ReadAllBytes(Path.Combine(root, "AGENTS.md"))
            let doctor, report = run root Doctor false false
            Assert.False((Option.get report.Doctor).IsCoherent)

            Assert.DoesNotContain(
                doctor.PendingEffects,
                function
                | WriteFile _ -> true
                | _ -> false
            )

            let _, refused = run root Upgrade false false
            Assert.Equal(1, exitCodeForReport refused)
            Assert.True((before = File.ReadAllBytes(Path.Combine(root, "AGENTS.md"))))
            let _, accepted = run root Upgrade true false
            Assert.Equal(0, exitCodeForReport accepted)
            Assert.False((Option.get accepted.Upgrade).ResidualDrift)
            Assert.StartsWith("# Edited Codex café\n", TestSupport.readRelative root "AGENTS.md")
            Assert.Equal("knowledge history", TestSupport.readRelative root ".fsgg/knowledge/custom.json")
            Assert.Equal("immutable Atlas history", TestSupport.readRelative root "docs/atlas/records/prior.json")
            let _, coherent = run root Doctor false false
            Assert.True((Option.get coherent.Doctor).IsCoherent)
            let second, noOp = run root Upgrade true false
            Assert.True((Option.get noOp.Upgrade).AlreadyCoherent)

            Assert.DoesNotContain(
                second.PendingEffects,
                function
                | WriteFile _ -> true
                | _ -> false
            )

    [<Fact>]
    let ``scaffold never reports complete when second root write fails`` () =
        if hasPayload () then
            let root, _, report = scaffoldFixture true
            Assert.NotEqual(0, exitCodeForReport report)
            Assert.Equal("incomplete", (Option.get report.Scaffold).Outcome)
            Assert.Contains("<!-- atlas:begin -->", TestSupport.readRelative root "AGENTS.md")
            Assert.Equal(Foundation.agentGuidance "Claude", TestSupport.readRelative root "CLAUDE.md.preserved")

    [<Fact>]
    let ``confirmed upgrade seeds a missing root then activates from a fresh contained observation`` () =
        if hasPayload () then
            let root, _, _ = scaffoldFixture false
            File.Delete(Path.Combine(root, "AGENTS.md"))
            let _, report = run root Upgrade true false
            Assert.Equal(0, exitCodeForReport report)
            Assert.False((Option.get report.Upgrade).ResidualDrift)
            Assert.Contains("<!-- atlas:begin -->", TestSupport.readRelative root "AGENTS.md")
            let _, doctor = run root Doctor false false
            Assert.True((Option.get doctor.Doctor).IsCoherent)

    [<Fact>]
    let ``retained owner policy conflict stays visible and upgrade preserves every root`` () =
        if hasPayload () then
            let root, _, _ = scaffoldFixture false
            TestSupport.writeRelative root "AGENTS.md" "# Custom Codex text\n"
            TestSupport.writeRelative root ".fsgg/atlas.json" "custom policy"
            let before = File.ReadAllBytes(Path.Combine(root, "AGENTS.md"))
            let doctor, report = run root Doctor false false
            Assert.False((Option.get report.Doctor).IsCoherent)
            Assert.Contains(report.Diagnostics, fun d -> d.Id = "ownerGuidance.incomplete")

            Assert.DoesNotContain(
                doctor.PendingEffects,
                function
                | WriteFile _ -> true
                | _ -> false
            )

            let upgrade, report = run root Upgrade true false
            Assert.True((Option.get report.Upgrade).ResidualDrift)
            Assert.Contains(report.Diagnostics, fun d -> d.Id = "ownerGuidance.incomplete")

            Assert.DoesNotContain(
                upgrade.PendingEffects,
                function
                | WriteFile(_, _, HybridArtifact(ObservedMerge _)) -> true
                | _ -> false
            )

            Assert.True((before = File.ReadAllBytes(Path.Combine(root, "AGENTS.md"))))
            Assert.Equal("custom policy", TestSupport.readRelative root ".fsgg/atlas.json")


    [<Fact>]
    let ``declined guidance upgrade confirms through the real input edge and preserves authored roots`` () =
        if hasPayload () then
            let root, _, _ = scaffoldFixture false
            TestSupport.writeRelative root "AGENTS.md" "# Edited Codex\n"
            let before = File.ReadAllBytes(Path.Combine(root, "AGENTS.md"))
            let model, report = runWithConfirmation root Upgrade false false (Some "n\n")
            Assert.Equal(0, exitCodeForReport report)
            Assert.True((Option.get report.Upgrade).ResidualDrift)
            Assert.Contains(ReconciliationStepId.ArtifactReSeed, (Option.get report.Upgrade).SkippedStepIds)

            Assert.DoesNotContain(
                model.PendingEffects,
                function
                | WriteFile _ -> true
                | _ -> false
            )

            Assert.True((before = File.ReadAllBytes(Path.Combine(root, "AGENTS.md"))))


    [<Fact>]
    let ``retained concurrent edit is preserved and reports a user conflict with owner digests`` () =
        if hasPayload () then
            let root, _, _ = scaffoldFixture false
            TestSupport.writeRelative root "CLAUDE.md" "# Prior authored edit\n"
            TestSupport.writeRelative root ".concurrent-guidance-edit" "fixture trigger"
            let _, report = run root Upgrade true false
            Assert.Equal(1, exitCodeForReport report)
            Assert.True((Option.get report.Upgrade).ResidualDrift)
            Assert.Equal("# Concurrent authored edit\n", TestSupport.readRelative root "CLAUDE.md")
            Assert.Contains(report.Diagnostics, fun d -> d.Id = "ownerGuidance.concurrentChange")

            Assert.Contains(
                report.Diagnostics,
                fun d ->
                    d.Id = "ownerGuidance.incomplete"
                    && d.Message.Contains("descriptor=", StringComparison.Ordinal)
                    && d.Message.Contains("atlas:", StringComparison.Ordinal)
            )

            Assert.DoesNotContain(report.Diagnostics, fun d -> d.IsToolDefect)


    [<Fact>]
    let ``bare initialized workspace does not activate an embedded owner descriptor`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.writeRelative root OwnerGuidance.descriptorPath "local unverified declaration"

        for command in [ Doctor; Upgrade ] do
            let model, report = run root command true false
            Assert.Equal(0, exitCodeForReport report)

            match command with
            | Doctor -> Assert.True((Option.get report.Doctor).HasProvenance)
            | Upgrade -> Assert.True((Option.get report.Upgrade).HasProvenance)
            | _ -> failwith "unexpected fixture command"

            Assert.DoesNotContain(
                report.Diagnostics,
                fun d -> d.Id.StartsWith("ownerGuidance.", StringComparison.Ordinal)
            )

            Assert.DoesNotContain(
                model.InterpretedEffects,
                fun result ->
                    match result.Effect with
                    | ReadContainedFile _
                    | WriteFile(_, _, HybridArtifact(ObservedMerge _)) -> true
                    | _ -> false
            )

        for path in [ "AGENTS.md"; "CLAUDE.md" ] do
            Assert.DoesNotContain("<!-- atlas:begin -->", TestSupport.readRelative root path)
