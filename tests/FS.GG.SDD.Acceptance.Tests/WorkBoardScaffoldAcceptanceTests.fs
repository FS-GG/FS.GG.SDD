namespace FS.GG.SDD.Acceptance.Tests

open System.IO
open System.Reflection
open System.Text.Json
open FS.GG.SDD.Commands.CommandTypes
open Xunit
open AcceptanceSupport

/// workBoard W5 (FS.GG.SDD#633, ADR-0064, design §9): the happy-path + graceful-fail acceptance
/// for the `workBoard` driver skill a scaffold materializes into a product workspace.
///
/// `workBoard` is a `.github`-authored `scope: driver` skill delivered as bytes in the pinned
/// `FS.GG.Drivers` package (W4/#632) and materialized at scaffold time. It is **always**
/// materialized, but it **refuses to run unless the workspace is board-capable** (design §9): it
/// composes the coordination kit — `FSGG_COORD_OWNER`/`FSGG_COORD_PROJECT` env, the `scripts/fsgg-coord`
/// engine, and the materialized `check-board`/`pnext-item` skills — that a coordination-wired scaffold
/// carries and a `--no-coordination` scaffold does not. On the first miss it stops cleanly with one
/// documented line naming `workRoadmap` / `new-sdd-workspace --board`, and touches no board.
///
/// Because `workBoard` is agent guidance (a SKILL.md consumed at runtime by the loop's host), the
/// board-capability *decision* it makes is the testable surface. This module encodes that decision
/// as `BoardCapability.evaluate` — the same ordered precondition the skill documents (§4.1) — and
/// drives it against two real fixture-scaffolded workspaces: one wired for coordination (→ drives)
/// and one `--no-coordination` (→ stops cleanly with the documented one-line message). BOTH paths
/// are asserted; the graceful-fail is a first-class assertion, not an afterthought of the happy path.
module WorkBoardScaffoldAcceptanceTests =

    // ----- the documented graceful-fail contract (design §9 / workBoard SKILL.md §4.1) -----

    /// The one-line message the skill prints on the first precondition miss — the
    /// `--no-coordination` (board env absent) case the acceptance names. Held as plain text; the
    /// shipped SKILL.md renders the same clauses with markdown links / backticks, and
    /// `theShippedWorkBoardSkillDocumentsTheGracefulFailMessage` pins the two together so a drift in
    /// the skill's wording fails this suite rather than silently diverging from the assertion.
    let documentedGracefulFailMessage =
        "this workspace has no coordination board (scaffolded --no-coordination?). "
        + "Use workRoadmap for a markdown roadmap, or re-wire with new-sdd-workspace --board owner/title."

    /// The load-bearing clauses of the documented message, exactly as they appear in the shipped
    /// SKILL.md (backticks / markdown link included). Asserting every one is present ties the
    /// test's stop-message to the skill's own bytes.
    let private documentedMessageClauses =
        [ "this workspace has no coordination board (scaffolded"
          "--no-coordination"
          "for a markdown roadmap"
          "new-sdd-workspace --board" ]

    /// The board-capability preconditions the skill also documents (§4.1 items 1-2): the env keys it
    /// reads and the kit skills it composes. A shipped skill that no longer names these is one whose
    /// graceful-fail no longer matches the decision this suite encodes.
    let private documentedPreconditionTokens =
        [ "FSGG_COORD_OWNER"; "FSGG_COORD_PROJECT"; "check-board"; "pnext-item" ]

    // ----- the board-capability decision, encoded from workBoard §4.1 -----

    /// What `workBoard` does when it starts in a workspace: drive the wired board, or stop cleanly
    /// with the documented one-line message.
    type BoardCapability =
        | Drives
        | StopsCleanly of message: string

    module BoardCapability =

        /// The coordination env, read from `.claude/settings.json`'s `env` object exactly where
        /// `new-sdd-workspace`'s `writeCoordinationEnv` records it (and where Claude Code / a
        /// skill-run `fsgg-coord` reads it). A missing file, missing `env`, or a blank value all read
        /// as "not set".
        let private coordEnv (root: string) (key: string) : string option =
            let settings = Path.Combine(root, ".claude", "settings.json")

            if File.Exists settings then
                try
                    use document = JsonDocument.Parse(File.ReadAllText settings)
                    let mutable env = Unchecked.defaultof<JsonElement>
                    let mutable value = Unchecked.defaultof<JsonElement>

                    if
                        document.RootElement.TryGetProperty("env", &env)
                        && env.TryGetProperty(key, &value)
                        && value.ValueKind = JsonValueKind.String
                    then
                        match value.GetString() with
                        | null -> None
                        | s when s.Trim() = "" -> None
                        | s -> Some s
                    else
                        None
                with _ ->
                    None
            else
                None

        let private skillPresent (root: string) (id: string) =
            existsRelative root $".claude/skills/{id}/SKILL.md"

        /// The ordered precondition of design §9 / workBoard §4.1: (1) the board is wired
        /// (`FSGG_COORD_OWNER` and `FSGG_COORD_PROJECT` set), and (2) the kit is present
        /// (`scripts/fsgg-coord` resolves, and `check-board` + `pnext-item` are materialized). Any
        /// miss stops cleanly with the one documented line; only a board-capable workspace drives.
        let evaluate (root: string) : BoardCapability =
            let boardWired =
                (coordEnv root "FSGG_COORD_OWNER").IsSome
                && (coordEnv root "FSGG_COORD_PROJECT").IsSome

            let kitPresent =
                existsRelative root "scripts/fsgg-coord"
                && skillPresent root "check-board"
                && skillPresent root "pnext-item"

            if boardWired && kitPresent then
                Drives
            else
                StopsCleanly documentedGracefulFailMessage

    // ----- fixtures: a real offline scaffold, and the two workspace shapes -----

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
    /// scaffold materializes the SDD skeleton AND the always-on driver skills (workBoard/workRoadmap)
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

        // Every scaffolded workspace — wired or not — carries workBoard (materializes-when: always).
        Assert.True(
            existsRelative root ".claude/skills/workBoard/SKILL.md",
            "a scaffold must materialize the workBoard driver skill into the workspace (W4/#632)."
        )

        root

    /// Wire a scaffolded workspace for coordination exactly as `new-sdd-workspace` does (default ON):
    /// record the board identity in `.claude/settings.json`'s `env`, vendor the `scripts/fsgg-coord`
    /// shim, and materialize the coordination-kit skills. This is the difference between a wired
    /// workspace and a `--no-coordination` one, and thus between workBoard driving and stopping.
    let private wireCoordination (root: string) =
        // 1 · the board env — the default org board (`FS-GG` / `Coordination`), which works on any engine.
        writeRelative
            root
            ".claude/settings.json"
            """{
  "env": {
    "FSGG_COORD_OWNER": "FS-GG",
    "FSGG_COORD_PROJECT": "Coordination"
  }
}"""

        // 2 · the fsgg-coord shim (executable), and 3 · the kit skills workBoard composes.
        writeRelative root "scripts/fsgg-coord" "#!/usr/bin/env bash\nexec fsgg-coord \"$@\"\n"
        let shim = Path.Combine(root, "scripts", "fsgg-coord")
        File.SetUnixFileMode(shim, File.GetUnixFileMode shim ||| UnixFileMode.UserExecute)

        for skill in [ "check-board"; "pnext-item"; "intra-repo-parallel-work"; "cross-repo-coordination" ] do
            writeRelative root $".claude/skills/{skill}/SKILL.md" $"---\nname: {skill}\n---\n"

    // ----- the shipped skill's own bytes: the documented-message ground truth -----

    /// The workBoard SKILL.md body a scaffold materializes, read from the `FS.GG.Drivers` bytes
    /// embedded in the `FS.GG.SDD.Commands` assembly (the same resources `DriverSkills` reads) — the
    /// authoritative source of the "documented one-line message". Robust to the `/` vs `\` a build's
    /// `%(RecursiveDir)` may have baked into the logical resource name.
    let private shippedWorkBoardBody () : string =
        let assembly = typeof<SddCommand>.Assembly

        let name =
            assembly.GetManifestResourceNames()
            |> Array.tryFind (fun n -> n.Replace('\\', '/') = "Driver.skill/workBoard/SKILL.md")
            |> Option.defaultWith (fun () ->
                failwith "the workBoard driver body must be embedded in FS.GG.SDD.Commands (FS.GG.Drivers 0.2.0).")

        match assembly.GetManifestResourceStream name with
        | null -> failwith "the embedded workBoard driver body could not be opened."
        | stream ->
            use stream = stream
            use reader = new StreamReader(stream)
            reader.ReadToEnd()

    // ================================================================================
    // Fact 1 (offline, always-run): the documented message ground truth. The one-line message this
    // suite asserts the graceful-fail path stops with is the one the shipped workBoard skill
    // actually documents — clause for clause — and the skill still names the preconditions the
    // board-capability decision keys on. Reads the embedded bytes; no scaffold, host-independent.
    // ================================================================================

    [<Fact>]
    let ``the shipped workBoard skill documents the graceful-fail message and its preconditions`` () =
        let body = shippedWorkBoardBody ()

        for clause in documentedMessageClauses do
            Assert.True(
                body.Contains clause,
                $"the shipped workBoard skill no longer documents the graceful-fail clause \"{clause}\" — its one-line message drifted from the acceptance's `documentedGracefulFailMessage`."
            )

        for token in documentedPreconditionTokens do
            Assert.True(
                body.Contains token,
                $"the shipped workBoard skill no longer names the board-capability precondition token \"{token}\" — the §4.1 decision this suite encodes no longer matches the skill."
            )

    // ================================================================================
    // Fact 2 (offline, slow, always-run): BOTH paths against real scaffolded workspaces. A
    // coordination-wired workspace makes workBoard DRIVE; a `--no-coordination` workspace makes it
    // STOP CLEANLY with the documented one-line message — asserted, not merely the happy path.
    // ================================================================================

    [<Fact>]
    [<Trait("tier", "slow")>]
    let ``a wired workspace drives workBoard and a no-coordination workspace stops with the documented line`` () =
        // The `--no-coordination` workspace: a plain scaffold — workBoard materialized, but no board
        // env and no kit. workBoard must STOP CLEANLY with the documented one-line message.
        let noCoordinationRoot = scaffoldWorkspace ()

        match BoardCapability.evaluate noCoordinationRoot with
        | StopsCleanly message ->
            Assert.Equal(documentedGracefulFailMessage, message)
            // And the message the skill stops with is the one the skill itself documents.
            let body = shippedWorkBoardBody ()

            for clause in documentedMessageClauses do
                Assert.True(body.Contains clause, $"the stop message clause \"{clause}\" is not documented by the shipped skill.")
        | Drives ->
            Assert.Fail
                "a --no-coordination workspace (no FSGG_COORD_* env, no kit) must make workBoard stop cleanly, not drive — the graceful-fail path (design §9) did not fire."

        // The coordination-wired workspace: the same scaffold, then wired for coordination exactly as
        // `new-sdd-workspace` wires it. workBoard is board-capable and DRIVES its wired board.
        let wiredRoot = scaffoldWorkspace ()
        wireCoordination wiredRoot

        match BoardCapability.evaluate wiredRoot with
        | Drives -> () // happy path: board wired, kit present → workBoard drives.
        | StopsCleanly message ->
            Assert.Fail
                $"a coordination-wired workspace must make workBoard drive its wired board, but it stopped cleanly with: {message}"
