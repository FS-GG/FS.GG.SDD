namespace FS.GG.SDD.Commands

open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandTypes

module LintEngine =

    [<Literal>]
    let private grammarDoc = "docs/reference/authoring-contracts.md"

    let grammarPointer (cls: LintDefectClass) : GrammarPointer option =
        let pointer anchor tag =
            Some
                { Doc = grammarDoc
                  Anchor = anchor
                  ExampleTag = tag }

        match cls with
        | CoverageLine -> pointer "acceptance-coverage-line" (Some "coverage:accepted")
        | MissingDecisionTag -> pointer "clarify-decision-tag-resolution" (Some "clarify-decision:resolved")
        | FrontMatter -> pointer "per-stage-front-matter" None
        | DuplicateId -> pointer "per-stage-front-matter" None
        | Parse
        | Unresolvable -> None

    // ---- kind detection (FR-002 / research D5) ----

    let private stageFromFrontMatter (text: string) =
        // Scan the leading `---` front-matter block for a `stage:` scalar.
        let m = Regex.Match(text, @"(?im)^\s*stage:\s*([a-z][a-z0-9-]*)\s*$")

        if m.Success then
            match m.Groups.[1].Value.Trim().ToLowerInvariant() with
            | "charter" -> Some LintArtifactKind.Charter
            | "specify"
            | "specification" -> Some LintArtifactKind.Specification
            | "clarify"
            | "clarification" -> Some LintArtifactKind.Clarification
            | "checklist" -> Some LintArtifactKind.Checklist
            | "plan" -> Some LintArtifactKind.Plan
            | "tasks" -> Some LintArtifactKind.Tasks
            | "evidence" -> Some LintArtifactKind.Evidence
            | _ -> None
        else
            None

    let private kindFromFileName (path: string) =
        let name = (path.Replace('\\', '/').Split('/') |> Array.last).ToLowerInvariant()

        if name.EndsWith "clarifications.md" then Some LintArtifactKind.Clarification
        elif name.EndsWith "checklist.md" then Some LintArtifactKind.Checklist
        elif name.EndsWith "charter.md" then Some LintArtifactKind.Charter
        elif name.EndsWith "plan.md" then Some LintArtifactKind.Plan
        elif name.EndsWith "spec.md" || name.EndsWith "specification.md" then Some LintArtifactKind.Specification
        elif name.EndsWith "tasks.yml" || name.EndsWith "tasks.yaml" then Some LintArtifactKind.Tasks
        elif name.EndsWith "evidence.yml" || name.EndsWith "evidence.yaml" then Some LintArtifactKind.Evidence
        else None

    let detectKind (snapshot: Core.FileSnapshot) : LintArtifactKind =
        // Front-matter `stage:` is the authoritative signal the stages themselves key on;
        // filename is the fallback for the YAML artifacts that carry no stage front matter.
        match stageFromFrontMatter snapshot.Text with
        | Some kind -> kind
        | None ->
            match kindFromFileName snapshot.Path with
            | Some kind -> kind
            | None -> LintArtifactKind.Unrecognized

    // ---- diagnostic classification (research D2/D3, I1 verified) ----

    // The parser-level ids are generic (`workModelInconsistent` covers both an incomplete
    // front matter and a missing stable id; `duplicateIdentifier` covers every duplicate),
    // so classification keys on id + message. Only the four load-bearing grammar classes are
    // surfaced (feature 076 scope); other diagnostics are not lint's concern.
    let private classify (diagnostic: Diagnostic) : LintDefectClass option =
        match diagnostic.Id with
        | "duplicateIdentifier" -> Some DuplicateId
        | "workModelInconsistent" ->
            let msg = diagnostic.Message.ToLowerInvariant()

            if msg.Contains "front matter is incomplete" then Some FrontMatter
            elif msg.Contains "missing a required stable id" then Some CoverageLine
            else None
        | _ -> None

    // Route to the live parser; `Ok` surfaces the parser's `facts.Diagnostics`, `Error`
    // surfaces the hard-failure list. The second element is the single-artifact blocking
    // ambiguity count (clarify only) from which a MissingDecisionTag defect is synthesized.
    let private parserDiagnostics (kind: LintArtifactKind) (snapshot: Core.FileSnapshot) : Diagnostic list * bool * int =
        let ofResult result =
            match result with
            | Ok diags -> diags, false
            | Error diags -> diags, true

        match kind with
        | LintArtifactKind.Charter ->
            // Charter has no facts-level Diagnostics list; front-matter incompleteness surfaces
            // as an `Error` from the metadata parser.
            match WorkItemMetadata.parseWorkItemMetadata snapshot with
            | Ok _ -> [], false, 0
            | Error diags -> diags, true, 0
        | LintArtifactKind.Specification ->
            let diags, failed =
                Specification.parseSpecificationFacts snapshot
                |> Result.map (fun f -> f.Diagnostics)
                |> ofResult

            diags, failed, 0
        | LintArtifactKind.Clarification ->
            match Clarification.parseClarificationFacts snapshot with
            | Ok facts -> facts.Diagnostics, false, facts.BlockingAmbiguityCount
            | Error diags -> diags, true, 0
        | LintArtifactKind.Checklist ->
            let diags, failed =
                Checklist.parseChecklistFacts snapshot
                |> Result.map (fun f -> f.Diagnostics)
                |> ofResult

            diags, failed, 0
        | LintArtifactKind.Plan ->
            let diags, failed =
                Plan.parsePlanFacts snapshot |> Result.map (fun f -> f.Diagnostics) |> ofResult

            diags, failed, 0
        | LintArtifactKind.Tasks ->
            let diags, failed =
                Task.parseTaskFacts snapshot |> Result.map (fun f -> f.Diagnostics) |> ofResult

            diags, failed, 0
        | LintArtifactKind.Evidence ->
            let diags, failed =
                Evidence.parseEvidenceArtifact snapshot
                |> Result.map (fun f -> f.Diagnostics)
                |> ofResult

            diags, failed, 0
        | LintArtifactKind.Unrecognized -> [], false, 0

    let private sortKey (defect: LintDefect) =
        let loc = defect.Diagnostic.Location
        let line = loc |> Option.bind (fun l -> l.Line) |> Option.defaultValue System.Int32.MaxValue
        let col = loc |> Option.bind (fun l -> l.Column) |> Option.defaultValue System.Int32.MaxValue
        line, col, defect.Diagnostic.Id

    let private toDefect (cls: LintDefectClass) (diagnostic: Diagnostic) =
        { Class = cls
          Diagnostic = diagnostic
          GrammarPointer = grammarPointer cls }

    let private missingDecisionTagDefect (count: int) =
        // A blocking remaining ambiguity means no `[AMB:AMB-###]`-tagged decision resolved it
        // (research D2). Synthesized single-artifact from `BlockingAmbiguityCount`.
        let diag =
            Diagnostics.create
                "unresolvedBlockingAmbiguity"
                DiagnosticError
                None
                None
                $"{count} blocking ambiguity(ies) are unresolved by a decision tag."
                "Resolve each blocking ambiguity with a DEC-### decision (or accepted deferral) line carrying its AMB-### id under ## Decisions / ## Accepted Deferrals."
                []

        toDefect MissingDecisionTag diag

    let lint (snapshot: Core.FileSnapshot) : LintSummary =
        let kind = detectKind snapshot

        let summary outcome defects =
            { ArtifactPath = snapshot.Path
              Kind = kind
              Defects = defects
              Outcome = outcome }

        match kind with
        | LintArtifactKind.Unrecognized ->
            let diag =
                Diagnostics.create
                    "lintUnrecognizedArtifact"
                    DiagnosticError
                    None
                    None
                    $"Cannot determine the SDD artifact kind of '{snapshot.Path}'."
                    "Lint a recognized authored artifact (charter/spec/clarifications/checklist/plan/tasks/evidence)."
                    []

            summary UnusableInput [ toDefect Unresolvable diag ]
        | _ ->
            let rawDiagnostics, parseFailed, blockingCount = parserDiagnostics kind snapshot

            let classified =
                rawDiagnostics
                |> List.filter (fun d -> d.Severity = DiagnosticError)
                |> List.choose (fun d -> classify d |> Option.map (fun cls -> toDefect cls d))

            let withSynthesized =
                if blockingCount > 0 then
                    classified @ [ missingDecisionTagDefect blockingCount ]
                else
                    classified

            let defects = withSynthesized |> List.sortBy sortKey

            if not (List.isEmpty defects) then
                summary DefectsFound defects
            elif parseFailed then
                // The parser hard-failed but nothing classified into a grammar class: the
                // artifact is too malformed to pre-flight meaningfully (FR-015).
                let diag =
                    Diagnostics.create
                        "lintUnparseableArtifact"
                        DiagnosticError
                        None
                        None
                        $"'{snapshot.Path}' could not be parsed as a {lintArtifactKindValue kind} artifact."
                        "Fix the artifact's structure (front matter and required sections) before rerunning lint."
                        []

                summary UnusableInput [ toDefect Parse diag ]
            else
                summary Clean []
