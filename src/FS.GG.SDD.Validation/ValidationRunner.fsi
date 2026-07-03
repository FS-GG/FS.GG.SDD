namespace FS.GG.SDD.Validation

open FS.GG.SDD.Validation.ValidationContracts
open FS.GG.SDD.Validation.ValidationHarness

/// Edge interpreter for the validation sweep (Constitution V). `run` performs all
/// real I/O — driving the real `FS.GG.SDD.Commands.CommandWorkflow` over disposable
/// temp directories, reproducing generated views, calling `ReleaseContract.evaluate`,
/// reconciling declared coverage against the real produced surface — and feeds every
/// result back through the pure `ValidationHarness` to assemble one deterministic
/// `ValidationReport`. It is reachable only via `fsgg-sdd validate`; no lifecycle
/// command, `init`/`update`, or effect interpreter references it (FR-008 / US3).
module ValidationRunner =
    type RunnerOptions =
        {
            /// Restrict the run to one declared matrix by name; `None` runs all four.
            /// The other matrices' cells are still reported `NotValidated` so a partial
            /// run never reads as a full pass (INV-1 / FR-007).
            OnlyMatrix: string option
            /// Override the declared plan. `None` uses the exhaustive `defaultPlan`; a
            /// focused plan keeps the deterministic sweep cheap for fixtures/tests.
            Plan: MatrixPlan option
            /// Harness self-test seam: force the named cells (matrix name + exact
            /// coordinates) to `Fail` with an actionable diagnostic. Used to prove the
            /// report isolates a single failing cell while all others pass (US1
            /// Independent Test). Empty for every real run. Genuine divergence detection
            /// is exercised by the baseline/coverage matrices over real artifacts.
            InjectedDivergences: (string * (string * string) list) list
        }

    /// The default options: run all four matrices.
    val defaultOptions: RunnerOptions

    /// Run the validation sweep and return the deterministic report. Performs all
    /// real I/O (matrix-runner C-1…C-9).
    val run: options: RunnerOptions -> ValidationReport
