namespace FS.GG.SDD.Artifacts.TypedSpecifications

/// A stable generated-binding failure.
type QuintBindingDiagnostic =
    { Code: string
      Path: string
      Message: string }

/// Deterministic native and Fable-compatible projections of compiled-contract v1.
type QuintGeneratedBindings =
    { CanonicalJson: string
      ContractFingerprint: string
      Identifiers: string list
      FSharpSource: string
      FableSource: string }

[<RequireQualifiedAccess>]
module QuintBindings =
    /// Generate collision-refusing, ordinally ordered bindings directly from a valid compiled-contract v1.
    val generate:
        moduleName: string ->
        contract: QuintCompiledContract ->
            Result<QuintGeneratedBindings, QuintBindingDiagnostic list>
