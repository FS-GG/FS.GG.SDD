namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.Internal.EarlyStageAuthoring
open Xunit

/// `answerKindValue` classifies a freeform clarify `--input` line as a decision,
/// an accepted deferral, or a still-open answer. It used to substring-sniff
/// (`lowered.Contains "defer"` / `"still open"`), which misfired two ways: a state
/// word embedded in a longer word (`distill opens`) and a state word that is
/// negated (`cannot defer`, `no longer still open`). The classifier now matches
/// whole words/phrases and skips a directly-negated keyword. These tests pin both.
module AnswerKindClassificationTests =

    [<Theory>]
    // Plain deferrals — the keyword or its stem, un-negated.
    [<InlineData("accepted deferral: revisit in the checklist feature", "acceptedDeferral")>]
    [<InlineData("defer to next release", "acceptedDeferral")>]
    [<InlineData("this is deferred for now", "acceptedDeferral")>]
    [<InlineData("recorded as a deferral", "acceptedDeferral")>]
    // A deferral word appearing later than a non-adjacent negator is still a deferral:
    // `not sure` does not negate the `defer` two words on.
    [<InlineData("not sure yet, will defer", "acceptedDeferral")>]
    // Still-open / unresolved.
    [<InlineData("still open: waiting on legal", "stillOpen")>]
    [<InlineData("this remains unresolved", "stillOpen")>]
    // Plain decisions.
    [<InlineData("Decisions expire after one release.", "decision")>]
    [<InlineData("chose the second option", "decision")>]
    // Word-boundary: a state phrase buried inside a longer word is not that state.
    [<InlineData("distill opens the flavour profile", "decision")>]
    // Directly-negated state words name the state only to reject it — the reported bug.
    [<InlineData("cannot defer, decided to use option A", "decision")>]
    [<InlineData("no longer still open, resolved as B", "decision")>]
    [<InlineData("will not defer this one", "decision")>]
    let ``answerKindValue classifies by whole, un-negated state words`` (line: string) (expected: string) =
        Assert.Equal(expected, answerKindValue line)
