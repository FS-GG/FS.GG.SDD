namespace FS.GG.SDD.Validation.Tests

open System.IO
open System.Reflection
open FS.GG.SDD.Validation
open FS.GG.SDD.TestShared
open Xunit

module SurfaceBaselineTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport

    [<Fact>]
    let ``Public validation surface matches baseline`` () =
        let capture () =
            let assembly = typeof<ValidationContracts.ValidationReport>.Assembly

            assembly.GetTypes()
            |> Array.filter (fun t -> t.Namespace = "FS.GG.SDD.Validation" && t.IsClass && t.IsAbstract && t.IsSealed)
            |> Array.collect (fun t ->
                t.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
                |> Array.filter (fun method -> not method.IsSpecialName)
                |> Array.map (fun method -> $"{t.FullName}.{method.Name}"))
            |> Array.sort

        // Feature 067 / FR-005: shared update-or-assert (set FSGG_UPDATE_BASELINE=1 to re-capture).
        let baselinePath =
            Path.Combine(Commands.repoRoot, "tests", "FS.GG.SDD.Validation.Tests", "PublicSurface.baseline")

        TestShared.SurfaceBaseline.verify baselinePath capture
