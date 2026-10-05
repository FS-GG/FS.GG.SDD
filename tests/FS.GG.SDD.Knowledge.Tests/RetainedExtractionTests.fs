namespace FS.GG.SDD.Knowledge.Tests

open System
open System.IO
open System.Text
open System.Text.Json
open System.Diagnostics
open System.Security.Cryptography
open Xunit
open FS.GG.SDD.Knowledge

[<Collection("ProcessGlobalEnv")>]
module RetainedExtractionTests =
    let private fixtures =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "RetainedExtraction")

    let private readRecord relative =
        JsonSerializer.Deserialize<Record>(File.ReadAllBytes(Path.Combine(fixtures, relative)))
        |> nonNull

    let private proposals () =
        Directory.GetFiles(Path.Combine(fixtures, "proposals"), "*.json")
        |> Array.sort
        |> Array.map (fun path -> JsonSerializer.Deserialize<Record>(File.ReadAllBytes path) |> nonNull)

    let private temporary test =
        let root =
            Path.Combine(Path.GetTempPath(), "retained-knowledge-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory root |> ignore

        try
            test root
        finally
            Directory.Delete(root, true)

    let private query text =
        {
            Text = text
            Kind = ""
            Scope = ""
            State = ""
            Id = ""
        }

    let private git root args =
        let start =
            ProcessStartInfo(
                "git",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            )

        start.ArgumentList.Add "-C"
        start.ArgumentList.Add root

        for arg: string in args do
            start.ArgumentList.Add arg

        use child =
            match Process.Start start with
            | null -> failwith "Git is required for retained-history qualification."
            | value -> value

        let output = child.StandardOutput.ReadToEndAsync()
        let error = child.StandardError.ReadToEndAsync()
        child.WaitForExit()

        if child.ExitCode <> 0 then
            failwith error.Result

        output.Result.Trim()

    let private commit root message =
        git root [ "add"; "." ] |> ignore

        git
            root
            [
                "-c"
                "user.name=Retained Fixture"
                "-c"
                "user.email=retained@example.invalid"
                "commit"
                "-m"
                message
            ]
        |> ignore

        git root [ "rev-parse"; "HEAD" ]

    [<Fact>]
    let ``reviewed public findings retain original documents negative results dates and provenance`` () =
        temporary (fun root ->
            let records = proposals ()
            let repository = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))

            let originals =
                records
                |> Array.collect _.Evidence
                |> Array.map _.Path
                |> Array.distinct
                |> Array.map (fun path ->
                    let full = Path.Combine(repository, path)
                    full, SHA256.HashData(File.ReadAllBytes full))

            let store = Path.Combine(root, ".fsgg", "knowledge")
            Assert.False(Directory.Exists store)

            // These are manually curated, owner-reviewable inputs, not an automatic extraction service.
            for proposal in records do
                let applied, size = Store.capture store None proposal
                Assert.Equal(proposal, applied.Record)
                Assert.Equal("reported", applied.Record.State)
                Assert.Equal("reported", applied.Record.Basis)
                Assert.InRange(size.Bytes, 1L, Store.byteLimit)

            Assert.Equal(6, (Store.all store).Length)
            let failed = Store.get store "q1-nonsaturating-damage"
            Assert.Equal("2026-08-26", failed.Record.AsOf)
            Assert.Contains("no Quint run", failed.Record.Limits)
            Assert.Equal(2, failed.Record.Evidence.Length)
            Assert.Single(Store.search store (query "non-saturating")) |> ignore

            let links = Store.related store "q1-liveness-defect" |> List.map _.Record.Id
            Assert.Contains("q1-liveness-cause", links)
            Assert.Contains("q1-liveness-fix", links)
            Assert.Contains("stale-refusal", (Store.get store "q1-liveness-cause").Record.Summary)

            for path, digest in originals do
                Assert.Equal<byte>(digest, SHA256.HashData(File.ReadAllBytes path))

            let canonical = Directory.GetFiles(store, "*", SearchOption.AllDirectories)
            Assert.Equal(records.Length + 1, canonical.Length)
            Assert.All(canonical, fun path -> Assert.Equal(".json", Path.GetExtension path)))

    [<Fact>]
    let ``repeated curated extraction is byte idempotent and never refreshes finding dates`` () =
        temporary (fun root ->
            let records = proposals ()

            for record in records do
                Store.capture root None record |> ignore

            let ids = records |> Array.map _.Id
            let before = Store.export root ids
            let size = Store.check root

            for record in proposals () do
                let prior = Store.get root record.Id
                let repeated, growth = Store.capture root None record
                Assert.Equal(prior, repeated)
                Assert.Equal(0L, growth.Growth)
                Assert.Equal(record.Created, repeated.Record.Created)
                Assert.Equal(record.Updated, repeated.Record.Updated)
                Assert.Equal(record.AsOf, repeated.Record.AsOf)

            Assert.Equal(size, Store.check root)
            Assert.Equal<byte>(before, Store.export root ids))

    [<Fact>]
    let ``owner edits and changed conclusions require explicit current revision reconciliation`` () =
        temporary (fun root ->
            let original = readRecord "proposals/q1-liveness-defect.json"
            let first, _ = Store.capture root None original
            let archived = Store.export root [| original.Id |]
            // These two fixtures stand in for a later owner edit and a distinct review proposal.
            let owner = readRecord "changes/owner-edited.json"
            let changed = readRecord "changes/changed-conclusion.json"
            let current, _ = Store.capture root (Some first.Revision) owner
            Assert.NotEqual<string>(first.Revision, current.Revision)

            Assert.Throws<InvalidDataException>(fun () -> Store.capture root None original |> ignore)
            |> ignore

            Assert.Throws<InvalidDataException>(fun () -> Store.capture root (Some first.Revision) changed |> ignore)
            |> ignore

            Assert.Throws<InvalidDataException>(fun () -> Store.restore root archived |> ignore)
            |> ignore

            Assert.Equal(current, Store.get root original.Id)
            Assert.Equal("2026-09-01", current.Record.Updated)

            let reconciled =
                { changed with
                    Summary =
                        changed.Summary
                        + " "
                        + "Project owner retains this result as a model-only lesson; production verification remains separate."
                    Author = owner.Author
                }

            let applied, _ = Store.capture root (Some current.Revision) reconciled
            Assert.Equal(reconciled, applied.Record)
            Assert.Equal(original.Created, applied.Record.Created)
            Assert.Equal(original.AsOf, applied.Record.AsOf)
            Assert.Equal("proposal", applied.Record.State)
            Assert.Equal("proposal", applied.Record.Basis)
            Assert.Equal<Evidence>(original.Evidence, applied.Record.Evidence))

    [<Fact>]
    let ``retained findings restore without cache and recover real Git versions from external bundle`` () =
        temporary (fun root ->
            let workspace = Path.Combine(root, "workspace")
            Directory.CreateDirectory workspace |> ignore
            git workspace [ "init" ] |> ignore
            File.WriteAllText(Path.Combine(workspace, ".gitignore"), ".fsgg/cache/\n")
            let store = Path.Combine(workspace, ".fsgg", "knowledge")

            for proposal in proposals () do
                Store.capture store None proposal |> ignore

            let firstCommit = commit workspace "Review and apply retained public findings"
            let first = Store.get store "q1-liveness-defect"
            let owner = readRecord "changes/owner-edited.json"
            Store.capture store (Some first.Revision) owner |> ignore
            let secondCommit = commit workspace "Preserve project owner qualification limit"
            let ids = proposals () |> Array.map _.Id
            let selected = Store.export store ids
            let restored = Path.Combine(root, "selected-restore")
            Store.restore restored selected |> ignore
            Assert.Equal<byte>(selected, Store.export restored ids)
            Assert.Single(Store.search restored (query "owner retains")) |> ignore
            Assert.False((JsonSerializer.Deserialize<Export>(selected) |> nonNull).HistoryIncluded)

            let cache = Path.Combine(workspace, ".fsgg", "cache")
            Directory.CreateDirectory cache |> ignore
            File.WriteAllText(Path.Combine(cache, "index"), "Synthetic disposable cache")
            let bundle = Path.Combine(root, "retained-history.bundle")
            git workspace [ "bundle"; "create"; bundle; "--all" ] |> ignore
            let recovered = Path.Combine(root, "recovered")
            git root [ "clone"; bundle; recovered ] |> ignore
            let recoveredStore = Path.Combine(recovered, ".fsgg", "knowledge")
            Assert.False(Directory.Exists(Path.Combine(recovered, ".fsgg", "cache")))
            Assert.Equal<byte>(selected, Store.export recoveredStore ids)
            let history = Store.history recoveredStore first.Record.Id
            Assert.Equal(2, history.Length)
            Assert.Equal<string>([| secondCommit; firstCommit |], history |> List.map _.GitCommit |> List.toArray)
            Assert.Equal(first.Record, (Store.getVersion recoveredStore first.Record.Id firstCommit).Record)
            Assert.Equal(owner, (Store.getVersion recoveredStore first.Record.Id secondCommit).Record)
            Assert.InRange((Store.check recoveredStore).Bytes, 1L, Store.byteLimit))
