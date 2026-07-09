namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.Internal
open FsCheck
open FsCheck.FSharp
open Xunit

// Feature 097 / ADR-0002 invariant 1 (FS.GG.SDD#201, residual #288).
//
// `EvidenceRoundTripPropertyTests` (T029) and `TasksRoundTripPropertyTests` (T030) locked the
// round-trip property for the two *YAML* authored families, which have a genuine model codec —
// one `render : model -> text` (`tasksArtifactText`/`evidenceArtifactText`) paired with a parser,
// so `parse(render m) = m` is directly expressible over the authored partition.
//
// The other four authored families — **spec / clarifications / checklist / plan** — are *Markdown*
// artifacts. The author owns the prose; there is no `render : Facts -> text`. Their emit is a fresh
// `*Template` (first run) and, on every re-run, the pure re-emit is `ensure*Sections existing.Text`
// — the function `specificationDiagnosticsTextAndSummary` and its siblings return to be written back.
// So for these four the round-trip in ADR-0002 invariant 1's own wording, `render(parse(x)) = x`,
// is the *text-space* identity: for a well-formed authored document `x`, re-running the stage must
// reproduce `x` byte-for-byte (`ensure*Sections x = x`) and must lose no authored id along the way.
// That is exactly the "silent revert on re-run" data-loss class #288 flags as still-open for these
// four, and the reason the Gap A epic closed only half-retired.
//
// This module retires it for all four. For each family, over a generated well-formed document `x`:
//   1. `parse x` succeeds (the generated document is valid).
//   2. `ensure*Sections x = x` — the re-run is byte-identity: no authored content is dropped,
//      reverted, or reordered (the anti-clobber property), and it is idempotent.
//   3. the authored ids `parse x` recovers equal the ids the generator authored — emit↔parse
//      symmetry over the id-bearing sections (a section silently dropped by a re-run would fail 2,
//      an id silently dropped by the parser would fail this).
// Plus, per family:
//   • a CRLF leg: a re-run over a CRLF copy normalizes to the LF document and preserves every id
//     (exercises `ensure`'s normalize branch, not just the all-sections-present short-circuit);
//   • a concrete append anchor: a document missing its trailing section is re-seeded by `ensure`
//     without dropping any authored id from the sections that were present (the append branch).
//
// Tool-owned/derived fields are excluded from the compared partition exactly as the YAML properties
// exclude them: `Diagnostics`, `StandardSections`, `SourceSnapshots`/digests, `Stale*Count`,
// `BlockingAmbiguityCount`. The generator draws internally-consistent "units" (each unit carries its
// own cross-referencing ids in every section it touches), so any subset is a valid document with
// distinct, sorted ids — the canonical form the parsers already impose on read.
module MarkdownArtifactRoundTripPropertyTests =

    // A generated document is a non-empty subset of the unit pool {1,2,3}, always including unit 1
    // (plan's fixed contract/obligation lines reference unit 1's decision, so it must be present).
    let private unitGen: Gen<int list> =
        gen {
            let! keep2 = Gen.elements [ true; false ]
            let! keep3 = Gen.elements [ true; false ]

            return
                [ 1
                  if keep2 then
                      2
                  if keep3 then
                      3 ]
        }

    let private snapshotOf path text : FileSnapshot = { Path = path; Text = text }

    // Assemble a document from a fixed prefix (front matter + H1 + prose, ending in a blank line)
    // and its ordered sections; an empty `lines` still emits the `## Heading` so `hasSection` holds.
    let private assemble (prefix: string) (sections: (string * string list) list) =
        let body =
            sections
            |> List.map (fun (heading, lines) -> "## " + heading + "\n" + String.concat "\n" lines)
            |> String.concat "\n\n"

        prefix + body + "\n"

    let private prefixLines lines = String.concat "\n" (lines @ [ ""; "" ])

    // 64-char sha256-shaped digests for the tool-owned Source Snapshot lines (excluded from the
    // compared partition; they only need to parse). Bound out of the interpolations because F#
    // forbids a string literal inside a single-quote interpolated fill.
    let private digestA = String.replicate 64 "a"
    let private digestB = String.replicate 64 "b"
    let private digestC = String.replicate 64 "c"

    // ── The uniform per-family case ────────────────────────────────────────────────────────────
    type private Family =
        { Name: string
          // selected units, includeTrailingSection -> full document text
          Build: int list -> bool -> string
          // text -> (missing standard sections, labelled id sets — each sorted)
          ParseIds: string -> Result<string list * (string * string list) list, string>
          // the re-run pure re-emit (`ensure*Sections`)
          Ensure: string -> string
          // the id sets the generator authored for these units — labels/order match ParseIds
          Expected: int list -> (string * string list) list
          // the heading `Build _ false` omits, re-seeded by Ensure
          TrailingSection: string }

    // ── spec ───────────────────────────────────────────────────────────────────────────────────
    let private specWorkId = "011-spec-roundtrip"
    let private specPath = $"work/{specWorkId}/spec.md"

    let private specPrefix =
        prefixLines
            [ "---"
              "schemaVersion: 1"
              $"workId: {specWorkId}"
              "title: Spec Roundtrip"
              "stage: specify"
              "changeTier: tier1"
              "status: specified"
              "publicOrToolFacingImpact: true"
              "---"
              ""
              "# Spec Roundtrip Specification"
              ""
              "Prose status: specified" ]

    let private specBuild (units: int list) (includeTrailing: bool) =
        assemble
            specPrefix
            [ "User Value", [ "Create a native command." ]
              "Scope", (units |> List.map (fun i -> sprintf "- SB-%03d: Scope boundary %d." i i))
              "Non-Goals", [ "- SB-900: No external enforcement." ]
              "User Stories", (units |> List.map (fun i -> sprintf "- US-%03d (P1): Story %d." i i))
              "Acceptance Scenarios",
              (units
               |> List.map (fun i ->
                   sprintf
                       "- AC-%03d [US-%03d] [FR-%03d]: Given %d, when the command runs, then output exists."
                       i
                       i
                       i
                       i))
              "Functional Requirements",
              (units
               |> List.map (fun i ->
                   sprintf "- FR-%03d: Requirement %d. (Stories: US-%03d; Acceptance: AC-%03d)" i i i i))
              "Ambiguities", [ "No material ambiguities recorded." ]
              "Public Or Tool-Facing Impact", [ "- Command report JSON includes facts." ]
              if includeTrailing then
                  "Lifecycle Notes", [ "- Next lifecycle action: clarify." ] ]

    let private specParseIds (text: string) =
        match parseSpecificationFacts (snapshotOf specPath text) with
        | Error diagnostics -> Error(sprintf "%A" diagnostics)
        | Ok facts ->
            Ok(
                facts.MissingStandardSections,
                [ "US", facts.UserStoryIds |> List.map Identifiers.userStoryIdValue |> List.sort
                  "FR", facts.RequirementIds |> List.map Identifiers.requirementIdValue |> List.sort
                  "AC",
                  facts.AcceptanceScenarioIds
                  |> List.map Identifiers.acceptanceScenarioIdValue
                  |> List.sort
                  "SB", facts.ScopeBoundaryIds |> List.map Identifiers.scopeBoundaryIdValue |> List.sort ]
            )

    let private specExpected (units: int list) =
        [ "US", units |> List.map (sprintf "US-%03d") |> List.sort
          "FR", units |> List.map (sprintf "FR-%03d") |> List.sort
          "AC", units |> List.map (sprintf "AC-%03d") |> List.sort
          "SB", (units |> List.map (sprintf "SB-%03d")) @ [ "SB-900" ] |> List.sort ]

    let private specFamily =
        { Name = "spec"
          Build = specBuild
          ParseIds = specParseIds
          Ensure = EarlyStageAuthoring.ensureSpecificationSections
          Expected = specExpected
          TrailingSection = "Lifecycle Notes" }

    // ── clarifications ───────────────────────────────────────────────────────────────────────────
    let private clarWorkId = "011-clar-roundtrip"
    let private clarPath = $"work/{clarWorkId}/clarifications.md"

    let private clarPrefix =
        prefixLines
            [ "---"
              "schemaVersion: 1"
              $"workId: {clarWorkId}"
              "title: Clar Roundtrip"
              "stage: clarify"
              "changeTier: tier1"
              "status: clarified"
              $"sourceSpec: work/{clarWorkId}/spec.md"
              "publicOrToolFacingImpact: true"
              "---"
              ""
              "# Clar Roundtrip Clarifications"
              ""
              "Prose status: clarified" ]

    let private clarBuild (units: int list) (includeTrailing: bool) =
        assemble
            clarPrefix
            [ "Source Specification", [ $"- work/{clarWorkId}/spec.md" ]
              "Clarification Questions",
              (units
               |> List.map (fun i ->
                   sprintf "- CQ-%03d [AMB:AMB-%03d] [FR-%03d] blocking answered: Question %d?" i i i i))
              "Answers",
              (units
               |> List.map (fun i -> sprintf "- CQ-%03d [AMB:AMB-%03d] decision: Answer %d recorded." i i i))
              "Decisions",
              (units
               |> List.map (fun i ->
                   sprintf "- DEC-%03d [CQ-%03d] [AMB:AMB-%03d] [FR-%03d]: Decision %d recorded." i i i i i))
              "Accepted Deferrals", [ "No accepted deferrals recorded." ]
              "Remaining Ambiguity", [ "- No remaining ambiguities recorded." ]
              if includeTrailing then
                  "Lifecycle Notes", [ "- Next lifecycle action: checklist." ] ]

    let private clarParseIds (text: string) =
        match parseClarificationFacts (snapshotOf clarPath text) with
        | Error diagnostics -> Error(sprintf "%A" diagnostics)
        | Ok facts ->
            Ok(
                facts.MissingStandardSections,
                [ "CQ", facts.Questions |> List.map (fun q -> q.QuestionId.Value) |> List.sort
                  "DEC", facts.Decisions |> List.map (fun d -> d.DecisionId.Value) |> List.sort ]
            )

    let private clarExpected (units: int list) =
        [ "CQ", units |> List.map (sprintf "CQ-%03d") |> List.sort
          "DEC", units |> List.map (sprintf "DEC-%03d") |> List.sort ]

    let private clarFamily =
        { Name = "clarifications"
          Build = clarBuild
          ParseIds = clarParseIds
          Ensure = EarlyStageAuthoring.ensureClarificationSections clarWorkId
          Expected = clarExpected
          TrailingSection = "Lifecycle Notes" }

    // ── checklist ────────────────────────────────────────────────────────────────────────────────
    let private chkWorkId = "011-chk-roundtrip"
    let private chkPath = $"work/{chkWorkId}/checklist.md"

    let private chkPrefix =
        prefixLines
            [ "---"
              "schemaVersion: 1"
              $"workId: {chkWorkId}"
              "title: Chk Roundtrip"
              "stage: checklist"
              "changeTier: tier1"
              "status: checklistReady"
              $"sourceSpec: work/{chkWorkId}/spec.md"
              $"sourceClarifications: work/{chkWorkId}/clarifications.md"
              "publicOrToolFacingImpact: true"
              "---"
              ""
              "# Chk Roundtrip Checklist"
              ""
              "Prose status: checklistReady" ]

    let private chkBuild (units: int list) (includeTrailing: bool) =
        assemble
            chkPrefix
            [ "Source Specification", [ $"- work/{chkWorkId}/spec.md" ]
              "Source Clarifications", [ $"- work/{chkWorkId}/clarifications.md" ]
              "Source Snapshot",
              [ $"- spec: work/{chkWorkId}/spec.md sha256:{digestA} schemaVersion:1"
                $"- clarifications: work/{chkWorkId}/clarifications.md sha256:{digestB} schemaVersion:1" ]
              "Checklist Items",
              (units
               |> List.map (fun i -> sprintf "- CHK-%03d [FR-%03d] [AC-%03d] blocking: Item %d is testable." i i i i))
              "Review Results",
              (units
               |> List.map (fun i ->
                   sprintf "- CR-%03d [CHK:CHK-%03d] [FR-%03d] [AC-%03d] pass: Item %d verified." i i i i i))
              "Accepted Deferrals", [ "No accepted deferrals recorded." ]
              "Blocking Findings", [ "No blocking findings recorded." ]
              "Advisory Notes", [ "- Advisory note." ]
              if includeTrailing then
                  "Lifecycle Notes", [ "- Next lifecycle action: plan." ] ]

    let private chkParseIds (text: string) =
        match parseChecklistFacts (snapshotOf chkPath text) with
        | Error diagnostics -> Error(sprintf "%A" diagnostics)
        | Ok facts ->
            Ok(
                facts.MissingStandardSections,
                [ "CHK", facts.Items |> List.map (fun i -> i.ItemId.Value) |> List.sort
                  "CR", facts.Results |> List.map (fun r -> r.ResultId.Value) |> List.sort ]
            )

    let private chkExpected (units: int list) =
        [ "CHK", units |> List.map (sprintf "CHK-%03d") |> List.sort
          "CR", units |> List.map (sprintf "CR-%03d") |> List.sort ]

    let private chkFamily =
        { Name = "checklist"
          Build = chkBuild
          ParseIds = chkParseIds
          Ensure = ChecklistPlanAuthoring.ensureChecklistSections chkWorkId
          Expected = chkExpected
          TrailingSection = "Lifecycle Notes" }

    // ── plan ─────────────────────────────────────────────────────────────────────────────────────
    let private planWorkId = "011-plan-roundtrip"
    let private planPath = $"work/{planWorkId}/plan.md"

    let private planPrefix =
        prefixLines
            [ "---"
              "schemaVersion: 1"
              $"workId: {planWorkId}"
              "title: Plan Roundtrip"
              "stage: plan"
              "changeTier: tier1"
              "status: planned"
              $"sourceSpec: work/{planWorkId}/spec.md"
              $"sourceClarifications: work/{planWorkId}/clarifications.md"
              $"sourceChecklist: work/{planWorkId}/checklist.md"
              "publicOrToolFacingImpact: true"
              "---"
              ""
              "# Plan Roundtrip Plan"
              ""
              "Prose status: planned" ]

    let private planBuild (units: int list) (includeTrailing: bool) =
        assemble
            planPrefix
            [ "Source Snapshot",
              [ $"- spec: work/{planWorkId}/spec.md sha256:{digestA} schemaVersion:1"
                $"- clarifications: work/{planWorkId}/clarifications.md sha256:{digestB} schemaVersion:1"
                $"- checklist: work/{planWorkId}/checklist.md sha256:{digestC} schemaVersion:1" ]
              "Plan Scope", [ $"- Work item {planWorkId} is planned." ]
              "Plan Decisions",
              (units
               |> List.map (fun i -> sprintf "- PD-%03d [FR-%03d] [AC-%03d] complete: Decision %d." i i i i))
              "Contract Impact", [ "- PC-001 [PD-001] command report: fsgg-sdd plan JSON is tool-facing." ]
              "Verification Obligations", [ "- VO-001 [PD-001] [PC-001] semanticTest: Run command tests." ]
              "Migration Posture", [ "- PM-001 [PC-001] diagnoseOnly: Plan schemaVersion 1 is accepted." ]
              "Generated View Impact", [ "- GV-001 [PD-001] workModel: work-model refreshes from plan sources." ]
              "Accepted Deferrals", [ "No accepted deferrals recorded." ]
              "Planning Findings", [ "No blocking planning findings recorded." ]
              "Advisory Notes", [ "- Advisory note." ]
              if includeTrailing then
                  "Lifecycle Notes", [ "- Next lifecycle action: tasks." ] ]

    let private planParseIds (text: string) =
        match parsePlanFacts (snapshotOf planPath text) with
        | Error diagnostics -> Error(sprintf "%A" diagnostics)
        | Ok facts ->
            Ok(
                facts.MissingStandardSections,
                [ "PD", facts.Decisions |> List.map (fun d -> d.DecisionId.Value) |> List.sort ]
            )

    let private planExpected (units: int list) =
        [ "PD", units |> List.map (sprintf "PD-%03d") |> List.sort ]

    let private planFamily =
        { Name = "plan"
          Build = planBuild
          ParseIds = planParseIds
          Ensure = ChecklistPlanAuthoring.ensurePlanSections planWorkId
          Expected = planExpected
          TrailingSection = "Lifecycle Notes" }

    // ── The properties ─────────────────────────────────────────────────────────────────────────

    // parse succeeds, the re-run is byte-identity + idempotent, and every authored id round-trips.
    let private reRunRoundTrips (family: Family) (units: int list) =
        let text = family.Build units true

        match family.ParseIds text with
        | Error message ->
            failwithf "%s: generated document did not parse: %s\n--- document ---\n%s" family.Name message text
        | Ok(missing, ids) ->
            if not (List.isEmpty missing) then
                failwithf
                    "%s: generated document reports missing sections %A\n--- document ---\n%s"
                    family.Name
                    missing
                    text

            let ensured = family.Ensure text
            let expected = family.Expected units
            ids = expected && ensured = text && family.Ensure ensured = ensured

    // A re-run over a CRLF copy normalizes to the LF document and preserves every authored id.
    let private crlfReRunNormalizes (family: Family) (units: int list) =
        let text = family.Build units true
        let ensured = family.Ensure(text.Replace("\n", "\r\n"))

        match family.ParseIds ensured with
        | Error message -> failwithf "%s: CRLF re-run did not parse: %s" family.Name message
        | Ok(missing, ids) -> List.isEmpty missing && ids = family.Expected units && ensured = text

    // The append branch: a document missing its trailing section is re-seeded without dropping ids.
    let private appendReSeedsWithoutDroppingIds (family: Family) =
        let units = [ 1; 2; 3 ]
        let trimmed = family.Build units false

        match family.ParseIds trimmed with
        | Error message -> failwithf "%s: trimmed document did not parse: %s" family.Name message
        | Ok(missing, _) -> Assert.Contains(family.TrailingSection, missing)

        let ensured = family.Ensure trimmed

        match family.ParseIds ensured with
        | Error message -> failwithf "%s: re-seeded document did not parse: %s" family.Name message
        | Ok(missing, ids) ->
            Assert.Empty missing
            Assert.True((ids = family.Expected units), sprintf "%s: re-seed dropped ids: got %A" family.Name ids)

    // ── spec ─────────────────────────────────────────────────────────────────────────────────────
    [<Fact>]
    let ``spec re-run is byte-identity and every authored id round-trips (097 FR-001/FR-005, #288)`` () =
        Check.QuickThrowOnFailure(Prop.forAll (Arb.fromGen unitGen) (reRunRoundTrips specFamily))

    [<Fact>]
    let ``spec CRLF re-run normalizes to the LF document without dropping ids`` () =
        Check.QuickThrowOnFailure(Prop.forAll (Arb.fromGen unitGen) (crlfReRunNormalizes specFamily))

    [<Fact>]
    let ``spec re-run re-seeds a dropped trailing section without dropping authored ids`` () =
        appendReSeedsWithoutDroppingIds specFamily

    // ── clarifications ───────────────────────────────────────────────────────────────────────────
    [<Fact>]
    let ``clarifications re-run is byte-identity and every authored id round-trips (097 FR-001/FR-005, #288)`` () =
        Check.QuickThrowOnFailure(Prop.forAll (Arb.fromGen unitGen) (reRunRoundTrips clarFamily))

    [<Fact>]
    let ``clarifications CRLF re-run normalizes to the LF document without dropping ids`` () =
        Check.QuickThrowOnFailure(Prop.forAll (Arb.fromGen unitGen) (crlfReRunNormalizes clarFamily))

    [<Fact>]
    let ``clarifications re-run re-seeds a dropped trailing section without dropping authored ids`` () =
        appendReSeedsWithoutDroppingIds clarFamily

    // ── checklist ────────────────────────────────────────────────────────────────────────────────
    [<Fact>]
    let ``checklist re-run is byte-identity and every authored id round-trips (097 FR-001/FR-005, #288)`` () =
        Check.QuickThrowOnFailure(Prop.forAll (Arb.fromGen unitGen) (reRunRoundTrips chkFamily))

    [<Fact>]
    let ``checklist CRLF re-run normalizes to the LF document without dropping ids`` () =
        Check.QuickThrowOnFailure(Prop.forAll (Arb.fromGen unitGen) (crlfReRunNormalizes chkFamily))

    [<Fact>]
    let ``checklist re-run re-seeds a dropped trailing section without dropping authored ids`` () =
        appendReSeedsWithoutDroppingIds chkFamily

    // ── plan ─────────────────────────────────────────────────────────────────────────────────────
    [<Fact>]
    let ``plan re-run is byte-identity and every authored id round-trips (097 FR-001/FR-005, #288)`` () =
        Check.QuickThrowOnFailure(Prop.forAll (Arb.fromGen unitGen) (reRunRoundTrips planFamily))

    [<Fact>]
    let ``plan CRLF re-run normalizes to the LF document without dropping ids`` () =
        Check.QuickThrowOnFailure(Prop.forAll (Arb.fromGen unitGen) (crlfReRunNormalizes planFamily))

    [<Fact>]
    let ``plan re-run re-seeds a dropped trailing section without dropping authored ids`` () =
        appendReSeedsWithoutDroppingIds planFamily
