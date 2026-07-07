namespace FS.GG.SDD.Commands.Internal

open System
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal.Foundation

/// `fsgg-sdd surface` handler (feature 086). Enforces the API-surface baseline convention: every
/// authored `src/**/*.fsi` signature has a byte-identical committed baseline under
/// `docs/api-surface/` at the mirrored path. `--check` (default) is read-only and blocks on drift
/// (a missing or byte-differing baseline) with a `surface.drift` `DiagnosticError` (exit 1);
/// `--update` refreshes the baselines from the authored `.fsi` (exit 0). Orphan baselines (a
/// committed baseline with no source) are advisory (`surface.orphanBaseline` warning) and never
/// removed. The plan enumerates the two roots; this driver gates the per-file body reads (mirroring
/// `doctor`'s skill-read gate) and then computes the pure drift picture / write set.
module internal HandlersSurface =

    // A candidate authored signature: ends with `.fsi`, and not inside a build-output tree
    // (`obj`/`bin`), which can hold compiler-generated signatures that are not the public surface.
    let private isAuthoredSignature (path: string) =
        path.EndsWith(".fsi", StringComparison.OrdinalIgnoreCase)
        && not (path.Contains "/obj/")
        && not (path.Contains "/bin/")

    // The sorted, de-duplicated authored-`.fsi` paths under a root, from its enumerate snapshot.
    let private listing root model =
        (directoryListing root model).Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map normalizeRelativePath
        |> Array.filter isAuthoredSignature
        |> Array.sort
        |> Array.toList

    /// Map a source-relative `.fsi` path to its baseline path by swapping the source-root prefix for
    /// the baseline root, preserving the `<Pkg>/<Name>.fsi` tail. Purely structural — no provider or
    /// package literal (FR-002 / FR-014).
    let baselinePathFor (sourceRoot: string) (baselineRoot: string) (sourcePath: string) =
        let src = normalizeRelativePath sourceRoot
        let baseline = normalizeRelativePath baselineRoot
        let prefix = if src = "" then "" else src + "/"

        let tail =
            if prefix <> "" && sourcePath.StartsWith(prefix, StringComparison.Ordinal) then
                sourcePath.Substring(prefix.Length)
            else
                sourcePath

        if baseline = "" then tail else baseline + "/" + tail

    // The body-read gate: read every source signature + its expected baseline before the drift is
    // computed. Mirrors `HandlersDoctor.skillReadGate` — `None` ⇒ ready to compute; `Some effects`
    // ⇒ not ready (emit the reads, or `[]` while awaiting their interpretation). A missing baseline
    // stays absent after its read, so the gate resolves on read *interpretation*, not presence.
    let private bodyReads model =
        let sourceRoot = surfaceSourceRoot model.Request
        let baselineRoot = surfaceBaselineRoot model.Request

        [ for s in listing sourceRoot model do
              ReadFile s
              ReadFile(baselinePathFor sourceRoot baselineRoot s) ]
        |> List.distinctBy effectKey

    let private readGate model =
        let reads = bodyReads model

        let allInterpreted =
            reads |> List.forall (fun effect -> hasInterpreted (effectKey effect) model)

        let anyPlanned =
            reads |> List.exists (fun effect -> hasPlanned (effectKey effect) model)

        if List.isEmpty reads || allInterpreted then None
        elif anyPlanned then Some []
        else Some reads

    // The pure drift picture plus, under `--update`, the baseline write effects. Every input is a
    // snapshot from the interpreted reads — no disk access here.
    let private computeSummary model =
        let sourceRoot = surfaceSourceRoot model.Request
        let baselineRoot = surfaceBaselineRoot model.Request
        let sources = listing sourceRoot model
        let baselines = listing baselineRoot model

        // (source, baseline, source body, baseline body) for every discovered signature.
        let classified =
            sources
            |> List.map (fun s ->
                let baseline = baselinePathFor sourceRoot baselineRoot s
                let sourceText = snapshot s model |> Option.map (fun snap -> snap.Text)
                let baselineText = snapshot baseline model |> Option.map (fun snap -> snap.Text)
                s, baseline, sourceText, baselineText)

        // A signature to (re)write: source present, and baseline absent or byte-differing.
        let needsWrite (sourceText: string option) (baselineText: string option) =
            match sourceText, baselineText with
            | Some _, None -> true
            | Some s, Some b -> s <> b
            | None, _ -> false

        let missing =
            classified
            |> List.filter (fun (_, _, sourceText, baselineText) ->
                Option.isSome sourceText && Option.isNone baselineText)
            |> List.map (fun (_, baseline, _, _) -> baseline)
            |> List.sort

        let drifted =
            classified
            |> List.filter (fun (_, _, sourceText, baselineText) ->
                match sourceText, baselineText with
                | Some s, Some b -> s <> b
                | _ -> false)
            |> List.map (fun (s, _, _, _) -> s)
            |> List.sort

        let expectedBaselines =
            classified |> List.map (fun (_, baseline, _, _) -> baseline) |> Set.ofList

        let orphans =
            baselines
            |> List.filter (fun b -> not (Set.contains b expectedBaselines))
            |> List.sort

        let updated =
            if model.Request.SurfaceUpdate then
                classified
                |> List.filter (fun (_, _, sourceText, baselineText) -> needsWrite sourceText baselineText)
                |> List.map (fun (_, baseline, _, _) -> baseline)
                |> List.sort
            else
                []

        let writes =
            if model.Request.SurfaceUpdate then
                classified
                |> List.choose (fun (_, baseline, sourceText, baselineText) ->
                    match sourceText with
                    | Some text when needsWrite sourceText baselineText ->
                        Some(WriteFile(baseline, text, GeneratedView))
                    | _ -> None)
            else
                []

        let summary =
            { SourceRoot = normalizeRelativePath sourceRoot
              BaselineRoot = normalizeRelativePath baselineRoot
              Mode = if model.Request.SurfaceUpdate then "update" else "check"
              CheckedCount = List.length sources
              MissingBaselinePaths = missing
              DriftedSourcePaths = drifted
              OrphanBaselinePaths = orphans
              UpdatedBaselinePaths = updated
              IsCoherent = List.isEmpty missing && List.isEmpty drifted }

        summary, writes

    let computeSurfaceNext model =
        match model.Surface with
        | Some _ -> model, []
        | None ->
            match readGate model with
            | Some effects ->
                // Source/baseline bodies not yet read: emit the gate reads (read-only) and let the
                // tick loop interpret them before the content-addressed drift runs.
                if List.isEmpty effects then
                    model, []
                else
                    { model with
                        PendingEffects = model.PendingEffects @ effects },
                    effects
            | None ->
                let summary, writes = computeSummary model

                // Drift blocks (exit 1) only under `--check`; `--update` reconciles it instead.
                let driftDiagnostics =
                    if (not model.Request.SurfaceUpdate) && not summary.IsCoherent then
                        [ surfaceDrift
                              (List.length summary.MissingBaselinePaths)
                              (List.length summary.DriftedSourcePaths)
                              (summary.MissingBaselinePaths @ summary.DriftedSourcePaths) ]
                    else
                        []

                // Orphan baselines are advisory in both modes (no delete effect exists).
                let orphanDiagnostics =
                    if List.isEmpty summary.OrphanBaselinePaths then
                        []
                    else
                        [ surfaceOrphanBaseline summary.OrphanBaselinePaths ]

                { model with
                    Surface = Some summary
                    Diagnostics = model.Diagnostics @ driftDiagnostics @ orphanDiagnostics
                    PendingEffects = model.PendingEffects @ writes },
                writes
