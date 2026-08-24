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

        let private parseCoreVersion (text: string) =
            let separator = text.IndexOfAny([| '-'; '+' |])
            let core = if separator < 0 then text else text.Substring(0, separator)
            Version.tryParse core

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
                    match parseCoreVersion text with
                    | None -> "unparseable", None, None
                    | Some version ->
                        let suggested = applyBump version requiredBump
                        let suggestion = if requiredBump = "none" then text else render suggested
                        "resolved", Some text, Some suggestion

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

        // FS.GG.SDD#745 (decision #754). Both bodies come through `readOf`, so this fold sees the
        // three states rather than `FileSnapshot option`, where `None` meant *absent* and absorbed
        // *present but unreadable* along with it.
        //
        // THE BUG THIS CLOSES, exactly. `sourceText`/`baselineText` were `snapshot … |> Option.map`;
        // `missing` requires `Option.isSome sourceText`, `drifted` requires `Some, Some`. An
        // unreadable source `.fsi` is in NEITHER list, and `IsCoherent` was
        // `isEmpty missing && isEmpty drifted` — so `surface --check`, a REQUIRED gate on both
        // version axes, reported `isCoherent: true` with an empty `driftedSourcePaths` over a real
        // API-surface drift hidden behind one mode bit, while `checkedCount` claimed the file had
        // been checked. Measured before this change: `chmod 000` on a drifted `ContractVersion.fsi`
        // → `isCoherent: true`, `driftedSourcePaths: []`, `checkedCount: 6`.
        //
        // Projecting to `string option` for the drift comparison is retained BELOW the split, after
        // the unreadable subjects have been separated out — the comparison genuinely only needs the
        // bytes, and it is the coherence verdict, not the comparison, that had to learn the third
        // state.
        let classified =
            sources
            |> List.map (fun s ->
                let baseline = baselinePathFor sourceRoot baselineRoot s
                s, baseline, readOf s model, readOf baseline model)

        // Every subject THIS RUN was responsible for comparing and could not read — sources and
        // their expected baselines alike. A baseline that cannot be read is exactly as blinding as
        // a source that cannot: the comparison has one side, and one side always compares equal.
        //
        // The two ROOT enumerations are in the same set, and they are the sharper edge of the two:
        // `listing` derives `sources` from the source root's listing, so a root this run could not
        // open yields an EMPTY candidate set — zero signatures, zero drift, `isCoherent: true` over
        // an entire tree. (The wider `EnumerateDirectory` lane is `FS.GG.SDD#743`, which this row
        // unblocks; what is closed here is `surface`'s own two roots.)
        let unreadable =
            (classified
             |> List.collect (fun (_, _, sourceRead, baselineRead) -> [ sourceRead; baselineRead ]))
            @ [ enumerationOf sourceRoot model; enumerationOf baselineRoot model ]
            |> unreadablePathsOf

        // A signature to (re)write: source present, and baseline absent or byte-differing.
        let needsWrite (sourceText: string option) (baselineText: string option) =
            match sourceText, baselineText with
            | Some _, None -> true
            | Some s, Some b -> s <> b
            | None, _ -> false

        // MISSING is `Bytes, Absent` and nothing else. `Bytes, Unreadable` used to land here once
        // the reads collapsed to `option`, which is #745 AC4's "never renders as missing": a
        // baseline that is present and unreadable would have been reported to the operator as a
        // baseline that does not exist, pointing `surface --update` at creating a file that is
        // already there. It is reported under `unreadable` instead.
        let missing =
            classified
            |> List.filter (fun (_, _, sourceRead, baselineRead) ->
                match sourceRead, baselineRead with
                | Bytes _, Absent -> true
                // `Truncated` is unreachable on both axes — these are `.fsi` FILE reads — and is
                // decided the same way `Unreadable` is (#743): not observed to be missing.
                | Bytes _, (Bytes _ | Unreadable _ | Truncated _)
                | (Absent | Unreadable _ | Truncated _), _ -> false)
            |> List.map (fun (_, baseline, _, _) -> baseline)
            |> List.sort

        // DRIFTED needs both bodies, so it is unchanged in substance — the total match is what
        // changed. `Unreadable` on either side is deliberately NOT drift: the tool did not observe
        // a difference, it failed to look, and conflating the two would produce a `surface.drift`
        // naming a file whose baseline may in fact match. `unreadable` blocks the verdict instead.
        let drifted =
            classified
            |> List.filter (fun (_, _, sourceRead, baselineRead) ->
                match sourceRead, baselineRead with
                | Bytes source, Bytes baseline -> source.Text <> baseline.Text
                // As above (#743): unreachable, and never drift — the tool did not observe a
                // difference, it failed to observe.
                | Bytes _, (Absent | Unreadable _ | Truncated _)
                | (Absent | Unreadable _ | Truncated _), _ -> false)
            |> List.map (fun (s, _, _, _) -> s)
            |> List.sort

        // CHECKED means READ (decision #754). This was `List.length sources` — the count of files
        // the run INTENDED to check, taken from the directory listing before a single body was
        // read — so the measured `chmod 000` run reported `checkedCount: 6` for five files it read
        // and one it did not. A count that includes unread files is the same defect in miniature.
        let checkedCount =
            classified
            |> List.filter (fun (_, _, sourceRead, _) ->
                match sourceRead with
                | Bytes _ -> true
                | Absent
                | Unreadable _
                // CHECKED means READ. A truncated listing is not a read signature (#743).
                | Truncated _ -> false)
            |> List.length

        let classified =
            classified
            |> List.map (fun (s, baseline, sourceRead, baselineRead) ->
                let bodyOf (read: ReadResult) =
                    match read with
                    | Bytes snap -> Some snap.Text
                    | Absent
                    | Unreadable _
                    // A listing is not a signature body (#743); unreachable on these reads.
                    | Truncated _ -> None

                s, baseline, bodyOf sourceRead, bodyOf baselineRead)

        let expectedBaselines =
            classified |> List.map (fun (_, baseline, _, _) -> baseline) |> Set.ofList

        let orphans =
            baselines
            |> List.filter (fun b -> not (Set.contains b expectedBaselines))
            |> List.sort

        // `--update` reconciles only pairs it could READ. An unreadable baseline would otherwise
        // look identical to an absent one here (`baselineText = None` ⇒ `needsWrite`), and the run
        // would plan a write over a file whose current bytes it never saw — the write edge refuses
        // it (`unreadableWriteTarget`), so nothing would be clobbered, but `UpdatedBaselinePaths`
        // would still name a path this run did not update. An unreadable SOURCE has no bytes to
        // write from at all. Both are already blocking through `unreadable`.
        let reconcilable (source: string) (baseline: string) =
            not (List.contains source unreadable) && not (List.contains baseline unreadable)

        let updated =
            if model.Request.SurfaceUpdate then
                classified
                |> List.filter (fun (source, baseline, sourceText, baselineText) ->
                    reconcilable source baseline && needsWrite sourceText baselineText)
                |> List.map (fun (_, baseline, _, _) -> baseline)
                |> List.sort
            else
                []

        let writes =
            if model.Request.SurfaceUpdate then
                classified
                |> List.choose (fun (source, baseline, sourceText, baselineText) ->
                    match sourceText with
                    | Some text when reconcilable source baseline && needsWrite sourceText baselineText ->
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

        //
        // Total over `ReadResult` since #745. An unreadable axis file collapses to
        // `undeterminable` alongside an absent one — correct here and not a fail-open: this is a
        // PROMPT, never a verdict and never an exit code (FR-008/FR-013), and the read edge has
        // already emitted the `unreadableFile` warning naming it.
        let axisSnapshot =
            if escapesRoot axisFile then
                None
            else
                match readOf axisFile model with
                | Bytes snap -> Some snap.Text
                | Absent
                | Unreadable _
                // Unreachable — the axis file is a file (#743) — and `undeterminable` either way.
                | Truncated _ -> None

        let versionBump =
            VersionAxis.prompt axisFile axisProperty axisSnapshot classification

        let summary =
            { SourceRoot = normalizeRelativePath sourceRoot
              BaselineRoot = normalizeRelativePath baselineRoot
              Mode = if model.Request.SurfaceUpdate then "update" else "check"
              CheckedCount = checkedCount
              MissingBaselinePaths = missing
              DriftedSourcePaths = drifted
              OrphanBaselinePaths = orphans
              UpdatedBaselinePaths = updated
              // Decision #754's binding rule: a verdict may never report coherent over a subject it
              // did not read. The third conjunct is the whole of #745 — without it this reads
              // `true` on a `chmod 000` file, at exit 0, on a required gate.
              IsCoherent = List.isEmpty missing && List.isEmpty drifted && List.isEmpty unreadable
              Classification = classification
              VersionBump = versionBump }

        summary, (unreadable, writes)

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
                let summary, (unreadable, writes) = computeSummary model

                // FS.GG.SDD#745 / decision #754. Blocking, and under BOTH modes — deliberately not
                // gated on `not SurfaceUpdate` the way `driftDiagnostics` is. `--update` is the
                // mode that RECONCILES drift, and it cannot reconcile bytes it could not read: an
                // `--update` that silently left an unreadable pair alone and exited 0 would report
                // the baselines as refreshed when one of them was not.
                //
                // Not a tool defect, so this is exit 1 and never exit 2 — a mode bit in the
                // workspace is not a broken tool. The per-file reasons ride in on the
                // `unreadableFile` warnings the read edge already emitted.
                let unreadableDiagnostics =
                    if List.isEmpty unreadable then
                        []
                    else
                        [ unreadableSubject "surface" unreadable ]

                // Drift blocks (exit 1) only under `--check`; `--update` reconciles it instead.
                // Keyed on the drift lists themselves, NOT on `not summary.IsCoherent` as before:
                // `IsCoherent` now also carries the unreadable subjects, and reusing it here would
                // emit `surface.drift` reading "0 missing, 0 differing" for a run whose only
                // finding was a file it could not open — the "never renders as drift" half of
                // #745 AC4. `unreadableDiagnostics` above is that run's finding.
                let driftDiagnostics =
                    if
                        (not model.Request.SurfaceUpdate)
                        && not (
                            List.isEmpty summary.MissingBaselinePaths
                            && List.isEmpty summary.DriftedSourcePaths
                        )
                    then
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
                    Diagnostics =
                        model.Diagnostics
                        @ unreadableDiagnostics
                        @ driftDiagnostics
                        @ orphanDiagnostics
                        @ versionDiagnostics
                    PendingEffects = model.PendingEffects @ writes },
                writes
