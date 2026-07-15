namespace FS.GG.SDD.Commands.Internal

open System
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Artifacts.Serialization
open FS.GG.SDD.Artifacts.WorkModel
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal.Foundation

module internal EarlyStageAuthoring =
    // Pure `Path` string ops only — the effectful `File`/`Directory` surface stays at the
    // `CommandEffects` edge and is deliberately kept out of scope in the MVU pure core.
    type private Path = System.IO.Path

    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module IdentifiersModule = FS.GG.SDD.Artifacts.Identifiers

    let parseCharterFrontMatter path text =
        match splitFrontMatter text with
        | None -> Error(malformedCharterFrontMatter path "Charter is missing YAML front matter.")
        | Some(yaml, _) ->
            match
                tryScalar "schemaVersion" yaml,
                tryScalar "workId" yaml,
                tryScalar "title" yaml,
                tryScalar "stage" yaml,
                tryScalar "changeTier" yaml,
                tryScalar "status" yaml
            with
            | Some schemaVersion, Some workId, Some title, Some stage, Some changeTier, Some status ->
                Ok
                    { SchemaVersion = schemaVersion
                      WorkId = workId
                      Title = title
                      Stage = stage
                      ChangeTier = changeTier
                      Status = status }
            | _ -> Error(malformedCharterFrontMatter path "Charter front matter is incomplete.")

    let titleFromWorkId (workId: string) =
        workId.Split('-', StringSplitOptions.RemoveEmptyEntries)
        |> Array.skipWhile (fun part -> part |> Seq.forall Char.IsDigit)
        |> fun parts ->
            if Array.isEmpty parts then
                workId.Split('-', StringSplitOptions.RemoveEmptyEntries)
            else
                parts
        |> Array.map (fun part ->
            if part.Length = 0 then
                part
            else
                Char.ToUpperInvariant(part.[0]).ToString() + part.Substring(1))
        |> String.concat " "

    let private nonBlank (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            Some(value.Trim())

    /// A front-matter scalar that survives its own re-parse.
    ///
    /// `titleFromSpec` feeds a YAML-*decoded* title into an unquoted `title:` slot, so a spec whose front
    /// matter legally reads `title: "Plan: upstream snapshot"` would emit `title: Plan: upstream snapshot`
    /// — not a YAML scalar — and `clarify` would block on the file it had just written, reporting the
    /// unhelpful "Clarification front matter is empty." A leading `#` is worse: it parses as a comment,
    /// silently yielding an empty title.
    ///
    /// Quote only when the plain form would not round-trip, so the common `title: Ambient audio bed` keeps
    /// its exact bytes and no committed artifact churns.
    let yamlFrontMatterScalar (value: string) =
        // An allowlist, not a blocklist: YAML's plain-scalar rules are subtle enough that enumerating the
        // unsafe cases invites exactly the miss this guards against (a *trailing* `:` ends the scalar just
        // as `: ` does mid-string). Anything outside the boring alphanumeric-with-punctuation set is quoted.
        let isSafeChar c =
            Char.IsLetterOrDigit c || "-_. ()/'".Contains(c: char)

        let plainIsSafe =
            not (String.IsNullOrEmpty value)
            && value = value.Trim()
            && Char.IsLetterOrDigit value.[0]
            && value |> Seq.forall isSafeChar

        if plainIsSafe then
            value
        else
            "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""

    let requestTitle (request: CommandRequest) workId =
        request.Title
        |> Option.bind nonBlank
        |> Option.defaultValue (titleFromWorkId workId)

    /// Title for an artifact derived from an existing specification: the author's explicit `--title`,
    /// else the specification's own front-matter title, else the humanized work id. The middle rung is
    /// what keeps a derived artifact's `title:` agreeing with the `spec.md` its `sourceSpec:` points at
    /// (#164) — the work id is a last resort, not a default.
    let titleFromSpec (request: CommandRequest) (specFacts: SpecificationFacts) workId =
        request.Title
        |> Option.bind nonBlank
        |> Option.orElseWith (fun () -> nonBlank specFacts.FrontMatter.Title)
        |> Option.defaultValue (titleFromWorkId workId)

    let numberedId prefix index = sprintf "%s-%03d" prefix (index + 1)

    let hasSection (heading: string) (text: string) =
        Regex.IsMatch(text, $"(?m)^##\\s+{Regex.Escape heading}\\s*$")

    /// The `ensure` half of every markdown hybrid's merge, for all five stages (#309). A heading the
    /// policy names but the file lacks is appended, rendered by the stage; a heading already present
    /// is left exactly as the author left it, body and all. This function never removes or rewrites.
    ///
    /// The policy supplies the headings, so a stage cannot ensure a section it does not declare —
    /// which is what makes `HybridArtifact`'s tag and its merge agree by construction.
    let ensureSections (policy: MergePolicy) (sectionText: string -> string) (text: string) =
        let normalized =
            (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

        let missing =
            MergePolicy.ensuredSections policy
            |> List.filter (fun heading -> not (hasSection heading normalized))

        if List.isEmpty missing then
            normalized
        else
            let suffix = missing |> List.map sectionText |> String.concat "\n"
            let trimmed = normalized.TrimEnd()
            $"{trimmed}\n\n{suffix}"

    let candidateSnapshots model =
        model.InterpretedEffects
        |> List.choose (fun result ->
            match result.Effect, result.Snapshot with
            | ReadFile path, Some snapshot when
                path.EndsWith("/charter.md", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/spec.md", StringComparison.OrdinalIgnoreCase)
                ->
                Some(
                    { snapshot with
                        Path = normalizeRelativePath path }
                )
            | _ -> None)

    let duplicateWorkIdDiagnostics workId model =
        candidateSnapshots model
        |> List.choose (fun snapshot ->
            if snapshot.Path.StartsWith($"work/{workId}/", StringComparison.OrdinalIgnoreCase) then
                None
            elif snapshot.Path.EndsWith("/charter.md", StringComparison.OrdinalIgnoreCase) then
                match parseCharterFrontMatter snapshot.Path snapshot.Text with
                | Ok frontMatter when String.Equals(frontMatter.WorkId, workId, StringComparison.OrdinalIgnoreCase) ->
                    Some snapshot.Path
                | _ -> None
            elif snapshot.Path.EndsWith("/spec.md", StringComparison.OrdinalIgnoreCase) then
                match parseWorkItemMetadata snapshot with
                | Ok metadata when String.Equals(metadata.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase) ->
                    Some snapshot.Path
                | _ -> None
            else
                None)
        |> function
            | [] -> []
            | paths -> [ duplicateWorkId workId paths ]

    let projectDiagnostics model =
        let project = snapshot ".fsgg/project.yml" model
        let sdd = snapshot ".fsgg/sdd.yml" model
        let agents = snapshot ".fsgg/agents.yml" model

        match project, sdd, agents with
        | None, None, None -> [ outsideProject () ]
        | _ ->
            let missing =
                [ if Option.isNone project then
                      missingProjectConfig ".fsgg/project.yml"
                  if Option.isNone sdd then
                      missingSddConfig ".fsgg/sdd.yml"
                  if Option.isNone agents then
                      missingAgentsConfig ".fsgg/agents.yml" ]

            let malformed =
                [ match project with
                  | Some snapshot ->
                      match parseProjectConfig snapshot with
                      | Ok _ -> ()
                      | Error _ -> yield malformedProjectConfig snapshot.Path
                  | None -> ()

                  match sdd with
                  | Some snapshot ->
                      match parseSddLifecyclePolicy snapshot with
                      | Ok _ -> ()
                      | Error _ -> yield malformedSddConfig snapshot.Path
                  | None -> ()

                  match agents with
                  | Some snapshot ->
                      match parseAgentGuidanceConfig snapshot with
                      | Ok _ -> ()
                      | Error _ -> yield malformedAgentsConfig snapshot.Path
                  | None -> () ]

            missing @ malformed

    let nextScopedIndex prefix (text: string) =
        Regex.Matches(text, $@"\b{Regex.Escape prefix}-(\d{{3,}})\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m ->
            match Int32.TryParse m.Groups.[1].Value with
            | true, value -> Some value
            | _ -> None)
        |> Seq.fold max 0
        |> (+) 1

    let scopedId prefix index = sprintf "%s-%03d" prefix index

    let inputLines (request: CommandRequest) =
        request.InputText
        |> Option.defaultValue ""
        |> fun text -> text.Replace("\r\n", "\n").Split('\n')
        |> Array.map (fun line -> line.Trim().TrimStart('-', '*').Trim())
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> Array.toList

    let idMatches pattern (text: string) =
        Regex.Matches(text, pattern, RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.map (fun m -> m.Value.ToUpperInvariant())
        |> Seq.distinct
        |> Seq.toList

    let appendToSection heading lines text =
        if List.isEmpty lines then
            text
        else
            let normalized =
                (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

            let split = normalized.Split('\n') |> Array.toList
            let headingPattern = $"^##\\s+{Regex.Escape heading}\\s*$"

            match split |> List.tryFindIndex (fun line -> Regex.IsMatch(line, headingPattern)) with
            | None ->
                let sectionBody = String.concat "\n" lines
                let suffix = $"## {heading}\n{sectionBody}"
                $"{normalized.TrimEnd()}\n\n{suffix}\n"
            | Some start ->
                let next =
                    split
                    |> List.mapi (fun index line -> index, line)
                    |> List.tryFind (fun (index, line) -> index > start && Regex.IsMatch(line, "^##\\s+"))
                    |> Option.map fst
                    |> Option.defaultValue split.Length

                let before = split |> List.take next
                let after = split |> List.skip next
                // Join the appended lines onto the section's existing content, *before* the
                // blank-line separator that precedes the next heading. Splicing after that blank
                // instead (the old `before @ lines @ after`) split the markdown list in two and
                // abutted the next `##` heading with no separating blank line (#211).
                let trimmedBefore =
                    before |> List.rev |> List.skipWhile (fun line -> line.Trim() = "") |> List.rev

                let separator = if List.isEmpty after then [] else [ "" ]
                (trimmedBefore @ lines @ separator @ after) |> String.concat "\n"

    // Replace the body of an existing section (the lines between its heading and the next
    // `##` heading) with the supplied lines, preserving the heading and the blank-line
    // separators. If the section is absent it is appended. Used to purge and re-derive
    // machine-generated checklist sections on a stale re-run (§3.1).
    let replaceSectionBody heading (bodyLines: string list) (text: string) =
        let normalized =
            (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

        let split = normalized.Split('\n') |> Array.toList
        let headingPattern = $"^##\\s+{Regex.Escape heading}\\s*$"

        match split |> List.tryFindIndex (fun line -> Regex.IsMatch(line, headingPattern)) with
        | None ->
            let sectionBody = String.concat "\n" bodyLines
            $"{normalized.TrimEnd()}\n\n## {heading}\n{sectionBody}\n"
        | Some start ->
            let next =
                split
                |> List.mapi (fun index line -> index, line)
                |> List.tryFind (fun (index, line) -> index > start && Regex.IsMatch(line, "^##\\s+"))
                |> Option.map fst
                |> Option.defaultValue split.Length

            let before = split |> List.take (start + 1)
            let after = split |> List.skip next
            // Re-insert a single blank-line separator before the next heading (or trailing).
            (before @ bodyLines @ [ "" ] @ after) |> String.concat "\n"

    // The policy-driven form of the two primitives above (#309). `headings` comes from a
    // `MergePolicy`, so the sections a stage may rewrite are the sections its write tag declares —
    // drop a heading from the policy and that region reverts to the author, with no code change.
    // `bodies` must cover every heading the policy names; `MergePolicyTests` pins that, so a miss
    // here is a tool defect and `Map.find` says so rather than silently blanking a section.
    let private mergeSections merge (bodies: Map<string, string list>) headings text =
        headings
        |> List.fold (fun acc heading -> merge heading (Map.find heading bodies) acc) text

    let replaceSectionBodies bodies headings text =
        mergeSections replaceSectionBody bodies headings text

    let appendToSections bodies headings text =
        mergeSections appendToSection bodies headings text

    /// Rewrite the body of an existing section (the lines between its heading and the next `##`)
    /// through `transform`, which sees only the section's content lines (blanks stripped) and
    /// returns the lines to keep. Blank lines are passed through to `transform` and preserved, so a
    /// retirement pass removes exactly the lines it targets and reformats nothing else (FR-014); a
    /// single blank-line separator is normalized before the next heading. A section that is absent,
    /// or one whose transform keeps every line, is left byte-identical.
    let transformSectionBody heading (transform: string list -> string list) (text: string) =
        let normalized =
            (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

        let split = normalized.Split('\n') |> Array.toList
        let headingPattern = $"^##\\s+{Regex.Escape heading}\\s*$"

        match split |> List.tryFindIndex (fun line -> Regex.IsMatch(line, headingPattern)) with
        | None -> normalized
        | Some start ->
            let next =
                split
                |> List.mapi (fun index line -> index, line)
                |> List.tryFind (fun (index, line) -> index > start && Regex.IsMatch(line, "^##\\s+"))
                |> Option.map fst
                |> Option.defaultValue split.Length

            let before = split |> List.take (start + 1)
            let after = split |> List.skip next
            let body = split |> List.skip (start + 1) |> List.take (next - start - 1)

            // Blank lines ride through the transform untouched: a retirement pass drops the lines it
            // targets and nothing else. Stripping them here would reflow the operator's prose —
            // collapsing paragraph breaks, and blank lines inside a fenced block — on every pass
            // (FR-014), and would make an otherwise byte-identical re-run report a changed artifact.
            let kept = transform body

            let trimmed =
                kept |> List.rev |> List.skipWhile String.IsNullOrWhiteSpace |> List.rev

            // Restore the single blank-line separator only when a following heading needs one.
            let separator = if List.isEmpty after then [] else [ "" ]

            let rebuilt = if List.isEmpty trimmed then [] else trimmed @ separator

            if rebuilt = body then
                normalized
            else
                (before @ rebuilt @ after) |> String.concat "\n"
