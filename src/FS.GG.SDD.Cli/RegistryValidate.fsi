namespace FS.GG.SDD.Cli

/// `fsgg-sdd registry validate <path>` — the cross-cutting, gate-callable entrypoint
/// that turns the on-disk `registry/dependencies.yml` into a verdict (feature 042).
/// A CLI-level peer of `validate`/`--version`, dispatched before `parseCommand` so the
/// lifecycle `CommandReport`/`parseCommand` contracts stay untouched (mirrors the
/// existing `validate` harness command). It composes the `FS.GG.SDD.Artifacts` YAML
/// `load` edge with the BCL-only `Fsgg.Registry.validateDocument` pure validator.
module RegistryValidate =

    /// One validation finding: the offending entry, the rule kind, and the message.
    type ReportDiagnostic =
        { Entry: string
          Rule: string
          Message: string }

    /// The deterministic verdict report. The `--json`/default projection is the
    /// automation contract the CI gate consumes (FR-007/SC-004).
    type RegistryValidateReport =
        { Path: string
          Valid: bool
          Diagnostics: ReportDiagnostic list }

    /// Compose `load <path>` with `validateDocument`. A load/parse failure becomes a
    /// single `MalformedDocument`-class diagnostic (never a crash — Constitution VIII).
    val validate: path: string -> RegistryValidateReport

    /// Canonical deterministic JSON automation contract.
    val serialize: report: RegistryValidateReport -> string

    /// Portable plain-text projection.
    val renderText: report: RegistryValidateReport -> string

    /// Exit code: `0` when `Valid`; `1` when `Invalid` or load failed.
    val exitCode: report: RegistryValidateReport -> int

    /// Drive `registry <args>` end-to-end: parse `validate <path>`, run, project in the
    /// requested format (`--rich` > `--text` > `--json` > default; rich degrades to
    /// plain text when non-interactive or color-disabled), write to stdout, and return
    /// the exit code. A missing/empty path or unknown subcommand is a user-input failure
    /// (exit 1) reported as a diagnostic, never a crash.
    val run: args: string list -> int
