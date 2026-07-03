namespace FS.GG.SDD.Acceptance.Tests

open System.IO
open System.Text
open System.Text.Json

/// The `composition-acceptance-result` v1 document (contracts/composition-acceptance-result.md):
/// the single deterministic, diffable per-run record of the composition acceptance. The body is
/// deterministic (stable key order, no timestamps/randomness); the `sensed` block carries the
/// only legitimately-variable metadata and is null-normalized before any byte comparison
/// (mirroring `ValidationContracts` INV-5).
module CompositionResult =

    // T006: the generator identity is a hard-coded constant — never derived from build/date/
    // random — so the body is deterministic (FR-011 / SC-005, finding F8).
    let generatorId = "fsgg-sdd-composition-acceptance"
    let generatorVersion = "1.0.0"

    /// Why a run failed. `FactFailed` carries the first failing asserted fact; the others carry
    /// the surfaced scaffold diagnostic that drove the verdict.
    type FailReason =
        | FactFailed of fact: string * diagnostic: string
        | Incomplete of diagnostic: string
        | ProviderDefect of diagnostic: string
        | ConfigError of diagnostic: string

    /// The single headline result (data-model.md). `SkipUnavailable` is never PASS or FAIL.
    type Verdict =
        | Pass
        | Fail of FailReason
        | SkipUnavailable

    /// The nine asserted facts; every one must hold for `Pass`.
    type Facts =
        { SkeletonPresent: bool
          ConstitutionPresent: bool
          AppBuilds: bool
          AppRuns: bool
          GitInitialized: bool
          ScriptsExecutable: bool
          ProvenancePartitioned: bool
          RefreshExcludes: bool
          ReportedComplete: bool }

    /// Legitimately-variable metadata, null-normalized for golden/diff comparison.
    type Sensed =
        { ResolvedTemplateVersion: string option
          ProviderAvailable: bool option
          Host: string option
          Timestamp: string option }

    type CompositionResultRecord =
        { SchemaVersion: int
          Verdict: Verdict
          ScaffoldOutcome: string
          ScaffoldDiagnostic: string option
          Facts: Facts
          Failure: (string * string) option
          Sensed: Sensed }

    /// All-false facts — the starting point before a successful scaffold lets facts be asserted.
    let noFacts =
        { SkeletonPresent = false
          ConstitutionPresent = false
          AppBuilds = false
          AppRuns = false
          GitInitialized = false
          ScriptsExecutable = false
          ProvenancePartitioned = false
          RefreshExcludes = false
          ReportedComplete = false }

    /// The canonical (name, value) ordering of the asserted facts — the serialization order and
    /// the order the "first failing fact" is resolved in.
    let orderedFacts (facts: Facts) =
        [ "skeletonPresent", facts.SkeletonPresent
          "constitutionPresent", facts.ConstitutionPresent
          "appBuilds", facts.AppBuilds
          "appRuns", facts.AppRuns
          "gitInitialized", facts.GitInitialized
          "scriptsExecutable", facts.ScriptsExecutable
          "provenancePartitioned", facts.ProvenancePartitioned
          "refreshExcludes", facts.RefreshExcludes
          "reportedComplete", facts.ReportedComplete ]

    let private firstFailingFact (facts: Facts) =
        orderedFacts facts |> List.tryFind (snd >> not) |> Option.map fst

    // T008: verdict resolution keyed on the `(outcome, diagnostic code)` pair — NOT the outcome
    // alone, because `providerFailed` covers both unavailable (SKIP) and defect (FAIL), so
    // keying on the outcome alone would collapse SKIP into FAIL and break SC-004 (finding F1).
    // `factDiagnostic` is the surfaced build/run/scaffold message attached to a fact failure.
    let resolveVerdict (outcome: string) (diagnostic: string option) (factDiagnostic: string) (facts: Facts) : Verdict =
        match outcome with
        | "providerSucceeded" ->
            match firstFailingFact facts with
            | None -> Pass
            | Some fact -> Fail(FactFailed(fact, factDiagnostic))
        | "providerSucceededEmpty" -> Fail(Incomplete(defaultArg diagnostic "scaffold.providerEmpty"))
        | "providerFailed" ->
            match diagnostic with
            | Some "scaffold.providerUnavailable" -> SkipUnavailable
            | Some code -> Fail(ProviderDefect code)
            | None -> Fail(ProviderDefect "scaffold.providerFailed")
        | "providerNotRun" -> Fail(ConfigError(defaultArg diagnostic "scaffold.providerNotRun"))
        | other -> Fail(ConfigError other)

    let verdictValue verdict =
        match verdict with
        | Pass -> "pass"
        | SkipUnavailable -> "skip-unavailable"
        | Fail _ -> "fail"

    /// The `failure` field: `null` on pass/skip; on fail, the (fact, diagnostic) pair. Non-fact
    /// failures attribute to `reportedComplete` (incomplete) or `scaffoldOutcome` (defect/config).
    let failureOf verdict =
        match verdict with
        | Pass
        | SkipUnavailable -> None
        | Fail(FactFailed(fact, diagnostic)) -> Some(fact, diagnostic)
        | Fail(Incomplete diagnostic) -> Some("reportedComplete", diagnostic)
        | Fail(ProviderDefect diagnostic) -> Some("scaffoldOutcome", diagnostic)
        | Fail(ConfigError diagnostic) -> Some("scaffoldOutcome", diagnostic)

    /// Assemble a result record from the run facts. The verdict is resolved here so the record,
    /// its verdict, and its failure block are always mutually consistent.
    let makeRecord
        (outcome: string)
        (diagnostic: string option)
        (factDiagnostic: string)
        (facts: Facts)
        (sensed: Sensed)
        =
        let verdict = resolveVerdict outcome diagnostic factDiagnostic facts

        { SchemaVersion = 1
          Verdict = verdict
          ScaffoldOutcome = outcome
          ScaffoldDiagnostic = diagnostic
          Facts = facts
          Failure = failureOf verdict
          Sensed = sensed }

    // T007: null-normalize the sensed block before any byte comparison (golden / two-run diff),
    // mirroring the `ValidationContracts.fs` INV-5 pattern (research D8, FR-011 / SC-005).
    let nullSensed =
        { ResolvedTemplateVersion = None
          ProviderAvailable = None
          Host = None
          Timestamp = None }

    let normalizeSensed (record: CompositionResultRecord) = { record with Sensed = nullSensed }

    // T006: the deterministic serializer — the same `Utf8JsonWriter` style the repo uses for
    // `scaffold-provenance.json` (stable key order, no timestamps/randomness in the body, UTF-8).
    let private writeNullableString (writer: Utf8JsonWriter) (name: string) (value: string option) =
        match value with
        | Some text -> writer.WriteString(name, text)
        | None -> writer.WriteNull name

    let serialize (record: CompositionResultRecord) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", record.SchemaVersion)

        writer.WriteStartObject("generator")
        writer.WriteString("id", generatorId)
        writer.WriteString("version", generatorVersion)
        writer.WriteEndObject()

        writer.WriteString("verdict", verdictValue record.Verdict)

        writer.WriteStartObject("inputs")
        writer.WriteString("provider", "rendering")
        writer.WriteStartObject("params")
        writer.WriteString("lifecycle", "sdd")
        writer.WriteEndObject()
        writer.WriteEndObject()

        writer.WriteString("scaffoldOutcome", record.ScaffoldOutcome)
        writeNullableString writer "scaffoldDiagnostic" record.ScaffoldDiagnostic

        writer.WriteStartObject("facts")

        orderedFacts record.Facts
        |> List.iter (fun (name, value) -> writer.WriteBoolean(name, value))

        writer.WriteEndObject()

        match record.Failure with
        | None -> writer.WriteNull "failure"
        | Some(fact, diagnostic) ->
            writer.WriteStartObject("failure")
            writer.WriteString("fact", fact)
            writer.WriteString("diagnostic", diagnostic)
            writer.WriteEndObject()

        writer.WriteStartObject("sensed")
        writeNullableString writer "resolvedTemplateVersion" record.Sensed.ResolvedTemplateVersion

        match record.Sensed.ProviderAvailable with
        | Some available -> writer.WriteBoolean("providerAvailable", available)
        | None -> writer.WriteNull "providerAvailable"

        writeNullableString writer "host" record.Sensed.Host
        writeNullableString writer "timestamp" record.Sensed.Timestamp
        writer.WriteEndObject()

        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    /// Write the result document to `path`, creating parent directories as needed.
    let write (path: string) (record: CompositionResultRecord) =
        match Path.GetDirectoryName path with
        | null -> ()
        | directory -> Directory.CreateDirectory directory |> ignore

        File.WriteAllText(path, serialize record)
