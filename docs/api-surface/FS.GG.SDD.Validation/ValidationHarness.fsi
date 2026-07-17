namespace FS.GG.SDD.Validation

open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Validation.ValidationContracts

/// Pure Elmish surface for the validation sweep (Constitution V). `init` enumerates
/// the declared cross-product into pending cells (each `NotValidated` until proven)
/// and emits the requested-I/O effects; `update` folds reported cell results into the
/// matrices; `BuildReport` projects the final deterministic `ValidationReport`. The
/// harness runs no commands and touches no files — the `ValidationRunner` edge
/// interpreter performs all real I/O and feeds results back as messages.
module ValidationHarness =
    /// The declared dimension values of the four broad matrices (FR-001). Inspectable
    /// before any I/O so coverage is auditable.
    type MatrixPlan =
        { LifecycleCommands: SddCommand list
          Projections: OutputFormat list
          States: string list
          DeterminismOutputs: string list
          Environments: EnvironmentClass list
          BaselineContracts: string list
          CompatibilityEntries: string list }

    type ValidationModel =
        { Matrices: Matrix list
          Report: ValidationReport option }

    type ValidationMsg =
        | CellEvaluated of matrix: string * MatrixCell
        | SurfaceReconciled of findings: (string * MatrixCell) list
        | BuildReport

    /// Requested I/O the edge interpreter fulfills (Constitution V).
    type ValidationEffect =
        | RunCommandCell of command: SddCommand * projection: OutputFormat * state: string
        | ReproduceForEnvironment of output: string * environment: EnvironmentClass
        | EvaluateBaselineConformance
        | EvaluateCompatibility
        | ReconcileDeclaredSurface

    /// The default declared plan: the 13 public commands, three projections, the
    /// representative state ladder, the catalogued determinism outputs, five
    /// environment classes, the release-catalog baseline contracts, and the declared
    /// compatibility entries.
    val defaultPlan: MatrixPlan

    /// Enumerate the declared cross-product into pending (`NotValidated`) cells and
    /// emit the requested-I/O effects.
    val init: plan: MatrixPlan -> ValidationModel * ValidationEffect list

    /// Pure transition folding `CellEvaluated` / `SurfaceReconciled`; `BuildReport`
    /// projects the final `ValidationReport`.
    val update: msg: ValidationMsg -> model: ValidationModel -> ValidationModel * ValidationEffect list
