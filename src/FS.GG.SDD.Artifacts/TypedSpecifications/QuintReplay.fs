namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.Globalization
open System.Security.Cryptography
open System.Text

type QuintReplaySourceBinding =
    { Path: string; Line: int; Column: int }

type QuintReplayValue =
    | Null
    | Boolean of bool
    | Integer of string
    | Text of string
    | Sequence of QuintReplayValue list
    | Set of QuintReplayValue list
    | Record of (string * QuintReplayValue) list

type QuintReplayState =
    { Identity: string
      Bindings: (string * QuintReplayValue) list }

type QuintReplayStep =
    { Index: int
      Action: string
      Source: QuintReplaySourceBinding
      Expected: QuintReplayState }

type QuintReplayEnvironment =
    { Seed: string
      Bounds: (string * int64) list
      ToolFingerprint: string
      ProfileFingerprint: string
      ContractFingerprint: string
      AdapterFingerprint: string
      ImplementationFingerprint: string }

type QuintReplayTrace =
    { SchemaVersion: int
      TraceIdentity: string
      Environment: QuintReplayEnvironment
      Initial: QuintReplayState
      Steps: QuintReplayStep list }

type QuintReplayObservation =
    { Index: int
      Action: string
      Source: QuintReplaySourceBinding
      Actual: QuintReplayState }

type QuintReplayDiagnostic =
    { Code: string
      Path: string
      Message: string }

type QuintReplayDivergence =
    { Step: int
      Action: string
      Source: QuintReplaySourceBinding
      Expected: QuintReplayState option
      Actual: QuintReplayState option
      Reason: string }

[<RequireQualifiedAccess>]
type QuintReplayResult =
    | Equivalent
    | Diverged of QuintReplayDivergence

module private ReplayInternal =
    let diagnostic code path message : QuintReplayDiagnostic =
        { Code = code
          Path = path
          Message = message }

    let sortDiagnostics diagnostics =
        diagnostics
        |> List.distinct
        |> List.sortBy (fun item -> item.Path, item.Code, item.Message)

    let isLowerSha256 (value: string) =
        not (String.IsNullOrWhiteSpace value)
        && value.Length = 64
        && value
           |> Seq.forall (fun character ->
               (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))

    let escapeJson (value: string) =
        let builder = StringBuilder(value.Length + 2)
        builder.Append('"') |> ignore

        for character in value do
            match character with
            | '"' -> builder.Append("\\\"") |> ignore
            | '\\' -> builder.Append("\\\\") |> ignore
            | '\b' -> builder.Append("\\b") |> ignore
            | '\f' -> builder.Append("\\f") |> ignore
            | '\n' -> builder.Append("\\n") |> ignore
            | '\r' -> builder.Append("\\r") |> ignore
            | '\t' -> builder.Append("\\t") |> ignore
            | value when int value < 0x20 ->
                builder.Append("\\u").Append((int value).ToString("x4", CultureInfo.InvariantCulture))
                |> ignore
            | value -> builder.Append(value) |> ignore

        builder.Append('"').ToString()

    let canonicalInteger (value: string) =
        let mutable parsed = 0I

        if String.IsNullOrWhiteSpace value then
            None
        elif
            System.Numerics.BigInteger.TryParse(
                value,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                &parsed
            )
        then
            Some(parsed.ToString(CultureInfo.InvariantCulture))
        else
            None

    let rec encodeAt path value =
        match value with
        | Null -> Ok "null"
        | Boolean true -> Ok "true"
        | Boolean false -> Ok "false"
        | Integer value ->
            match canonicalInteger value with
            | Some canonical -> Ok canonical
            | None -> Error [ diagnostic "QRP-VALUE-INTEGER" path "Integer values must use base-10 integer syntax." ]
        | Text value when Object.ReferenceEquals(value, null) ->
            Error [ diagnostic "QRP-VALUE-TEXT" path "Text values cannot be null." ]
        | Text value -> Ok(escapeJson value)
        | Sequence values ->
            values
            |> List.mapi (fun index item -> encodeAt $"%s{path}[%d{index}]" item)
            |> collect $"%s{path}"
            |> Result.map (String.concat "," >> fun body -> $"[%s{body}]")
        | Set values ->
            values
            |> List.mapi (fun index item -> encodeAt $"%s{path}[%d{index}]" item)
            |> collect path
            |> Result.bind (fun encoded ->
                let canonical =
                    encoded
                    |> List.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))

                if List.distinct canonical |> List.length <> canonical.Length then
                    Error
                        [ diagnostic
                              "QRP-VALUE-SET-DUPLICATE"
                              path
                              "Set values must be unique after canonical encoding." ]
                else
                    let body = String.concat "," canonical
                    Ok("{\"#set\":[" + body + "]}"))
        | Record fields ->
            let names = fields |> List.map fst

            let duplicates =
                names
                |> List.countBy id
                |> List.choose (fun (name, count) -> if count > 1 then Some name else None)
                |> List.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))

            let keyDiagnostics =
                fields
                |> List.mapi (fun index (name, _) ->
                    if String.IsNullOrWhiteSpace name then
                        [ diagnostic "QRP-VALUE-RECORD-KEY" $"%s{path}[%d{index}]" "Record keys cannot be blank." ]
                    else
                        [])
                |> List.concat

            if not duplicates.IsEmpty || not keyDiagnostics.IsEmpty then
                [ for duplicate in duplicates do
                      diagnostic "QRP-VALUE-RECORD-DUPLICATE" path $"Record key '%s{duplicate}' is duplicated."
                  yield! keyDiagnostics ]
                |> sortDiagnostics
                |> Error
            else
                fields
                |> List.sortWith (fun (left, _) (right, _) -> StringComparer.Ordinal.Compare(left, right))
                |> List.map (fun (name, item) ->
                    encodeAt $"%s{path}.%s{name}" item
                    |> Result.map (fun encoded -> $"%s{escapeJson name}:%s{encoded}"))
                |> collect path
                |> Result.map (String.concat "," >> fun body -> "{" + body + "}")

    and collect path results =
        let values =
            results
            |> List.choose (function
                | Ok value -> Some value
                | Error _ -> None)

        let diagnostics =
            results
            |> List.collect (function
                | Ok _ -> []
                | Error findings -> findings)

        if diagnostics.IsEmpty then
            Ok values
        else
            Error(sortDiagnostics diagnostics)

    let validateSource path (source: QuintReplaySourceBinding) =
        [ if String.IsNullOrWhiteSpace source.Path then
              diagnostic "QRP-SOURCE-PATH" $"%s{path}.path" "Source path is required."
          if source.Line < 1 then
              diagnostic "QRP-SOURCE-LINE" $"%s{path}.line" "Source line must be positive."
          if source.Column < 1 then
              diagnostic "QRP-SOURCE-COLUMN" $"%s{path}.column" "Source column must be positive." ]

    let validateStateContent path (state: QuintReplayState) =
        let bindingNames = state.Bindings |> List.map fst

        let structural =
            [ for index, (name, _) in state.Bindings |> List.indexed do
                  if String.IsNullOrWhiteSpace name then
                      diagnostic
                          "QRP-STATE-BINDING"
                          $"%s{path}.bindings[%d{index}]"
                          "State binding names cannot be blank."

              for name, count in bindingNames |> List.countBy id |> List.sortBy fst do
                  if count > 1 then
                      diagnostic
                          "QRP-STATE-BINDING-DUPLICATE"
                          $"%s{path}.bindings"
                          $"State binding '%s{name}' is duplicated." ]

        let valueDiagnostics =
            state.Bindings
            |> List.mapi (fun index (_, value) ->
                match encodeAt $"%s{path}.bindings[%d{index}]" value with
                | Ok _ -> []
                | Error findings -> findings)
            |> List.concat

        sortDiagnostics (structural @ valueDiagnostics)

    let encodeStateUnchecked (state: QuintReplayState) =
        state.Bindings
        |> List.sortWith (fun (left, _) (right, _) -> StringComparer.Ordinal.Compare(left, right))
        |> List.map (fun (name, value) ->
            match encodeAt "$.bindings" value with
            | Ok encoded -> $"%s{escapeJson name}:%s{encoded}"
            | Error _ -> invalidOp "State was encoded before validation completed.")
        |> String.concat ","
        |> fun bindings -> "{" + bindings + "}"

    let stateFingerprint (state: QuintReplayState) =
        let bytes = Encoding.UTF8.GetBytes(encodeStateUnchecked state)

        SHA256.HashData bytes
        |> Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()

    let validateState path (state: QuintReplayState) =
        let contentDiagnostics = validateStateContent path state

        [ yield! contentDiagnostics

          if not (isLowerSha256 state.Identity) then
              diagnostic "QRP-STATE-IDENTITY" $"%s{path}.identity" "State identity must be a lowercase SHA-256 digest."
          elif contentDiagnostics.IsEmpty then
              let expected = stateFingerprint state

              if not (String.Equals(state.Identity, expected, StringComparison.Ordinal)) then
                  diagnostic
                      "QRP-STATE-FINGERPRINT"
                      $"%s{path}.identity"
                      $"State identity does not match canonical state fingerprint '%s{expected}'." ]
        |> sortDiagnostics

    let validateFingerprint path value =
        if isLowerSha256 value then
            []
        else
            [ diagnostic "QRP-ENV-FINGERPRINT" path "Environment fingerprints must be lowercase SHA-256 digests." ]

[<RequireQualifiedAccess>]
module QuintReplay =
    let encodeValue value = ReplayInternal.encodeAt "$" value

    let encodeState state =
        match ReplayInternal.validateState "$" state with
        | [] -> Ok(ReplayInternal.encodeStateUnchecked state)
        | diagnostics -> Error diagnostics

    let stateFingerprint state =
        match ReplayInternal.validateStateContent "$" state with
        | [] -> Ok(ReplayInternal.stateFingerprint state)
        | diagnostics -> Error diagnostics

    let validateTrace trace =
        let environment = trace.Environment

        let boundsDiagnostics =
            [ for index, (name, value) in environment.Bounds |> List.indexed do
                  if String.IsNullOrWhiteSpace name then
                      ReplayInternal.diagnostic
                          "QRP-BOUND-NAME"
                          $"$.environment.bounds[%d{index}]"
                          "Bound names cannot be blank."

                  if value < 0L then
                      ReplayInternal.diagnostic
                          "QRP-BOUND-VALUE"
                          $"$.environment.bounds[%d{index}]"
                          "Bound values cannot be negative."

              for name, count in environment.Bounds |> List.map fst |> List.countBy id |> List.sortBy fst do
                  if count > 1 then
                      ReplayInternal.diagnostic
                          "QRP-BOUND-DUPLICATE"
                          "$.environment.bounds"
                          $"Bound '%s{name}' is duplicated." ]

        let stepDiagnostics =
            [ for ordinal, step in trace.Steps |> List.indexed do
                  let expectedIndex = ordinal + 1

                  if step.Index <> expectedIndex then
                      ReplayInternal.diagnostic
                          "QRP-STEP-ORDER"
                          $"$.steps[%d{ordinal}].index"
                          $"Expected step index %d{expectedIndex}."

                  if String.IsNullOrWhiteSpace step.Action then
                      ReplayInternal.diagnostic "QRP-STEP-ACTION" $"$.steps[%d{ordinal}].action" "Action is required."

                  yield! ReplayInternal.validateSource $"$.steps[%d{ordinal}].source" step.Source
                  yield! ReplayInternal.validateState $"$.steps[%d{ordinal}].expected" step.Expected ]

        [ if trace.SchemaVersion <> 1 then
              ReplayInternal.diagnostic
                  "QRP-SCHEMA-VERSION"
                  "$.schemaVersion"
                  "Only quint-replay-v1 schema version 1 is supported."
          if not (ReplayInternal.isLowerSha256 trace.TraceIdentity) then
              ReplayInternal.diagnostic
                  "QRP-TRACE-IDENTITY"
                  "$.traceIdentity"
                  "Trace identity must be a lowercase SHA-256 digest."
          if String.IsNullOrWhiteSpace environment.Seed then
              ReplayInternal.diagnostic "QRP-SEED" "$.environment.seed" "Replay seed is required."

          yield! ReplayInternal.validateFingerprint "$.environment.toolFingerprint" environment.ToolFingerprint
          yield! ReplayInternal.validateFingerprint "$.environment.profileFingerprint" environment.ProfileFingerprint
          yield! ReplayInternal.validateFingerprint "$.environment.contractFingerprint" environment.ContractFingerprint
          yield! ReplayInternal.validateFingerprint "$.environment.adapterFingerprint" environment.AdapterFingerprint
          yield!
              ReplayInternal.validateFingerprint
                  "$.environment.implementationFingerprint"
                  environment.ImplementationFingerprint
          yield! boundsDiagnostics
          yield! ReplayInternal.validateState "$.initial" trace.Initial
          yield! stepDiagnostics ]
        |> ReplayInternal.sortDiagnostics

    let compare trace observations =
        let traceDiagnostics = validateTrace trace

        let observationDiagnostics =
            [ for ordinal, observation in observations |> List.indexed do
                  if observation.Index <> ordinal + 1 then
                      ReplayInternal.diagnostic
                          "QRP-OBSERVATION-ORDER"
                          $"$.observations[%d{ordinal}].index"
                          $"Expected observation index %d{ordinal + 1}."

                  if String.IsNullOrWhiteSpace observation.Action then
                      ReplayInternal.diagnostic
                          "QRP-OBSERVATION-ACTION"
                          $"$.observations[%d{ordinal}].action"
                          "Observed action is required."

                  yield! ReplayInternal.validateSource $"$.observations[%d{ordinal}].source" observation.Source
                  yield! ReplayInternal.validateState $"$.observations[%d{ordinal}].actual" observation.Actual ]
            |> ReplayInternal.sortDiagnostics

        let diagnostics =
            ReplayInternal.sortDiagnostics (traceDiagnostics @ observationDiagnostics)

        if not diagnostics.IsEmpty then
            Error diagnostics
        else
            let rec first (expected: QuintReplayStep list) (actual: QuintReplayObservation list) =
                match expected, actual with
                | [], [] -> QuintReplayResult.Equivalent
                | step :: _, [] ->
                    QuintReplayResult.Diverged
                        { Step = step.Index
                          Action = step.Action
                          Source = step.Source
                          Expected = Some step.Expected
                          Actual = None
                          Reason = "missing-observation" }
                | [], observation :: _ ->
                    QuintReplayResult.Diverged
                        { Step = observation.Index
                          Action = observation.Action
                          Source = observation.Source
                          Expected = None
                          Actual = Some observation.Actual
                          Reason = "unexpected-observation" }
                | step :: remainingExpected, observation :: remainingActual ->
                    let expectedJson = ReplayInternal.encodeStateUnchecked step.Expected
                    let actualJson = ReplayInternal.encodeStateUnchecked observation.Actual

                    if step.Index <> observation.Index then
                        QuintReplayResult.Diverged
                            { Step = min step.Index observation.Index
                              Action = step.Action
                              Source = step.Source
                              Expected = Some step.Expected
                              Actual = Some observation.Actual
                              Reason = "step-identity" }
                    elif not (String.Equals(step.Action, observation.Action, StringComparison.Ordinal)) then
                        QuintReplayResult.Diverged
                            { Step = step.Index
                              Action = step.Action
                              Source = step.Source
                              Expected = Some step.Expected
                              Actual = Some observation.Actual
                              Reason = "action-identity" }
                    elif step.Source <> observation.Source then
                        QuintReplayResult.Diverged
                            { Step = step.Index
                              Action = step.Action
                              Source = step.Source
                              Expected = Some step.Expected
                              Actual = Some observation.Actual
                              Reason = "source-binding" }
                    elif
                        not (
                            String.Equals(step.Expected.Identity, observation.Actual.Identity, StringComparison.Ordinal)
                        )
                        || not (String.Equals(expectedJson, actualJson, StringComparison.Ordinal))
                    then
                        QuintReplayResult.Diverged
                            { Step = step.Index
                              Action = step.Action
                              Source = step.Source
                              Expected = Some step.Expected
                              Actual = Some observation.Actual
                              Reason = "state" }
                    else
                        first remainingExpected remainingActual

            Ok(first trace.Steps observations)
