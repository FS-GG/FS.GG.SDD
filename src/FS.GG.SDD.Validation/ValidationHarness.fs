namespace FS.GG.SDD.Validation

open FS.GG.SDD.Artifacts.ReleaseContract
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Validation.ValidationContracts

module ValidationHarness =
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

    type ValidationEffect =
        | RunCommandCell of command: SddCommand * projection: OutputFormat * state: string
        | ReproduceForEnvironment of output: string * environment: EnvironmentClass
        | EvaluateBaselineConformance
        | EvaluateCompatibility
        | ReconcileDeclaredSurface

    /// The catalogued determinism outputs: the ten generated views plus the
    /// `--json` command-report (matrix-runner matrix 2 / FR-003).
    let determinismOutputs =
        [ "work-model.json"
          "analysis.json"
          "verify.json"
          "ship.json"
          "ship-verdict.json"
          "governance-handoff.json"
          "summary.md"
          "agent-commands/<target>/guidance.json"
          "agent-commands/<target>/commands.md"
          "agent-commands/<target>/skills.md"
          "command-report (--json)" ]

    let defaultPlan =
        let release = currentRelease ()

        { LifecycleCommands =
            [ Init
              Charter
              Specify
              Clarify
              Checklist
              Plan
              Tasks
              Analyze
              Evidence
              Verify
              Ship
              Agents
              Refresh ]
          Projections = [ Json; Text; Rich ]
          States =
            [ "fresh"
              "specified"
              "planReady"
              "tasksReady"
              "analyzed"
              "evidenced"
              "verified"
              "shipped"
              "blocked" ]
          DeterminismOutputs = determinismOutputs
          Environments =
            [ ColorDisabled
              TermDumb
              NonInteractiveRedirected
              Interactive
              PerturbedHostEnvironment ]
          BaselineContracts = release.Catalog |> List.map (fun entry -> entry.Contract)
          CompatibilityEntries = release.Compatibility |> List.map (fun entry -> entry.SddVersionLine) }

    let pending = NotValidated "not yet evaluated"

    let cell coordinates =
        { Coordinates = coordinates
          Status = pending }

    let init (plan: MatrixPlan) =
        let lifecycleCells =
            [ for command in plan.LifecycleCommands do
                  for projection in plan.Projections do
                      for state in plan.States do
                          cell
                              [ "command", commandName command
                                "projection", outputFormatValue projection
                                "state", state ] ]

        let determinismCells =
            [ for output in plan.DeterminismOutputs do
                  for environment in plan.Environments do
                      cell [ "output", output; "environment", environmentClassValue environment ] ]

        let baselineCells =
            [ for contract in plan.BaselineContracts do
                  for check in [ "baseline"; "conformance" ] do
                      cell [ "contract", contract; "check", check ] ]

        let compatibilityCells =
            [ for entry in plan.CompatibilityEntries do
                  for check in [ "handoffContractVersion"; "specKitRange" ] do
                      cell [ "entry", entry; "check", check ] ]

        let matrices =
            [ { Name = lifecycleMatrixName
                Dimensions = [ "command"; "projection"; "state" ]
                Cells = lifecycleCells }
              { Name = determinismMatrixName
                Dimensions = [ "output"; "environment" ]
                Cells = determinismCells }
              { Name = baselineMatrixName
                Dimensions = [ "contract"; "check" ]
                Cells = baselineCells }
              { Name = compatibilityMatrixName
                Dimensions = [ "entry"; "check" ]
                Cells = compatibilityCells } ]

        let effects =
            [ for command in plan.LifecycleCommands do
                  for projection in plan.Projections do
                      for state in plan.States do
                          RunCommandCell(command, projection, state) ]
            @ [ for output in plan.DeterminismOutputs do
                    for environment in plan.Environments do
                        ReproduceForEnvironment(output, environment) ]
            @ [ EvaluateBaselineConformance; EvaluateCompatibility; ReconcileDeclaredSurface ]

        { Matrices = matrices; Report = None }, effects

    let update (msg: ValidationMsg) (model: ValidationModel) : ValidationModel * ValidationEffect list =
        match msg with
        | CellEvaluated(matrixName, evaluated) ->
            let matrices =
                model.Matrices
                |> List.map (fun matrix ->
                    if matrix.Name = matrixName then
                        { matrix with
                            Cells =
                                matrix.Cells
                                |> List.map (fun current ->
                                    if current.Coordinates = evaluated.Coordinates then
                                        evaluated
                                    else
                                        current) }
                    else
                        matrix)

            { model with Matrices = matrices }, []

        | SurfaceReconciled findings ->
            let matrices =
                model.Matrices
                |> List.map (fun matrix ->
                    let extras =
                        findings |> List.filter (fun (name, _) -> name = matrix.Name) |> List.map snd

                    if List.isEmpty extras then
                        matrix
                    else
                        { matrix with
                            Cells = matrix.Cells @ extras })

            { model with Matrices = matrices }, []

        | BuildReport ->
            let report =
                { SchemaVersion = 1
                  GeneratorVersion = currentGeneratorVersion ()
                  Matrices = model.Matrices
                  Summary = summarize model.Matrices
                  Sensed = emptySensed }

            { model with Report = Some report }, []
