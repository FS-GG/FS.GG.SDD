namespace FS.GG.SDD.Cli

open FS.GG.SDD.Commands.CommandTypes

/// The CLI's option-recognition contract (FS-GG/FS.GG.SDD#196).
///
/// `Program.fs` parses with positive lookups only (`optionValue "--root" rest`, …), so any
/// token it does not ask for falls through silently: `init --project-root /tmp/b` seeded the
/// current directory and reported `outcome: succeeded`. An agent driving the `--json` contract
/// cannot distinguish "the flag I passed was honored" from "the flag was dropped".
///
/// This module names, per command, every option the parser actually consumes, so the residue
/// — the `--`-prefixed tokens nobody claimed — can be rejected instead of ignored. Recognition
/// is deliberately *wider* than the advertised help: `--dry-run` is honored by the effect
/// interpreter for every command, and `--explain` is accepted everywhere (commands with no
/// primary artifact answer with `explainUnsupported`), yet neither is listed for every command
/// in `CommandHelp`.
module Options =
    /// One recognized option token, and whether it consumes the token that follows it.
    /// A valued option's argument is skipped by the scanner, so `--title --rich` passes
    /// `--rich` as the title rather than reporting it twice.
    type OptionSpec = { Token: string; TakesValue: bool }

    /// Options accepted by every command that routes through `parseCommand`.
    val globalOptions: OptionSpec list

    /// Options accepted only by `command`, in help-declaration order.
    val commandOptions: command: SddCommand -> OptionSpec list

    /// `globalOptions` followed by `commandOptions command`, deduplicated by token.
    val recognized: command: SddCommand -> OptionSpec list

    /// The recognized tokens for `command`, in the order `recognized` declares them.
    /// This is what the `unknownOption` correction enumerates.
    val recognizedTokens: command: SddCommand -> string list

    /// Every `--`/`-`-prefixed token in `args` that `command` does not recognize, in order of
    /// appearance and without duplicates removed — arguments to valued options are skipped, so
    /// a value that happens to look like a flag is never reported.
    val unrecognized: command: SddCommand -> args: string list -> string list

    /// The recognized token `token` most plausibly meant, or `None` when nothing is close.
    /// Deterministic: candidates are ranked by (containment before edit distance, distance,
    /// declaration order), never by a score that could reorder between runs.
    val suggestion: command: SddCommand -> token: string -> string option
