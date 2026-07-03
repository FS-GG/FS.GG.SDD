namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open Fsgg.Provider
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal

/// Shared real-filesystem fixture builders for the `doctor`/`upgrade` remediation suites
/// (feature 053). No mocks: fixtures are on-disk scaffold shapes written under a fresh temp
/// root. The installed CLI version is the test build's generator version; fixtures declare a
/// provider minimum above/below it to drive the CLI drift axis.
module RemediationSupport =
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    let installedVersion = SchemaVersionModule.currentGeneratorVersion().Version

    /// A minimum well above any real installed version (drives `cliAxis = behind`).
    let farAheadMinimum = "999.0.0"

    /// A minimum well below any real installed version (`cliAxis = atOrAbove`).
    let farBehindMinimum = "0.0.1"

    let private providersYml (minimum: string option) =
        let minBlock =
            match minimum with
            | Some v -> $"    minimumFsggSdd:\n      version: \"{v}\"\n"
            | None -> ""

        "schemaVersion: 1\nproviders:\n  - name: rendering\n    contractVersion: \"1.0.0\"\n    templateId: fsgg-app\n    source: nuget\n"
        + minBlock
        + "    parameters: []\n"

    let private provenanceJson (minimum: string option) =
        let mv =
            match minimum with
            | Some v -> "\"" + v + "\""
            | None -> "null"

        "{ \"schemaVersion\":1,\"generator\":{\"id\":\"fsgg-sdd\",\"version\":\"0.1.0\"},"
        + $"\"requiredMinimumCliVersion\":{mv},\"providerName\":\"rendering\","
        + "\"providerContractVersion\":\"1.0.0\",\"templateRef\":\"fsgg-app\",\"outcome\":\"providerSucceeded\",\"producedPaths\":[],\"effectiveParameters\":[] }"

    // 058/ADR-0014 P1: drift is content-addressed, so a "present" seeded skill copy must carry
    // its CANONICAL body (not a placeholder), else `verify` reports a hash-mismatch. The canonical
    // seeded body by skill id, and a per-path lookup for a seeded skill copy path.
    let private seededBodyById =
        SeededSkills.seededSkills |> List.map (fun s -> s.Name, s.Body) |> Map.ofList

    let canonicalSkillBody (path: string) : string option =
        Fsgg.SkillMirror.skillIdOfPath path
        |> Option.bind (fun id -> Map.tryFind id seededBodyById)

    /// The `skillBodies` content map (path -> canonical body) for a set of present seeded copies —
    /// the content-addressed input the `Drift.compute` unit tests pass.
    let skillBodiesFor (present: string list) : Map<string, string> =
        present
        |> List.choose (fun path -> canonicalSkillBody path |> Option.map (fun body -> path, body))
        |> Map.ofList

    /// Write a fixture root. `minimum` = provider-declared minimum (None = coherent-by-absence).
    /// `presentArtifacts` = expected seeded paths to materialize as present (the rest are
    /// missing). `withProvenance` = whether to write `.fsgg/scaffold-provenance.json`. A present
    /// seeded skill copy is written with its canonical body (content-addressed coherence); a
    /// non-skill present artifact keeps the placeholder body.
    let makeFixture (minimum: string option) (presentArtifacts: string list) (withProvenance: bool) =
        let root = TestSupport.tempDirectory ()
        TestSupport.writeRelative root ".fsgg/providers.yml" (providersYml minimum)

        if withProvenance then
            TestSupport.writeRelative root ".fsgg/scaffold-provenance.json" (provenanceJson minimum)

        for path in presentArtifacts do
            let body = canonicalSkillBody path |> Option.defaultValue "present\n"
            TestSupport.writeRelative root path body

        root

    /// A fully coherent scaffold: all expected seeded artifacts present, CLI at/above minimum.
    let coherentFixture () =
        makeFixture (Some farBehindMinimum) Drift.expectedArtifactPaths true

    /// A behind scaffold: CLI below the minimum AND every seeded artifact missing.
    let behindMissingFixture () =
        makeFixture (Some farAheadMinimum) [] true

    /// CLI at/above minimum but every seeded artifact missing (only re-seed is actionable).
    let atOrAboveMissingFixture () =
        makeFixture (Some farBehindMinimum) [] true

    /// Provider declares no minimum; all artifacts present (coherent-by-absence, FR-016).
    let noMinimumFixture () =
        makeFixture None Drift.expectedArtifactPaths true

    /// A bare skeleton with no scaffold provenance (FR-015 degradation).
    let noProvenanceFixture () =
        makeFixture (Some farBehindMinimum) [] false

    /// 056: a pre-056 product — the `.claude`/`.codex` seeded copies present, but the third
    /// `.agents/skills/` root entirely missing (scaffolded by a two-root CLI). CLI at/above
    /// minimum so ONLY the third-root re-seed is actionable.
    let pre056Fixture () =
        let present =
            Drift.expectedArtifactPaths
            |> List.filter (fun p -> not (p.StartsWith(".agents/skills/", StringComparison.Ordinal)))

        makeFixture (Some farBehindMinimum) present true

    // 058/ADR-0014 P1: a coherent scaffold that ALSO carries a provider *product* skill —
    // recorded in provenance (produced `.agents` canonical + mirrored `.claude`/`.codex` copies,
    // each with the content digest) and materialized byte-identically to all three roots. The
    // baseline is fully coherent; a test then injects content divergence / skill loss.
    let productSkillId = "fs-gg-demo"
    let productSkillBody = "# fs-gg-demo\n\nA provider product skill.\n"

    // A produced path that merely LOOKS skill-shaped but lives OUTSIDE the provider source root
    // (`.agents/skills/`) — it must never be treated as an agent skill (058 review Finding 1).
    let decoyAppSkillPath = "app/content/skills/widget/SKILL.md"

    let private productProvenanceJson (extraProduced: string list) =
        let digest = Fsgg.SkillMirror.sha256 productSkillBody

        let extra =
            extraProduced
            |> List.map (fun p -> $",{{\"path\":\"{p}\",\"owner\":\"generatedProduct\"}}")
            |> String.concat ""

        "{ \"schemaVersion\":1,\"generator\":{\"id\":\"fsgg-sdd\",\"version\":\"0.1.0\"},"
        + $"\"requiredMinimumCliVersion\":\"{farBehindMinimum}\",\"providerName\":\"rendering\","
        + "\"providerContractVersion\":\"1.0.0\",\"templateRef\":\"fsgg-app\",\"outcome\":\"providerSucceeded\","
        + $"\"producedPaths\":[{{\"path\":\".agents/skills/{productSkillId}/SKILL.md\",\"owner\":\"generatedProduct\",\"sha256\":\"{digest}\"}}{extra}],"
        + $"\"mirroredPaths\":[{{\"path\":\".claude/skills/{productSkillId}/SKILL.md\",\"owner\":\"mirrored\",\"sha256\":\"{digest}\"}},"
        + $"{{\"path\":\".codex/skills/{productSkillId}/SKILL.md\",\"owner\":\"mirrored\",\"sha256\":\"{digest}\"}}],"
        + "\"effectiveParameters\":[] }"

    /// The three root copies of the provider product skill (`.claude`/`.codex`/`.agents`).
    let productSkillCopies =
        Fsgg.Schemas.agentSkillRoots
        |> List.map (fun root -> Fsgg.SkillMirror.skillPath root productSkillId)

    /// A fully coherent scaffold (CLI at/above minimum, every seeded artifact present with its
    /// canonical body) that additionally carries the coherent provider product skill. When
    /// `extraProduced` is non-empty, those extra paths are recorded in provenance `producedPaths`
    /// (used to prove a skill-shaped app file outside `.agents/skills/` is not a phantom skill).
    let productCoherentFixtureWith (extraProduced: string list) =
        let root = TestSupport.tempDirectory ()
        TestSupport.writeRelative root ".fsgg/providers.yml" (providersYml (Some farBehindMinimum))
        TestSupport.writeRelative root ".fsgg/scaffold-provenance.json" (productProvenanceJson extraProduced)

        for path in Drift.expectedArtifactPaths do
            let body = canonicalSkillBody path |> Option.defaultValue "present\n"
            TestSupport.writeRelative root path body

        for path in productSkillCopies do
            TestSupport.writeRelative root path productSkillBody

        root

    let productCoherentFixture () = productCoherentFixtureWith []

    let doctorReport root =
        TestSupport.request Doctor root |> TestSupport.runRequest

    let upgradeYes root =
        { TestSupport.request Upgrade root with
            AssumeYes = true }
        |> TestSupport.runRequest

    /// Non-interactive, no `--yes`: the fail-closed refusal path.
    let upgradeNonInteractive root =
        TestSupport.request Upgrade root |> TestSupport.runRequest

    /// Drive the interactive confirm loop with synthetic scripted stdin (disclosed in the
    /// test name via the `Synthetic` token). Serialized by the `Console` collection.
    let upgradeInteractive root (scriptedStdin: string) =
        let originalIn = Console.In
        let originalOut = Console.Out

        try
            Console.SetIn(new StringReader(scriptedStdin))
            Console.SetOut(new StringWriter())

            { TestSupport.request Upgrade root with
                IsInteractive = true }
            |> TestSupport.runRequest
        finally
            Console.SetIn originalIn
            Console.SetOut originalOut

    /// SHA over every file in the tree, for zero-write / byte-identical assertions.
    let treeHash (root: string) =
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        |> Seq.sort
        |> Seq.map (fun file -> file + ":" + (File.ReadAllText file))
        |> String.concat " "
        |> SchemaVersionModule.sha256Text

    // ----- pure Drift inputs -----

    let record (minimum: string option) : ScaffoldProvenanceRecord =
        { SchemaVersion = 1
          Generator = { Id = "fsgg-sdd"; Version = "0.1.0" }
          RequiredMinimumCliVersion = minimum
          ProviderName = "rendering"
          ProviderContractVersion = "1.0.0"
          TemplateRef = "fsgg-app"
          Outcome = "providerSucceeded"
          ProducedPaths = []
          MirroredPaths = []
          EffectiveParameters = [] }

    let descriptor (minimum: string option) : ProviderDescriptor =
        { Name = "rendering"
          ContractVersion = "1.0.0"
          TemplateId = "fsgg-app"
          Source = "nuget"
          Parameters = []
          Build = None
          Test = None
          Run = None
          Verify = None
          NameParameter = "name"
          MinimumCliVersion = minimum }

    let exitCode (report: CommandReport) = exitCodeForReport report

    let diagnosticIds (report: CommandReport) =
        report.Diagnostics |> List.map (fun d -> d.Id)
