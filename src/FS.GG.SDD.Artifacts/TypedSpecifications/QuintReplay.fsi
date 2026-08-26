namespace FS.GG.SDD.Artifacts.TypedSpecifications

/// A source binding retained from the authored literate specification.
type QuintReplaySourceBinding =
    { Path: string
      Line: int
      Column: int }

/// A runtime-neutral value in an ITF state.
type QuintReplayValue =
    | Null
    | Boolean of bool
    | Integer of string
    | Text of string
    | Sequence of QuintReplayValue list
    | Set of QuintReplayValue list
    | Record of (string * QuintReplayValue) list

/// One fingerprinted state. Bindings are canonicalized by ordinal variable name.
type QuintReplayState =
    { Identity: string
      Bindings: (string * QuintReplayValue) list }

/// One expected transition in a generic ITF trace.
type QuintReplayStep =
    { Index: int
      Action: string
      Source: QuintReplaySourceBinding
      Expected: QuintReplayState }

/// Every identity needed to prove which toolchain and adapter produced a trace.
type QuintReplayEnvironment =
    { Seed: string
      Bounds: (string * int64) list
      ToolFingerprint: string
      ProfileFingerprint: string
      ContractFingerprint: string
      AdapterFingerprint: string
      ImplementationFingerprint: string }

/// A schema-v1 generic ITF trace. It contains no product transition function.
type QuintReplayTrace =
    { SchemaVersion: int
      TraceIdentity: string
      Environment: QuintReplayEnvironment
      Initial: QuintReplayState
      Steps: QuintReplayStep list }

/// One implementation observation aligned to an expected trace step.
type QuintReplayObservation =
    { Index: int
      Action: string
      Source: QuintReplaySourceBinding
      Actual: QuintReplayState }

/// A stable validation finding emitted before replay comparison.
type QuintReplayDiagnostic =
    { Code: string
      Path: string
      Message: string }

/// The exact first model-observable mismatch.
type QuintReplayDivergence =
    { Step: int
      Action: string
      Source: QuintReplaySourceBinding
      Expected: QuintReplayState option
      Actual: QuintReplayState option
      Reason: string }

/// Replay either agrees exactly or reports the first divergence.
[<RequireQualifiedAccess>]
type QuintReplayResult =
    | Equivalent
    | Diverged of QuintReplayDivergence

[<RequireQualifiedAccess>]
module QuintReplay =
    /// Encode one value as deterministic JSON. Record keys and set values are canonicalized.
    val encodeValue: value: QuintReplayValue -> Result<string, QuintReplayDiagnostic list>

    /// Encode one state as deterministic JSON after validating its identity and bindings.
    val encodeState: state: QuintReplayState -> Result<string, QuintReplayDiagnostic list>

    /// Return the lowercase SHA-256 identity of a valid state's canonical JSON bytes.
    val stateFingerprint: state: QuintReplayState -> Result<string, QuintReplayDiagnostic list>

    /// Validate all trace identities, ordered steps, bounds, states, and source bindings.
    val validateTrace: trace: QuintReplayTrace -> QuintReplayDiagnostic list

    /// Compare ordered observations and return the exact first action/source/state divergence.
    val compare:
        trace: QuintReplayTrace ->
        observations: QuintReplayObservation list ->
            Result<QuintReplayResult, QuintReplayDiagnostic list>
