namespace FS.GG.SDD.Commands.Internal

open System
open System.Xml
open System.Xml.Linq
open Fsgg
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

    /// Feature 087 (FS-GG/.github ADR-0025): the pure additive-vs-breaking classification of a
    /// drifted `.fsi`. Compares the *member tokens* parsed from the two signature texts — comments,
    /// blank lines, and ordering are stripped, so only real declaration changes register. No parser,
    /// no reflection: the `.fsi` text is the source of truth (consistent with feature 086).
    module private SurfaceClassify =

        // Remove `(* … *)` block comments (simple, non-nested) so a comment cannot masquerade as a
        // member token. Line (`//`/`///`) comments are stripped per line in `memberTokens`.
        let private stripBlockComments (text: string) =
            let sb = System.Text.StringBuilder(text.Length)
            let mutable i = 0
            let mutable inBlock = false

            while i < text.Length do
                if inBlock then
                    if i + 1 < text.Length && text.[i] = '*' && text.[i + 1] = ')' then
                        inBlock <- false
                        i <- i + 2
                    else
                        i <- i + 1
                elif i + 1 < text.Length && text.[i] = '(' && text.[i + 1] = '*' then
                    inBlock <- true
                    i <- i + 2
                else
                    sb.Append text.[i] |> ignore
                    i <- i + 1

            sb.ToString()

        // The set of member tokens declared in a signature text: comment-stripped, blank-dropped,
        // whitespace-collapsed, one token per significant line. A `Set` makes ordering and duplicate
        // formatting irrelevant, which is exactly the additive/breaking/cosmetic contract.
        let memberTokens (text: string) : Set<string> =
            (stripBlockComments text).Split([| '\n'; '\r' |])
            |> Array.map (fun line ->
                let commentAt = line.IndexOf "//"

                let code =
                    if commentAt >= 0 then
                        line.Substring(0, commentAt)
                    else
                        line

                Text.RegularExpressions.Regex.Replace(code.Trim(), @"\s+", " "))
            |> Array.filter (fun token -> token <> "")
            |> Set.ofArray

        // Feature 094 / FR-015. This is deliberately NOT `ReleaseContract.bumpRule`, and the two must
        // not be unified: `bumpRule` maps the *release-contract* change classes
        // (Breaking→major, Additive→minor, Clarifying→**patch**), whereas this maps the
        // *surface-mutation* verdicts (breaking→major, additive→minor, cosmetic/none→**none**).
        // A cosmetic `.fsi` reformat implies no release at all; a Clarifying contract change implies
        // a patch. Collapsing them would silently turn every cosmetic drift into a patch bump — a
        // behavior change, not a refactor (spec 094 AMB-005, research R5).
        let private bumpFor classification =
            match classification with
            | "breaking" -> "major"
            | "additive" -> "minor"
            | _ -> "none"

        // Classify one drifted pair (called only when the two texts already differ byte-for-byte).
        // A prior member gone ⇒ breaking; only additions ⇒ additive; equal member sets ⇒ cosmetic.
        // A non-empty source that yields no member token is unparseable ⇒ breaking (FR-011).
        let classifyPair (path: string) (baselineText: string) (sourceText: string) : ClassifiedEntry =
            let baselineTokens = memberTokens baselineText
            let sourceTokens = memberTokens sourceText
            let removedOrChanged = Set.difference baselineTokens sourceTokens |> Set.toList
            let added = Set.difference sourceTokens baselineTokens |> Set.toList

            let unparseable =
                (not (String.IsNullOrWhiteSpace sourceText)) && Set.isEmpty sourceTokens

            let classification =
                if unparseable || not (List.isEmpty removedOrChanged) then
                    "breaking"
                elif not (List.isEmpty added) then
                    "additive"
                else
                    "cosmetic"

            { Path = path
              Classification = classification
              RecommendedBump = bumpFor classification
              AddedMembers = added |> List.sort
              RemovedOrChangedMembers = removedOrChanged |> List.sort
              UnparseableFallback = unparseable }

        let private severity classification =
            match classification with
            | "breaking" -> 3
            | "additive" -> 2
            | "cosmetic" -> 1
            | _ -> 0

        // Roll the per-file entries up to the most-severe run verdict + its recommended bump.
        let rollup (entries: ClassifiedEntry list) : SurfaceClassification =
            let sorted = entries |> List.sortBy (fun entry -> entry.Path)

            let verdict =
                if List.isEmpty sorted then
                    "none"
                else
                    sorted |> List.map (fun entry -> entry.Classification) |> List.maxBy severity

            { Verdict = verdict
              RecommendedBump = bumpFor verdict
              Entries = sorted }

    /// Feature 094 (FS-GG/.github ADR-0025 reconcile step 3a): the coherent-set version obligation a
    /// classified mutation implies. Pure over the interpreted axis snapshot — no disk access here.
    module private VersionAxis =

        /// Read one MSBuild property out of the axis file's *text*. `XElement.Value` concatenates
        /// text nodes and ignores comments, so `<Version>0.8.0<!-- pinned --></Version>` resolves
        /// cleanly; the `.Trim()` is load-bearing for the usual `<Version>\n  0.8.0\n</Version>`
        /// (research R4). Matched on `LocalName` because some repos' `Directory.Build.props` still
        /// declares the legacy MSBuild 2003 namespace (research R8).
        ///
        /// MSBuild is NOT evaluated (FR-002): no imports, no `$(…)` expansion, no conditions, no
        /// property functions. A malformed file is `None` — `undeterminable`, never an exception.
        let readAxisText (property: string) (text: string) : string option =
            try
                XDocument.Parse(text).Descendants()
                |> Seq.tryFind (fun element -> element.Name.LocalName = property)
                |> Option.map (fun element -> element.Value.Trim())
            with :? XmlException ->
                None

        /// Pure, total. `bumpFor` supplies the bump; see its comment for why this is not
        /// `ReleaseContract.bumpRule`.
        let applyBump (version: Version.Version) bump : Version.Version =
            match bump with
            | "major" ->
                { Major = version.Major + 1
                  Minor = 0
                  Patch = 0 }
            | "minor" ->
                { version with
                    Minor = version.Minor + 1
                    Patch = 0 }
            | _ -> version

        let private render (version: Version.Version) =
            $"{version.Major}.{version.Minor}.{version.Patch}"

        /// Fold the axis snapshot and the run verdict into the prompt. `RequiredBump` is a total
        /// function of the classification alone, so it lands in *every* axis state (FR-006, I1) —
        /// an unresolvable axis still tells the operator what the mutation costs.
        let prompt
            (axisFile: string)
            (axisProperty: string)
            (axisSnapshot: string option)
            (classification: SurfaceClassification)
            =
            let requiredBump = classification.RecommendedBump

            // `resolved` requires both a readable property and a parseable triple. The two `None`
            // branches collapse to `undeterminable`; a present-but-bad value is `unparseable`, and
            // its text is deliberately NOT echoed (it is not a version).
            let axisState, currentVersion, suggestedVersion =
                match axisSnapshot |> Option.bind (readAxisText axisProperty) with
                | None -> "undeterminable", None, None
                | Some text ->
                    match Version.tryParse text with
                    | None -> "unparseable", None, None
                    | Some version ->
                        let suggested = applyBump version requiredBump
                        "resolved", Some(render version), Some(render suggested)

            { AxisFile = axisFile
              AxisProperty = axisProperty
              AxisState = axisState
              CurrentVersion = currentVersion
              RequiredBump = requiredBump
              SuggestedVersion = suggestedVersion }

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

        // Feature 087: classify only the drifted set (baseline present and byte-differing). A
        // `missing-baseline` file is a *new* surface (fresh registration), and `matched`/`orphan`
        // have no delta — none of those are classified. Advisory: no diagnostic, no exit change.
        let classification =
            classified
            |> List.choose (fun (source, _, sourceText, baselineText) ->
                match sourceText, baselineText with
                | Some sourceBody, Some baselineBody when sourceBody <> baselineBody ->
                    Some(SurfaceClassify.classifyPair source baselineBody sourceBody)
                | _ -> None)
            |> SurfaceClassify.rollup

        // Feature 094: the version-bump prompt. Read from the first-wave axis snapshot, so it is
        // computed from the tree as it was *before* any `--update` write above (R1) — the run that
        // erases the drift still reports what the drift cost. `escapesRoot` may have planned no read
        // at all, in which case the snapshot is `None` ⇒ `undeterminable`, exactly as for an absent
        // file. Belt and braces: a snapshot is only trusted when the raw param stayed inside the
        // root, so a future change to `normalizeRelativePath`/`fullPath` cannot silently reopen the
        // hole (FR-017) — a predicate over strings is not a containment proof.
        let axisFile = versionAxisFile model.Request
        let axisProperty = versionAxisProperty model.Request

        let axisSnapshot =
            if escapesRoot axisFile then
                None
            else
                snapshot axisFile model |> Option.map (fun snap -> snap.Text)

        let versionBump =
            VersionAxis.prompt axisFile axisProperty axisSnapshot classification

        let summary =
            { SourceRoot = normalizeRelativePath sourceRoot
              BaselineRoot = normalizeRelativePath baselineRoot
              Mode = if model.Request.SurfaceUpdate then "update" else "check"
              CheckedCount = List.length sources
              MissingBaselinePaths = missing
              DriftedSourcePaths = drifted
              OrphanBaselinePaths = orphans
              UpdatedBaselinePaths = updated
              IsCoherent = List.isEmpty missing && List.isEmpty drifted
              Classification = classification
              VersionBump = versionBump }

        summary, writes

    // FS-GG/FS.GG.SDD#185: containment is enforced at ONE place — `Foundation.plan` refuses every
    // effect for an escaping root and records the blocking `surface.rootEscape` diagnostic. With no
    // effect planned, the tick loop never interprets anything, so this function is never entered on
    // the escape path and `model.Surface` stays `None` (the diagnostic is the whole report). No
    // second guard is added here: a handler-side check would be unreachable dead code (proven — a
    // `failwith` in that arm leaves every escape test green), and duplicating the decision would
    // invite the two copies to drift.
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

                // Feature 094 / ADR-0025 step 3a. Emitted under BOTH modes — deliberately NOT gated
                // on `not model.Request.SurfaceUpdate` the way `driftDiagnostics` is (FR-011, US2).
                // `--update` is the run that *erases* the drift; a prompt only under `--check` would
                // never be seen by the normal PR workflow. Emitted iff the mutation actually implies
                // a bump (I4) — a cosmetic or absent drift is inert and stays silent (FR-008).
                let versionDiagnostics =
                    let bump = summary.VersionBump

                    if bump.RequiredBump = "major" || bump.RequiredBump = "minor" then
                        [ surfaceVersionBumpRequired
                              summary.Classification.Verdict
                              bump.AxisFile
                              bump.AxisProperty
                              bump.AxisState
                              bump.CurrentVersion
                              bump.RequiredBump
                              bump.SuggestedVersion ]
                    else
                        []

                { model with
                    Surface = Some summary
                    Diagnostics = model.Diagnostics @ driftDiagnostics @ orphanDiagnostics @ versionDiagnostics
                    PendingEffects = model.PendingEffects @ writes },
                writes
