namespace FS.GG.SDD.Commands

open FS.GG.SDD.Commands.CommandTypes

module CommandEffects =
    /// Per-stream retention bound (characters) for captured provider stdout/stderr at the
    /// `RunProcess` edge (feature 054, E4). Content beyond this is drained but not retained;
    /// the stream's truncation flag records the drop (FR-005 / SC-005).
    val providerOutputCapChars: int

    val interpret: projectRoot: string -> dryRun: bool -> effect: CommandEffect -> CommandEffectResult
    val interpretAll: projectRoot: string -> dryRun: bool -> effects: CommandEffect list -> CommandEffectResult list

    /// Drive an MVU command to its final report: init, interpret-and-fold effects until idle,
    /// build and resolve the report. The single run loop shared by the CLI and the validation
    /// harness (feature 061 / issue #71).
    val driveToReport: request: CommandRequest -> CommandReport
