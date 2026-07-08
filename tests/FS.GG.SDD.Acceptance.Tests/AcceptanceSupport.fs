namespace FS.GG.SDD.Acceptance.Tests

open System
open System.Diagnostics
open System.IO
open Fsgg.Provider
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open Xunit

/// Shared, network-gated harness for the real-provider composition acceptance. It carries
/// NO rendering package id / template id / path / docs URL (FR-009): the real provider
/// identity is reached only through the external registry named by
/// `FSGG_SDD_ACCEPTANCE_REGISTRY` and copied verbatim into the product's `.fsgg/`.
module AcceptanceSupport =
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    // Delegates to the shared primitives (feature 067 / FR-010).
    let findRepoRoot = FS.GG.SDD.TestShared.TestShared.findRepoRoot
    let repoRoot = FS.GG.SDD.TestShared.TestShared.repoRoot

    // ---------- T003: env gating + run scaffolding ----------

    let registryEnvVar = "FSGG_SDD_ACCEPTANCE_REGISTRY"
    let resultPathEnvVar = "FSGG_SDD_ACCEPTANCE_RESULT_PATH"

    /// Dynamically skip the current fact (xUnit v2 dynamic skip): `SkipException.ForSkip`
    /// produces the runner's dynamic-skip token, reported as Skipped — not passed, not failed.
    let skipTest (reason: string) : 'a =
        raise (Xunit.Sdk.SkipException.ForSkip reason)

    /// The external registry path, when the gating env var is set to a non-empty value.
    let registryPath () =
        Environment.GetEnvironmentVariable registryEnvVar
        |> Option.ofObj
        |> Option.map (fun value -> value.Trim())
        |> Option.filter (fun value -> value <> "")

    /// The offline-inner-loop guard: when the registry env is unset/empty the call SKIPs (it
    /// raises the xUnit `SkipException`), so no network is touched. Returns the resolved
    /// registry path when present. (Discovery-time gating is done by `RequiresRegistryFact`
    /// below; this guard protects any direct call site.)
    let requireRegistry () =
        match registryPath () with
        | Some path when File.Exists path -> path
        | Some path -> skipTest $"{registryEnvVar} points to a missing file: {path}"
        | None -> skipTest $"{registryEnvVar} is unset; the composition acceptance is opt-in and network-gated."

    /// A `[<Fact>]` that is **statically skipped at discovery** when the registry env is
    /// unset/empty, so the default offline `dotnet test` reports the network-gated facts as
    /// Skipped and stays green (contracts/acceptance-protocol.md §Gating). Discovery-time
    /// static skip is used because the pinned v3-era VSTest adapter does not convert xUnit v2's
    /// dynamic-skip token to a Skipped result. The scheduled workflow sets the env, so the same
    /// facts run there.
    type RequiresRegistryFactAttribute() as this =
        inherit Xunit.FactAttribute()

        do
            match registryPath () with
            | Some path when File.Exists path -> ()
            | _ -> this.Skip <- $"{registryEnvVar} is unset; the composition acceptance is opt-in and network-gated."

    /// A fresh, empty product root for one run (temp dir, sensed — never compared). Nested under
    /// the shared per-run temp root so it is swept at process exit (feature 067 / FR-007).
    let newProductRoot = FS.GG.SDD.TestShared.TestShared.tempDirectory

    /// Copy the external registry file into `<root>/.fsgg/providers.yml` verbatim. The
    /// registry is the only channel carrying the real template identity; it is never
    /// committed to this repo.
    let copyRegistry (registry: string) (root: string) =
        let target = Path.Combine(root, ".fsgg", "providers.yml")

        match Path.GetDirectoryName target with
        | null -> ()
        | directory -> Directory.CreateDirectory directory |> ignore

        File.Copy(registry, target, true)

    let writeRelative = FS.GG.SDD.TestShared.TestShared.writeRelative

    let existsRelative (root: string) (path: string) =
        File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)))

    let readRelative (root: string) (path: string) =
        File.ReadAllText(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)))

    // ---------- T004: in-process driver over the real provider ----------

    /// A neutral command request (mirrors `FS.GG.SDD.Commands.Tests.TestSupport.request`).
    let request (command: SddCommand) (root: string) =
        { Command = command
          ProjectRoot = root
          WorkId = None
          Title = None
          InputText = None
          OutputFormat = Json
          DryRun = false
          GeneratorVersion = SchemaVersionModule.currentGeneratorVersion ()
          Provider = None
          Parameters = []
          Force = false
          TemplateUpdate = true
          AssumeYes = false
          IsInteractive = false
          Artifact = None
          Explain = false
          FromTests = None
          SurfaceUpdate = false
          AcceptUpstream = false }

    /// The acceptance's fixed composition request: `--provider rendering --param
    /// lifecycle=sdd`. `rendering` is the author-supplied provider *name* (a generic
    /// token, not an identifier); the real template identity lives only in the registry.
    ///
    /// 050 T016 (FR-006): the request carries NO explicit starter parameter — only the
    /// generic `lifecycle=sdd` marker. Omitting any starter selection is precisely the
    /// by-reference default-starter exercise: whatever default starter the Templates-owned
    /// registry declares is forwarded, never named here (FR-004). The `effectiveParameters`
    /// recorded in provenance reflect exactly what was sent (no invented starter key).
    let scaffoldRequest (root: string) =
        { request Scaffold root with
            Provider = Some "rendering"
            Parameters = [ "lifecycle", "sdd" ] }

    /// Drive the `init`→…→Scaffold MVU loop to quiescence and return the `--json`
    /// `CommandReport` (mirrors `TestSupport.runRequest`).
    let runRequest request =
        let model, effects = init request

        let rec interpretUntilIdle state pending =
            match pending with
            | [] -> state
            | effects ->
                let results = interpretAll request.ProjectRoot request.DryRun effects

                let nextState, nextEffects =
                    results
                    |> List.fold
                        (fun (currentState, accumulatedEffects) result ->
                            let updatedState, producedEffects = update (EffectInterpreted result) currentState
                            updatedState, accumulatedEffects @ producedEffects)
                        (state, [])

                interpretUntilIdle nextState nextEffects

        let finalModel =
            interpretUntilIdle model effects |> fun state -> update BuildReport state |> fst

        finalModel.Report |> Option.defaultWith (fun () -> buildReport finalModel)

    /// Run the real composition scaffold over the registry already copied into `root`.
    let runScaffold (root: string) = scaffoldRequest root |> runRequest

    /// The scaffold summary, or a failure if the report carried none.
    let scaffoldSummary (report: CommandReport) =
        report.Scaffold
        |> Option.defaultWith (fun () -> failwith "Expected a scaffold summary.")

    /// All diagnostic ids surfaced on the report.
    let diagnosticIds (report: CommandReport) =
        report.Diagnostics |> List.map (fun diagnostic -> diagnostic.Id)

    /// The first `scaffold.*` diagnostic code on the report, when present — the
    /// discriminator the verdict resolution keys on alongside the outcome.
    let scaffoldDiagnostic (report: CommandReport) =
        report.Diagnostics
        |> List.map (fun diagnostic -> diagnostic.Id)
        |> List.tryFind (fun id -> id.StartsWith("scaffold.", StringComparison.Ordinal))

    /// Resolve the canonical provider descriptor the run scaffolded from the registry copied
    /// into `<root>/.fsgg/providers.yml` — the same `parseProviderRegistry` → find-by-name path
    /// the scaffold handler uses. `None` when the registry is absent, unparseable, or names no
    /// such provider. Pure read; no provider invoked (FR-008). Extracted so the harness's
    /// descriptor-resolve-and-bind glue is testable offline without a real provider.
    let resolveProviderDescriptor (root: string) (providerName: string) : ProviderDescriptor option =
        let registryPath = ".fsgg/providers.yml"

        if existsRelative root registryPath then
            let snapshot: FileSnapshot =
                { Path = registryPath
                  Text = readRelative root registryPath }

            Config.parseProviderRegistry snapshot
            |> Result.toOption
            |> Option.bind (List.tryFind (fun descriptor -> descriptor.Name = providerName))
        else
            None

    /// Feature 083 (implements 080 FR-011): a scaffold request that additionally forwards the
    /// product name on the **provider-declared name parameter**, resolved from the registry
    /// descriptor copied into `<root>/.fsgg/providers.yml` — never a hardcoded `productName`,
    /// so generic SDD carries no rendering identity (FR-006). SDD's 080 identifier derivation
    /// then derives + forwards the valid F# identifier. When no descriptor or name parameter
    /// resolves, `resolveNameParameter` falls back to the contract default key, so the request
    /// stays well-formed. Only the generic `lifecycle=sdd` marker and the author-chosen name
    /// value are added by generic SDD; the key is provider-owned.
    let namedScaffoldRequest (root: string) (name: string) =
        let request = scaffoldRequest root

        let nameKey =
            resolveProviderDescriptor root "rendering"
            |> Option.map resolveNameParameter
            |> Option.defaultValue defaultNameParameter

        { request with
            Parameters = request.Parameters @ [ nameKey, name ] }

    // ---------- T005: process-shell probes at the test edge ----------

    /// The outcome of a `dotnet`/`git` probe at the test edge: the exit code, whether the
    /// process started at all, and a surfaced diagnostic for `failure.diagnostic`.
    type ProbeResult =
        { Started: bool
          ExitCode: int
          Diagnostic: string }

    /// The resolved command a probe actually invokes — the single value handed to the
    /// existing process-shell edge. Either the declared command or the `dotnet` default.
    type ProbeCommand =
        { Executable: string
          Arguments: string list
          WorkingDirectory: string }

    let private startProcess (fileName: string) (args: string list) (workingDir: string) =
        let info =
            ProcessStartInfo(
                FileName = fileName,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            )

        args |> List.iter info.ArgumentList.Add

        try
            Process.Start info |> Option.ofObj
        with _ ->
            None

    /// Run a process to completion under `timeoutMs`; a hung process is killed and reported
    /// as a non-zero, timed-out probe (so it fails rather than hangs). This is the shared
    /// bounded edge `buildProbe` routes through; it is exposed so the timeout-kill diagnostic
    /// can be exercised at a short bound without waiting out the 300 s production bound.
    let runToCompletion (fileName: string) (args: string list) (workingDir: string) (timeoutMs: int) =
        match startProcess fileName args workingDir with
        | None ->
            { Started = false
              ExitCode = -1
              Diagnostic = $"could not start `{fileName}`." }
        | Some started ->
            use proc = started
            let stdout = proc.StandardOutput.ReadToEndAsync()
            let stderr = proc.StandardError.ReadToEndAsync()

            if proc.WaitForExit timeoutMs then
                let surfaced = (stderr.Result + stdout.Result).Trim()

                { Started = true
                  ExitCode = proc.ExitCode
                  Diagnostic = (if proc.ExitCode = 0 then "" else surfaced) }
            else
                (try
                    proc.Kill true
                 with _ ->
                     ())

                { Started = true
                  ExitCode = -1
                  Diagnostic = $"`{fileName}` timed out after {timeoutMs} ms." }

    // ---------- feature 035: declared-or-default probe-command resolution ----------

    /// Deterministic runnable-project discovery (FR-008): enumerate `*.fsproj`/`*.csproj`
    /// under `root`, map to forward-slash relative paths, ordinal-sort, and take the first;
    /// `None` when none exist. The same product always yields the same target. References only
    /// generic tooling — no provider/template/package/path/docs token.
    let discoverRunnableProject (root: string) : string option =
        [ "*.fsproj"; "*.csproj" ]
        |> Seq.collect (fun pattern -> Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
        |> Seq.map (fun full -> Path.GetRelativePath(root, full).Replace('\\', '/'))
        |> Seq.sortWith (fun left right -> String.CompareOrdinal(left, right))
        |> Seq.tryHead

    /// A declared command is honored only when its executable is non-blank; a blank
    /// (null/empty/whitespace) executable falls through to the default (FR-010).
    let private declaredCommandOrDefault (declared: DeclaredCommand option) (root: string) : ProbeCommand option =
        match declared with
        | Some command when not (String.IsNullOrWhiteSpace command.Executable) ->
            Some
                { Executable = command.Executable
                  Arguments = command.Arguments
                  WorkingDirectory = root }
        | _ -> None

    /// Resolve the build command (pure): a non-blank declared command wins; otherwise the
    /// platform-standard `dotnet build` at the product root (FR-001/FR-003/FR-010).
    let resolveBuildCommand (declared: DeclaredCommand option) (root: string) : ProbeCommand =
        match declaredCommandOrDefault declared root with
        | Some command -> command
        | None ->
            { Executable = "dotnet"
              Arguments = [ "build" ]
              WorkingDirectory = root }

    /// Resolve the run command (pure): a non-blank declared command wins; otherwise
    /// `dotnet run --project <discovered>` at the product root, or `None` when no runnable
    /// project is discoverable — so the probe can emit a diagnosed not-started outcome rather
    /// than hang (FR-002/FR-003/FR-008/FR-010).
    let resolveRunCommand (declared: DeclaredCommand option) (root: string) : ProbeCommand option =
        match declaredCommandOrDefault declared root with
        | Some command -> Some command
        | None ->
            discoverRunnableProject root
            |> Option.map (fun project ->
                { Executable = "dotnet"
                  Arguments = [ "run"; "--project"; project ]
                  WorkingDirectory = root })

    /// The build probe: resolve the declared-or-default command and route it through the
    /// shared 300 s bounded edge (research D6). `declared = None` is today's `dotnet build`.
    let buildProbe (declared: DeclaredCommand option) (root: string) =
        let command = resolveBuildCommand declared root
        runToCompletion command.Executable command.Arguments command.WorkingDirectory 300_000

    /// Feature 083 (080 FR-011): the test probe — run `dotnet test` at the product root through
    /// the same shared 300 s bounded edge as `buildProbe`. Exit 0 (including an empty-but-green
    /// run — zero tests) is green; a non-zero exit surfaces the diagnostic; a hang is killed and
    /// diagnosed rather than left to stall. Distinct from `runProbe` (which is a start-and-grace
    /// smoke for a long-lived app): `dotnet test` runs to completion with a meaningful exit code.
    let testProbe (root: string) =
        runToCompletion "dotnet" [ "test" ] root 300_000

    /// A headless, bounded run smoke (research D6, contracts/acceptance-protocol.md §run
    /// probe): launch the resolved run command, require it to either exit 0 within the grace
    /// window or survive the grace window without a non-zero exit (it started and did not
    /// crash), then terminate it. Overall cap 60 s so a hung app fails rather than hangs.
    let private runWithGrace (command: ProbeCommand) =
        let graceMs = 10_000
        let overallMs = 60_000

        match startProcess command.Executable command.Arguments command.WorkingDirectory with
        | None ->
            { Started = false
              ExitCode = -1
              Diagnostic = $"could not start `{command.Executable}`." }
        | Some started ->
            use proc = started
            let stdout = proc.StandardOutput.ReadToEndAsync()
            let stderr = proc.StandardError.ReadToEndAsync()

            if proc.WaitForExit graceMs then
                // Exited within the grace window: pass iff it exited cleanly.
                let surfaced = (stderr.Result + stdout.Result).Trim()

                { Started = true
                  ExitCode = proc.ExitCode
                  Diagnostic = (if proc.ExitCode = 0 then "" else surfaced) }
            else
                // Survived the grace window without crashing: it started and is running.
                (try
                    proc.Kill true
                 with _ ->
                     ())

                proc.WaitForExit(max 0 (overallMs - graceMs)) |> ignore

                { Started = true
                  ExitCode = 0
                  Diagnostic = "" }

    /// The run probe: `declared = None` resolves to `dotnet run --project <discovered>`; no
    /// runnable project discoverable ⇒ a diagnosed not-started ProbeResult (FR-007). Declared
    /// and default share the same bounded edge.
    let runProbe (declared: DeclaredCommand option) (root: string) =
        match resolveRunCommand declared root with
        | None ->
            { Started = false
              ExitCode = -1
              Diagnostic = "no runnable project discovered." }
        | Some command -> runWithGrace command

    // ---------- refresh probe (in-process MVU over the public command surface) ----------

    let private validSpec workId title =
        $"""---
schemaVersion: 1
workId: {workId}
title: {title}
stage: charter
changeTier: tier1
status: draft
---

# {title} Specification

- FR-001: The selected work item has one typed requirement.
"""

    let private validTasks =
        """schemaVersion: 1
tasks:
  - id: T001
    title: Implement selected lifecycle work
    status: pending
    owner: sdd
    dependencies: []
    requirements: [FR-001]
    decisions: []
    requiredSkills: []
    requiredEvidence: []
"""

    let private validEvidence =
        """schemaVersion: 1
evidence: []
"""

    /// Plant a minimal, valid work item so `refresh` has SDD-owned views to regenerate.
    let writeValidWorkSources root workId title =
        writeRelative root $"work/{workId}/spec.md" (validSpec workId title)
        writeRelative root $"work/{workId}/tasks.yml" validTasks
        writeRelative root $"work/{workId}/evidence.yml" validEvidence

    /// Run `fsgg-sdd refresh` in-process for `workId` (mirrors `TestSupport.runRefresh`).
    let runRefresh root workId =
        { request Refresh root with
            WorkId = Some workId }
        |> runRequest

    // ---------- byte-hash helper for refresh-exclusion comparison ----------

    /// Relative (forward-slash, sorted) file paths under `root`, excluding the `.git`
    /// repository scaffold initializes (never a scaffold-produced artifact).
    let relativeFiles (root: string) =
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        |> Seq.map (fun full -> Path.GetRelativePath(root, full).Replace('\\', '/'))
        |> Seq.filter (fun path -> not (path = ".git" || path.StartsWith(".git/", StringComparison.Ordinal)))
        |> Seq.sort
        |> Seq.toList

    /// A stable digest of a file's bytes, for before/after refresh-exclusion comparison.
    let fileDigest (root: string) (relativePath: string) =
        let bytes =
            File.ReadAllBytes(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)))

        use sha = System.Security.Cryptography.SHA256.Create()
        Convert.ToHexString(sha.ComputeHash bytes)
