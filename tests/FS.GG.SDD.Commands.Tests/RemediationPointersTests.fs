namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.Internal
open Xunit

/// Feature 078 (#125) guard: every authoring-grammar blocking diagnostic carries a *resolving*
/// remediation pointer, and no pointer dangles. Encodes contract invariants 1–6 from
/// specs/078-diagnostic-remediation-pointers/contracts/remediation-pointer.md over the live
/// `RemediationPointers.registry` and the on-disk example/anchor targets. If a grammar heading is
/// renamed or an example file is moved, this fails until the citation (or the target) is fixed.
module RemediationPointersTests =

    /// GitHub heading-slug algorithm: lowercase, drop everything that is not alphanumeric, space,
    /// or hyphen (so backticks and periods vanish), then spaces → hyphens (consecutive hyphens are
    /// preserved, e.g. "specify --input" → "specify---input").
    let private slugify (headingText: string) : string =
        headingText.ToLowerInvariant()
        |> Seq.filter (fun c -> Char.IsLetterOrDigit c || c = ' ' || c = '-')
        |> Seq.map (fun c -> if c = ' ' then '-' else c)
        |> Seq.toArray
        |> String

    /// The set of anchor slugs the live authoring-contracts.md actually exposes.
    let private liveAnchorSlugs () : Set<string> =
        let markdown =
            TestSupport.readRelative TestSupport.repoRoot "docs/reference/authoring-contracts.md"

        markdown.Replace("\r\n", "\n").Split('\n')
        |> Array.choose (fun line ->
            let trimmed = line.TrimStart()

            if trimmed.StartsWith("#") then
                Some(slugify (trimmed.TrimStart('#').Trim()))
            else
                None)
        |> Set.ofArray

    let private anchorSlug (anchor: string) =
        anchor.Substring(anchor.IndexOf('#') + 1)

    // --- Invariant 1: coverage — every covered id renders a non-empty suffix ---

    [<Fact>]
    let ``every covered id renders a non-empty pointer suffix`` () =
        Assert.NotEmpty RemediationPointers.registry

        for KeyValue(id, _) in RemediationPointers.registry do
            Assert.False(
                String.IsNullOrWhiteSpace(RemediationPointers.suffixFor id),
                $"covered id '{id}' renders an empty pointer suffix"
            )

    // --- Invariant 2: every cited example path exists on disk ---

    [<Fact>]
    let ``every cited example path resolves on disk`` () =
        for KeyValue(id, pointer) in RemediationPointers.registry do
            match pointer.Example with
            | Some relative ->
                let full =
                    Path.Combine(TestSupport.repoRoot, relative.Replace('/', Path.DirectorySeparatorChar))

                Assert.True(File.Exists full, $"covered id '{id}' cites missing example '{relative}'")
            | None -> ()

    // --- Invariant 3: every cited anchor resolves to a real authoring-contracts.md heading ---

    [<Fact>]
    let ``every cited grammar anchor resolves to a live heading`` () =
        let slugs = liveAnchorSlugs ()

        for KeyValue(id, pointer) in RemediationPointers.registry do
            match pointer.Anchor with
            | Some anchor ->
                Assert.Contains("docs/reference/authoring-contracts.md#", anchor)

                Assert.True(
                    Set.contains (anchorSlug anchor) slugs,
                    $"covered id '{id}' cites anchor '{anchor}' with no matching heading in authoring-contracts.md"
                )
            | None -> ()

    [<Fact>]
    let ``every registry entry cites at least one target`` () =
        for KeyValue(id, pointer) in RemediationPointers.registry do
            Assert.True(
                pointer.Example.IsSome || pointer.Anchor.IsSome,
                $"covered id '{id}' cites neither an example nor an anchor"
            )

    // --- Invariant 4: determinism — no absolute path / timestamp / env content ---

    [<Fact>]
    let ``pointer suffixes are deterministic and environment-independent`` () =
        for KeyValue(id, _) in RemediationPointers.registry do
            let suffix = RemediationPointers.suffixFor id
            Assert.Equal(suffix, RemediationPointers.suffixFor id) // idempotent
            Assert.DoesNotContain(TestSupport.repoRoot, suffix) // no absolute path
            Assert.DoesNotContain("\\", suffix) // POSIX separators only

            Assert.False(
                Seq.exists Char.IsDigit suffix,
                $"pointer for '{id}' contains a digit (possible timestamp/version)"
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

            Assert.Contains("docs/examples/lifecycle-artifacts/", diagnostic.Correction)
            Assert.Contains("docs/reference/authoring-contracts.md#", diagnostic.Correction)

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
            Assert.DoesNotContain("docs/examples/lifecycle-artifacts/", diagnostic.Correction)
            Assert.DoesNotContain("docs/reference/authoring-contracts.md#", diagnostic.Correction)
