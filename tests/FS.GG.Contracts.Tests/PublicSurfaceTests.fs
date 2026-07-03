namespace FS.GG.Contracts.Tests

open System
open System.IO
open System.Reflection
open FS.GG.SDD.TestShared
open Xunit

module PublicSurfaceTests =

    let private baselinePath =
        Path.Combine(TestSupport.repoRoot, "tests", "FS.GG.Contracts.Tests", "PublicSurface.baseline")

    /// Deterministic capture of the package's exported surface: every public (or
    /// nested-public) type under `Fsgg`, each module's public static members, and
    /// each record/DU's public instance properties.
    let private capture () =
        let assembly = typeof<Fsgg.Schemas.SchemaContractEntry>.Assembly

        let startsWithFsgg (name: string | null) =
            match name with
            | null -> false
            | n -> n.StartsWith("Fsgg", StringComparison.Ordinal)

        let visibleTypes =
            assembly.GetTypes()
            |> Array.filter (fun t -> (t.IsPublic || t.IsNestedPublic) && startsWithFsgg t.FullName)

        let typeNames = visibleTypes |> Array.map (fun t -> $"type {t.FullName}")

        let isModule (t: Type) = t.IsAbstract && t.IsSealed

        let moduleMembers =
            visibleTypes
            |> Array.filter isModule
            |> Array.collect (fun t ->
                let functions =
                    t.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
                    |> Array.filter (fun m -> not m.IsSpecialName)
                    |> Array.map (fun m -> $"val {t.FullName}.{m.Name}")

                let constants =
                    t.GetProperties(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
                    |> Array.map (fun p -> $"val {t.FullName}.{p.Name}")

                Array.append functions constants)

        let recordMembers =
            visibleTypes
            |> Array.filter (isModule >> not)
            |> Array.collect (fun t ->
                t.GetProperties(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
                |> Array.map (fun p -> $"member {t.FullName}.{p.Name}"))

        Array.concat [ typeNames; moduleMembers; recordMembers ] |> Array.sort

    // Principle III / quickstart Scenario I: the exported surface matches the deliberate
    // baseline. Set FSGG_UPDATE_BASELINE=1 to re-capture intentionally. Feature 067 / FR-005:
    // the update-or-assert logic is now the shared TestShared.SurfaceBaseline.verify used by all
    // five baseline tests (this was the one test that already carried the switch).
    [<Fact>]
    let ``public surface matches baseline`` () =
        TestShared.SurfaceBaseline.verify baselinePath capture
