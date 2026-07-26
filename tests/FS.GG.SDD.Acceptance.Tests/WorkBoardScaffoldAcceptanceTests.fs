namespace FS.GG.SDD.Acceptance.Tests

open System.IO
open System.Reflection
open FS.GG.SDD.Commands.CommandTypes
open Xunit
open AcceptanceSupport

/// Acceptance for the package-delivered board drivers materialized into product workspaces.
///
/// `work-board` and `padd-item` are `.github`-authored `scope: driver` skills carried by the pinned
/// `FS.GG.Drivers` package. The materializer must preserve their exact package bytes. Because these
/// are agent guidance rather than executable application code, their load-bearing runtime contract
/// is pinned directly from the embedded body: work-board's immediate two-line reporting and
/// padd-item's configured-board/no-mutation boundary.
module WorkBoardScaffoldAcceptanceTests =

    /// The current work-board body no longer owns workspace wiring; padd-item below owns and tests
    /// that boundary. Pin work-board's host-loop and immediate two-line reporting contract here.
    let private documentedWorkBoardTokens =
        [ "check-board"
          "backlog-triage"
          "pnext-item"
          "Whenever the host changes or observes a material transition"
          "<item> — <new status>"
          "Active:" ]

    // ----- fixture: a real offline scaffold -----

    let private fixturesRoot =
        Path.Combine(repoRoot, "tests", "fixtures", "scaffold-provider")

    /// Install the committed `ok` fixture registry (a local `dotnet new` provider, no network),
    /// resolving its `__FIXTURE__` token — mirrors ScaffoldCommandTests' `writeRegistry`.
    let private writeFixtureRegistry (root: string) =
        let template =
            File.ReadAllText(Path.Combine(fixturesRoot, "registries", "ok.providers.yml"))

        let resolved = template.Replace("__FIXTURE__", fixturesRoot.Replace('\\', '/'))
        writeRelative root ".fsgg/providers.yml" resolved

    /// Scaffold a fresh workspace over the offline fixture provider and assert it succeeded. The
    /// scaffold materializes the SDD skeleton AND the always-on driver skills
    /// (padd-item/work-board/work-roadmap)
    /// from the embedded `FS.GG.Drivers` bytes — the substrate this acceptance stands on.
    let private scaffoldWorkspace () : string =
        let root = newProductRoot ()
        writeFixtureRegistry root

        let report =
            { request Scaffold root with
                Provider = Some "fixture"
                Parameters = [ "productName", "Acme" ] }
            |> runRequest

        let summary = scaffoldSummary report

        Assert.True(
            summary.Outcome = "providerSucceeded",
            $"the offline fixture scaffold did not succeed (outcome={summary.Outcome}, diagnostics=%A{diagnosticIds report})."
        )

        // Every scaffolded workspace — wired or not — carries work-board (materializes-when: always).
        Assert.True(
            existsRelative root ".claude/skills/work-board/SKILL.md",
            "a scaffold must materialize the work-board driver skill into the workspace (W4/#632)."
        )

        Assert.True(
            existsRelative root ".claude/skills/padd-item/SKILL.md",
            "a scaffold must materialize the padd-item product-workspace board filer (#703)."
        )

        root

    // ----- the shipped skill's own bytes -----

    /// The work-board SKILL.md body a scaffold materializes, read from the `FS.GG.Drivers` bytes
    /// embedded in the `FS.GG.SDD.Commands` assembly (the same resources `DriverSkills` reads) — the
    /// authoritative source of the "documented one-line message". Robust to the `/` vs `\` a build's
    /// `%(RecursiveDir)` may have baked into the logical resource name.
    let private shippedDriverBody (id: string) : string =
        let assembly = typeof<SddCommand>.Assembly

        let name =
            assembly.GetManifestResourceNames()
            |> Array.tryFind (fun n -> n.Replace('\\', '/') = $"Driver.skill/{id}/SKILL.md")
            |> Option.defaultWith (fun () ->
                failwith $"the {id} driver body must be embedded in FS.GG.SDD.Commands (FS.GG.Drivers 0.8.3).")

        match assembly.GetManifestResourceStream name with
        | null -> failwith $"the embedded {id} driver body could not be opened."
        | stream ->
            use stream = stream
            use reader = new StreamReader(stream)
            reader.ReadToEnd()

    let private shippedWorkBoardBody () = shippedDriverBody "work-board"

    [<Fact>]
    let ``the shipped work-board skill documents its host loop and immediate status reporting`` () =
        let body = shippedWorkBoardBody ()

        for token in documentedWorkBoardTokens do
            Assert.True(
                body.Contains token,
                $"the shipped work-board skill no longer carries the host-loop/status-reporting token \"{token}\"."
            )

    [<Fact>]
    let ``the shipped padd-item skill targets only the configured board and fails without mutation when wiring is missing``
        ()
        =
        let body = shippedDriverBody "padd-item"

        for token in
            [ "FSGG_COORD_OWNER_TYPE"
              "FSGG_COORD_OWNER"
              "FSGG_COORD_PROJECT"
              "organization/named-user owner"
              "authenticated viewer's board"
              "Never silently fall back to the FS-GG organization board"
              "stop non-zero without mutation"
              "new-sdd-workspace retrofit"
              "work-roadmap" ] do
            Assert.True(
                body.Contains token,
                $"the package-delivered padd-item body no longer carries the configured-board or no-mutation contract token \"{token}\"."
            )

    [<Fact>]
    [<Trait("tier", "slow")>]
    let ``a fresh product scaffold carries the exact package-delivered padd-item body in all roots`` () =
        let root = scaffoldWorkspace ()
        let delivered = shippedDriverBody "padd-item"

        for skillRoot in [ ".agents"; ".claude"; ".codex" ] do
            let path = Path.Combine(root, skillRoot, "skills", "padd-item", "SKILL.md")
            Assert.True(File.Exists path, $"expected materialized padd-item at {path}")
            Assert.Equal(delivered, File.ReadAllText path)
