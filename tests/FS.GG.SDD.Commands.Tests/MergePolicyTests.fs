namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open Xunit

/// FS.GG.SDD#309 (child of #304). `HybridArtifact` used to be a bare tag: it said a stage's write
/// was a merge result, but not which regions the merge owned. Three stages hardcoded three
/// different answers, and the prose that documented them disagreed three ways — the lifecycle
/// skill's stage table and `artifact-taxonomy.md` both called every lifecycle artifact "authored",
/// which a single re-run disproves.
///
/// The tag now carries a `MergePolicy`, and the merge functions read their section names from it.
/// These tests pin the two halves that make that load-bearing:
///   1. the policies are internally coherent, and each stage's merge covers exactly what its
///      policy declares — so removing a heading from a policy really does hand that region back;
///   2. the two documents are pinned to `MergePolicies.byStage`, so neither can re-acquire the
///      claim that the tool leaves these files alone.
module MergePolicyTests =

    let private repoRoot = TestSupport.repoRoot

    let private lifecycleSkill =
        Path.Combine(repoRoot, ".claude", "skills", "fs-gg-sdd-lifecycle", "SKILL.md")

    let private taxonomyDoc =
        Path.Combine(repoRoot, "docs", "reference", "artifact-taxonomy.md")

    let private policies =
        MergePolicies.byStage
        |> List.map (fun (command, file, policy) -> commandName command, file, policy)

    // --- 1. The policies are coherent ---------------------------------------------------------

    [<Fact>]
    let ``every lifecycle stage writes a hybrid`` () =
        // The point of #308: no `work/<id>/` artifact is `AuthoredSource`, and none is a pure
        // `GeneratedView`. If a stage ever writes one, this list — and the docs pinned to it —
        // must say so explicitly rather than by omission.
        let files = policies |> List.map (fun (_, file, _) -> file) |> List.sort

        Assert.Equal<string list>(
            [ "charter.md"
              "checklist.md"
              "clarifications.md"
              "evidence.yml"
              "plan.md"
              "spec.md"
              "tasks.yml" ],
            files
        )

    [<Fact>]
    let ``rederived and appended sections are disjoint subsets of ensured`` () =
        for stage, file, policy in policies do
            let ensured = MergePolicy.ensuredSections policy |> Set.ofList
            let rederived = MergePolicy.rederivedSections policy |> Set.ofList
            let appended = MergePolicy.appendedSections policy |> Set.ofList

            Assert.True(
                Set.isSubset rederived ensured,
                $"{stage} ({file}): re-derives sections it does not ensure exist: {Set.difference rederived ensured}"
            )

            Assert.True(
                Set.isSubset appended ensured,
                $"{stage} ({file}): appends to sections it does not ensure exist: {Set.difference appended ensured}"
            )

            // A section cannot be both replaced wholesale and appended to: the replace would
            // discard the append, or the append would double the replaced body, depending on order.
            Assert.True(
                Set.isEmpty (Set.intersect rederived appended),
                $"{stage} ({file}): sections are both re-derived and appended to: {Set.intersect rederived appended}"
            )

    [<Fact>]
    let ``each markdown policy ensures exactly its artifact's standard sections`` () =
        let expected =
            [ "charter.md", MergePolicies.charterSections
              "spec.md", specificationStandardSections ()
              "clarifications.md", clarificationStandardSections ()
              "checklist.md", checklistStandardSections ()
              "plan.md", planStandardSections () ]

        for file, sections in expected do
            let _, _, policy = policies |> List.find (fun (_, candidate, _) -> candidate = file)
            Assert.Equal<string list>(sections, MergePolicy.ensuredSections policy)

    [<Fact>]
    let ``the two yaml artifacts merge structurally, not by section`` () =
        for file in [ "tasks.yml"; "evidence.yml" ] do
            let _, _, policy = policies |> List.find (fun (_, candidate, _) -> candidate = file)
            Assert.Equal(StructuredMerge, policy)

    // --- 2. The merge functions really are driven by the policy --------------------------------

    /// `rederiveChecklist` and `appendPlanEntries` fold over their policy's section list and
    /// `Map.find` each body. A heading in the policy with no body raises — which is the whole
    /// point (the policy cannot name a region the stage has nothing to put in) — so exercise both
    /// folds over the real policies here rather than discovering it on a user's first re-run.
    [<Fact>]
    let ``rederiveChecklist covers every section its policy declares`` () =
        let workId = "001-example"

        let ensured = ChecklistAuthoring.ensureChecklistSections workId "# Checklist\n"

        let text = ChecklistAuthoring.rederiveChecklist workId "spec" "clar" [] ensured

        for heading in MergePolicy.rederivedSections MergePolicies.checklist do
            Assert.Contains($"## {heading}", text)

    [<Fact>]
    let ``a section the checklist policy does not re-derive keeps the author's body`` () =
        // The behavioural statement the docs now make: drop a heading from `rederived` and the
        // author owns it. `Advisory Notes` is ensured but never re-derived.
        let workId = "001-example"

        let authored =
            ChecklistAuthoring.ensureChecklistSections workId "# Checklist\n"
            |> EarlyStageAuthoring.replaceSectionBody "Advisory Notes" [ "- Mine, and mine alone." ]

        Assert.DoesNotContain(
            "Advisory Notes",
            MergePolicy.rederivedSections MergePolicies.checklist |> String.concat "|"
        )

        let text = ChecklistAuthoring.rederiveChecklist workId "spec" "clar" [] authored

        Assert.Contains("- Mine, and mine alone.", text)

    [<Fact>]
    let ``mergeAuthoredTaskState under a section policy preserves the prior graph`` () =
        // Mis-tagging `tasks.yml` as a markdown hybrid must cost a regeneration, never an author's
        // `status`/`owner`. The merge refuses rather than replacing what it cannot carry.
        let prior =
            [ TaskGraphAuthoring.plannedTask "001-example" [] "Carry me" [] [] [] [] 1 1 ]

        // `StructuredMerge` would drop this task: nothing derived matches its title and it carries
        // no live disposition ref, so it is an orphan. A section policy cannot make that judgement
        // at all, so it must hand the prior graph back untouched.
        let dropped =
            TaskGraphAuthoring.mergeAuthoredTaskState MergePolicies.tasks Set.empty prior []

        Assert.Empty dropped

        let preserved =
            TaskGraphAuthoring.mergeAuthoredTaskState MergePolicies.checklist Set.empty prior []

        Assert.Equal<string list>(
            prior |> List.map (fun task -> task.Id.Value),
            preserved |> List.map (fun task -> task.Id.Value)
        )

    // --- 3. The prose is pinned to the tag -----------------------------------------------------

    let private lines (path: string) =
        File.ReadAllText(path).Replace("\r\n", "\n").Split('\n') |> Array.toList

    /// `| a | b | c |` -> `["a"; "b"; "c"]`. Separator rows (`|---|`) yield dashes and are skipped
    /// by the callers, which key on a known first cell.
    let private cells (row: string) =
        row.Trim().Trim('|').Split('|') |> Array.map _.Trim() |> Array.toList

    /// AC2. The `fs-gg-sdd-lifecycle` stage table's third column names the `work/<id>/` file each
    /// stage writes. Every one of those files is a `HybridArtifact`, so the column header must say
    /// so, and the rows must name exactly the files `MergePolicies.byStage` names — no more (a
    /// stage that writes nothing must show `—`) and no fewer.
    [<Fact>]
    let ``the lifecycle skill's stage table agrees with the write tag`` () =
        let table =
            lines lifecycleSkill
            |> List.filter (fun line -> line.StartsWith "|")
            |> List.map cells

        let header =
            table
            |> List.tryFind (fun row -> List.tryHead row = Some "Stage")
            |> Option.defaultWith (fun () -> failwith "fs-gg-sdd-lifecycle: no stage table found.")

        let ownershipColumn = header.[2]

        Assert.True(
            ownershipColumn.IndexOf("hybrid", StringComparison.OrdinalIgnoreCase) >= 0,
            "fs-gg-sdd-lifecycle: the stage table's source column must name the hybrid class — every "
            + $"`work/<id>/` artifact is written `HybridArtifact`, not authored outright. Found: '{ownershipColumn}'."
        )

        let stageRows =
            table
            |> List.filter (fun row -> row.Length = 5 && row.[1].StartsWith "`fsgg-sdd ")

        Assert.Equal(10, stageRows.Length)

        let declared =
            MergePolicies.byStage
            |> List.map (fun (command, file, _) -> commandName command, file)
            |> Map.ofList

        for row in stageRows do
            let stage = row.[0]
            let cell = row.[2]

            match Map.tryFind stage declared with
            | Some file ->
                Assert.True(
                    cell.Contains $"`{file}`",
                    $"fs-gg-sdd-lifecycle: stage '{stage}' writes {file} as a hybrid, but its table row says '{cell}'."
                )
            | None ->
                Assert.True(
                    (cell = "—"),
                    $"fs-gg-sdd-lifecycle: stage '{stage}' writes no `work/<id>/` source, but its table row says '{cell}'."
                )

    /// AC3. The taxonomy's durable table classified all seven lifecycle artifacts as "authored",
    /// which is the claim `HybridArtifact` denies. Pin each row to the tag. `contracts/…` is the
    /// one genuinely authored entry — it has no write site at all — so it must NOT say hybrid.
    [<Fact>]
    let ``the artifact taxonomy names the hybrid class for every lifecycle artifact`` () =
        let rows =
            lines taxonomyDoc
            |> List.filter (fun line -> line.StartsWith "|")
            |> List.map cells
            |> List.filter (fun row -> row.Length = 3)

        let reasonFor (path: string) =
            rows
            |> List.tryPick (fun row -> if row.[1] = $"`{path}`" then Some row.[2] else None)
            |> Option.defaultWith (fun () -> failwith $"artifact-taxonomy.md: no durable row for {path}.")

        for _, file, _ in MergePolicies.byStage do
            let reason = reasonFor $"work/<id>/{file}"

            Assert.True(
                reason.StartsWith("hybrid", StringComparison.OrdinalIgnoreCase),
                $"artifact-taxonomy.md: `work/<id>/{file}` is written `HybridArtifact`, but its row reads '{reason}'."
            )

        let contracts = reasonFor "work/<id>/contracts/…"

        Assert.False(
            contracts.IndexOf("hybrid", StringComparison.OrdinalIgnoreCase) >= 0,
            $"artifact-taxonomy.md: `contracts/…` has no write site — it is `AuthoredSource`, not a hybrid. Found '{contracts}'."
        )

    /// AC4. `checklist.md` and `tasks.yml` are re-derived by their stage, so a skill that hands the
    /// author a block to copy into them teaches work the next run erases. The remaining marked
    /// example must show only regions the policy leaves alone.
    [<Fact>]
    let ``the checklist and tasks skills teach no tool-owned region`` () =
        let checklistSkill =
            File.ReadAllText(Path.Combine(repoRoot, ".claude", "skills", "fs-gg-sdd-checklist", "SKILL.md"))

        for heading in MergePolicy.rederivedSections MergePolicies.checklist do
            Assert.False(
                checklistSkill.Contains $"\n## {heading}\n- ",
                $"fs-gg-sdd-checklist: shows an authored body under `## {heading}`, which `checklist` re-derives."
            )

        let tasksSkill =
            File.ReadAllText(Path.Combine(repoRoot, ".claude", "skills", "fs-gg-sdd-tasks", "SKILL.md"))

        Assert.DoesNotContain("schemaVersion: 1", tasksSkill)
