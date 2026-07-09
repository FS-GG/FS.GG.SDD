namespace FS.GG.SDD.Cli

open System
open FS.GG.SDD.Commands.CommandTypes

module Options =
    type OptionSpec = { Token: string; TakesValue: bool }

    let private flag token = { Token = token; TakesValue = false }
    let private valued token = { Token = token; TakesValue = true }

    // Recognized everywhere, because `Program.fs` reads all of these before it knows which
    // command it holds: `--root` seeds the request, the four format/color tokens are resolved
    // by `Rendering.selectFormat` / `forceColorRequested`, `--dry-run` is honored by the effect
    // interpreter for every command (`CommandEffects.interpretAll request.ProjectRoot
    // request.DryRun`), and `--explain` is answered on every command (feature 076: one with no
    // primary artifact reports `explainUnsupported` rather than rejecting the token).
    let globalOptions =
        [ valued "--root"
          flag "--json"
          flag "--text"
          flag "--rich"
          flag "--force-color"
          flag "--dry-run"
          flag "--explain"
          flag "--help"
          flag "-h" ]

    let private work = valued "--work"
    let private title = valued "--title"

    let commandOptions (command: SddCommand) =
        match command with
        // `init` and `doctor` take the global flags only.
        | Init
        | Doctor -> []
        | Charter -> [ work; title ]
        | Specify
        | Clarify -> [ work; title; valued "--input" ]
        | Evidence -> [ work; title; valued "--from-tests" ]
        | Plan -> [ work; title; flag "--accept-upstream" ]
        | Checklist
        | Tasks
        | Analyze
        | Verify
        | Ship
        | Agents
        | Refresh -> [ work; title ]
        | Scaffold -> [ valued "--provider"; valued "--param"; flag "--force"; flag "--no-update" ]
        | Upgrade -> [ flag "--yes" ]
        // `lint` takes its artifact as a positional; `--explain` is global.
        | Lint -> []
        | Surface -> [ flag "--check"; flag "--update"; valued "--param" ]

    let recognized (command: SddCommand) =
        globalOptions @ commandOptions command
        |> List.distinctBy (fun spec -> spec.Token)

    let recognizedTokens (command: SddCommand) =
        recognized command |> List.map (fun spec -> spec.Token)

    /// A token the scanner should classify — everything the CLI spells with a leading dash.
    /// `lint`'s positional artifact and every option *value* are excluded by construction:
    /// values are consumed alongside their option, and a bare positional carries no dash.
    let private isOptionToken (token: string) =
        token.StartsWith("-", StringComparison.Ordinal) && token <> "-"

    let unrecognized (command: SddCommand) (args: string list) =
        let known = recognized command

        let rec scan args acc =
            match args with
            | [] -> List.rev acc
            | token :: rest when isOptionToken token ->
                match known |> List.tryFind (fun spec -> spec.Token = token) with
                // Skip a valued option's argument so `--title --rich` reads `--rich` as the
                // title, exactly as `optionValue` does, rather than reporting it as residue.
                | Some spec when spec.TakesValue ->
                    match rest with
                    | _value :: tail -> scan tail acc
                    | [] -> scan [] acc
                | Some _ -> scan rest acc
                | None -> scan rest (token :: acc)
            | _ :: rest -> scan rest acc

        scan args []

    /// Levenshtein distance. Bounded by the token lengths the CLI deals in (< 20 chars), so the
    /// quadratic table is free and the result is exact rather than a heuristic score.
    let private editDistance (left: string) (right: string) =
        let previous = Array.init (right.Length + 1) id
        let current = Array.zeroCreate (right.Length + 1)

        for i in 1 .. left.Length do
            current[0] <- i

            for j in 1 .. right.Length do
                let substitution = if left[i - 1] = right[j - 1] then 0 else 1

                current[j] <- min (min (current[j - 1] + 1) (previous[j] + 1)) (previous[j - 1] + substitution)

            Array.blit current 0 previous 0 (right.Length + 1)

        previous[right.Length]

    /// The dashes carry no information for a near-miss comparison, and keeping them would make
    /// every candidate look two characters closer than it is.
    let private body (token: string) = token.TrimStart('-')

    let suggestion (command: SddCommand) (token: string) =
        let unknown = body token

        // A candidate qualifies two ways: one body contains the other (`--project-root` ⊃
        // `--root`, which no edit-distance bound would ever reach), bounded below at four
        // characters so `-h` does not match every token sharing a letter; or it is a typo within
        // two edits (`--dryrun` → `--dry-run`). Qualified candidates then rank by *distance
        // first* — `--forcecolor` must land on `--force-color` (1 edit) and not on the
        // shorter-but-contained `--force`. Containment breaks a distance tie, and declaration
        // order breaks the rest, so the answer is total and never reorders between runs.
        let qualify candidate =
            let known = body candidate
            let distance = editDistance unknown known

            let contained =
                min known.Length unknown.Length >= 4
                && (unknown.Contains known || known.Contains unknown)

            if contained then Some(distance, 0)
            elif distance <= 2 then Some(distance, 1)
            else None

        recognizedTokens command
        |> List.indexed
        |> List.choose (fun (index, candidate) ->
            qualify candidate
            |> Option.map (fun (distance, tieBreak) -> (distance, tieBreak, index), candidate))
        |> List.sortBy fst
        |> List.tryHead
        |> Option.map snd
