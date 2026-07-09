namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open FsCheck
open FsCheck.FSharp
open Xunit

// Feature 097 Phase 6 (T030) / ADR-0002 invariant 1 (FS.GG.SDD#201, child #260).
//
// The authored `tasks.yml` round-trip property, the sibling of `EvidenceRoundTripPropertyTests`
// (T029). For every well-formed authored tasks model `m`, `parse(render m)` reproduces `m` over
// the *authored partition*. This is the regression lock behind the tasks-side data-loss findings
// PR #272 fixed on the render side — the custom `work.title` and `publicOrToolFacingImpact: false`
// that a re-run used to silently revert (title → humanized work id, impact → `true`), plus every
// authored task field. A field that is written must be read back identically, so re-running
// `fsgg-sdd tasks` never drops an authored value.
//
// Scope of the property (per specs/097-authored-artifact-codec/data-model.md Partition B):
//   • Authored, compared:   `work.title`, `work.publicOrToolFacingImpact`, every WorkTask field
//     except its parse-assigned provenance (`Source`, `SourceLocation`), and the artifact-level
//     `acceptedDeferrals`/`advisoryNotes`/`lifecycleNotes`.
//   • Tool-owned, excluded: `sources` (recomputed each run), `findings` (re-derived tool signal),
//     `staleTaskCount`, and the canonical front matter
//     (`schemaVersion`/`workId`/`stage`/`status`/`source*`).
//
// Boundaries deliberately outside the generated domain, because the renderer is *not* the identity
// there by design (each is covered by its own tests elsewhere):
//   • an *empty* `lifecycleNotes` seeds a default note (renderScalarBlock's canned next-action
//     line), so the generator only emits non-empty notes;
//   • `publicOrToolFacingImpact` renders as a hard `true`/`false` line and reads back `Some bool`,
//     while a model `None` renders the `true` default — so the domain is `Some true`/`Some false`
//     (a `None` and an authored `Some true` are the same rendered value);
//   • `sourceIds` is upper-cased on read, so the generator emits already-upper-cased tokens; the
//     inline-list writers `List.distinct |> List.sort`, so every generated list is
    //     distinct-and-sorted — its canonical round-trip form.
    //
    // An *empty* task list is in-domain: the emitter renders the inline `tasks: []` marker (mirroring
    // `evidence: []` and the scalar note blocks), which `parseTaskFacts` reads back as no tasks — so
    // the generator emits zero-or-more tasks (FS.GG.SDD#279).
module TasksRoundTripPropertyTests =

    let private workId = "011-round-trip"
    let private tasksPath = $"work/{workId}/tasks.yml"

    let private orFail label =
        function
        | Ok value -> value
        | Error(error: string) -> failwithf "%s: %s" label error

    // The render entry ignores its `request` whenever an `existingFrontMatter` is supplied (it only
    // falls back to the request-derived title for a freshly-created file), and the property always
    // supplies one — so any Tasks request suffices for construction.
    let private tasksRequest = TestSupport.request Tasks "."

    // Tool-owned source texts drive the recomputed `sources:` block only; they are excluded from the
    // compared partition, so any non-empty texts suffice.
    let private specText = "# spec\n- FR-001: a requirement."
    let private clarificationText = "# clarifications"
    let private checklistText = "# checklist"
    let private planText = "# plan"

    // ── Generators (constrained to well-formed, canonical authored values) ───────────────

    // No whitespace, comma, quote, backslash, or newline: every string round-trips through the
    // double-quoting `yamlString` writer and the YAML reader without escaping subtleties, so a
    // failure is a genuine read/write asymmetry, not a YAML-encoding artifact (out of scope).
    let private safeChars =
        [ 'a' .. 'z' ] @ [ 'A' .. 'Z' ] @ [ '0' .. '9' ] @ [ '.'; '-'; '_' ]

    let private safeToken: Gen<string> =
        gen {
            let! n = Gen.choose (1, 8)
            let! chars = Gen.listOfLength n (Gen.elements safeChars)
            return System.String(List.toArray chars)
        }

    // A random subset of a distinct pool (each element kept independently), order preserved.
    // Rolled by hand so the property does not depend on a specific FsCheck combinator name.
    let private sublistOf (items: 'a list) : Gen<'a list> =
        gen {
            let! flags = Gen.listOfLength items.Length (Gen.elements [ true; false ])
            return List.zip items flags |> List.filter snd |> List.map fst
        }

    // `yamlInlineList`/`renderScalarBlock` both `List.distinct |> List.sort` on write while the
    // reader preserves order, so an authored string list only round-trips when already
    // distinct-and-sorted. These pools emit exactly that canonical form.
    let private tokenPool = [ "alpha"; "beta"; "gamma-1"; "d.e"; "f_g"; "h9"; "i.j.k" ]

    let private sortedDistinctTokens: Gen<string list> =
        Gen.map (List.distinct >> List.sort) (sublistOf tokenPool)

    // `sourceIds` reads back `ToUpperInvariant()`, so only already-upper-cased tokens round-trip.
    let private upperTokenPool =
        [ "FR-001"; "AC-002"; "US-003"; "SB-004"; "AMB-005"; "DEC-006" ]

    let private sortedDistinctUpperTokens: Gen<string list> =
        Gen.map (List.distinct >> List.sort) (sublistOf upperTokenPool)

    // lifecycleNotes must be non-empty: an empty list seeds the default next-action note (header).
    let private nonEmptyTokens: Gen<string list> =
        gen {
            let! head = Gen.elements tokenPool
            let! tail = sublistOf tokenPool
            return (head :: tail) |> List.distinct |> List.sort
        }

    let private idSubset (make: int -> 'id) (value: 'id -> string) : Gen<'id list> =
        Gen.map (fun idxs -> idxs |> List.map make |> List.sortBy value) (sublistOf [ 1..5 ])

    // Every task status round-trips through `taskStatusYaml`/`parseTaskStatus`. `Skipped` carries a
    // non-empty rationale, which the writer emits as a `skipRationale:` line and the reader folds
    // back into `Skipped rationale`; an empty rationale is the writer default and outside the domain.
    let private status: Gen<TaskStatus> =
        Gen.oneof
            [ Gen.constant TaskStatus.Pending
              Gen.constant TaskStatus.InProgress
              Gen.constant TaskStatus.Done
              Gen.constant TaskStatus.Stale
              Gen.map TaskStatus.Skipped safeToken ]

    // The task's own `Source` is provenance the reader assigns from the file path; it is excluded
    // from the compared partition, so any valid ref suffices for construction.
    let private provenanceSource =
        ArtifactRef.create tasksPath ArtifactKind.Tasks ArtifactOwner.Sdd false
        |> orFail "artifactRef"

    let private task (id: TaskId) : Gen<WorkTask> =
        gen {
            let! title = safeToken
            let! st = status
            let! owner = safeToken

            let! dependencies =
                idSubset (fun i -> createTaskId (sprintf "T%03d" i) |> orFail "taskId") (fun x -> x.Value)

            let! requirements =
                idSubset (fun i -> createRequirementId (sprintf "FR-%03d" i) |> orFail "reqId") (fun x -> x.Value)

            let! decisions =
                idSubset (fun i -> createDecisionId (sprintf "DEC-%03d" i) |> orFail "decId") (fun x -> x.Value)

            let! sourceIds = sortedDistinctUpperTokens
            let! requiredSkills = sortedDistinctTokens

            let! requiredEvidence =
                idSubset (fun i -> createEvidenceId (sprintf "EV%03d" i) |> orFail "evId") (fun x -> x.Value)

            return
                { Id = id
                  Title = title
                  Status = st
                  Owner = owner
                  Dependencies = dependencies
                  Requirements = requirements
                  Decisions = decisions
                  SourceIds = sourceIds
                  RequiredSkills = requiredSkills
                  RequiredEvidence = requiredEvidence
                  Source = provenanceSource
                  SourceLocation = None }
        }

    let rec private sequenceGen (gens: Gen<'a> list) : Gen<'a list> =
        match gens with
        | [] -> Gen.constant []
        | head :: tail ->
            gen {
                let! value = head
                let! rest = sequenceGen tail
                return value :: rest
            }

    // The authored inputs to a render: the two round-tripping front-matter scalars plus the task
    // list and the three note blocks. The tool-owned front matter is derived from `workId`.
    type private AuthoredTasks =
        { Title: string
          PublicOrToolFacingImpact: bool
          Tasks: WorkTask list
          AcceptedDeferrals: string list
          AdvisoryNotes: string list
          LifecycleNotes: string list }

    let private model: Gen<AuthoredTasks> =
        gen {
            let! title = safeToken
            let! impact = Gen.elements [ true; false ]
            // Zero-or-more tasks: the empty list is in-domain, rendered as the inline `tasks: []`
            // marker that round-trips to no tasks (FS.GG.SDD#279), so the generator draws a plain
            // subset of the id pool — possibly empty.
            let! idxs = sublistOf [ 1..5 ]

            let ids =
                idxs
                |> List.distinct
                |> List.map (fun i -> createTaskId (sprintf "T%03d" i) |> orFail "taskId")
                |> List.sortBy (fun (id: TaskId) -> id.Value)

            let! tasks = sequenceGen (ids |> List.map task)
            let! acceptedDeferrals = sortedDistinctTokens
            let! advisoryNotes = sortedDistinctTokens
            let! lifecycleNotes = nonEmptyTokens

            return
                { Title = title
                  PublicOrToolFacingImpact = impact
                  Tasks = tasks
                  AcceptedDeferrals = acceptedDeferrals
                  AdvisoryNotes = advisoryNotes
                  LifecycleNotes = lifecycleNotes }
        }

    // ── The property ─────────────────────────────────────────────────────────────────────

    // A minimal, valid tasks.yml parsed once as the front-matter template. Only its tool-owned
    // fields are reused; the property overrides `Title`/`PublicOrToolFacingImpact` per model, and
    // the renderer derives the rest from `workId`, so the template's own values never reach the
    // compared partition.
    let private baseFrontMatter =
        let text =
            String.concat
                "\n"
                [ "schemaVersion: 1"
                  "work:"
                  $"  id: {workId}"
                  "  title: template"
                  "  stage: tasks"
                  "  status: tasksReady"
                  $"  sourceSpec: work/{workId}/spec.md"
                  $"  sourceClarifications: work/{workId}/clarifications.md"
                  $"  sourceChecklist: work/{workId}/checklist.md"
                  $"  sourcePlan: work/{workId}/plan.md"
                  "  publicOrToolFacingImpact: true"
                  "tasks: []" ]

        match parseTaskFacts { Path = tasksPath; Text = text } with
        | Ok facts -> facts.FrontMatter
        | Error diagnostics -> failwithf "base tasks.yml did not parse: %A" diagnostics

    // The `existingFrontMatter` carrying the authored title + impact so the renderer preserves them
    // rather than regenerating; the other fields are tool-owned and never reach the compared side.
    let private existingFrontMatter (authored: AuthoredTasks) : TaskFrontMatter =
        { baseFrontMatter with
            Title = authored.Title
            PublicOrToolFacingImpact = Some authored.PublicOrToolFacingImpact }

    let private renderText (authored: AuthoredTasks) =
        TaskGraphAuthoring.tasksArtifactText
            tasksRequest
            workId
            (Some(existingFrontMatter authored))
            specText
            clarificationText
            checklistText
            planText
            authored.Tasks
            authored.AcceptedDeferrals
            []
            authored.AdvisoryNotes
            authored.LifecycleNotes

    // Project a task to its authored partition, dropping the parse-assigned provenance.
    let private authoredTask (task: WorkTask) =
        {| Id = task.Id
           Title = task.Title
           Status = task.Status
           Owner = task.Owner
           Dependencies = task.Dependencies
           Requirements = task.Requirements
           Decisions = task.Decisions
           SourceIds = task.SourceIds
           RequiredSkills = task.RequiredSkills
           RequiredEvidence = task.RequiredEvidence |}

    let private authoredPartition (authored: AuthoredTasks) =
        // Compare `publicOrToolFacingImpact` as the raw `bool option`, not `Option.defaultValue true`:
        // the generator always authors `Some`, and a well-formed render always emits the line, so a
        // regression that *dropped* the line for a `true`-valued model (parse → None) is caught here
        // too — collapsing None→true on the parsed side would mask that for every `true` seed.
        {| Title = authored.Title
           PublicOrToolFacingImpact = Some authored.PublicOrToolFacingImpact
           Tasks = authored.Tasks |> List.map authoredTask |> List.sortBy (fun t -> t.Id.Value)
           AcceptedDeferrals = authored.AcceptedDeferrals |> List.sort
           AdvisoryNotes = authored.AdvisoryNotes |> List.sort
           LifecycleNotes = authored.LifecycleNotes |}

    // The same projection over a parsed `TaskFacts`, so the two sides are compared like-for-like.
    let private parsedPartition (facts: TaskFacts) =
        {| Title = facts.FrontMatter.Title
           PublicOrToolFacingImpact = facts.FrontMatter.PublicOrToolFacingImpact
           Tasks = facts.Tasks |> List.map authoredTask |> List.sortBy (fun t -> t.Id.Value)
           AcceptedDeferrals = facts.AcceptedDeferrals
           AdvisoryNotes = facts.AdvisoryNotes
           LifecycleNotes = facts.LifecycleNotes |}

    let private roundTrips (authored: AuthoredTasks) =
        let text = renderText authored

        match parseTaskFacts { Path = tasksPath; Text = text } with
        | Error diagnostics -> failwithf "round-trip parse failed: %A\n--- rendered ---\n%s" diagnostics text
        | Ok facts -> authoredPartition authored = parsedPartition facts

    [<Fact>]
    let ``parse(render m) = m for every well-formed authored tasks model (097 FR-001/FR-005, #181)`` () =
        Check.QuickThrowOnFailure(Prop.forAll (Arb.fromGen model) roundTrips)

    // A concrete, readable anchor: one fully-populated task plus the two front-matter scalars the
    // re-run used to revert, so the regression is legible without decoding a shrunk counterexample.
    [<Fact>]
    let ``round-trip preserves the custom title, publicOrToolFacingImpact false, and every task field`` () =
        let authored =
            { Title = "Custom-Authored-Title"
              PublicOrToolFacingImpact = false
              Tasks =
                [ { Id = createTaskId "T001" |> orFail "taskId"
                    Title = "Do-the-work"
                    Status = TaskStatus.Skipped "not-needed-yet"
                    Owner = "team-sdd"
                    Dependencies = [ createTaskId "T002" |> orFail "taskId" ]
                    Requirements = [ createRequirementId "FR-001" |> orFail "reqId" ]
                    Decisions = [ createDecisionId "DEC-001" |> orFail "decId" ]
                    SourceIds = [ "AC-002"; "FR-001" ]
                    RequiredSkills = [ "fsharp"; "yaml" ]
                    RequiredEvidence = [ createEvidenceId "EV001" |> orFail "evId" ]
                    Source = provenanceSource
                    SourceLocation = None } ]
              AcceptedDeferrals = [ "deferred-1" ]
              AdvisoryNotes = [ "advisory-1" ]
              LifecycleNotes = [ "kept-note" ] }

        match
            parseTaskFacts
                { Path = tasksPath
                  Text = renderText authored }
        with
        | Error diagnostics -> failwithf "anchor round-trip parse failed: %A" diagnostics
        | Ok facts ->
            Assert.Equal(authoredPartition authored, parsedPartition facts)
            // Explicit witnesses for the two reverted-on-re-run defects, so a regression names itself.
            Assert.Equal("Custom-Authored-Title", facts.FrontMatter.Title) // title not reverted to work id
            Assert.Equal(Some false, facts.FrontMatter.PublicOrToolFacingImpact) // impact not flipped to true
            let reparsed = Assert.Single facts.Tasks
            Assert.Equal(TaskStatus.Skipped "not-needed-yet", reparsed.Status) // skipRationale round-trips
            Assert.Equal<string list>([ "AC-002"; "FR-001" ], reparsed.SourceIds)

    // The empty-task-list boundary (FS.GG.SDD#279): the emitter must render the inline `tasks: []`
    // marker — valid YAML that `parseTaskFacts` reads back as zero tasks — not `tasks:` followed by a
    // bare `[]` on the next line, which the parser rejected as an empty file.
    [<Fact>]
    let ``round-trip preserves an empty task list as inline tasks: [] (#279)`` () =
        let authored =
            { Title = "Empty-Tasks"
              PublicOrToolFacingImpact = true
              Tasks = []
              AcceptedDeferrals = []
              AdvisoryNotes = []
              LifecycleNotes = [ "kept-note" ] }

        let text = renderText authored
        Assert.Contains("tasks: []", text) // inline marker, not `tasks:\n[]`

        match parseTaskFacts { Path = tasksPath; Text = text } with
        | Error diagnostics -> failwithf "empty-tasks round-trip parse failed: %A\n--- rendered ---\n%s" diagnostics text
        | Ok facts ->
            Assert.Equal(authoredPartition authored, parsedPartition facts)
            Assert.Empty facts.Tasks
