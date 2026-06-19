namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Reflection
open FS.GG.SDD.Commands
open Xunit

module SurfaceBaselineTests =
    [<Fact>]
    let ``Public command surface matches baseline`` () =
        let assembly = typeof<CommandTypes.SddCommand>.Assembly

        let actual =
            assembly.GetTypes()
            |> Array.filter (fun t -> t.Namespace = "FS.GG.SDD.Commands" && t.IsClass && t.IsAbstract && t.IsSealed)
            |> Array.collect (fun t ->
                t.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
                |> Array.filter (fun method -> not method.IsSpecialName)
                |> Array.map (fun method -> $"{t.FullName}.{method.Name}"))
            |> Array.sort

        let baseline =
            Path.Combine(TestSupport.repoRoot, "tests", "FS.GG.SDD.Commands.Tests", "PublicSurface.baseline")
            |> File.ReadAllLines
            |> Array.filter (String.IsNullOrWhiteSpace >> not)
            |> Array.sort

        Assert.Equal<string array>(baseline, actual)
