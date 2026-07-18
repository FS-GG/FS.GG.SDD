namespace FS.GG.SDD.Commands.Tests

open System
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.Internal
open Xunit

/// Feature 078 (#125) guard: every authoring-grammar blocking diagnostic carries a *resolving*
/// remediation pointer, and no pointer dangles. Encodes contract invariants 1–7 from
/// specs/078-diagnostic-remediation-pointers/contracts/remediation-pointer.md over the live
/// `RemediationPointers.registry` and the on-disk vendored skills the pointers cite. If a grammar
/// section in the fs-gg-sdd-authoring-contracts skill is renamed or a stage skill is removed, this
/// fails until the citation (or the target) is fixed.
///
/// The pointer targets the vendored `fs-gg-sdd-*` process skills (present in every scaffold and in
/// this tool repo) rather than tool-repo-only docs (FS.GG.SDD#539) — so resolution here checks the
/// on-disk skill trees, not `docs/`.
module RemediationPointersTests =

    /// The agent-skill roots a scaffold vendors byte-identically (drift-guarded). A cited skill
    /// resolves when its SKILL.md is present under any one of them: this tool repo carries the
    /// `.claude`/`.codex` roots; a scaffold additionally carries the neutral `.agents` root.
    let private skillRoots = [ ".claude"; ".codex"; ".agents" ]

    let private skillRelPath (root: string) (name: string) = $"{root}/skills/{name}/SKILL.md"

    let private skillExists (name: string) : bool =
        skillRoots
        |> List.exists (fun root -> TestSupport.existsRelative TestSupport.repoRoot (skillRelPath root name))

    /// The cross-cutting skill whose numbered `## N. …` section anchors the grammar pointers cite,
    /// resolved from whichever vendored root is present (the roots are byte-identical). Root-agnostic
    /// like `skillExists`, so removing one root does not turn a resolvable anchor into a crash.
    let private authoringContractsSkill =
        skillRoots
        |> List.map (fun root -> skillRelPath root "fs-gg-sdd-authoring-contracts")
        |> List.tryFind (TestSupport.existsRelative TestSupport.repoRoot)
        |> Option.defaultWith (fun () ->
            failwith "fs-gg-sdd-authoring-contracts skill present under no agent-skill root")

    /// GitHub heading-slug algorithm: lowercase, drop everything that is not alphanumeric, space,
    /// or hyphen (so backticks and periods vanish), then spaces → hyphens (consecutive hyphens are
    /// preserved, e.g. "specify --input" → "specify---input").
    let private slugify (headingText: string) : string =
        headingText.ToLowerInvariant()
        |> Seq.filter (fun c -> Char.IsLetterOrDigit c || c = ' ' || c = '-')
        |> Seq.map (fun c -> if c = ' ' then '-' else c)
        |> Seq.toArray
        |> String

    /// The set of anchor slugs the live fs-gg-sdd-authoring-contracts skill actually renders. `#`
    /// lines inside fenced code blocks are skipped — the skill embeds example artifacts whose
    /// `## Decisions` etc. are prose, not clickable GitHub anchors, so counting them would let a
    /// registry anchor "resolve" against a heading that does not exist in the rendered doc.
    let private liveAnchorSlugs () : Set<string> =
        let markdown = TestSupport.readRelative TestSupport.repoRoot authoringContractsSkill

        let mutable inFence = false

        markdown.Replace("\r\n", "\n").Split('\n')
        |> Array.choose (fun line ->
            let trimmed = line.TrimStart()

            if trimmed.StartsWith("```") || trimmed.StartsWith("~~~") then
                inFence <- not inFence
                None
            elif (not inFence) && trimmed.StartsWith("#") then
                Some(slugify (trimmed.TrimStart('#').Trim()))
            else
                None)
        |> Set.ofArray

    // --- Invariant 1: coverage — every covered id renders a non-empty suffix ---

    [<Fact>]
    let ``every covered id renders a non-empty pointer suffix`` () =
        Assert.NotEmpty RemediationPointers.registry

        for KeyValue(id, _) in RemediationPointers.registry do
            Assert.False(
                String.IsNullOrWhiteSpace(RemediationPointers.suffixFor id),
                $"covered id '{id}' renders an empty pointer suffix"
            )

    // --- Invariant 2: every cited skill is present on disk under some agent-skill root ---

    [<Fact>]
    let ``every cited skill resolves on disk`` () =
        for KeyValue(id, pointer) in RemediationPointers.registry do
            match pointer.Skill with
            | Some name -> Assert.True(skillExists name, $"covered id '{id}' cites missing skill '{name}'")
            | None -> ()

    // --- Invariant 3: every cited grammar anchor resolves to a real authoring-contracts skill heading ---

    [<Fact>]
    let ``every cited grammar anchor resolves to a live heading`` () =
        let slugs = liveAnchorSlugs ()

        for KeyValue(id, pointer) in RemediationPointers.registry do
            match pointer.Grammar with
            | Some slug ->
                Assert.True(
                    Set.contains slug slugs,
                    $"covered id '{id}' cites grammar anchor '{slug}' with no matching heading in the fs-gg-sdd-authoring-contracts skill"
                )
            | None -> ()

    [<Fact>]
    let ``every registry entry cites at least one target`` () =
        for KeyValue(id, pointer) in RemediationPointers.registry do
            Assert.True(
                pointer.Skill.IsSome || pointer.Grammar.IsSome,
                $"covered id '{id}' cites neither a skill nor a grammar anchor"
            )

    // --- Invariant 4: determinism — no absolute path, backslash, or other env-dependent content ---
    // A grammar anchor legitimately carries a section number (e.g. "3-specify---input-…"), so the
    // suffix is NOT digit-free; determinism instead means the suffix is a pure function of the
    // static registry — no absolute path, machine name, or timestamp, and POSIX separators only.

    [<Fact>]
    let ``pointer suffixes are deterministic and environment-independent`` () =
        for KeyValue(id, _) in RemediationPointers.registry do
            let suffix = RemediationPointers.suffixFor id
            Assert.DoesNotContain(TestSupport.repoRoot, suffix) // no absolute path
            Assert.DoesNotContain("\\", suffix) // POSIX separators only

    // --- Registry keys are real diagnostic ids ---
    // The suffix is appended in the shared `commandDiagnostic` keyed on the diagnostic id string, so
    // a registry key that is a typo or a renamed/removed id would silently attach a pointer to
    // nothing (and the self-referential invariant-1 iteration would not catch it). Pin every key to
    // a quoted id literal in the constructor source so key drift fails the build.
    [<Fact>]
    let ``every registry key is a real diagnostic id in DiagnosticConstructors`` () =
        let constructorSource =
            TestSupport.readRelative
                TestSupport.repoRoot
                "src/FS.GG.SDD.Commands/CommandReports/DiagnosticConstructors.fs"

        for KeyValue(id, _) in RemediationPointers.registry do
            Assert.True(
                constructorSource.Contains($"\"{id}\""),
                $"registry key '{id}' is not emitted as a quoted id literal by any constructor — typo or renamed diagnostic?"
            )

    // --- Invariant 5: containment — a representative covered diagnostic ends with its suffix ---

    let private representativeCovered: Diagnostic list =
        [ malformedCharterFrontMatter "work/x/charter.md" "bad charter front matter"
          missingSpecificationId "work/x/spec.md" "requirement"
          missingClarificationAnswer "work/x/clarifications.md" [ "AMB-001" ]
          failedChecklistPrerequisite "work/x/checklist.md" "coverage failed" []
          malformedPlanFrontMatter "work/x/plan.md" "bad plan front matter"
          duplicateTaskId "work/x/tasks.yml" "T001"
          undisclosedSyntheticEvidence "work/x/evidence.yml" [ "E-001" ]
          missingRequiredTest "work/x/evidence.yml" [ "O-001" ] ]

    [<Fact>]
    let ``covered diagnostics end with their remediation pointer`` () =
        for diagnostic in representativeCovered do
            let suffix = RemediationPointers.suffixFor diagnostic.Id
            Assert.NotEqual<string>("", suffix)

            Assert.True(
                diagnostic.Correction.EndsWith(suffix, StringComparison.Ordinal),
                $"correction for '{diagnostic.Id}' does not end with its pointer: {diagnostic.Correction}"
            )

            // The pointer names a vendored skill (never a `.claude`/`.codex`/`.agents` path).
            Assert.Contains("fs-gg-sdd-", diagnostic.Correction)
            Assert.Contains(" skill", diagnostic.Correction)

    // --- Invariant 6: non-interference — non-covered corrections carry no pointer ---

    let private representativeNonCovered: Diagnostic list =
        [ outsideProject ()
          unsafeOverwrite "work/x/spec.md"
          toolDefect None "some tool failure"
          missingProjectConfig ".fsgg/project.yml" ]

    [<Fact>]
    let ``non-covered diagnostics carry no remediation pointer`` () =
        for diagnostic in representativeNonCovered do
            Assert.Equal("", RemediationPointers.suffixFor diagnostic.Id)
            Assert.DoesNotContain("fs-gg-sdd-", diagnostic.Correction)
