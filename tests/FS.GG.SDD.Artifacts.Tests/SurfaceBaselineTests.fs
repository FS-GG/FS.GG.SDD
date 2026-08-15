namespace FS.GG.SDD.Artifacts.Tests

open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open FS.GG.SDD.Artifacts
open FS.GG.SDD.TestShared
open Xunit

module SurfaceBaselineTests =
    [<Fact>]
    let ``Public surface matches baseline`` () =
        let capture () =
            let assembly = typeof<Identifiers.WorkId>.Assembly

            assembly.GetTypes()
            |> Array.filter (fun t ->
                t.Namespace = "FS.GG.SDD.Artifacts"
                && t.IsClass
                && t.IsAbstract
                && t.IsSealed
                // F# 10.1.400 exposes local implementation closures through Assembly.GetTypes().
                // They carry CompilerGeneratedAttribute and are not callable API contracts.
                && not (t.IsDefined(typeof<CompilerGeneratedAttribute>, false)))
            |> Array.collect (fun t ->
                t.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
                |> Array.filter (fun method ->
                    not method.IsSpecialName
                    && not (method.IsDefined(typeof<CompilerGeneratedAttribute>, false)))
                |> Array.map (fun method -> $"{t.FullName}.{method.Name}"))
            |> Array.sort

        // Feature 067 / FR-005: shared update-or-assert (set FSGG_UPDATE_BASELINE=1 to re-capture).
        let baselinePath =
            Path.Combine(TestSupport.repoRoot, "tests", "FS.GG.SDD.Artifacts.Tests", "PublicSurface.baseline")

        TestShared.SurfaceBaseline.verify baselinePath capture
