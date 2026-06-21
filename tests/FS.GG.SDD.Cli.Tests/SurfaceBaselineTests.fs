namespace FS.GG.SDD.Cli.Tests

open System
open System.IO
open System.Reflection
open FS.GG.SDD.Cli
open Xunit

module SurfaceBaselineTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport

    [<Fact>]
    let ``Public CLI rendering surface matches baseline`` () =
        let assembly = typeof<Rendering.TerminalCapabilities>.Assembly

        let actual =
            assembly.GetTypes()
            |> Array.filter (fun t -> t.Namespace = "FS.GG.SDD.Cli" && t.IsClass && t.IsAbstract && t.IsSealed)
            |> Array.collect (fun t ->
                t.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
                |> Array.filter (fun method -> not method.IsSpecialName)
                |> Array.map (fun method -> $"{t.FullName}.{method.Name}"))
            |> Array.sort

        let baseline =
            Path.Combine(Commands.repoRoot, "tests", "FS.GG.SDD.Cli.Tests", "PublicSurface.baseline")
            |> File.ReadAllLines
            |> Array.filter (String.IsNullOrWhiteSpace >> not)
            |> Array.sort

        Assert.Equal<string array>(baseline, actual)
