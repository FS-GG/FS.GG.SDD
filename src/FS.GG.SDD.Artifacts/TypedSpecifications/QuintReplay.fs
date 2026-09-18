namespace FS.GG.SDD.Artifacts.TypedSpecifications


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
    {
        Identity: string
        Bindings: (string * QuintReplayValue) list
    }

type QuintReplayStep =
    {
        Index: int
        Action: string
        Source: QuintReplaySourceBinding
        Expected: QuintReplayState
    }

type QuintReplayEnvironment =
    {
        Seed: string
        Bounds: (string * int64) list
        ToolFingerprint: string
        ProfileFingerprint: string
        ContractFingerprint: string
        AdapterFingerprint: string
        ImplementationFingerprint: string
    }

type QuintReplayTrace =
    {
        SchemaVersion: int
        TraceIdentity: string
        Environment: QuintReplayEnvironment
        Initial: QuintReplayState
        Steps: QuintReplayStep list
    }

type QuintItfStepBinding =
    {
        Index: int
        Action: string
        Source: QuintReplaySourceBinding
    }

type QuintItfDecodeContext =
    {
        Environment: QuintReplayEnvironment
        Steps: QuintItfStepBinding list
    }

type QuintReplayObservation =
    {
        Index: int
        Action: string
        Source: QuintReplaySourceBinding
        Actual: QuintReplayState
    }

type QuintReplayDiagnostic =
    {
        Code: string
        Path: string
        Message: string
    }

type QuintReplayDivergence =
    {
        Step: int
        Action: string
        Source: QuintReplaySourceBinding
        Expected: QuintReplayState option
        Actual: QuintReplayState option
        Reason: string
    }

[<RequireQualifiedAccess>]
type QuintReplayResult =
    | Equivalent
    | Diverged of QuintReplayDivergence

// These CLR types remain in SDD for existing compiled consumers. Only representation
// conversion lives here; FsQuint is the sole owner of generic replay algorithms.
module private ReplayMapping =
    let rec valueTo =
        function
        | Null -> FsQuint.QuintReplayValue.Null
        | Boolean x -> FsQuint.QuintReplayValue.Boolean x
        | Integer x -> FsQuint.QuintReplayValue.Integer x
        | Text x -> FsQuint.QuintReplayValue.Text x
        | Sequence xs -> FsQuint.QuintReplayValue.Sequence(List.map valueTo xs)
        | Set xs -> FsQuint.QuintReplayValue.Set(List.map valueTo xs)
        | Record xs -> FsQuint.QuintReplayValue.Record(List.map (fun (k, v) -> k, valueTo v) xs)

    let rec valueFrom =
        function
        | FsQuint.QuintReplayValue.Null -> Null
        | FsQuint.QuintReplayValue.Boolean x -> Boolean x
        | FsQuint.QuintReplayValue.Integer x -> Integer x
        | FsQuint.QuintReplayValue.Text x -> Text x
        | FsQuint.QuintReplayValue.Sequence xs -> Sequence(List.map valueFrom xs)
        | FsQuint.QuintReplayValue.Set xs -> Set(List.map valueFrom xs)
        | FsQuint.QuintReplayValue.Record xs -> Record(List.map (fun (k, v) -> k, valueFrom v) xs)

    let sourceTo (x: QuintReplaySourceBinding) : FsQuint.QuintReplaySourceBinding =
        {
            Path = x.Path
            Line = x.Line
            Column = x.Column
        }

    let sourceFrom (x: FsQuint.QuintReplaySourceBinding) : QuintReplaySourceBinding =
        {
            Path = x.Path
            Line = x.Line
            Column = x.Column
        }

    let stateTo (x: QuintReplayState) : FsQuint.QuintReplayState =
        {
            Identity = x.Identity
            Bindings = x.Bindings |> List.map (fun (k, v) -> k, valueTo v)
        }

    let stateFrom (x: FsQuint.QuintReplayState) : QuintReplayState =
        {
            Identity = x.Identity
            Bindings = x.Bindings |> List.map (fun (k, v) -> k, valueFrom v)
        }

    let stepTo (x: QuintReplayStep) : FsQuint.QuintReplayStep =
        {
            Index = x.Index
            Action = x.Action
            Source = sourceTo x.Source
            Expected = stateTo x.Expected
        }

    let stepFrom (x: FsQuint.QuintReplayStep) : QuintReplayStep =
        {
            Index = x.Index
            Action = x.Action
            Source = sourceFrom x.Source
            Expected = stateFrom x.Expected
        }

    let environmentTo (x: QuintReplayEnvironment) : FsQuint.QuintReplayEnvironment =
        {
            Seed = x.Seed
            Bounds = x.Bounds
            ToolFingerprint = x.ToolFingerprint
            ProfileFingerprint = x.ProfileFingerprint
            ContractFingerprint = x.ContractFingerprint
            AdapterFingerprint = x.AdapterFingerprint
            ImplementationFingerprint = x.ImplementationFingerprint
        }

    let environmentFrom (x: FsQuint.QuintReplayEnvironment) : QuintReplayEnvironment =
        {
            Seed = x.Seed
            Bounds = x.Bounds
            ToolFingerprint = x.ToolFingerprint
            ProfileFingerprint = x.ProfileFingerprint
            ContractFingerprint = x.ContractFingerprint
            AdapterFingerprint = x.AdapterFingerprint
            ImplementationFingerprint = x.ImplementationFingerprint
        }

    let traceTo (x: QuintReplayTrace) : FsQuint.QuintReplayTrace =
        {
            SchemaVersion = x.SchemaVersion
            TraceIdentity = x.TraceIdentity
            Environment = environmentTo x.Environment
            Initial = stateTo x.Initial
            Steps = List.map stepTo x.Steps
        }

    let traceFrom (x: FsQuint.QuintReplayTrace) : QuintReplayTrace =
        {
            SchemaVersion = x.SchemaVersion
            TraceIdentity = x.TraceIdentity
            Environment = environmentFrom x.Environment
            Initial = stateFrom x.Initial
            Steps = List.map stepFrom x.Steps
        }

    let bindingTo (x: QuintItfStepBinding) : FsQuint.QuintItfStepBinding =
        {
            Index = x.Index
            Action = x.Action
            Source = sourceTo x.Source
        }

    let bindingFrom (x: FsQuint.QuintItfStepBinding) : QuintItfStepBinding =
        {
            Index = x.Index
            Action = x.Action
            Source = sourceFrom x.Source
        }

    let contextTo (x: QuintItfDecodeContext) : FsQuint.QuintItfDecodeContext =
        {
            Environment = environmentTo x.Environment
            Steps = List.map bindingTo x.Steps
        }

    let contextFrom (x: FsQuint.QuintItfDecodeContext) : QuintItfDecodeContext =
        {
            Environment = environmentFrom x.Environment
            Steps = List.map bindingFrom x.Steps
        }

    let observationTo (x: QuintReplayObservation) : FsQuint.QuintReplayObservation =
        {
            Index = x.Index
            Action = x.Action
            Source = sourceTo x.Source
            Actual = stateTo x.Actual
        }

    let observationFrom (x: FsQuint.QuintReplayObservation) : QuintReplayObservation =
        {
            Index = x.Index
            Action = x.Action
            Source = sourceFrom x.Source
            Actual = stateFrom x.Actual
        }

    let diagnosticTo (x: QuintReplayDiagnostic) : FsQuint.QuintReplayDiagnostic =
        {
            Code = x.Code
            Path = x.Path
            Message = x.Message
        }

    let diagnosticFrom (x: FsQuint.QuintReplayDiagnostic) : QuintReplayDiagnostic =
        {
            Code = x.Code
            Path = x.Path
            Message = x.Message
        }

    let divergenceTo (x: QuintReplayDivergence) : FsQuint.QuintReplayDivergence =
        {
            Step = x.Step
            Action = x.Action
            Source = sourceTo x.Source
            Expected = Option.map stateTo x.Expected
            Actual = Option.map stateTo x.Actual
            Reason = x.Reason
        }

    let divergenceFrom (x: FsQuint.QuintReplayDivergence) : QuintReplayDivergence =
        {
            Step = x.Step
            Action = x.Action
            Source = sourceFrom x.Source
            Expected = Option.map stateFrom x.Expected
            Actual = Option.map stateFrom x.Actual
            Reason = x.Reason
        }

    let errors result =
        Result.mapError (List.map diagnosticFrom) result

[<RequireQualifiedAccess>]
module QuintReplay =
    let encodeValue value =
        FsQuint.QuintReplay.encodeValue (ReplayMapping.valueTo value)
        |> ReplayMapping.errors

    let encodeState state =
        FsQuint.QuintReplay.encodeState (ReplayMapping.stateTo state)
        |> ReplayMapping.errors

    let stateFingerprint state =
        FsQuint.QuintReplay.stateFingerprint (ReplayMapping.stateTo state)
        |> ReplayMapping.errors

    let validateTrace trace =
        FsQuint.QuintReplay.validateTrace (ReplayMapping.traceTo trace)
        |> List.map ReplayMapping.diagnosticFrom

    let traceFingerprint trace =
        FsQuint.QuintReplay.traceFingerprint (ReplayMapping.traceTo trace)
        |> ReplayMapping.errors

    let decodeItf context text =
        FsQuint.QuintReplay.decodeItf (ReplayMapping.contextTo context) text
        |> Result.map ReplayMapping.traceFrom
        |> ReplayMapping.errors

    let compare trace observations =
        FsQuint.QuintReplay.compare (ReplayMapping.traceTo trace) (List.map ReplayMapping.observationTo observations)
        |> Result.map (function
            | FsQuint.QuintReplayResult.Equivalent -> QuintReplayResult.Equivalent
            | FsQuint.QuintReplayResult.Diverged d -> QuintReplayResult.Diverged(ReplayMapping.divergenceFrom d))
        |> ReplayMapping.errors
