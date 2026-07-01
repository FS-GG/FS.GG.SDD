namespace FS.GG.SDD.Acceptance.Tests

open System.IO
open Fsgg.Provider
open AcceptanceSupport
open Xunit

/// Offline coverage for the declared-or-default probe-command resolver (feature 035). These
/// facts run in the default inner loop: the pure-resolver facts spawn NO process (SC-002), and
/// the synthetic-command execution facts drive the real edge with generic, platform-standard
/// tooling only — no provider/template/package/path/docs token (FR-009).
module ProbeResolutionTests =

    /// A fresh temp product root with the given relative project files planted, for discovery
    /// and default-run resolution. Generic tooling only — no provider identity.
    let private productWith (relativeProjects: string list) =
        let root = newProductRoot ()
        relativeProjects |> List.iter (fun path -> writeRelative root path "<Project />")
        root

    // ---------- US1 (P1): default branch is the normalized dotnet command ----------

    // T004 (US1 / FR-001/FR-002): the default branch resolves to `dotnet build` and
    // `dotnet run --project <discovered>` over the product root. No process is spawned.
    [<Fact>]
    let ``resolveBuildCommand None is dotnet build at root`` () =
        let root = newProductRoot ()
        Assert.Equal(
            { Executable = "dotnet"; Arguments = [ "build" ]; WorkingDirectory = root },
            resolveBuildCommand None root)

    [<Fact>]
    let ``resolveRunCommand None is dotnet run --project discovered at root`` () =
        let root = productWith [ "App.fsproj" ]
        Assert.Equal(
            Some { Executable = "dotnet"; Arguments = [ "run"; "--project"; "App.fsproj" ]; WorkingDirectory = root },
            resolveRunCommand None root)

    // T005 (US1 / FR-008): discovery is deterministic (ordinal-first) and an empty product
    // yields None for both discovery and the default run resolution (so the probe can emit a
    // diagnosed not-started outcome rather than hang).
    [<Fact>]
    let ``discoverRunnableProject is deterministic ordinal-first`` () =
        let root = productWith [ "z.fsproj"; "b/b.fsproj"; "a/a.csproj" ]
        let first = discoverRunnableProject root
        let second = discoverRunnableProject root
        Assert.Equal(Some "a/a.csproj", first)
        Assert.Equal<string option>(first, second)

    [<Fact>]
    let ``empty product discovers no project and run default is None`` () =
        let root = newProductRoot ()
        Assert.Equal<string option>(None, discoverRunnableProject root)
        Assert.Equal<ProbeCommand option>(None, resolveRunCommand None root)

    // ---------- US2 (P2): declared command beats the default ----------

    // T011 (US2 / SC-002 / FR-004): a non-blank declared command is invoked verbatim at the
    // product root, never `dotnet`. The read shape is the 1:1 H2 forward-compat form.
    [<Fact>]
    let ``resolveBuildCommand declared invokes the declared command never dotnet`` () =
        let root = newProductRoot ()
        let declared: DeclaredCommand = { Executable = "mybuild"; Arguments = [ "--fast" ] }
        let resolved = resolveBuildCommand (Some declared) root
        Assert.Equal({ Executable = "mybuild"; Arguments = [ "--fast" ]; WorkingDirectory = root }, resolved)
        Assert.NotEqual<string>("dotnet", resolved.Executable)

    [<Fact>]
    let ``resolveRunCommand declared is Some of the declared command at root`` () =
        let root = newProductRoot ()
        let declared: DeclaredCommand = { Executable = "myrun"; Arguments = [ "--headless" ] }
        Assert.Equal(
            Some { Executable = "myrun"; Arguments = [ "--headless" ]; WorkingDirectory = root },
            resolveRunCommand (Some declared) root)

    // T012 (US2 / FR-010): a `Some` whose Executable is empty/whitespace falls through to the
    // default for both probes — never an attempt to launch a blank executable.
    [<Theory>]
    [<InlineData("")>]
    [<InlineData("   ")>]
    let ``blank declared executable resolves to the default`` (blank: string) =
        let root = productWith [ "App.fsproj" ]
        let declared: DeclaredCommand = { Executable = blank; Arguments = [ "ignored" ] }
        Assert.Equal(resolveBuildCommand None root, resolveBuildCommand (Some declared) root)
        Assert.Equal(resolveRunCommand None root, resolveRunCommand (Some declared) root)

    // T013 (US2 / FR-003/FR-006): a SYNTHETIC declared command — generic, platform-standard
    // tooling only (`true`, exit 0) — driven through the real probe edge returns a ProbeResult
    // reflecting that command's exit. SYNTHETIC: no real provider; proves the declared path
    // end-to-end through the actual process edge.
    [<Fact>]
    let ``buildProbe declared executes the Synthetic command and reflects its exit`` () =
        let root = newProductRoot ()
        let declared: DeclaredCommand = { Executable = "true"; Arguments = [] }
        let result = buildProbe (Some declared) root
        Assert.True(result.Started)
        Assert.Equal(0, result.ExitCode)

    [<Fact>]
    let ``runProbe declared executes the Synthetic command and reflects its exit`` () =
        let root = newProductRoot ()
        let declared: DeclaredCommand = { Executable = "true"; Arguments = [] }
        let result = runProbe (Some declared) root
        Assert.True(result.Started)
        Assert.Equal(0, result.ExitCode)

    // T014 (US2 / FR-007 / SC-005): three distinct diagnosed, non-zero outcomes through the
    // real edge; none hangs. All use generic, platform-standard tooling (SYNTHETIC commands).
    [<Fact>]
    let ``Synthetic missing executable yields a could-not-start ProbeResult`` () =
        let root = newProductRoot ()
        let declared: DeclaredCommand = { Executable = "fsgg-sdd-nonexistent-binary-xyz"; Arguments = [] }
        let result = buildProbe (Some declared) root
        Assert.False(result.Started)
        Assert.Equal(-1, result.ExitCode)
        Assert.StartsWith("could not start", result.Diagnostic)

    [<Fact>]
    let ``Synthetic non-zero exit yields a started non-zero ProbeResult`` () =
        let root = newProductRoot ()
        let declared: DeclaredCommand = { Executable = "false"; Arguments = [] }
        let result = buildProbe (Some declared) root
        Assert.True(result.Started)
        Assert.NotEqual(0, result.ExitCode)

    [<Fact>]
    let ``Synthetic hanging command is killed at its bound with a timeout diagnostic`` () =
        // The shared bounded edge buildProbe uses (`runToCompletion`), driven at a short bound so
        // the 300 s production bound's kill-on-timeout path is proven fast. SYNTHETIC: `sleep` is
        // generic platform tooling standing in for a hung build.
        let root = newProductRoot ()
        let result = runToCompletion "sleep" [ "3600" ] root 750
        Assert.True(result.Started)
        Assert.Equal(-1, result.ExitCode)
        Assert.Contains("timed out", result.Diagnostic)

    // ---------- US3 (P3): the defaults reference only generic tooling ----------

    // T017 (US3 / FR-009 / SC-003): the default ProbeCommand tokens are exactly the generic set
    // (`dotnet` + `build` / `run` + `--project` + the discovered project path) — no provider,
    // template, package, path, or docs-URL token enters the defaults.
    [<Fact>]
    let ``default ProbeCommand tokens are the generic dotnet set only`` () =
        let root = productWith [ "App.fsproj" ]
        let build = resolveBuildCommand None root
        let run = (resolveRunCommand None root).Value

        Assert.Equal("dotnet", build.Executable)
        Assert.Equal("dotnet", run.Executable)

        let genericTokens = set [ "build"; "run"; "--project"; "App.fsproj" ]
        let allTokens = (build.Arguments @ run.Arguments) |> Set.ofList
        Assert.True(
            Set.isSubset allTokens genericTokens,
            "Default probe arguments must contain only generic tokens; saw: " + string (Set.toList allTokens))

    // ---------- Feature 038 (T020): probes honor a synthetic descriptor's declared commands ----------

    /// A SYNTHETIC canonical descriptor — no real provider; the build/run fields drive the probes.
    let private syntheticDescriptor: ProviderDescriptor =
        { Name = "demo"
          ContractVersion = "1.0.0"
          TemplateId = "demo-template"
          Source = "__FIXTURE__/ok"
          Parameters = []
          Build = None
          Test = None
          Run = None
          Verify = None
          NameParameter = "name"
          MinimumCliVersion = None }

    // T020 (SC-005 / FR-009): a descriptor declaring no build/run falls through to the `dotnet`
    // defaults — the reference-provider case, observably unchanged.
    [<Fact>]
    let ``descriptor with no declared build or run resolves to the dotnet defaults`` () =
        let root = productWith [ "App.fsproj" ]
        Assert.Equal(
            { Executable = "dotnet"; Arguments = [ "build" ]; WorkingDirectory = root },
            resolveBuildCommand syntheticDescriptor.Build root)
        Assert.Equal(
            Some { Executable = "dotnet"; Arguments = [ "run"; "--project"; "App.fsproj" ]; WorkingDirectory = root },
            resolveRunCommand syntheticDescriptor.Run root)

    // T020 (SC-005 / FR-010): a descriptor declaring a trivial build command makes `buildProbe`
    // invoke THAT command — proven by its deterministic exit 0 in an empty root, where the `dotnet`
    // default would be non-zero (no project). So no `dotnet` process is started for the declared case.
    [<Fact>]
    let ``buildProbe honors the descriptor's declared command and starts no dotnet`` () =
        let root = newProductRoot () // empty: a dotnet build here would fail (non-zero)
        let descriptor = { syntheticDescriptor with Build = Some { Executable = "true"; Arguments = [] } }
        let result = buildProbe descriptor.Build root
        Assert.True(result.Started)
        Assert.Equal(0, result.ExitCode) // the trivial command ran, not `dotnet build`

    // T020 mirror for the run probe under the grace/overall window.
    [<Fact>]
    let ``runProbe honors the descriptor's declared command and starts no dotnet`` () =
        let root = newProductRoot () // empty: no runnable project, so the default would not start
        let descriptor = { syntheticDescriptor with Run = Some { Executable = "true"; Arguments = [] } }
        let result = runProbe descriptor.Run root
        Assert.True(result.Started)
        Assert.Equal(0, result.ExitCode) // the trivial command ran, not `dotnet run`
