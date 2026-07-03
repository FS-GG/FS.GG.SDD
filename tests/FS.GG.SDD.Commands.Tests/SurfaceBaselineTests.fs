namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Reflection
open FS.GG.SDD.Commands
open FS.GG.SDD.TestShared
open Xunit

module SurfaceBaselineTests =
    [<Fact>]
    let ``Public command surface matches baseline`` () =
        let capture () =
            let assembly = typeof<CommandTypes.SddCommand>.Assembly

            assembly.GetTypes()
            |> Array.filter (fun t -> t.Namespace = "FS.GG.SDD.Commands" && t.IsClass && t.IsAbstract && t.IsSealed)
            |> Array.collect (fun t ->
                t.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
                |> Array.filter (fun method -> not method.IsSpecialName)
                |> Array.map (fun method -> $"{t.FullName}.{method.Name}"))
            |> Array.sort

        // Feature 067 / FR-005: shared update-or-assert (set FSGG_UPDATE_BASELINE=1 to re-capture).
        let baselinePath =
            Path.Combine(TestSupport.repoRoot, "tests", "FS.GG.SDD.Commands.Tests", "PublicSurface.baseline")

        TestShared.SurfaceBaseline.verify baselinePath capture
