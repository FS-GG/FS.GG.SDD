namespace FS.GG.SDD.Commands.Internal

open System
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal.Foundation

/// `fsgg-sdd dependency-surface` handler (feature 105, Phase 2; design of record ADR-0004 D2).
/// Compares each committed capture under `docs/dependency-surface/<Pkg>/<ver>.json` — and an
/// explicit `--param packageId=/version=` target — against the package's **real restored surface**,
/// read by reflection at the edge (`ReadPackageSurface`). `--check` (default) blocks on drift (a
/// committed digest disagreeing with the real surface); `--update` refreshes/creates the captures.
/// An unreadable real surface is advisory, never a false drift (fail-open, ADR-0002 / #266).
///
/// Staged like `surface`: the first wave (in `Foundation.plan`) enumerates the baseline root; this
/// driver then gates the per-target reads (the committed capture + the real surface) before it
/// computes the pure drift picture / write set.
module internal HandlersDependencySurface =

    // A committed capture path `<root>/<Pkg>/<ver>.json` → its `(packageId, version)`. Purely
    // structural — the package id and version are the two path segments below the root, so generic
    // SDD embeds no package literal (FR-009).
    let private parseCapturePath (baselineRoot: string) (path: string) : (string * string) option =
        let root = normalizeRelativePath baselineRoot
        let normalized = normalizeRelativePath path
        let prefix = if root = "" then "" else root + "/"

        if
            normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            && (prefix = "" || normalized.StartsWith(prefix, StringComparison.Ordinal))
        then
            let tail =
                if prefix = "" then
                    normalized
                else
                    normalized.Substring prefix.Length

            match tail.Split('/') with
            | [| packageId; versionFile |] when
                not (String.IsNullOrWhiteSpace packageId)
                && versionFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ->
                let version = versionFile.Substring(0, versionFile.Length - ".json".Length)

                if String.IsNullOrWhiteSpace version then
                    None
                else
                    Some(packageId, version)
            | _ -> None
        else
            None

    // The `<PackageId>@<Version>` id used in reports and diagnostics.
    let private targetId (packageId: string, version: string) = $"{packageId}@{version}"

    // Every committed capture discovered under the baseline root, from its enumerate snapshot.
    let private committedTargets baselineRoot model =
        (directoryListing baselineRoot model).Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.choose (parseCapturePath baselineRoot)
        |> Array.distinct
        |> Array.sortBy targetId
        |> Array.toList

    // An explicit `--update`/`--check` target from `--param packageId=/version=`, if both are given.
    let private explicitTarget model =
        let packageId = dependencySurfacePackageId model.Request
        let version = dependencySurfaceVersion model.Request

        if packageId <> "" && version <> "" then
            Some(packageId, version)
        else
            None

    // Every package this run examines: committed captures ∪ the explicit target, deduplicated.
    let private allTargets baselineRoot model =
        let committed = committedTargets baselineRoot model

        match explicitTarget model with
        | Some target when not (List.contains target committed) -> committed @ [ target ]
        | _ -> committed

    // The real surface of a target, from its interpreted `ReadPackageSurface` result. `None` ⇒ the
    // package could not be read (advisory). By the time this runs the read gate has interpreted it.
    let private observedSymbols (packageId, version) model : string list option =
        let key = $"read-package-surface:{packageId}@{version}"

        model.InterpretedEffects
        |> List.tryPick (fun result ->
            if effectKey result.Effect = key then
                Some result.Snapshot
            else
                None)
        |> Option.flatten
        |> Option.map (fun snapshot ->
            snapshot.Text.Split([| '\n' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.toList)

    // The committed capture's recorded digest, from the interpreted `ReadFile` of its path.
    let private committedDigest baselineRoot (packageId, version) model : string option =
        snapshot (DependencySurface.capturePath baselineRoot packageId version) model
        |> Option.bind (fun snapshot ->
            match DependencySurface.tryParse snapshot.Text with
            | Ok capture -> Some capture.Sha256
            | Error _ -> None)

    // The per-target read gate: read the committed capture + the real surface before drift is
    // computed. Mirrors `HandlersSurface.readGate`.
    let private bodyReads baselineRoot model =
        let committed = committedTargets baselineRoot model

        [ for target in allTargets baselineRoot model do
              ReadPackageSurface target
          for packageId, version in committed do
              ReadFile(DependencySurface.capturePath baselineRoot packageId version) ]
        |> List.distinctBy effectKey

    let private readGate baselineRoot model =
        let reads = bodyReads baselineRoot model

        let allInterpreted =
            reads |> List.forall (fun effect -> hasInterpreted (effectKey effect) model)

        let anyPlanned =
            reads |> List.exists (fun effect -> hasPlanned (effectKey effect) model)

        if List.isEmpty reads || allInterpreted then None
        elif anyPlanned then Some []
        else Some reads

    // The pure drift picture plus, under `--update`, the capture write effects. Every input is a
    // snapshot from the interpreted reads — no disk access here.
    let private computeSummary baselineRoot model =
        let update = model.Request.SurfaceUpdate
        let committed = committedTargets baselineRoot model |> Set.ofList
        let targets = allTargets baselineRoot model

        // Per target: (target, committed digest option, observed symbols option).
        let classified =
            targets
            |> List.map (fun target ->
                let committedSha =
                    if Set.contains target committed then
                        committedDigest baselineRoot target model
                    else
                        None

                target, committedSha, observedSymbols target model)

        // A target's verdict, before any `--update` reconciliation.
        let statusOf committedSha observed =
            match committedSha, observed with
            | _, None -> "unavailable"
            | Some committedDigest, Some symbols ->
                if committedDigest = DependencySurface.symbolDigest symbols then
                    "matched"
                else
                    "drifted"
            | None, Some _ -> "new"

        let entries =
            classified
            |> List.map (fun (target, committedSha, observed) ->
                let baseStatus = statusOf committedSha observed

                // `--update` reconciles a real difference by writing it: drifted/new ⇒ written.
                let status =
                    if update && (baseStatus = "drifted" || baseStatus = "new") then
                        "written"
                    else
                        baseStatus

                let packageId, version = target

                { PackageId = packageId
                  Version = version
                  Status = status
                  CommittedSha256 = committedSha
                  ObservedSha256 = observed |> Option.map DependencySurface.symbolDigest
                  ObservedSymbolCount = observed |> Option.map List.length |> Option.defaultValue 0 })

        let idsWithStatus wanted =
            entries
            |> List.filter (fun entry -> entry.Status = wanted)
            |> List.map (fun entry -> targetId (entry.PackageId, entry.Version))
            |> List.sort

        // Under `--update`, write a fresh canonical capture for every reconciled target.
        let writes =
            if update then
                classified
                |> List.choose (fun (target, committedSha, observed) ->
                    match observed with
                    | Some symbols when statusOf committedSha observed <> "matched" ->
                        let packageId, version = target

                        let capture = DependencySurface.create packageId version "nuget-cache" symbols

                        Some(
                            WriteFile(
                                DependencySurface.capturePath baselineRoot packageId version,
                                DependencySurface.serialize capture,
                                GeneratedView
                            )
                        )
                    | _ -> None)
            else
                []

        let summary =
            { BaselineRoot = normalizeRelativePath baselineRoot
              Mode = if update then "update" else "check"
              CheckedCount = List.length targets
              Entries = entries |> List.sortBy (fun entry -> targetId (entry.PackageId, entry.Version))
              DriftedPackages = idsWithStatus "drifted"
              UnavailablePackages = idsWithStatus "unavailable"
              UpdatedPackages = idsWithStatus "written"
              IsCoherent = List.isEmpty (idsWithStatus "drifted") }

        summary, writes

    let computeDependencySurfaceNext model =
        match model.DependencySurface with
        | Some _ -> model, []
        | None ->
            let baselineRoot = dependencySurfaceBaselineRoot model.Request

            match readGate baselineRoot model with
            | Some effects ->
                if List.isEmpty effects then
                    model, []
                else
                    { model with
                        PendingEffects = model.PendingEffects @ effects },
                    effects
            | None ->
                let summary, writes = computeSummary baselineRoot model

                // Drift blocks (exit 1) only under `--check`; `--update` reconciles it into a write.
                let driftDiagnostics =
                    if (not model.Request.SurfaceUpdate) && not (List.isEmpty summary.DriftedPackages) then
                        [ dependencySurfaceDrift summary.DriftedPackages ]
                    else
                        []

                // Unreadable real surface is advisory in both modes.
                let unavailableDiagnostics =
                    if List.isEmpty summary.UnavailablePackages then
                        []
                    else
                        [ dependencySurfaceUnavailable summary.UnavailablePackages ]

                { model with
                    DependencySurface = Some summary
                    Diagnostics = model.Diagnostics @ driftDiagnostics @ unavailableDiagnostics
                    PendingEffects = model.PendingEffects @ writes },
                writes
