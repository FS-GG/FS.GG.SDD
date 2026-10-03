namespace FS.GG.SDD.Knowledge

module Workspace =
    val guidance: string
    val ignoreBlock: string
    val initialRecord: Record
    val initialFiles: (string * string) list
    val initialize: root: string -> SizeReport
