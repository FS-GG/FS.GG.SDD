namespace FS.GG.SDD.Cli.Tests

open System
open FS.GG.SDD.Cli.Rendering
open Xunit

/// Feature 088 / FS.GG.SDD#172 — the force-color override. `FORCE_COLOR` (boolean-ish)
/// and `--force-color` re-enable rich ANSI over a redirected sink or `TERM=dumb`, but
/// never over `NO_COLOR`. Precedence: NO_COLOR > force-color > capability sensing.
module ForceColorTests =

    // Env vars are process-global; serialize these mutations and always restore them so a
    // concurrent test never observes a half-set environment.
    let private envLock = obj ()

    let private withEnv (pairs: (string * string option) list) (f: unit -> unit) =
        lock envLock (fun () ->
            let saved =
                pairs
                |> List.map (fun (name, _) -> name, Option.ofObj (Environment.GetEnvironmentVariable name))

            try
                for name, value in pairs do
                    Environment.SetEnvironmentVariable(name, Option.toObj value)

                f ()
            finally
                for name, value in saved do
                    Environment.SetEnvironmentVariable(name, Option.toObj value))

    // ----- forceColorRequested: FORCE_COLOR boolean-ish + --force-color flag -----

    [<Theory>]
    [<InlineData("1", true)>]
    [<InlineData("true", true)>]
    [<InlineData("always", true)>]
    [<InlineData("0", false)>]
    [<InlineData("", false)>]
    let ``forceColorRequested reads FORCE_COLOR boolean-ish`` (value: string) (expected: bool) =
        withEnv [ "FORCE_COLOR", Some value ] (fun () -> Assert.Equal(expected, forceColorRequested []))

    [<Fact>]
    let ``forceColorRequested is false when FORCE_COLOR is unset and no flag`` () =
        withEnv [ "FORCE_COLOR", None ] (fun () -> Assert.False(forceColorRequested []))

    [<Fact>]
    let ``the --force-color flag forces even without the env var`` () =
        withEnv [ "FORCE_COLOR", None ] (fun () -> Assert.True(forceColorRequested [ "--force-color" ]))

    // ----- detectCapabilities: effective interactivity/color under force -----
    // Signature: detectCapabilities forceColor outputRedirected.

    [<Fact>]
    let ``force-color re-enables interactivity and color on a redirected sink`` () =
        withEnv [ "NO_COLOR", None; "TERM", Some "xterm" ] (fun () ->
            let caps = detectCapabilities true true
            Assert.True(caps.IsInteractive)
            Assert.True(caps.ColorEnabled)
            // Width stays None on a redirected sink even when forced (no WindowWidth read on a pipe).
            Assert.Equal(None, caps.Width))

    [<Fact>]
    let ``without force-color a redirected sink is non-interactive`` () =
        withEnv [ "NO_COLOR", None; "TERM", Some "xterm" ] (fun () ->
            Assert.False((detectCapabilities false true).IsInteractive))

    [<Fact>]
    let ``force-color overrides TERM=dumb but plain sensing does not`` () =
        withEnv [ "NO_COLOR", None; "TERM", Some "dumb" ] (fun () ->
            Assert.True((detectCapabilities true true).ColorEnabled)
            // TERM=dumb disables color when not forced.
            Assert.False((detectCapabilities false false).ColorEnabled))

    [<Fact>]
    let ``NO_COLOR wins over force-color`` () =
        withEnv [ "NO_COLOR", Some "1"; "TERM", Some "xterm" ] (fun () ->
            let caps = detectCapabilities true true
            Assert.False(caps.ColorEnabled)
            // The rich gate is `IsInteractive && ColorEnabled`, so output still degrades.
            Assert.False(caps.IsInteractive && caps.ColorEnabled))

    [<Fact>]
    let ``NO_COLOR present with empty value still wins over force-color`` () =
        withEnv [ "NO_COLOR", Some ""; "TERM", Some "xterm" ] (fun () ->
            Assert.False((detectCapabilities true true).ColorEnabled))
