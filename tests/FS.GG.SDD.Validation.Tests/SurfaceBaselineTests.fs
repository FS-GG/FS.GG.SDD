namespace FS.GG.SDD.Validation.Tests

open System
open System.IO
open System.Reflection
open FS.GG.SDD.Validation
open Xunit

module SurfaceBaselineTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport

    [<Fact>]
    let ``Public validation surface matches baseline`` () =
        let assembly = typeof<ValidationContracts.ValidationReport>.Assembly

        let actual =
            assembly.GetTypes()
            |> Array.filter (fun t -> t.Namespace = "FS.GG.SDD.Validation" && t.IsClass && t.IsAbstract && t.IsSealed)
            |> Array.collect (fun t ->
                t.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
                |> Array.filter (fun method -> not method.IsSpecialName)
                |> Array.map (fun method -> $"{t.FullName}.{method.Name}"))
            |> Array.sort

        let baseline =
            Path.Combine(Commands.repoRoot, "tests", "FS.GG.SDD.Validation.Tests", "PublicSurface.baseline")
            |> File.ReadAllLines
            |> Array.filter (String.IsNullOrWhiteSpace >> not)
            |> Array.sort

        Assert.Equal<string array>(baseline, actual)
