namespace FS.GG.SDD.Artifacts.Tests

open System
open System.Security.Cryptography
open System.Text
open System.Text.Json
open FS.GG.SDD.Artifacts
open Xunit

/// Feature 092 (ADR-0026) — the compact, committed merge-boundary verdict.
///
/// The verdict drops *inventory* and no *facts*: `ship.json`'s `sources[]` digest list is
/// replaced in place by one aggregate `sourcesDigest` that binds the verdict to the exact
/// authored inputs.
module ShipVerdictTests =

    let private digestOf value =
        $"""{{ "algorithm": "sha256", "value": "{value}" }}"""

    let private source path value =
        $"""{{
      "path": "{path}",
      "kind": "verification",
      "digest": {digestOf value},
      "schemaVersion": 1,
      "schemaStatus": "current"
    }}"""

    let private aDigest = String.replicate 64 "a"
    let private bDigest = String.replicate 64 "b"

    let private shipJsonWith (sources: string) (blockingFindingIds: string) =
        $"""{{
  "schemaVersion": 1,
  "viewVersion": "1.0",
  "workId": "092-ship-verdict",
  "stage": "ship",
  "status": "shipReady",
  "generator": "fsgg-sdd/1.0.0",
  "sources": [{sources}],
  "lifecycleReadiness": {{ "status": "shipReady", "stages": [] }},
  "verificationReadiness": {{
    "status": "verificationReady",
    "blockingFindingIds": [],
    "evidenceSupportedCount": 0,
    "evidenceDeferredCount": 0,
    "evidenceMissingCount": 0,
    "evidenceStaleCount": 0,
    "evidenceSyntheticCount": 0,
    "evidenceInvalidCount": 0
  }},
  "evidenceDispositions": [],
  "generatedViews": [],
  "disposition": {{
    "state": "shipReady",
    "blockingFindingIds": [{blockingFindingIds}],
    "warningFindingIds": [],
    "advisoryFindingIds": [],
    "contributingStages": [],
    "correction": ""
  }},
  "findings": [],
  "governanceCompatibility": [],
  "diagnostics": [],
  "readiness": "shipReady",
  "nextAction": {{ "command": "", "reason": "" }}
}}"""

    let private twoSources =
        (source "readiness/092/verify.json" aDigest) + "," + (source "readiness/092/work-model.json" bDigest)

    let private parse text =
        match Ship.parseShipView { Path = "readiness/092/ship.json"; Text = text } with
        | Ok view -> view
        | Error diagnostics -> failwithf "expected a parseable ship.json, got %A" diagnostics

    let private verdictOf text = ShipVerdict.fromShipView (parse text)
    let private jsonOf text = ShipVerdict.toJson (verdictOf text)

    /// Recompute the aggregate independently of the implementation (SC-003): the canonical
    /// pre-image is `<path>|<algorithm>:<value>` per source, path-sorted, joined with `\n`.
    let private recomputeAggregate (pairs: (string * string) list) =
        let preImage =
            pairs
            |> List.sortBy fst
            |> List.map (fun (path, value) -> $"{path}|sha256:{value}")
            |> String.concat "\n"

        SHA256.HashData(Encoding.UTF8.GetBytes preImage)
        |> Array.map (fun b -> b.ToString("x2"))
        |> String.concat ""

    // ---------- FR-002: every fact, and no other field ----------

    [<Fact>]
    let ``the verdict carries exactly the eleven projected facts`` () =
        use doc = JsonDocument.Parse(jsonOf (shipJsonWith twoSources ""))

        let actual =
            doc.RootElement.EnumerateObject() |> Seq.map (fun p -> p.Name) |> Set.ofSeq

        let expected =
            Set.ofList
                [ "schemaVersion"
                  "viewVersion"
                  "workId"
                  "stage"
                  "status"
                  "generator"
                  "sourcesDigest"
                  "verificationReadiness"
                  "disposition"
                  "readiness" ]

        // Ten top-level keys; `disposition` carries the eleventh and twelfth facts as its two
        // members, mirroring ship.json's nesting.
        Assert.Equal<Set<string>>(expected, actual)

        let disposition = doc.RootElement.GetProperty "disposition"

        Assert.Equal<Set<string>>(
            Set.ofList [ "state"; "blockingFindingIds" ],
            disposition.EnumerateObject() |> Seq.map (fun p -> p.Name) |> Set.ofSeq
        )

        Assert.Equal<Set<string>>(
            Set.ofList [ "status" ],
            (doc.RootElement.GetProperty "verificationReadiness").EnumerateObject()
            |> Seq.map (fun p -> p.Name)
            |> Set.ofSeq
        )

    [<Fact>]
    let ``the projection copies the view's facts verbatim`` () =
        let text = shipJsonWith twoSources "\"SF002\", \"SF001\""
        let view = parse text
        let verdict = ShipVerdict.fromShipView view

        Assert.Equal(view.SchemaVersion, verdict.SchemaVersion)
        Assert.Equal(view.ViewVersion, verdict.ViewVersion)
        Assert.Equal(Identifiers.workIdValue view.WorkId, verdict.WorkId)
        Assert.Equal(Identifiers.stageValue view.Stage, verdict.Stage)
        Assert.Equal(view.Status, verdict.Status)
        Assert.Equal(view.Generator, verdict.Generator)
        Assert.Equal(view.VerificationReadiness.Status, verdict.VerificationReadinessStatus)
        Assert.Equal(view.Disposition, verdict.DispositionState)
        Assert.Equal(view.Readiness, verdict.Readiness)
        Assert.Equal<string list>([ "SF001"; "SF002" ], verdict.DispositionBlockingFindingIds) // sorted

    // ---------- FR-003: the aggregate binds path -> digest ----------

    [<Fact>]
    let ``sourcesDigest equals the independently recomputed aggregate`` () =
        let verdict = verdictOf (shipJsonWith twoSources "")

        let expected =
            recomputeAggregate
                [ "readiness/092/verify.json", aDigest
                  "readiness/092/work-model.json", bDigest ]

        Assert.Equal("sha256", verdict.SourcesDigest.Algorithm)
        Assert.Equal(expected, verdict.SourcesDigest.Value)

    [<Fact>]
    let ``changing a source digest changes the aggregate`` () =
        let before = (verdictOf (shipJsonWith twoSources "")).SourcesDigest.Value

        let mutated =
            (source "readiness/092/verify.json" aDigest)
            + ","
            + (source "readiness/092/work-model.json" (String.replicate 64 "c"))

        let after = (verdictOf (shipJsonWith mutated "")).SourcesDigest.Value
        Assert.NotEqual<string>(before, after)

    [<Fact>]
    let ``changing a source path changes the aggregate`` () =
        // The pairing is the point: hashing the digest *values* alone would miss this.
        let before = (verdictOf (shipJsonWith twoSources "")).SourcesDigest.Value

        let renamed =
            (source "readiness/092/verify.json" aDigest)
            + ","
            + (source "readiness/092/renamed.json" bDigest)

        let after = (verdictOf (shipJsonWith renamed "")).SourcesDigest.Value
        Assert.NotEqual<string>(before, after)

    [<Fact>]
    let ``swapping two sources' digests changes the aggregate`` () =
        // Two sources exchanging content must not be indistinguishable from no change.
        let before = (verdictOf (shipJsonWith twoSources "")).SourcesDigest.Value

        let swapped =
            (source "readiness/092/verify.json" bDigest)
            + ","
            + (source "readiness/092/work-model.json" aDigest)

        let after = (verdictOf (shipJsonWith swapped "")).SourcesDigest.Value
        Assert.NotEqual<string>(before, after)

    [<Fact>]
    let ``source order does not affect the aggregate - it is a function of the set`` () =
        let reversed =
            (source "readiness/092/work-model.json" bDigest)
            + ","
            + (source "readiness/092/verify.json" aDigest)

        Assert.Equal(
            (verdictOf (shipJsonWith twoSources "")).SourcesDigest.Value,
            (verdictOf (shipJsonWith reversed "")).SourcesDigest.Value
        )

    [<Fact>]
    let ``an empty sources array yields the empty-string sha256, not an omitted field`` () =
        let verdict = verdictOf (shipJsonWith "" "")

        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", verdict.SourcesDigest.Value)

    // ---------- FR-004 / FR-008: compact and byte-stable ----------

    [<Fact>]
    let ``a ship-ready verdict renders in exactly 20 lines`` () =
        let json = jsonOf (shipJsonWith twoSources "")
        Assert.Equal(20, json.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').Length)

    [<Fact>]
    let ``a non-empty blocking list expands the array - 21 lines plus one per id`` () =
        // `Utf8JsonWriter(Indented = true)` renders `[]` inline but expands a non-empty array over
        // its own bracket lines, so the growth is 2 + n, not n. Pinned exactly, because "<= 20 lines"
        // is a contract claim and this is the shape that exceeds it.
        let lines (ids: string) = (jsonOf (shipJsonWith twoSources ids)).Split('\n').Length

        Assert.Equal(20, lines "")
        Assert.Equal(22, lines "\"SF001\"")
        Assert.Equal(23, lines "\"SF001\", \"SF002\"")
        Assert.Equal(24, lines "\"SF001\", \"SF002\", \"SF003\"")

    [<Fact>]
    let ``serialization is byte-stable across repeated projections`` () =
        let text = shipJsonWith twoSources "\"SF001\""
        Assert.Equal(jsonOf text, jsonOf text)

    [<Fact>]
    let ``the verdict carries no clock, absolute path, or ANSI`` () =
        let json = jsonOf (shipJsonWith twoSources "")

        // NOTE: `Assert.DoesNotContain(string, string)` compares CULTURE-sensitively, and ESC is a
        // zero-weight character — searching for "ESC[" matches a bare `[`, so the array `[]` trips it.
        // Test the char, whose overload compares ordinally.
        Assert.False(json.Contains '\u001b', "the verdict must carry no ANSI")

        Assert.DoesNotContain("/home/", json)

        // A bare year check would false-positive on the sha256 hex; match an ISO-8601 stamp instead.
        Assert.False(
            System.Text.RegularExpressions.Regex.IsMatch(json, @"\d{4}-\d{2}-\d{2}T\d{2}:"),
            "the verdict must carry no timestamp"
        )

    [<Fact>]
    let ``the blocking finding ids survive the projection into json`` () =
        use doc = JsonDocument.Parse(jsonOf (shipJsonWith twoSources "\"SF002\", \"SF001\""))

        let ids =
            (doc.RootElement.GetProperty "disposition").GetProperty("blockingFindingIds").EnumerateArray()
            |> Seq.map (fun e -> e.GetString() |> Option.ofObj |> Option.defaultValue "")
            |> List.ofSeq

        Assert.Equal<string list>([ "SF001"; "SF002" ], ids)
