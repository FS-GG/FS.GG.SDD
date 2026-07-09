namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Commands.Internal
open FsCheck
open FsCheck.FSharp
open Xunit

// Feature 097 Phase 6 (T029) / ADR-0002 invariant 1 (FS.GG.SDD#201, child #260).
//
// The authored `evidence.yml` round-trip property: for every well-formed authored evidence
// model `m`, `parse(render m)` reproduces `m` over the *authored partition*. This is the
// regression lock behind the data-loss findings #180/#181/#182 that PR #272 fixed on the
// render side — a field that is written must be read back identically, so re-running
// `fsgg-sdd evidence` never silently drops an authored value.
//
// Scope of the property (per specs/097-authored-artifact-codec/data-model.md Partition A):
//   • Authored, compared:   every EvidenceDeclaration field except its parse-assigned
//     provenance (`Source`, `SourceLocation`, and each sourceRef's `SourceLocation`), plus
//     the artifact-level `lifecycleNotes`.
//   • Tool-owned, excluded: `sourceSnapshots` (recomputed each run) and the canonical
//     front matter (`schemaVersion`/`workId`/`stage`/`status`/`source*`).
//
// Two boundaries are deliberately outside the generated domain because the renderer is *not*
// the identity there, by design — and both are covered by their own tests elsewhere:
//   • an *empty* `lifecycleNotes` seeds a default note (renderScalarBlock's canned
//     `defaultEvidenceLifecycleNote`), so the generator only emits non-empty notes;
//   • the writer omits an absent optional scalar rather than emitting `null` (feature 091),
//     which the reader collapses to the same `None` — so a `None` and a bare `null` are the
//     same authored value. The generator ranges every optional present/absent and also emits
//     a quoted `Some "null"` to pin that a *quoted* null survives as the literal string (#180).
module EvidenceRoundTripPropertyTests =

    let private workId = "011-round-trip"
    let private evidencePath = $"work/{workId}/evidence.yml"

    let private orFail label =
        function
        | Ok value -> value
        | Error(error: string) -> failwithf "%s: %s" label error

    // A minimal, valid evidence.yml parsed once as the record template. Only its tool-owned
    // front matter is reused; the property overrides `Evidence`/`LifecycleNotes` per model and
    // the renderer derives the front matter from `workId`, so the template's own values never
    // reach the compared partition.
    let private baseArtifact =
        let text =
            String.concat
                "\n"
                [ "schemaVersion: 1"
                  $"workId: {workId}"
                  "stage: evidence"
                  "status: evidenceReady"
                  $"sourceSpec: work/{workId}/spec.md"
                  $"sourceClarifications: work/{workId}/clarifications.md"
                  $"sourceChecklist: work/{workId}/checklist.md"
                  $"sourcePlan: work/{workId}/plan.md"
                  $"sourceTasks: work/{workId}/tasks.yml"
                  $"sourceAnalysis: readiness/{workId}/analysis.json"
                  "sourceSnapshots: []"
                  "evidence: []"
                  "lifecycleNotes: []" ]

        match parseEvidenceArtifact { Path = evidencePath; Text = text } with
        | Ok artifact -> artifact
        | Error diagnostics -> failwithf "base evidence.yml did not parse: %A" diagnostics

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

    // Optional scalars read null-aware: absent (None) and a quoted literal `"null"` are distinct
    // authored values and both must survive (#180).
    let private optScalar: Gen<string option> =
        Gen.oneof [ Gen.constant None; Gen.map Some safeToken; Gen.constant (Some "null") ]

    // A random subset of a distinct pool (each element kept independently), order preserved.
    // Rolled by hand so the property does not depend on a specific FsCheck combinator name.
    let private sublistOf (items: 'a list) : Gen<'a list> =
        gen {
            let! flags = Gen.listOfLength items.Length (Gen.elements [ true; false ])
            return List.zip items flags |> List.filter snd |> List.map fst
        }

    // `yamlInlineList` / `renderScalarBlock` both `List.distinct |> List.sort` on write while the
    // reader preserves order, so an authored list only round-trips to itself when it is already
    // distinct-and-sorted. The generators below emit exactly that canonical form.
    let private tokenPool = [ "alpha"; "beta"; "gamma-1"; "d.e"; "f_g"; "h9"; "i.j.k" ]

    let private sortedDistinctTokens: Gen<string list> =
        Gen.map (List.distinct >> List.sort) (sublistOf tokenPool)

    // lifecycleNotes must be non-empty: an empty list seeds the default note (see header).
    let private nonEmptyTokens: Gen<string list> =
        gen {
            let! head = Gen.elements tokenPool
            let! tail = sublistOf tokenPool
            return (head :: tail) |> List.distinct |> List.sort
        }

    let private idSubset (make: int -> 'id) (value: 'id -> string) : Gen<'id list> =
        Gen.map (fun idxs -> idxs |> List.map make |> List.sortBy value) (sublistOf [ 1..5 ])

    let private mkArtifactRef path =
        ArtifactRef.create path (ArtifactKind.Other "evidenceArtifact") ArtifactOwner.Sdd false
        |> orFail "artifactRef"

    // The declaration's own `Source` is provenance the reader assigns from the file path; it is
    // excluded from the compared partition, so any valid ref suffices for construction.
    let private provenanceSource = mkArtifactRef evidencePath

    let private artifactPathPool =
        [ "docs/a.md"; "docs/b.md"; "src/c.fs"; "notes/d.txt"; "e.md" ]

    let private artifactRefSubset: Gen<ArtifactRef list> =
        // Sort/distinct by the *normalized* path (post-`create`), matching how the writer maps
        // each ref to its path and then `List.distinct |> List.sort`s the strings.
        Gen.map
            (fun paths ->
                paths
                |> List.map mkArtifactRef
                |> List.distinctBy (fun ref -> ref.Path)
                |> List.sortBy (fun ref -> ref.Path))
            (sublistOf artifactPathPool)

    let private sourceRef: Gen<EvidenceSourceReference> =
        gen {
            let! kind = safeToken
            let! id = optScalar
            let! path = optScalar
            let! uri = optScalar
            let! digest = optScalar
            let! related = optScalar
            let! result = optScalar

            return
                { ReferenceId = id
                  Kind = kind
                  Path = path
                  Uri = uri
                  Digest = digest
                  RelatedSourceId = related
                  Result = result
                  SourceLocation = None }
        }

    let private sourceRefs: Gen<EvidenceSourceReference list> =
        gen {
            let! count = Gen.choose (0, 3)
            return! Gen.listOfLength count sourceRef
        }

    // A required, non-whitespace scalar that also ranges the quoted literal `"null"`, so a
    // parser that collapsed a *quoted* `standsInFor: "null"`/`reason: "null"` back to `None`
    // (dropping an authored value, invariant 3 / #180) is caught here too — the disclosure's
    // inner scalars otherwise never carry the literal. (A *bare*-null disclosure is a
    // reader-robustness concern the renderer can never emit, and is pinned by the dedicated
    // `parseSyntheticDisclosure` reader tests, not by this round-trip property.)
    let private nonNullSafeToken: Gen<string> =
        Gen.oneof [ safeToken; Gen.constant "null" ]

    let private disclosure: Gen<SyntheticDisclosure option> =
        Gen.oneof
            [ Gen.constant None
              gen {
                  let! standsInFor = nonNullSafeToken
                  let! reason = nonNullSafeToken

                  return
                      Some
                          { StandsInFor = standsInFor
                            Reason = reason }
              } ]

    let private evidenceKind: Gen<EvidenceKind> =
        Gen.elements
            [ EvidenceKind.Implementation
              EvidenceKind.Verification
              EvidenceKind.Review
              EvidenceKind.GeneratedViewEvidence
              EvidenceKind.Synthetic
              EvidenceKind.Deferral
              EvidenceKind.Note
              EvidenceKind.Missing ]

    // A subject `type: task`/`requirement` prepends the subject id into the task/requirement ref
    // set on read; the property avoids that reader-side merge by generating other subject types,
    // so the ref lists round-trip in isolation. (The merge itself is covered by scaffold tests.)
    let private subjectType: Gen<string> =
        Gen.elements [ "component"; "artifact"; "concept"; "surface"; "module" ]

    let private evidenceResult: Gen<string> =
        Gen.elements [ "pass"; "fail"; "deferred"; "missing"; "stale"; "advisory"; "blocked" ]

    let private declaration (id: EvidenceId) : Gen<EvidenceDeclaration> =
        gen {
            let! kind = evidenceKind
            let! subjType = subjectType
            let! subjId = safeToken
            let! taskRefs = idSubset (fun i -> createTaskId (sprintf "T%03d" i) |> orFail "taskId") (fun x -> x.Value)

            let! requirementRefs =
                idSubset (fun i -> createRequirementId (sprintf "FR-%03d" i) |> orFail "reqId") (fun x -> x.Value)

            let! acceptanceRefs =
                idSubset (fun i -> createAcceptanceScenarioId (sprintf "AC-%03d" i) |> orFail "acId") (fun x -> x.Value)

            let! decisionRefs =
                idSubset (fun i -> createDecisionId (sprintf "DEC-%03d" i) |> orFail "decId") (fun x -> x.Value)

            let! checklistRefs =
                idSubset (fun i -> createChecklistResultId (sprintf "CR-%03d" i) |> orFail "crId") (fun x -> x.Value)

            let! planRefs =
                idSubset (fun i -> createPlanDecisionId (sprintf "PD-%03d" i) |> orFail "pdId") (fun x -> x.Value)

            let! obligationRefs = sortedDistinctTokens
            let! artifactRefs = artifactRefSubset
            let! refs = sourceRefs
            let! result = evidenceResult
            let! synthetic = Gen.elements [ true; false ]
            let! syntheticDisclosure = disclosure
            let! rationale = optScalar
            let! owner = optScalar
            let! scope = optScalar
            let! visibility = optScalar
            let! notes = sortedDistinctTokens

            return
                { Id = id
                  Kind = kind
                  Subject = { SubjectType = subjType; Id = subjId }
                  TaskRefs = taskRefs
                  RequirementRefs = requirementRefs
                  AcceptanceScenarioRefs = acceptanceRefs
                  ClarificationDecisionRefs = decisionRefs
                  ChecklistResultRefs = checklistRefs
                  PlanDecisionRefs = planRefs
                  ObligationRefs = obligationRefs
                  ArtifactRefs = artifactRefs
                  SourceRefs = refs
                  Result = result
                  Synthetic = synthetic
                  SyntheticDisclosure = syntheticDisclosure
                  Rationale = rationale
                  Owner = owner
                  Scope = scope
                  LaterLifecycleVisibility = visibility
                  Notes = notes
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

    let private model: Gen<EvidenceArtifact> =
        gen {
            let! indices = sublistOf [ 1..5 ]

            let ids =
                indices
                |> List.map (fun i -> createEvidenceId (sprintf "EV%03d" i) |> orFail "evId")
                |> List.sortBy (fun (id: EvidenceId) -> id.Value)

            let! declarations = sequenceGen (ids |> List.map declaration)
            let! lifecycleNotes = nonEmptyTokens

            return
                { baseArtifact with
                    Evidence = declarations
                    LifecycleNotes = lifecycleNotes
                    SourceSnapshots = [] }
        }

    // ── The property ─────────────────────────────────────────────────────────────────────

    // Project a declaration to its authored partition, dropping parse-assigned provenance
    // (`Source`, `SourceLocation`, and each sourceRef's `SourceLocation`).
    let private authored (declaration: EvidenceDeclaration) =
        {| Id = declaration.Id
           Kind = declaration.Kind
           Subject = declaration.Subject
           TaskRefs = declaration.TaskRefs
           RequirementRefs = declaration.RequirementRefs
           AcceptanceScenarioRefs = declaration.AcceptanceScenarioRefs
           ClarificationDecisionRefs = declaration.ClarificationDecisionRefs
           ChecklistResultRefs = declaration.ChecklistResultRefs
           PlanDecisionRefs = declaration.PlanDecisionRefs
           ObligationRefs = declaration.ObligationRefs
           ArtifactRefs = declaration.ArtifactRefs
           SourceRefs =
            declaration.SourceRefs
            |> List.map (fun ref -> { ref with SourceLocation = None })
           Result = declaration.Result
           Synthetic = declaration.Synthetic
           SyntheticDisclosure = declaration.SyntheticDisclosure
           Rationale = declaration.Rationale
           Owner = declaration.Owner
           Scope = declaration.Scope
           LaterLifecycleVisibility = declaration.LaterLifecycleVisibility
           Notes = declaration.Notes |}

    let private authoredPartition (artifact: EvidenceArtifact) =
        artifact.Evidence |> List.map authored |> List.sortBy (fun d -> d.Id.Value), artifact.LifecycleNotes

    let private renderText (artifact: EvidenceArtifact) =
        HandlersEvidence.evidenceArtifactText workId artifact (HandlersEvidence.evidenceSummary workId artifact [])

    let private roundTrips (artifact: EvidenceArtifact) =
        let text = renderText artifact

        match parseEvidenceArtifact { Path = evidencePath; Text = text } with
        | Error diagnostics -> failwithf "round-trip parse failed: %A\n--- rendered ---\n%s" diagnostics text
        | Ok parsed -> authoredPartition artifact = authoredPartition parsed

    [<Fact>]
    let ``parse(render m) = m for every well-formed authored evidence model (097 FR-001/FR-005, #180/#181)`` () =
        Check.QuickThrowOnFailure(Prop.forAll (Arb.fromGen model) roundTrips)

    // A concrete, readable anchor: one fully-populated declaration exercising every authored
    // field at once — including the three sourceRef ids that #181 dropped and a quoted `"null"`
    // that #180 mishandled — so the regression is legible without decoding a shrunk counterexample.
    [<Fact>]
    let ``round-trip preserves every authored field, the #181 sourceRef ids, and a quoted null`` () =
        let declaration =
            { Id = createEvidenceId "EV001" |> orFail "evId"
              Kind = EvidenceKind.Verification
              Subject =
                { SubjectType = "component"
                  Id = "renderer" }
              TaskRefs = [ createTaskId "T001" |> orFail "taskId" ]
              RequirementRefs = [ createRequirementId "FR-001" |> orFail "reqId" ]
              AcceptanceScenarioRefs = [ createAcceptanceScenarioId "AC-001" |> orFail "acId" ]
              ClarificationDecisionRefs = [ createDecisionId "DEC-001" |> orFail "decId" ]
              ChecklistResultRefs = [ createChecklistResultId "CR-001" |> orFail "crId" ]
              PlanDecisionRefs = [ createPlanDecisionId "PD-001" |> orFail "pdId" ]
              ObligationRefs = [ "OBL-1"; "OBL-2" ]
              ArtifactRefs = [ mkArtifactRef "docs/a.md" ]
              SourceRefs =
                [ { ReferenceId = Some "src-1"
                    Kind = "artifact"
                    Path = Some "docs/a.md"
                    Uri = None
                    Digest = Some "deadbeef"
                    RelatedSourceId = Some "src-0"
                    Result = Some "null"
                    SourceLocation = None } ]
              Result = "pass"
              Synthetic = true
              SyntheticDisclosure =
                Some
                    { StandsInFor = "null"
                      Reason = "stub" }
              Rationale = Some "why"
              Owner = Some "team"
              Scope = None
              LaterLifecycleVisibility = Some "null"
              Notes = [ "n1"; "n2" ]
              Source = provenanceSource
              SourceLocation = None }

        let artifact =
            { baseArtifact with
                Evidence = [ declaration ]
                LifecycleNotes = [ "kept-note" ]
                SourceSnapshots = [] }

        match
            parseEvidenceArtifact
                { Path = evidencePath
                  Text = renderText artifact }
        with
        | Error diagnostics -> failwithf "anchor round-trip parse failed: %A" diagnostics
        | Ok parsed ->
            Assert.Equal(authoredPartition artifact, authoredPartition parsed)
            // Explicit witnesses for the two fixed defects, so a regression names itself.
            let reparsed = Assert.Single parsed.Evidence
            let reparsedRef = Assert.Single reparsed.SourceRefs
            Assert.Equal(Some "src-1", reparsedRef.ReferenceId) // #181
            Assert.Equal(Some "deadbeef", reparsedRef.Digest) // #181
            Assert.Equal(Some "src-0", reparsedRef.RelatedSourceId) // #181
            Assert.Equal(Some "null", reparsedRef.Result) // #180: quoted null is the literal string
            // #180: a *quoted* disclosure scalar survives as the literal, keeping the gate honest.
            Assert.Equal(
                Some
                    { StandsInFor = "null"
                      Reason = "stub" },
                reparsed.SyntheticDisclosure
            )

            Assert.Equal<string list>([ "kept-note" ], parsed.LifecycleNotes) // #1: authored note not clobbered
