namespace FS.GG.SDD.Acceptance.Tests

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text
open Xunit

/// Offline, deterministic process-edge coverage for the registry-source resolver
/// `scripts/workflows/resolve-acceptance-registry.sh` (feature 041, the consumer half of
/// the `composition-registry-updated` cross-repo dispatch contract). These are plain
/// `[<Fact>]` — NOT `RequiresRegistryFact` — so they run in the default offline inner loop:
/// a real bash process over real temp files, no network (plan.md Testing). The whole module
/// is skipped on a host without bash (Windows) so the cross-platform inner loop stays green
/// (plan.md Target Platform). It carries NO rendering identity (FR-003 / SC-003).
module RegistryResolverTests =

    /// Captured stdout/stderr/exit of one resolver invocation. The resolver prints its
    /// resolved path to stdout on success and `::error::` diagnostics to stderr on a
    /// fail-closed exit; both are asserted regardless of exit code — a fuller capture than
    /// `AcceptanceSupport.runToCompletion`, which folds output into a diagnostic only on a
    /// non-zero exit. Still the real process edge (real bash, real temp files).
    type ResolverRun =
        { ExitCode: int
          Stdout: string
          Stderr: string }

    /// The script copied beside the test assembly (fsproj `<None CopyToOutputDirectory>`).
    let private scriptPath =
        Path.Combine(AppContext.BaseDirectory, "resolve-acceptance-registry.sh")

    /// The resolver is POSIX bash; on a host without bash (Windows) the module skips so the
    /// cross-platform inner loop stays green. Invoking via `bash <script>` also avoids any
    /// dependence on the copied file's executable bit surviving the build copy.
    let private bashPath =
        [ "/usr/bin/bash"; "/bin/bash"; "/usr/sbin/bash" ] |> List.tryFind File.Exists

    let private skipUnlessBash () =
        match bashPath with
        | Some _ -> ()
        | None -> raise (Xunit.Sdk.SkipException.ForSkip "bash is unavailable on this host; the resolver is POSIX shell.")

    /// A fresh, empty `RUNNER_TEMP` for one invocation (temp dir, sensed — never compared).
    let private freshRunnerTemp () =
        let dir = Path.Combine(Path.GetTempPath(), "fsgg-resolver-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        dir

    /// sha256 of `text` (UTF-8 bytes), first 12 lowercase hex — mirrors the resolver's
    /// `sha256sum | cut` over the materialized bytes, so an advertised value computed here
    /// must equal the resolver's recomputed value for identical content.
    let private sha256First12 (text: string) =
        use sha = SHA256.Create()
        Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes text)).ToLowerInvariant().Substring(0, 12)

    /// Invoke the resolver via a real bash process with a fully controlled environment. The
    /// child inherits the parent env (PATH etc.), then the resolver-specific keys are cleared
    /// and the provided overrides applied — so no stale parent value leaks and this (parallel)
    /// test process's own globals are never mutated.
    let private runResolver (env: (string * string) list) (args: string list) : ResolverRun =
        let bash = bashPath |> Option.defaultWith (fun () -> failwith "bash is required")

        let info =
            ProcessStartInfo(
                FileName = bash,
                WorkingDirectory = AppContext.BaseDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false)

        info.ArgumentList.Add scriptPath
        args |> List.iter info.ArgumentList.Add

        [ "REGISTRY_PATH_INPUT"; "FSGG_DISPATCH_REGISTRY_CONTENT"; "FSGG_DISPATCH_REGISTRY_SHA256_12"
          "REGISTRY_SECRET_CONTENT"; "GITHUB_EVENT_NAME"; "RUNNER_TEMP"; "GITHUB_ENV"; "GITHUB_STEP_SUMMARY" ]
        |> List.iter (fun key -> info.Environment.Remove key |> ignore)

        env |> List.iter (fun (key, value) -> info.Environment[key] <- value)

        match Process.Start info |> Option.ofObj with
        | None -> failwith "could not start bash for the resolver"
        | Some started ->
            use proc = started
            let stdout = proc.StandardOutput.ReadToEndAsync()
            let stderr = proc.StandardError.ReadToEndAsync()

            if not (proc.WaitForExit 30_000) then
                (try proc.Kill true with _ -> ())
                failwith "resolver did not exit within 30 s"

            { ExitCode = proc.ExitCode
              Stdout = stdout.Result
              Stderr = stderr.Result }

    /// The `FSGG_SDD_ACCEPTANCE_REGISTRY=<path>` value from `--print-env` stdout, if present.
    let private exportedPath (stdout: string) =
        let prefix = "FSGG_SDD_ACCEPTANCE_REGISTRY="

        stdout.Replace("\r\n", "\n").Split('\n')
        |> Array.tryPick (fun line ->
            if line.StartsWith(prefix, StringComparison.Ordinal) then Some(line.Substring prefix.Length) else None)

    // ---------- T006 (US1 / FR-002/FR-008): dispatch source, verbatim materialization ----------

    [<Fact>]
    let ``dispatch source materializes content verbatim and the file sha matches the advertised`` () =
        skipUnlessBash ()
        let runnerTemp = freshRunnerTemp ()

        // Multi-line YAML with special characters that must survive byte-for-byte.
        let content =
            "schemaVersion: 1\nproviders:\n  - name: demo\n    note: \"a $VAR `backtick` \\\\backslash 'quote'\"\n    body: |\n      line-one\n      line-two\n"

        let sha = sha256First12 content

        let run =
            runResolver
                [ "GITHUB_EVENT_NAME", "repository_dispatch"
                  "FSGG_DISPATCH_REGISTRY_CONTENT", content
                  "FSGG_DISPATCH_REGISTRY_SHA256_12", sha
                  "RUNNER_TEMP", runnerTemp ]
                [ "--print-env" ]

        Assert.Equal(0, run.ExitCode)

        let path =
            exportedPath run.Stdout
            |> Option.defaultWith (fun () -> failwithf "no exported path in stdout: %s" run.Stdout)

        Assert.True(File.Exists path)
        Assert.Equal(content, File.ReadAllText path) // byte-for-byte verbatim (D4)
        Assert.Equal(sha, sha256First12 (File.ReadAllText path)) // integrity cross-check (D5)

    // ---------- T007 (US1 / FR-004): deterministic precedence — manual input overrides ----------

    [<Fact>]
    let ``manual input path overrides dispatch and secret (precedence 1)`` () =
        skipUnlessBash ()
        let runnerTemp = freshRunnerTemp ()
        let inputPath = Path.Combine(runnerTemp, "checked-out-registry.yml")
        File.WriteAllText(inputPath, "schemaVersion: 1\n# checked-out input\n")

        // A dispatch payload with a deliberately WRONG sha is also present: input must win
        // and return before any dispatch integrity check, so the bogus sha is never evaluated.
        let run =
            runResolver
                [ "REGISTRY_PATH_INPUT", inputPath
                  "GITHUB_EVENT_NAME", "repository_dispatch"
                  "FSGG_DISPATCH_REGISTRY_CONTENT", "schemaVersion: 1\n# dispatched\n"
                  "FSGG_DISPATCH_REGISTRY_SHA256_12", "deadbeef0000"
                  "REGISTRY_SECRET_CONTENT", "schemaVersion: 1\n# secret\n"
                  "RUNNER_TEMP", runnerTemp ]
                [ "--print-env" ]

        Assert.Equal(0, run.ExitCode)
        Assert.Equal(Some inputPath, exportedPath run.Stdout)

    // ---------- T008 (US1 / FR-004 precedence 3): secret fallback ----------

    [<Fact>]
    let ``secret content is materialized when it is the only source (precedence 3)`` () =
        skipUnlessBash ()
        let runnerTemp = freshRunnerTemp ()
        let content = "schemaVersion: 1\n# secret-sourced\n"

        // No GITHUB_EVENT_NAME=repository_dispatch and no input ⇒ the scheduled secret path.
        let run = runResolver [ "REGISTRY_SECRET_CONTENT", content; "RUNNER_TEMP", runnerTemp ] [ "--print-env" ]

        Assert.Equal(0, run.ExitCode)

        let path =
            exportedPath run.Stdout
            |> Option.defaultWith (fun () -> failwithf "no exported path in stdout: %s" run.Stdout)

        Assert.True(File.Exists path)
        Assert.Equal(content, File.ReadAllText path)

    // ---------- T009 (US1 / FR-005, D5): fail closed on empty dispatch and on sha mismatch ----------

    [<Fact>]
    let ``dispatch with empty content fails closed`` () =
        skipUnlessBash ()
        let runnerTemp = freshRunnerTemp ()

        let run =
            runResolver
                [ "GITHUB_EVENT_NAME", "repository_dispatch"
                  "FSGG_DISPATCH_REGISTRY_CONTENT", ""
                  "RUNNER_TEMP", runnerTemp ]
                [ "--print-env" ]

        Assert.NotEqual(0, run.ExitCode)
        Assert.Contains("::error::", run.Stderr)
        Assert.Equal(None, exportedPath run.Stdout) // no path printed

    [<Fact>]
    let ``dispatch whose recomputed sha differs from the advertised fails closed`` () =
        skipUnlessBash ()
        let runnerTemp = freshRunnerTemp ()

        let run =
            runResolver
                [ "GITHUB_EVENT_NAME", "repository_dispatch"
                  "FSGG_DISPATCH_REGISTRY_CONTENT", "schemaVersion: 1\n# good content\n"
                  "FSGG_DISPATCH_REGISTRY_SHA256_12", "000000000000" // wrong
                  "RUNNER_TEMP", runnerTemp ]
                [ "--print-env" ]

        Assert.NotEqual(0, run.ExitCode)
        Assert.Contains("::error::", run.Stderr)
        Assert.Equal(None, exportedPath run.Stdout)

    // The no-source-at-all case is distinct from the fail-closed dispatch cases: it stays the
    // exit-1 error the workflow already had (data-model.md outcome table, last row).
    [<Fact>]
    let ``no source at all fails closed (unchanged from today)`` () =
        skipUnlessBash ()
        let runnerTemp = freshRunnerTemp ()
        let run = runResolver [ "RUNNER_TEMP", runnerTemp ] [ "--print-env" ]

        Assert.NotEqual(0, run.ExitCode)
        Assert.Contains("::error::", run.Stderr)

    // ---------- T012 (US2 / FR-008, SC-006): default mode surfaces the drift signal ----------

    [<Fact>]
    let ``default mode appends the registry path to GITHUB_ENV and the drift sha to the step summary`` () =
        skipUnlessBash ()
        let runnerTemp = freshRunnerTemp ()
        let content = "schemaVersion: 1\n# dispatched\n"
        let sha = sha256First12 content
        let githubEnv = Path.Combine(runnerTemp, "github_env")
        let stepSummary = Path.Combine(runnerTemp, "step_summary")

        let run =
            runResolver
                [ "GITHUB_EVENT_NAME", "repository_dispatch"
                  "FSGG_DISPATCH_REGISTRY_CONTENT", content
                  "FSGG_DISPATCH_REGISTRY_SHA256_12", sha
                  "RUNNER_TEMP", runnerTemp
                  "GITHUB_ENV", githubEnv
                  "GITHUB_STEP_SUMMARY", stepSummary ]
                [] // default mode (CI path)

        Assert.Equal(0, run.ExitCode)
        Assert.Contains("FSGG_SDD_ACCEPTANCE_REGISTRY=", File.ReadAllText githubEnv)
        Assert.Contains(sha, File.ReadAllText stepSummary) // drift signal at the run layer (FR-008)
