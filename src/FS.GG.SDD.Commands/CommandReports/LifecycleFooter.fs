namespace FS.GG.SDD.Commands

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandTypes

module LifecycleFooter =

    type FooterCell = { Label: string; State: StageState }

    type FooterView =
        { Summary: string
          Cells: FooterCell list
          StagesPlain: string
          Next: string option
          Blocked: string list }

    // Delegates to the single canonical map (CommandTypes.stageStateName) — kept as a public alias
    // so callers of the footer projection have the token without reaching across modules.
    let stageStateToken (state: StageState) = stageStateName state

    // The blocking diagnostic for the failure explanation: the one named by the next-action's
    // blocking ids, else the first error-severity diagnostic. Reused facts only (FR-017).
    let private blockingDiagnostic (report: CommandReport) =
        let blockingIds =
            report.NextAction
            |> Option.map (fun action -> action.BlockingDiagnosticIds)
            |> Option.defaultValue []

        report.Diagnostics
        |> List.tryFind (fun diagnostic -> List.contains diagnostic.Id blockingIds)
        |> Option.orElse (
            report.Diagnostics
            |> List.tryFind (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
        )

    let view (report: CommandReport) : FooterView =
        let status = report.LifecycleStatus
        let total = status.TotalStages
        let workText = status.WorkId |> Option.defaultValue "none"
        let outcomeText = outcomeValue status.Outcome
        let position = status.CurrentOrdinal |> Option.defaultValue 0

        let cells =
            status.Stages
            |> List.map (fun entry ->
                { Label = commandName entry.Command
                  State = entry.State })

        let stagesPlain =
            status.Stages
            |> List.map (fun entry -> $"{commandName entry.Command}={stageStateToken entry.State}")
            |> String.concat " "

        let summary =
            if status.IsLifecycleStage then
                let stageName = commandName report.Command

                let marker =
                    if status.Outcome = CommandOutcome.Blocked then
                        "blocked"
                    else
                        "current"

                $"lifecycle: {position}/{total} {stageName} ({marker}) · work={workText} · outcome={outcomeText}"
            else
                $"lifecycle: {position}/{total} done · work={workText} · outcome={outcomeText} · ({commandName report.Command} is not a lifecycle stage)"

        let next =
            status.NextCommand
            |> Option.map (fun command -> $"fsgg-sdd {commandName command}")

        let blocked =
            if status.Outcome = CommandOutcome.Blocked then
                let diagnostic = blockingDiagnostic report

                let why = diagnostic |> Option.map (fun d -> d.Message) |> Option.defaultValue ""

                let fix =
                    diagnostic
                    |> Option.map (fun d -> d.Correction)
                    |> Option.defaultValue ""
                    |> fun value -> value.Trim()

                // The option on a blocked stage is to address the diagnostic and re-run: prefer the
                // next-action's own command, else re-run the command that just blocked (report.Command).
                let optionCommand =
                    report.NextAction
                    |> Option.bind (fun action -> action.Command)
                    |> Option.defaultValue report.Command
                    |> fun command -> Some $"fsgg-sdd {commandName command}"

                [ yield $"blocked: {commandName report.Command}"
                  if why <> "" then
                      yield $"why: {why}"
                  if fix <> "" then
                      yield $"fix: {fix}"
                  match optionCommand with
                  | Some option -> yield $"options: {option}"
                  | None -> () ]
            else
                []

        { Summary = summary
          Cells = cells
          StagesPlain = stagesPlain
          Next = next
          Blocked = blocked }

    let plainLines (report: CommandReport) : string list =
        let footer = view report

        [ yield footer.Summary
          yield $"stages: {footer.StagesPlain}"
          if not (List.isEmpty footer.Blocked) then
              yield! footer.Blocked
          else
              match footer.Next with
              | Some next -> yield $"next: {next}"
              | None -> () ]
