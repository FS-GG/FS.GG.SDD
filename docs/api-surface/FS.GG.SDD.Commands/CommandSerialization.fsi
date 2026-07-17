namespace FS.GG.SDD.Commands

open FS.GG.SDD.Commands.CommandTypes

module CommandSerialization =
    val serializeReport: report: CommandReport -> string
