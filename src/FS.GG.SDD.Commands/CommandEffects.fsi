namespace FS.GG.SDD.Commands

open FS.GG.SDD.Commands.CommandTypes

module CommandEffects =
    val interpret: projectRoot: string -> dryRun: bool -> effect: CommandEffect -> CommandEffectResult
    val interpretAll: projectRoot: string -> dryRun: bool -> effects: CommandEffect list -> CommandEffectResult list
