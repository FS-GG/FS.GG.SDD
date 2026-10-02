namespace FS.GG.SDD.Artifacts.Tests

open System
open System.IO
open System.Text
open FS.GG.SDD.Artifacts
open Xunit

module KnowledgeTests =
    let private fixture () =
        let root = Path.Combine(TestSupport.repoRoot, "tests/fixtures/project-knowledge/.fsgg/knowledge")
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        |> Array.map (fun p -> ({ Path = Path.GetRelativePath(root, p).Replace('\\', '/'); Bytes = File.ReadAllBytes p }: Knowledge.Snapshot))
        |> Array.toList

    let private valid snapshots =
        match Knowledge.load snapshots with
        | Ok store -> store
        | Error errors -> failwithf "%A" errors

    let private has code snapshots =
        match Knowledge.load snapshots with
        | Ok _ -> failwith "Expected refusal"
        | Error errors -> Assert.Contains(errors, fun (d: Knowledge.Diagnostic) -> d.Code = code)

    [<Fact>]
    let ``Disclosed four findings share identity status provenance and Unicode filters`` () =
        let store = valid (fixture ())
        Assert.Equal(4, store.Records.Length)
        let query: Knowledge.Query = { Text = "CAFÉ"; Kind = Some "operating-lesson"; Status = Some "observed"; Scope = Some "日本語" }
        let record = Knowledge.search query store |> List.exactlyOne
        Assert.Equal("lesson", record.Id)
        Assert.Equal("synthetic-run", record.Evidence.Head.Run)
        Assert.Equal(Some record, Knowledge.get "lesson" store)
        Assert.Equal("superseded", (Knowledge.get "experiment" store).Value.Status)
        match Knowledge.related "architecture" store with
        | Ok records -> Assert.Equal<string list>([ "bug" ], records |> List.map _.Id)
        | Error errors -> failwithf "%A" errors

    [<Fact>]
    let ``Inclusive aggregate budget counts manifest schema guidance and exact UTF8 bytes`` () =
        let snapshots = fixture ()
        let store = valid snapshots
        let padding = int (Knowledge.maxBytes - store.TotalBytes)
        let grow extra = snapshots |> List.map (fun s -> if s.Path = "README.md" then { s with Bytes = Array.append s.Bytes (Array.create (padding + extra) 32uy) } else s)
        let atLimit = valid (grow 0)
        Assert.Equal(Knowledge.maxBytes, atLimit.TotalBytes)
        Assert.Equal(0L, atLimit.RemainingBytes)
        has "knowledge.budget" (grow 1)

    [<Fact>]
    let ``Malformed paths duplicates schema binary and broken links refuse without partial results`` () =
        let snapshots = fixture ()
        has "knowledge.path" ({ Path = "../escape.json"; Bytes = [||] } :: snapshots)
        has "knowledge.duplicatePath" (snapshots.Head :: snapshots)
        let record = snapshots |> List.find (fun s -> s.Path = "records/bug.json")
        has "knowledge.duplicateId" (record :: snapshots)
        has "knowledge.utf8" ({ Path = "README.md"; Bytes = [| 255uy |] } :: (snapshots |> List.filter (fun s -> s.Path <> "README.md")))
        has "knowledge.file" ({ Path = "cache.db"; Bytes = [||] } :: snapshots)
        let change (before: string) (after: string) = snapshots |> List.map (fun s -> { s with Bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(s.Bytes).Replace(before, after)) })
        has "knowledge.schema" (change "\"schemaVersion\": 1" "\"schemaVersion\": 2")
        has "knowledge.link" (change "\"related\": []" "\"related\": [\"missing\"]")
        has "knowledge.path" (change "docs/example.md" "../secret")

    [<Fact>]
    let ``Duplicate properties and forbidden privacy promises refuse records`` () =
        let snapshot = fixture () |> List.find (fun s -> s.Path = "records/bug.json")
        let text = Encoding.UTF8.GetString snapshot.Bytes
        for bad in [ text.Replace("{", "{\"id\":\"duplicate\",", StringComparison.Ordinal); text.Replace("\"schemaVersion\": 1", "\"private\": true, \"schemaVersion\": 1") ] do
            match Knowledge.parse snapshot.Path (Encoding.UTF8.GetBytes bad) with
            | Ok _ -> failwith "Expected refusal"
            | Error errors -> Assert.NotEmpty errors
