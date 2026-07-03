namespace FS.GG.SDD.Cli.Tests

open System.IO
open System.Reflection
open FS.GG.SDD.Cli
open FS.GG.SDD.TestShared
open Xunit

module SurfaceBaselineTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport

    [<Fact>]
    let ``Public CLI rendering surface matches baseline`` () =
        let capture () =
            let assembly = typeof<Rendering.TerminalCapabilities>.Assembly

            assembly.GetTypes()
            |> Array.filter (fun t -> t.Namespace = "FS.GG.SDD.Cli" && t.IsClass && t.IsAbstract && t.IsSealed)
            |> Array.collect (fun t ->
                t.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
                |> Array.filter (fun method -> not method.IsSpecialName)
                |> Array.map (fun method -> $"{t.FullName}.{method.Name}"))
            |> Array.sort

        // Feature 067 / FR-005: shared update-or-assert (set FSGG_UPDATE_BASELINE=1 to re-capture).
        let baselinePath =
            Path.Combine(Commands.repoRoot, "tests", "FS.GG.SDD.Cli.Tests", "PublicSurface.baseline")

        TestShared.SurfaceBaseline.verify baselinePath capture
