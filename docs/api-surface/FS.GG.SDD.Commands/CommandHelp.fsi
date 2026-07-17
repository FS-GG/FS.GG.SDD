namespace FS.GG.SDD.Commands

open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.CommandTypes

/// §3.5: the static, deterministic flag/description table that backs `--help`. All content
/// is fixed (no clock/path/env), so the projected report is byte-identical across runs.
module CommandHelp =
    /// Flags accepted by every command (project root, output-format selection, help).
    val globalFlags: HelpFlag list

    /// Every command the CLI dispatches: the 14 lifecycle/cross-cutting `SddCommand` cases
    /// plus the CLI-level peers `version`, `validate`, and `registry`.
    val commandEntries: HelpCommandEntry list

    /// The value-taking and switch flags accepted by a specific lifecycle command, beyond
    /// the global flags.
    val commandFlags: command: SddCommand -> HelpFlag list

    /// Top-level help: usage line, the full command list, and the global flags.
    val topLevelHelp: generator: GeneratorVersion -> HelpSummary

    /// Per-command help: usage line, the command's flags, and the global flags.
    val commandHelp: command: SddCommand -> HelpSummary
