namespace FS.GG.SDD.Commands

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandTypes

/// The pure pre-flight engine behind `fsgg-sdd lint <artifact>` and `<stage> --explain`
/// (feature 076). It routes an authored artifact to the live `FS.GG.SDD.Artifacts`
/// parsers and surfaces the grammar-defect diagnostics they already produce — deriving no
/// new grammar (research D2). Read-only and single-artifact (research D4); has no MVU
/// ceremony (Constitution IV — a simple pure classifier).
module LintEngine =

    /// The stable grammar-of-record pointer for a defect class (FR-007b). `None` for the
    /// structural classes `Parse`/`Unresolvable`, which are not grammar defects.
    val grammarPointer: cls: LintDefectClass -> GrammarPointer option

    /// Auto-detect the artifact kind (FR-002): the front-matter `stage:` value first, then
    /// the filename/extension; `Unrecognized` when neither resolves.
    val detectKind: snapshot: Core.FileSnapshot -> LintArtifactKind

    /// Pre-flight one artifact snapshot into a deterministic `LintSummary` (defects ordered
    /// by (line, column, id); every defect an `Error`). An `Unrecognized` kind yields the
    /// `UnusableInput` outcome (exit 2); recognized-with-defects yields `DefectsFound`
    /// (exit 1); recognized-and-clean yields `Clean` (exit 0).
    val lint: snapshot: Core.FileSnapshot -> LintSummary
