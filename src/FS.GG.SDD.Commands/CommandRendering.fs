namespace FS.GG.SDD.Commands

open System.Text
open FS.GG.SDD.Commands.CommandTypes

module CommandRendering =
    let renderText report =
        let builder = StringBuilder()
        builder.AppendLine($"command: {commandName report.Command}") |> ignore
        builder.AppendLine($"outcome: {outcomeValue report.Outcome}") |> ignore
        builder.AppendLine($"changedArtifacts: {List.length report.ChangedArtifacts}") |> ignore
        builder.AppendLine($"generatedViews: {List.length report.GeneratedViews}") |> ignore
        builder.AppendLine($"diagnostics: {List.length report.Diagnostics}") |> ignore

        match report.NextAction with
        | Some action -> builder.AppendLine($"nextAction: {action.ActionId}") |> ignore
        | None -> builder.AppendLine("nextAction: none") |> ignore

        builder.ToString()
