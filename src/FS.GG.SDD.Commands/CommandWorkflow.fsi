namespace FS.GG.SDD.Commands

open FS.GG.SDD.Commands.CommandTypes

module CommandWorkflow =
    val init: request: CommandRequest -> CommandModel * CommandEffect list
    val update: msg: CommandMsg -> model: CommandModel -> CommandModel * CommandEffect list
