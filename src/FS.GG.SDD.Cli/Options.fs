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
        | Evidence -> [ work; title; valued "--from-tests"; valued "--from-test-report" ]
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
        // `Help` is a report scope, not an invocable command — argv never routes to it.
        | Help -> []

    let recognized (command: SddCommand) =
        globalOptions @ commandOptions command
        |> List.distinctBy (fun spec -> spec.Token)

    let recognizedTokens (command: SddCommand) =
        recognized command |> List.map (fun spec -> spec.Token)

    /// A token the scanner should classify — everything the CLI spells with a leading dash.
    /// `lint`'s positional artifact and every option *value* are excluded by construction:
    /// values are consumed alongside their option, and a bare positional carries no dash.
    /// Bare `-` and the POSIX end-of-options separator `--` are option *syntax*, not option
    /// names, so neither is residue — the pre-#196 positional finder skipped `--`, and this
    /// preserves that (`fsgg-sdd lint -- work/x/spec.md` still lints the file) rather than
    /// rejecting `--` with a spurious `unknownOption` / "did you mean '-h'?" (FS-GG/FS.GG.SDD#246).
    let private isOptionToken (token: string) =
        token.StartsWith("-", StringComparison.Ordinal) && token <> "-" && token <> "--"

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

    // FS-GG/FS.GG.SDD#253 (Gap C finding 3 / #203): the `lint <artifact>` positional used to be the
    // first token that did not start with `--`. That predicate cannot see which tokens are *values*
    // of a preceding valued option, so `lint --root . spec.md` resolved `.` (the `--root` value) as
    // the artifact and never read `spec.md`. The same value-skipping scan `unrecognized` already runs
    // is the correct classifier: a token is the positional only when it is neither an option token nor
    // a valued option's argument. `--` (the POSIX end-of-options separator) is option *syntax*, not a
    // positional, so it is skipped and the token after it is selected (`lint -- work/x/spec.md` still
    // lints the file, FS-GG/FS.GG.SDD#246); a bare `-` carries no dash and remains selectable.
    let positional (command: SddCommand) (args: string list) =
        let known = recognized command

        let rec scan args =
            match args with
            | [] -> None
            | token :: rest when isOptionToken token ->
                match known |> List.tryFind (fun spec -> spec.Token = token) with
                | Some spec when spec.TakesValue ->
                    match rest with
                    | _value :: tail -> scan tail
                    | [] -> None
                | _ -> scan rest
            // The end-of-options separator is not itself a positional; the token after it is.
            | "--" :: rest -> scan rest
            | token :: _ -> Some token

        scan args

    // FS-GG/FS.GG.SDD#264 (Gap C finding 6 / #203): a value-taking option supplied with no following
    // value used to read as absent — `optionValue` returns `None` for a trailing valued option and the
    // command then falls back to its default (`charter --work` ran against no work id; a trailing
    // `--root` defaulted to `.`). `optionValue` consumes the token *after* an option as its value, so a
    // valued option can lack a value only when it is the final token. Walking left-to-right consuming
    // each valued option's argument — the identical scan `unrecognized`/`positional` run — the option
    // that reaches the end of the list with nothing to consume is the one missing its value. At most one
    // token can be in that position, so the result is a single option (or `None`). A valued option
    // *followed* by any token is satisfied (its value may look like a flag, exactly as `optionValue`
    // treats it), so `--title --rich` is not flagged — mirroring the value-skipping care of #196.
    let missingValue (command: SddCommand) (args: string list) =
        let known = recognized command

        let rec scan args =
            match args with
            | [] -> None
            | token :: rest when isOptionToken token ->
                match known |> List.tryFind (fun spec -> spec.Token = token) with
                | Some spec when spec.TakesValue ->
                    match rest with
                    | _value :: tail -> scan tail
                    | [] -> Some token
                | _ -> scan rest
            | _ :: rest -> scan rest

        scan args

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
