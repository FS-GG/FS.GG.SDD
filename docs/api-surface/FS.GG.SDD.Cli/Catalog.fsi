namespace FS.GG.SDD.Cli

/// Explicit read-only local catalog inspection; no process launch or workspace mutation.
module Catalog =
    val run: args: string list -> int
    val unavailable: args: string list -> int
