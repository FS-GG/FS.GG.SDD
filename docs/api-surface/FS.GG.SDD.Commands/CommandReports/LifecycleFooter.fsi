namespace FS.GG.SDD.Commands

open FS.GG.SDD.Commands.CommandTypes

/// Feature 084: the single projection of the lifecycle-status footer's textual facts, shared by
/// the plain-text renderer (`CommandRendering`) and the rich renderer (`FS.GG.SDD.Cli.Rendering`)
/// so the two projections cannot drift. The failure explanation/options are computed here from the
/// report's existing `Diagnostics`/`NextAction` (FR-017) — never a new stored fact.
module LifecycleFooter =

    /// One stage's display label plus its sensed state (for colouring the rich rail).
    type FooterCell = { Label: string; State: StageState }

    /// The footer's facts, ready for either projection to format.
    type FooterView =
        {
            /// The `lifecycle: N/M …` summary line.
            Summary: string
            /// The ten stage cells in canonical order.
            Cells: FooterCell list
            /// The `charter=done specify=… ` stage tokens as one string.
            StagesPlain: string
            /// The `fsgg-sdd <command>` next action, when there is one.
            Next: string option
            /// On a blocked/failed outcome: `blocked:`/`why:`/`fix:`/`options:` lines drawn from the
            /// existing blocking diagnostic + next-action. Empty otherwise. }
            Blocked: string list
        }

    val stageStateToken: state: StageState -> string

    val view: report: CommandReport -> FooterView

    /// The full footer as ordered plain-text lines (summary, stages, then blocked lines or next).
    val plainLines: report: CommandReport -> string list
