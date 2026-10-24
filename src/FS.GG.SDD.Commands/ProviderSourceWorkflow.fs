namespace FS.GG.SDD.Commands

open System
open System.IO
open FS.GG.SDD.Artifacts

/// Pure acquisition progression. Data declarations and emitted effects grant no runtime authority.
module ProviderSourceWorkflow =
    type Request =
        {
            Sources: FS.GG.SDD.Artifacts.ProviderSource.SourceSet
            SourceSetSha256: string
            Policy: FS.GG.SDD.Artifacts.ProviderSource.Policy
            CacheRoot: string
            TargetRoot: string option
            Offline: bool
        }

    type Phase =
        | Preparing
        | ReadingCache
        | Acquiring
        | Validating
        | PublishingCache
        | Retiring
        | Terminal

    type Outcome =
        | Pending
        | Prepared
        | Refused
        | CleanupUnknown


    type Msg =
        | CacheObserved of complete: bool
        | AcquisitionCompleted
        | ValidationCompleted
        | CachePublished
        | FailureObserved of Fsgg.ProviderCatalog.Diagnostic list
        | RetirementObserved of settled: bool

    type Effect =
        | ReadExactCache
        | AcquireSelectedData
        | VerifyClosedSourceSet
        | PublishVerifiedCache
        | RetireSourceOperation

    type Model =
        private
            { Request: Request
              Phase: Phase
              Outcome: Outcome
              Diagnostics: Fsgg.ProviderCatalog.Diagnostic list
              FromCache: bool
              Validated: bool
              Published: bool }

    let private diagnostic code path message : Fsgg.ProviderCatalog.Diagnostic =
        { Code = code; Path = path; Message = message }
    let private boundedDiagnostics (items: Fsgg.ProviderCatalog.Diagnostic list) =
        let shorten maximum (text: string) =
            if obj.ReferenceEquals(text, null) then ""
            elif text.Length <= maximum then text
            else text[..maximum - 1] + " [truncated]"
        let rec retain bytes count (rest: Fsgg.ProviderCatalog.Diagnostic list) =
            match rest with
            | [] -> []
            | item :: tail ->
                let value = { item with Code = shorten 128 item.Code; Path = shorten 1024 item.Path; Message = shorten 4096 item.Message }
                let size = System.Text.Encoding.UTF8.GetByteCount(value.Code + value.Path + value.Message)
                if count >= 63 || bytes + size > 63 * 1024 then
                    [ diagnostic "source.diagnosticsTruncated" "$" "Further diagnostics exceed the 64-entry/64KiB report bound." ]
                else value :: retain (bytes + size) (count + 1) tail
        retain 0 0 items
    let private overlap (a: string) (b: string) =
        let a = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar)
        let b = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar)
        a = b || a.StartsWith(b + string Path.DirectorySeparatorChar, StringComparison.Ordinal)
        || b.StartsWith(a + string Path.DirectorySeparatorChar, StringComparison.Ordinal)
    let private validate request =
        ProviderSource.validateSet request.Sources @ ProviderSource.validatePolicy request.Policy @
        [
            if not (Path.IsPathFullyQualified request.CacheRoot) then
                diagnostic "source.cachePath" "$.cacheRoot" "Source cache must be an explicitly selected absolute directory."
            match request.TargetRoot with
            | Some target when not (Path.IsPathFullyQualified target) ->
                diagnostic "source.targetPath" "$.targetRoot" "Scaffold target must be absolute."
            | Some target when Path.IsPathFullyQualified request.CacheRoot ->
                if overlap request.CacheRoot target then
                    diagnostic "source.targetOverlap" "$.targetRoot" "Cache/staging and target cannot contain each other."
                for source in request.Sources.Sources do
                    match source.Locator with
                    | ProviderSource.LocalDirectory path when Path.IsPathFullyQualified path && overlap path target ->
                        diagnostic "source.targetOverlap" "$.targetRoot" "Local source and target cannot contain each other."
                    | _ -> ()
            | _ -> ()
        ]
    let init request =
        let diagnostics = validate request |> boundedDiagnostics
        let model =
            { Request = request; Phase = ReadingCache; Outcome = Pending; Diagnostics = diagnostics
              FromCache = false; Validated = false; Published = false }
        if diagnostics.IsEmpty then model, [ ReadExactCache ]
        else { model with Phase = Terminal; Outcome = Refused }, []
    let private fail diagnostics model =
        let first = if model.Diagnostics.IsEmpty then boundedDiagnostics diagnostics else model.Diagnostics
        { model with Phase = Retiring; Outcome = Refused; Diagnostics = first }, [ RetireSourceOperation ]
    let update message model =
        if model.Phase = Terminal then model, []
        else
            match message with
            | FailureObserved diagnostics -> fail diagnostics model
            | RetirementObserved settled when model.Phase = Retiring ->
                { model with
                    Phase = Terminal
                    Outcome = if not settled then CleanupUnknown
                              elif model.Diagnostics.IsEmpty && model.Validated && (model.FromCache || model.Published) then Prepared
                              else Refused }, []
            | _ when not model.Diagnostics.IsEmpty -> model, []
            | CacheObserved true when model.Phase = ReadingCache ->
                { model with Phase = Validating; FromCache = true }, [ VerifyClosedSourceSet ]
            | CacheObserved false when model.Phase = ReadingCache && model.Request.Offline ->
                fail [ diagnostic "source.offlineCacheMiss" "$.cacheRoot" "Offline source closure is absent; no network attempt is permitted." ] model
            | CacheObserved false when model.Phase = ReadingCache ->
                { model with Phase = Acquiring }, [ AcquireSelectedData ]
            | AcquisitionCompleted when model.Phase = Acquiring ->
                { model with Phase = Validating }, [ VerifyClosedSourceSet ]
            | ValidationCompleted when model.Phase = Validating && model.FromCache ->
                { model with Phase = Retiring; Validated = true }, [ RetireSourceOperation ]
            | ValidationCompleted when model.Phase = Validating ->
                { model with Phase = PublishingCache; Validated = true }, [ PublishVerifiedCache ]
            | CachePublished when model.Phase = PublishingCache ->
                { model with Phase = Retiring; Published = true }, [ RetireSourceOperation ]
            | _ ->
                fail [ diagnostic "source.phase" "$" "Source event does not match the original operation phase." ] model
    let phase model = model.Phase
    let outcome model = model.Outcome
    let diagnostics model = model.Diagnostics

    let validateScaffoldJoin (selectedTarget: string option) (selectedCatalog: CatalogScaffoldWorkflow.CatalogSelection)
            selectedArchive selectedArchiveSha256 (actual: CatalogScaffoldWorkflow.CatalogScaffoldRequest) =
        [
            if selectedTarget.IsNone || selectedTarget <> Some actual.TargetRoot then
                diagnostic "source.targetBinding" "$.targetRoot" "Inspection-only or differently targeted source handles cannot admit this scaffold request."
            if selectedCatalog.Provider.IsNone || selectedCatalog.Provider <> actual.Selection.Provider
               || selectedCatalog.Overrides <> actual.Selection.Overrides then
                diagnostic "source.providerBinding" "$.selection" "Selected provider and ordered literal overrides must match exactly."
            if selectedCatalog.CatalogBytes <> actual.Selection.CatalogBytes
               || selectedCatalog.ExpectedRawDigest <> actual.Selection.ExpectedRawDigest then
                diagnostic "source.catalogBinding" "$.selection" "Actual catalog bytes and raw digest differ from the selected source."
            if selectedArchive <> actual.TemplateArchive || selectedArchiveSha256 <> actual.ExpectedArchiveDigest then
                diagnostic "source.archiveBinding" "$.templateArchive" "Selected archive path and raw digest must match exactly."
        ]

