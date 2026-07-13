namespace FS.GG.SDD.Artifacts

open System
open System.Globalization
open System.Xml.Linq
open FS.GG.SDD.Artifacts.SchemaVersion

[<RequireQualifiedAccess>]
module TestReport =

    /// Attributes are matched by LOCAL name throughout. TRX carries a default namespace
    /// (`http://microsoft.com/schemas/VisualStudio/TeamTest/2010`) and JUnit emitters vary on whether
    /// they declare one at all, so matching on the qualified name would make the parser pass or fail
    /// on a detail of the producer rather than on the content.
    let private localName (element: XElement) = element.Name.LocalName

    let private attribute (name: string) (element: XElement) =
        element.Attributes()
        |> Seq.tryFind (fun a -> String.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
        |> Option.map _.Value

    /// A missing count is `0`, not an error: JUnit emitters routinely omit `skipped`/`errors` when
    /// there are none. A *malformed* one is also `0` — the report is still parseable, and a count that
    /// cannot be believed is better reported as nothing than as a guess.
    let private count (name: string) (element: XElement) =
        match attribute name element with
        | Some value ->
            match Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, parsed when parsed >= 0 -> parsed
            | _ -> 0
        | None -> 0

    let private descendantsNamed (name: string) (root: XElement) =
        root.Descendants()
        |> Seq.filter (fun e -> String.Equals(localName e, name, StringComparison.OrdinalIgnoreCase))

    /// TRX: `<TestRun><ResultSummary><Counters passed="…" failed="…" error="…" notExecuted="…"/>`.
    /// `failed` folds in `error` — a test that errored did not pass, and a receipt that counted it as
    /// neither would report a green run with a missing test.
    let private parseTrx (root: XElement) =
        match descendantsNamed "Counters" root |> Seq.tryHead with
        | None -> Error "TRX report has no <Counters> element."
        | Some counters ->
            let passed = count "passed" counters
            let failed = count "failed" counters + count "error" counters
            let skipped = count "notExecuted" counters
            Ok(passed, failed, skipped)

    /// JUnit: `tests`/`failures`/`errors`/`skipped`.
    ///
    /// When the root is `<testsuites>`, the counts are summed over its **direct** `<testsuite>`
    /// children rather than read off the root's own aggregate attributes: those aggregates are
    /// optional, and several emitters omit them entirely — reading them would silently yield a
    /// `0/0/0` receipt for a real run. Direct children only, so a nested suite is not counted twice.
    /// A `<testsuites>` with no children falls back to its own attributes, which is the only thing
    /// left to believe.
    let private parseJUnit (root: XElement) =
        let suites =
            if String.Equals(localName root, "testsuites", StringComparison.OrdinalIgnoreCase) then
                root.Elements()
                |> Seq.filter (fun e -> String.Equals(localName e, "testsuite", StringComparison.OrdinalIgnoreCase))
                |> List.ofSeq
            else
                [ root ]

        let suites = if List.isEmpty suites then [ root ] else suites

        let sum name = suites |> List.sumBy (count name)

        let total = sum "tests"
        let failures = sum "failures"
        let errors = sum "errors"
        let skipped = sum "skipped"
        let failed = failures + errors

        // `passed` is not a JUnit attribute — it is the remainder. Clamped at zero so a report whose
        // parts exceed its total (a producer bug) yields a coherent receipt rather than a negative
        // count that `observedRunInconsistency` would then have to reject.
        let passed = max 0 (total - failed - skipped)

        Ok(passed, failed, skipped)

    let parse (source: string) (text: string) : Result<ObservedRun, string> =
        if String.IsNullOrWhiteSpace text then
            Error "The report is empty."
        else

            // XDocument.Parse prohibits DTD processing by default, so a report cannot pull in an external
            // entity. Total: any malformed XML surfaces as `Error`, never as an exception out of a pure
            // Artifacts function.
            let parsed =
                try
                    Ok(XDocument.Parse(text, LoadOptions.None))
                with
                | :? System.Xml.XmlException as ex -> Error $"The report is not well-formed XML: {ex.Message}"
                | ex -> Error $"The report could not be read: {ex.Message}"

            parsed
            |> Result.bind (fun document ->
                match document.Root with
                | null -> Error "The report has no root element."
                | root ->
                    let counts =
                        match localName root with
                        | name when String.Equals(name, "TestRun", StringComparison.OrdinalIgnoreCase) -> parseTrx root
                        | name when
                            String.Equals(name, "testsuites", StringComparison.OrdinalIgnoreCase)
                            || String.Equals(name, "testsuite", StringComparison.OrdinalIgnoreCase)
                            ->
                            parseJUnit root
                        | name ->
                            Error
                                $"Unrecognised report root <{name}>: expected a TRX (<TestRun>) or JUnit (<testsuites>/<testsuite>) report."

                    counts
                    |> Result.bind (fun (passed, failed, skipped) ->
                        // A run in which NOTHING EXECUTED is not evidence of anything, and must never
                        // become a passing receipt. Without this, `failed = 0` derives
                        // `outcome: passed` and `isObserved` returns `true` for:
                        //
                        //   * a TRX whose every test was filtered out (a `--filter` typo),
                        //   * a JUnit `<testsuites>` whose children carry no count attributes,
                        //   * a suite whose tests were all skipped.
                        //
                        // Each would discharge an obligation on a run that proved nothing — the exact
                        // fail-open this feature exists to close, rebuilt one level down. `skipped` is
                        // deliberately NOT counted as execution: a skipped test is one nobody ran.
                        if passed + failed = 0 then
                            Error
                                $"The report records no executed tests (passed: {passed}, failed: {failed}, skipped: {skipped}). A run in which nothing executed proves nothing."
                        else

                            let digest = sha256Text text

                            Ok
                                { Source = source
                                  Digest = $"sha256:{digest.Value}"
                                  // Derived, never copied from the report's own summary attribute (FR-005):
                                  // TRX says `outcome="Completed"` for a run with failures, and JUnit has no
                                  // outcome at all. The counts are the only thing both formats agree on.
                                  Outcome = (if failed = 0 then "passed" else "failed")
                                  Passed = passed
                                  Failed = failed
                                  Skipped = skipped }))
