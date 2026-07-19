namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion
open YamlDotNet.RepresentationModel

[<AutoOpen>]
module Evidence =
    type EvidenceKind =
        | Implementation
        | Verification
        | Review
        | GeneratedViewEvidence
        | Synthetic
        | Deferral
        | Note
        | Missing

    type EvidenceSubject = { SubjectType: string; Id: string }

    type EvidenceSourceSnapshot =
        { Label: string
          Path: string
          Digest: string option
          SchemaVersion: int option
          SourceLocation: SourceLocation option }

    type EvidenceSourceReference =
        { ReferenceId: string option
          Kind: string
          Path: string option
          Uri: string option
          Digest: string option
          RelatedSourceId: string option
          Result: string option
          SourceLocation: SourceLocation option }

    type SyntheticDisclosure = { StandsInFor: string; Reason: string }

    /// A run the tool **read**, rather than a `pass` an agent **typed** (FS.GG.SDD#350, ADR-0035).
    ///
    /// Recorded by `evidence --from-test-report` from a runner-produced report (TRX / JUnit XML): SDD opens
    /// the file, parses it, and hashes its bytes. Every field here is derived from that report — none
    /// is authored, and `Digest` in particular cannot be supplied by the author.
    ///
    /// This does NOT make evidence unforgeable, and must not be sold as if it did. It moves the bar
    /// from an assertion to an artifact of a declared format, whose counts must agree and whose file
    /// must still be on disk at `verify` (via `citedArtifactPaths` → the #349 cascade). Trusting the
    /// receipt's *provenance* is CI's job; deciding what an unobserved obligation costs is
    /// Governance's (ADR-0035 §3).
    type ObservedRun =
        { Source: string
          Digest: string
          Outcome: string
          Passed: int
          Failed: int
          Skipped: int }

    type EvidenceDeclaration =
        {
            Id: EvidenceId
            Kind: EvidenceKind
            Subject: EvidenceSubject
            TaskRefs: TaskId list
            RequirementRefs: RequirementId list
            AcceptanceScenarioRefs: AcceptanceScenarioId list
            ClarificationDecisionRefs: DecisionId list
            ChecklistResultRefs: ChecklistResultId list
            PlanDecisionRefs: PlanDecisionId list
            ObligationRefs: string list
            ArtifactRefs: ArtifactRef list
            SourceRefs: EvidenceSourceReference list
            Result: string
            Synthetic: bool
            SyntheticDisclosure: SyntheticDisclosure option
            /// FS.GG.SDD#350: the receipt, when a run was observed. `None` is the honest state for an
            /// obligation discharged on the author's word — it is what `isSelfAttested` counts.
            ObservedRun: ObservedRun option
            Rationale: string option
            Owner: string option
            Scope: string option
            LaterLifecycleVisibility: string option
            Notes: string list
            Source: ArtifactRef
            SourceLocation: SourceLocation option
        }

    type EvidenceObligation =
        { ObligationId: string
          Kind: string
          SourceArtifactPath: string
          SourceId: string option
          LinkedTaskIds: TaskId list
          LinkedRequirementIds: RequirementId list
          LinkedDecisionIds: string list
          // Feature 077: the originating task's full source-id lineage bag, carried verbatim so
          // scaffolding can grammar-route it into the declaration's typed ref buckets. Recovers
          // the plan-decision id (and any FR it traces to) that task.Requirements/task.Decisions
          // drop for a plan-decision task.
          LinkedSourceIds: string list
          ExpectedEvidenceKinds: string list
          // WI-4 (ADR-0048): the "real test kind ∧ synthetic:false" gate a classified {gameplay}
          // FR obligation carries. Non-empty ⇒ satisfied only by a non-synthetic pass whose kind is
          // one of these. Empty (every other obligation) ⇒ no kind restriction — additive and
          // backward-compatible.
          RequiredEvidenceKinds: string list
          RequiredSkillOrCapabilityTags: string list
          Blocking: bool
          Correction: string }

    type EvidenceArtifact =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Stage: LifecycleStage
          Status: string
          SourceSpec: string
          SourceClarifications: string
          SourceChecklist: string
          SourcePlan: string
          SourceTasks: string
          SourceAnalysis: string
          SourceSnapshots: EvidenceSourceSnapshot list
          Evidence: EvidenceDeclaration list
          LifecycleNotes: string list
          Diagnostics: Diagnostic list }

    let parseEvidenceKind (value: string) =
        match
            if String.IsNullOrEmpty value then
                ""
            else
                value.Trim().ToLowerInvariant()
        with
        | "implementation" -> Implementation
        | "verification" -> Verification
        | "review" -> Review
        | "generated-view" -> GeneratedViewEvidence
        | "generatedview" -> GeneratedViewEvidence
        | "synthetic" -> Synthetic
        | "deferral" -> Deferral
        | "note" -> Note
        | "missing" -> Missing
        | _ -> Verification

    // The inverse serialization mappings, moved here from HandlersEvidence (Commands) so the shared
    // `EvidenceCodec.declarationFields` can drive both the reader and the renderer over one list
    // (FS.GG.SDD#260). Pure functions; every existing call site resolves unchanged via AutoOpen.
    let evidenceKindSourceValue kind =
        match kind with
        | EvidenceKind.Implementation -> "implementation"
        | EvidenceKind.Verification -> "verification"
        | EvidenceKind.Review -> "review"
        | EvidenceKind.GeneratedViewEvidence -> "generated-view"
        | EvidenceKind.Synthetic -> "synthetic"
        | EvidenceKind.Deferral -> "deferral"
        | EvidenceKind.Note -> "note"
        | EvidenceKind.Missing -> "missing"

    let allowedEvidenceResults =
        [ "pass"; "fail"; "deferred"; "missing"; "stale"; "advisory"; "blocked" ]
        |> Set.ofList

    // FS-GG/FS.GG.SDD#306: the skill tag that marks a task — and therefore the obligation minted
    // from it — as discharged by rendering a frame and looking at it. It lives here, in Artifacts,
    // because the task generator that stamps it and the evidence handler that reads it back off the
    // obligation sit in different modules of `Commands` and must agree on one literal.
    let visualInspectionSkill = "visual-inspection"

    /// Does this obligation's skill/capability tag set mark it a visual-inspection obligation?
    let isVisualInspectionTagged (tags: string list) =
        tags
        |> List.exists (fun tag -> String.Equals(tag, visualInspectionSkill, StringComparison.OrdinalIgnoreCase))

    // WI-4 (ADR-0048): the FR classification facet that carries the per-FR non-synthetic test
    // obligation. It is one of `RequirementModel.recognizedRequirementClasses` (currently the only
    // one); named here because it is *this* class — not the vocabulary at large — that the task
    // generator maps to a gameplay-test obligation.
    let gameplayClassification = "gameplay"

    // WI-4 (ADR-0048): the capability tag marking a task — and the obligation minted from it — as a
    // per-classified-FR gameplay test obligation, discharged only by a real, non-synthetic test. It
    // lives here, in Artifacts, for the same reason as `visualInspectionSkill`: the task generator
    // that stamps it and the evidence/verify handlers that read it back off the obligation sit in
    // different modules of `Commands` and must agree on one literal.
    let gameplayTestCapability = "gameplay-test"

    /// The evidence kinds that count as a *real test* for a classified-FR obligation (ADR-0048). A
    /// gameplay obligation is satisfied only by one of these kinds with a non-synthetic pass — the
    /// single source of truth for the derived obligation's `RequiredEvidenceKinds`.
    let realTestEvidenceKinds = [ "verification" ]

    /// Does this obligation's skill/capability tag set mark it a classified-FR gameplay obligation?
    let isGameplayTestTagged (tags: string list) =
        tags
        |> List.exists (fun tag -> String.Equals(tag, gameplayTestCapability, StringComparison.OrdinalIgnoreCase))

    let private evidenceArtifactRef path =
        tryArtifact path (ArtifactKind.Other "evidenceArtifact") ArtifactOwner.Sdd false

    /// The raw authored `sourceRefs[].path` scalars of one evidence mapping. Read from the YAML
    /// rather than from the parsed declaration so a malformed path can be named back to the author.
    let private sourceRefPaths mapping =
        trySequenceAt [ "sourceRefs" ] mapping
        |> Option.map (fun sequence ->
            sequence.Children
            |> Seq.choose tryMapping
            |> Seq.choose (fun node -> tryScalarAt [ "path" ] node)
            |> List.ofSeq)
        |> Option.defaultValue []

    /// The one lexical containment rule for every CITED path — `artifacts:` and `sourceRefs[].path`
    /// alike. `ArtifactRef.create` already encodes it (repository-relative, no `..`); this states it
    /// once, totally, so it can be *reported* rather than thrown or skipped.
    ///
    /// Both cited buckets needed it and neither had it:
    ///   * `artifacts:` reached the rule only by RAISING out of the pure core, so a `..` was
    ///     reported to the author as a tool defect (#359);
    ///   * `sourceRefs[].path` never reached the rule at all — it is a raw scalar — so a `..` chain
    ///     escaped the workspace and let a file OUTSIDE the repository discharge the #349
    ///     cited-artifact gate (#365). `citedArtifactPaths` reads both buckets; only one was checked.
    let citedPathIsContained (path: string) = evidenceArtifactRef path |> Result.isOk

    /// Does this declaration name a rendered artifact — an `artifacts:` entry, or a `sourceRefs[]`
    /// entry carrying a `path` or a `uri`? Blank strings do not count (FS.GG.SDD#306, FR-004).
    let namesRenderedArtifact (declaration: EvidenceDeclaration) =
        let named (value: string) = not (String.IsNullOrWhiteSpace value)

        declaration.ArtifactRefs |> List.exists (fun ref -> named ref.Path)
        || declaration.SourceRefs
           |> List.exists (fun source -> (source.Path |> Option.exists named) || (source.Uri |> Option.exists named))

    let normalizedEvidenceResult (result: string) =
        (if String.IsNullOrEmpty result then
             ""
         else
             result.Trim().ToLowerInvariant())

    /// The visual-inspection artifact rule (FS.GG.SDD#306, FR-004), stated once. A declaration that
    /// claims a real, non-synthetic pass while naming no rendered artifact asserts that someone
    /// looked at a frame that does not exist. Three call sites read this — the `evidence` pre-write
    /// gate, the `ED-` disposition cascade, and the `TD-` mirror — so the rule cannot drift between
    /// what blocks and what the readiness view records.
    ///
    /// A disclosed synthetic pass and a deferral both fall outside it: neither claims a real pass.
    let passesWithoutRenderedArtifact (declaration: EvidenceDeclaration) =
        normalizedEvidenceResult declaration.Result = "pass"
        && not declaration.Synthetic
        && not (namesRenderedArtifact declaration)

    /// WI-4 (ADR-0048): does this declaration satisfy a required-evidence-kind gate — a real,
    /// non-synthetic pass whose kind is one of `requiredKinds`? A synthetic pass never satisfies (the
    /// epic's core rule: synthetic state can never discharge a gameplay obligation), and neither does
    /// a non-test kind (e.g. `implementation`). Stated once so the `ED-` disposition cascade and its
    /// `TD-` verify mirror cannot drift on what discharges a classified-FR obligation.
    let satisfiesRequiredEvidenceKinds (requiredKinds: string list) (declaration: EvidenceDeclaration) =
        normalizedEvidenceResult declaration.Result = "pass"
        && not declaration.Synthetic
        && List.contains (evidenceKindSourceValue declaration.Kind) requiredKinds

    /// FS.GG.SDD#349 (FR-002). Both path-bearing buckets, because `namesRenderedArtifact` above
    /// discharges an obligation from either one: checking only `artifacts:` would leave the
    /// identical hole one field to the left, and an author who writes the phantom path into
    /// `sourceRefs` would pass exactly as before. `uri` is not a local file and is never probed.
    let citedArtifactPaths (declaration: EvidenceDeclaration) =
        let named (value: string) = not (String.IsNullOrWhiteSpace value)

        // `ArtifactRefs` are already contained by construction (they only exist if `ArtifactRef.create`
        // accepted them). `SourceRefs[].path` is a raw authored scalar, so it is filtered by the same
        // rule HERE, before the caller plans a probe for it: an escaping path is malformed input and is
        // blocked by `malformedArtifactPath`, never statted (#365 — the probe used to resolve `..`
        // right out of the workspace, so an out-of-repo file could discharge this very gate).
        [ for ref in declaration.ArtifactRefs do
              if named ref.Path then
                  ref.Path
          for source in declaration.SourceRefs do
              match source.Path with
              | Some path when named path && citedPathIsContained path -> path
              | _ -> ()
          // FS.GG.SDD#350 (FR-009). The receipt's report IS a cited local path, so it belongs in the
          // same bucket — and then the #349 cascade probes it for free. A report deleted *after* the
          // receipt was recorded turns its obligation `invalid` at `verify`, the merge boundary,
          // rather than only at authoring time. That is what "compare against reality, not against a
          // record of reality" means for a receipt: the record is not self-certifying.
          match declaration.ObservedRun with
          | Some run when named run.Source && citedPathIsContained run.Source -> run.Source
          | _ -> () ]
        |> List.distinct
        |> List.sort

    /// The cited-artifact existence rule (FS.GG.SDD#349, FR-006/FR-007), stated once. A declaration
    /// that claims a real, non-synthetic pass while citing a file that is not on disk asserts that
    /// something was proven by an artifact nobody can open.
    ///
    /// Gated on the satisfaction rule (`pass` ∧ not synthetic) *inside* the rule, so that the three
    /// call sites — the `evidence` pre-write gate, the `ED-` cascade, and the `TD-` mirror — cannot
    /// drift on which declarations are held to it. A deferral legitimately cites an artifact that
    /// does not exist yet; blocking it would teach authors to stop deferring.
    let missingCitedArtifacts (exists: string -> bool) (declaration: EvidenceDeclaration) =
        if normalizedEvidenceResult declaration.Result <> "pass" || declaration.Synthetic then
            []
        else
            citedArtifactPaths declaration |> List.filter (exists >> not)

    /// The attestation-basis rule (FS.GG.SDD#398, FR-001/FR-002), stated once for `verify`'s
    /// dispositions, `ship`'s counters, and the committed `ship-verdict.json`.
    ///
    /// FS.GG.SDD#350 / ADR-0035: this now reads a **receipt** — a run `evidence --from-test-report` opened,
    /// parsed, and hashed — rather than returning the constant `false` that #398 left as the seam.
    /// It was written as a function precisely so that this body could change alone: every counter
    /// downstream is computed from it, so `observed` rises and `selfAttested` falls with no schema,
    /// projection, or consumer touched.
    ///
    /// A receipt counts only when the run it records actually passed. `Failed = 0` is checked
    /// alongside `Outcome`, so a receipt that says `passed` while carrying failures — which
    /// `TestReport.parse` cannot produce, but a hand-authored evidence.yml can — never discharges an
    /// obligation here. (It is also blocked outright, as `observedRunInconsistent`; this is the
    /// belt to that braces, and it keeps the rule true when read in isolation.)
    ///
    /// Total and I/O-free: the read happened at the effect edge, and only its *result* reaches here.
    let isObserved (declaration: EvidenceDeclaration) =
        declaration.ObservedRun
        |> Option.exists (fun run ->
            // Normalised, NOT compared raw. `observedRunInconsistency` below trims and lowercases
            // before judging the same field, so an authored `outcome: Passed` reads as coherent
            // there. Comparing it raw here would then silently answer `false` — no diagnostic, no
            // explanation, and an obligation quietly demoted to `selfAttested` despite carrying a
            // receipt the tool just told the author was fine. Two rules over one field have to agree
            // on what the field says.
            let outcome = run.Outcome.Trim().ToLowerInvariant()

            outcome = "passed" && run.Failed = 0 && run.Passed > 0)

    /// The receipt's internal-consistency rule (FS.GG.SDD#350, FR-005). `TestReport.parse` derives
    /// `Outcome` from the counts, so a *recorded* receipt cannot fail this. An **authored** one can:
    /// `evidence.yml` is a text file, and a hand-written `observedRun` is user input like any other.
    /// Rejecting it here is what stops the receipt from becoming a new place to type `pass`.
    ///
    /// Returns the reason, or `None` when the receipt is coherent.
    let observedRunInconsistency (run: ObservedRun) : string option =
        let normalizedOutcome = run.Outcome.Trim().ToLowerInvariant()

        // The recorded form: `sha256:` + the 64-hex digest `SchemaVersion.sha256Text` produces.
        let wellFormedDigest =
            Regex.IsMatch(run.Digest, @"^sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant)

        if run.Passed < 0 || run.Failed < 0 || run.Skipped < 0 then
            Some "a run count is negative"
        elif run.Passed + run.Failed = 0 then
            // The authored twin of `TestReport.parse`'s no-executed-tests refusal. A receipt claiming a
            // run in which nothing executed is not a receipt — and left unblocked it would be the
            // cheapest possible forgery, needing no report at all. `skipped` is not execution.
            Some $"the run executed no tests (passed: {run.Passed}, failed: {run.Failed})"
        elif normalizedOutcome <> "passed" && normalizedOutcome <> "failed" then
            Some $"outcome '{run.Outcome}' is not 'passed' or 'failed'"
        elif normalizedOutcome = "passed" && run.Failed > 0 then
            Some $"outcome 'passed' contradicts failed: {run.Failed}"
        elif normalizedOutcome = "failed" && run.Failed = 0 then
            Some "outcome 'failed' contradicts failed: 0"
        elif not wellFormedDigest then
            Some $"digest '{run.Digest}' is not a sha256:<hex> digest"
        elif String.IsNullOrWhiteSpace run.Source then
            Some "source names no report"
        else
            None

    /// Does this declaration claim a real pass — `result: pass`, not disclosed `synthetic`? The
    /// satisfaction rule, named once because the attestation split below partitions exactly it.
    let claimsRealPass (declaration: EvidenceDeclaration) =
        normalizedEvidenceResult declaration.Result = "pass"
        && not declaration.Synthetic

    /// Does this declaration discharge its obligation on the author's word alone? (FS.GG.SDD#398.)
    /// The exact complement of `isObserved` over the satisfaction rule, so that
    /// `supported = selfAttested + observed` holds by construction, not by coincidence (FR-007).
    let isSelfAttested (declaration: EvidenceDeclaration) =
        claimsRealPass declaration && not (isObserved declaration)

    /// Was an *obligation* — matched by these declarations — discharged by an observed run?
    /// (FS.GG.SDD#398, FR-003.) The one rule `verify`, `ship`, and the committed verdict all read.
    ///
    /// Two decisions are load-bearing. They were written while `isObserved` was constantly `false`,
    /// and were moot then; FS.GG.SDD#350 made them live, and both now do real work:
    ///
    ///   * **Only the declarations that claim a real pass are consulted.** A `supported` obligation
    ///     may also carry a deferral or an advisory alongside the pass that supports it; those say
    ///     nothing about *how* it was supported, and folding them in would report every mixed
    ///     obligation as self-attested regardless of what was run.
    ///   * **`forall`, not `exists`.** An obligation backed by one observed run *and* one
    ///     hand-asserted pass is NOT observed. `exists` would let the observed declaration launder
    ///     the self-attested one out of the count — a disclosure that under-reports self-attestation
    ///     fails open, which is precisely the defect class this feature sits in.
    let obligationIsObserved (declarations: EvidenceDeclaration list) =
        let passes = declarations |> List.filter claimsRealPass

        not (List.isEmpty passes) && passes |> List.forall isObserved

    let parseArtifactRefs values =
        // Total: a rejected path is DROPPED here rather than raised, and is reported as malformed
        // user input from the raw YAML by `parseEvidenceArtifact` — so nothing is silently lost.
        values |> List.choose (evidenceArtifactRef >> Result.toOption)

    let parseEvidenceSourceSnapshots root =
        trySequenceAt [ "sourceSnapshots" ] root
        |> Option.map (fun sequence ->
            sequence.Children
            |> Seq.mapi (fun index node ->
                node
                |> tryMapping
                |> Option.map (fun mapping ->
                    // `digest`/`schemaVersion` are `option` because absence is meaningful:
                    // an absent digest means "not snapshotted", not "the empty digest".
                    // Read null-aware (FS.GG.SDD#182) so a bare-null token is absence rather
                    // than `Some "null"`, and blank-aware so an empty value — plain (`digest:`)
                    // or quoted (`digest: ''`), which `isPlainNullScalar` deliberately does not
                    // treat as null — is absence too. Either read as `Some ""` would make
                    // `evidenceSourceSnapshotStale` compare "" against the real digest as a
                    // permanent, unfixable mismatch, and would re-render as a trailing-whitespace
                    // `digest: ` line. Unlike `rationale`, an empty digest is never a real value.
                    { Label = tryScalarAt [ "label" ] mapping |> Option.defaultValue ""
                      Path = tryScalarAt [ "path" ] mapping |> Option.defaultValue ""
                      Digest =
                        tryScalarNonNullAt [ "digest" ] mapping
                        |> Option.filter (String.IsNullOrWhiteSpace >> not)
                      SchemaVersion =
                        tryScalarNonNullAt [ "schemaVersion" ] mapping
                        |> Option.bind (fun value ->
                            match Int32.TryParse value with
                            | true, parsed -> Some parsed
                            | _ -> None)
                      SourceLocation = sourceLocation (index + 1) }))
            |> Seq.choose id
            |> Seq.toList)
        |> Option.defaultValue []

    // Shared field lists — ADR-0002 invariant 1 / FR-007 (FS.GG.SDD#201, #260). One `FieldCodec`
    // list per authored record drives BOTH the reader here and the renderer in `HandlersEvidence`,
    // so a field can no longer be read without being written or vice versa — the read/write
    // asymmetry behind #180 (bare-null disclosure) and #181 (dropped `id`/`digest`/`relatedSourceId`)
    // becomes unrepresentable. Optional scalars read null-aware (a bare-null token is absence; a
    // quoted "null" survives as the literal string).
    module EvidenceCodec =
        let sourceRefSeed: EvidenceSourceReference =
            { ReferenceId = None
              Kind = "artifact"
              Path = None
              Uri = None
              Digest = None
              RelatedSourceId = None
              Result = None
              SourceLocation = None }

        let sourceRefFields: ArtifactCodec.FieldCodec<EvidenceSourceReference> list =
            [ ArtifactCodec.defaultedScalar "kind" "artifact" (fun r -> r.Kind) (fun v r -> { r with Kind = v })
              ArtifactCodec.optionalScalar "id" (fun r -> r.ReferenceId) (fun v r -> { r with ReferenceId = v })
              ArtifactCodec.optionalScalar "path" (fun r -> r.Path) (fun v r -> { r with Path = v })
              ArtifactCodec.optionalScalar "uri" (fun r -> r.Uri) (fun v r -> { r with Uri = v })
              ArtifactCodec.optionalScalar "digest" (fun r -> r.Digest) (fun v r -> { r with Digest = v })
              ArtifactCodec.optionalScalar "relatedSourceId" (fun r -> r.RelatedSourceId) (fun v r ->
                  { r with RelatedSourceId = v })
              ArtifactCodec.optionalScalar "result" (fun r -> r.Result) (fun v r -> { r with Result = v }) ]

        // The disclosure's inner scalars read null-aware into an option-carrying draft (#180); the
        // caller lifts a fully-populated, non-blank draft to `Some SyntheticDisclosure` and everything
        // else (bare null, absence, blank) to `None`, so the undisclosed-synthetic gate stays honest.
        type DisclosureDraft =
            { StandsInFor: string option
              Reason: string option }

        let disclosureDraftSeed = { StandsInFor = None; Reason = None }

        let disclosureFields: ArtifactCodec.FieldCodec<DisclosureDraft> list =
            [ ArtifactCodec.optionalScalar "standsInFor" (fun d -> d.StandsInFor) (fun v d ->
                  { d with StandsInFor = v })
              ArtifactCodec.optionalScalar "reason" (fun d -> d.Reason) (fun v d -> { d with Reason = v }) ]

        // The disclosure draft <-> field projection (the #180 gate lives in `lift`): a blank/partial
        // draft lifts to None (undisclosed), a fully-populated one to Some.
        let liftDisclosure (draft: DisclosureDraft) : SyntheticDisclosure option =
            match draft.StandsInFor, draft.Reason with
            | Some standsInFor, Some reason when
                not (String.IsNullOrWhiteSpace standsInFor)
                && not (String.IsNullOrWhiteSpace reason)
                ->
                Some
                    { StandsInFor = standsInFor
                      Reason = reason }
            | _ -> None

        let lowerDisclosure (d: SyntheticDisclosure) : DisclosureDraft =
            { StandsInFor = Some d.StandsInFor
              Reason = Some d.Reason }

        // FS.GG.SDD#350. The receipt reads through a draft for the same reason the disclosure does:
        // its two identifying scalars are null-aware, and a partial/blank mapping must lift to `None`
        // (no receipt) rather than to a receipt made of empty strings. An empty receipt that still
        // said "observed" would be the fail-open this feature exists to close.
        //
        // The counts are NOT option-carrying: a receipt with a source and a digest but a junk count
        // reads as `0`, and `observedRunInconsistency` then decides whether that is coherent —
        // rather than the codec silently dropping the whole receipt over one bad token.
        type ObservedRunDraft =
            { Source: string option
              Digest: string option
              Outcome: string option
              Passed: int
              Failed: int
              Skipped: int }

        let observedRunDraftSeed =
            { Source = None
              Digest = None
              Outcome = None
              Passed = 0
              Failed = 0
              Skipped = 0 }

        let observedRunFields: ArtifactCodec.FieldCodec<ObservedRunDraft> list =
            [ ArtifactCodec.optionalScalar "source" (fun r -> r.Source) (fun v r -> { r with Source = v })
              ArtifactCodec.optionalScalar "digest" (fun r -> r.Digest) (fun v r -> { r with Digest = v })
              ArtifactCodec.optionalScalar "outcome" (fun r -> r.Outcome) (fun v r -> { r with Outcome = v })
              ArtifactCodec.intScalar "passed" 0 (fun r -> r.Passed) (fun v r -> { r with Passed = v })
              ArtifactCodec.intScalar "failed" 0 (fun r -> r.Failed) (fun v r -> { r with Failed = v })
              ArtifactCodec.intScalar "skipped" 0 (fun r -> r.Skipped) (fun v r -> { r with Skipped = v }) ]

        // A receipt exists only if it names BOTH what was read and the hash of what was read. Either
        // one alone is not a receipt: a source with no digest is a filename, and a digest with no
        // source is a number. Both blank/absent → `None`, and the obligation is self-attested.
        let liftObservedRun (draft: ObservedRunDraft) : ObservedRun option =
            match draft.Source, draft.Digest with
            | Some source, Some digest when
                not (String.IsNullOrWhiteSpace source) && not (String.IsNullOrWhiteSpace digest)
                ->
                Some
                    { Source = source
                      Digest = digest
                      Outcome = draft.Outcome |> Option.defaultValue ""
                      Passed = draft.Passed
                      Failed = draft.Failed
                      Skipped = draft.Skipped }
            | _ -> None

        let lowerObservedRun (run: ObservedRun) : ObservedRunDraft =
            { Source = Some run.Source
              Digest = Some run.Digest
              Outcome = Some run.Outcome
              Passed = run.Passed
              Failed = run.Failed
              Skipped = run.Skipped }

        let subjectSeed: EvidenceSubject = { SubjectType = "task"; Id = "" }

        let subjectFields: ArtifactCodec.FieldCodec<EvidenceSubject> list =
            [ ArtifactCodec.defaultedScalar "type" "task" (fun s -> s.SubjectType) (fun v s ->
                  { s with SubjectType = v })
              ArtifactCodec.defaultedScalar "id" "" (fun s -> s.Id) (fun v s -> { s with Id = v }) ]

        // A placeholder declaration; the semantic layer in `parseEvidenceArtifact` overwrites `Id`,
        // `Source`, and `SourceLocation` (parse provenance) and applies the subject-type ref merge
        // after `foldInto`, so these seed values never reach the decoded result.
        let declarationSeed: EvidenceDeclaration =
            { Id = { Value = "EV000" }
              Kind = Verification
              Subject = subjectSeed
              TaskRefs = []
              RequirementRefs = []
              AcceptanceScenarioRefs = []
              ClarificationDecisionRefs = []
              ChecklistResultRefs = []
              PlanDecisionRefs = []
              ObligationRefs = []
              ArtifactRefs = []
              SourceRefs = []
              Result = "pending"
              Synthetic = false
              SyntheticDisclosure = None
              ObservedRun = None
              Rationale = None
              Owner = None
              Scope = None
              LaterLifecycleVisibility = None
              Notes = []
              Source = sourceArtifact "work/seed/evidence.yml" ArtifactKind.Evidence
              SourceLocation = None }

        // The whole authored declaration, in emission order — `id` first, so the artifact's `evidence`
        // `recordList` frames each item as `  - id: …`. One list drives both the reader and the
        // renderer (FR-007). The semantic layer still validates `id` (malformed → skip + diagnostic)
        // and re-applies it after decode; typed-id ref lists read leniently — the malformed-ref
        // diagnostics stay the semantic layer's job.
        let declarationFields: ArtifactCodec.FieldCodec<EvidenceDeclaration> list =
            [ ArtifactCodec.requiredScalar "id" (fun d -> d.Id.Value) (fun v d -> { d with Id = { Value = v } })
              ArtifactCodec.mappedScalar "kind" evidenceKindSourceValue parseEvidenceKind (fun d -> d.Kind) (fun v d ->
                  { d with Kind = v })
              ArtifactCodec.nested "subject" subjectFields subjectSeed (fun d -> d.Subject) (fun v d ->
                  { d with Subject = v })
              ArtifactCodec.refList
                  "taskRefs"
                  Identifiers.createTaskId
                  (fun (id: TaskId) -> id.Value)
                  (fun d -> d.TaskRefs)
                  (fun v d -> { d with TaskRefs = v })
              ArtifactCodec.refList
                  "requirementRefs"
                  Identifiers.createRequirementId
                  (fun (id: RequirementId) -> id.Value)
                  (fun d -> d.RequirementRefs)
                  (fun v d -> { d with RequirementRefs = v })
              ArtifactCodec.refList
                  "acceptanceScenarioRefs"
                  Identifiers.createAcceptanceScenarioId
                  (fun (id: AcceptanceScenarioId) -> id.Value)
                  (fun d -> d.AcceptanceScenarioRefs)
                  (fun v d -> { d with AcceptanceScenarioRefs = v })
              ArtifactCodec.refList
                  "clarificationDecisionRefs"
                  Identifiers.createDecisionId
                  (fun (id: DecisionId) -> id.Value)
                  (fun d -> d.ClarificationDecisionRefs)
                  (fun v d -> { d with ClarificationDecisionRefs = v })
              ArtifactCodec.refList
                  "checklistResultRefs"
                  Identifiers.createChecklistResultId
                  (fun (id: ChecklistResultId) -> id.Value)
                  (fun d -> d.ChecklistResultRefs)
                  (fun v d -> { d with ChecklistResultRefs = v })
              ArtifactCodec.refList
                  "planDecisionRefs"
                  Identifiers.createPlanDecisionId
                  (fun (id: PlanDecisionId) -> id.Value)
                  (fun d -> d.PlanDecisionRefs)
                  (fun v d -> { d with PlanDecisionRefs = v })
              ArtifactCodec.alwaysInlineList
                  "obligationRefs"
                  (fun d -> d.ObligationRefs)
                  // The reader distinct+sorts obligationRefs to match the pre-codec parser (the
                  // renderer already distinct+sorts every inline list); notes deliberately do not.
                  (fun v d ->
                      { d with
                          ObligationRefs = v |> List.distinct |> List.sort })
              ArtifactCodec.alwaysInlineList
                  "artifacts"
                  (fun d -> d.ArtifactRefs |> List.map (fun (a: ArtifactRef) -> a.Path))
                  (fun v d ->
                      { d with
                          ArtifactRefs = parseArtifactRefs v })
              ArtifactCodec.recordList "sourceRefs" sourceRefFields sourceRefSeed (fun d -> d.SourceRefs) (fun v d ->
                  { d with SourceRefs = v })
              ArtifactCodec.mappedScalar "result" normalizedEvidenceResult id (fun d -> d.Result) (fun v d ->
                  { d with Result = v })
              ArtifactCodec.boolScalar "synthetic" false (fun d -> d.Synthetic) (fun v d -> { d with Synthetic = v })
              ArtifactCodec.optionalNestedVia
                  "syntheticDisclosure"
                  disclosureFields
                  disclosureDraftSeed
                  liftDisclosure
                  lowerDisclosure
                  (fun d -> d.SyntheticDisclosure)
                  (fun v d -> { d with SyntheticDisclosure = v })
              // FS.GG.SDD#350. Recorded by `evidence --from-test-report`, never authored — but it round-trips
              // through the SAME shared field list as everything else, so it cannot be written without
              // being read (ADR-0002 invariant 1). A receipt the renderer emitted and the reader
              // dropped would silently un-observe every obligation on the next `evidence` run.
              ArtifactCodec.optionalNestedVia
                  "observedRun"
                  observedRunFields
                  observedRunDraftSeed
                  liftObservedRun
                  lowerObservedRun
                  (fun d -> d.ObservedRun)
                  (fun v d -> { d with ObservedRun = v })
              ArtifactCodec.optionalScalar "rationale" (fun d -> d.Rationale) (fun v d -> { d with Rationale = v })
              ArtifactCodec.optionalScalar "owner" (fun d -> d.Owner) (fun v d -> { d with Owner = v })
              ArtifactCodec.optionalScalar "scope" (fun d -> d.Scope) (fun v d -> { d with Scope = v })
              ArtifactCodec.optionalScalar "laterLifecycleVisibility" (fun d -> d.LaterLifecycleVisibility) (fun v d ->
                  { d with LaterLifecycleVisibility = v })
              ArtifactCodec.alwaysInlineList "notes" (fun d -> d.Notes) (fun v d -> { d with Notes = v }) ]

    // `parseEvidenceSourceRefs`/`parseSyntheticDisclosure` were retired when the declaration moved onto
    // `declarationFields` (FS.GG.SDD#260): its `recordList "sourceRefs"` and
    // `optionalNestedVia "syntheticDisclosure"` now own both directions for those records.

    let workIdFromEvidencePath (path: string) =
        let normalized = normalizePath path
        let parts = normalized.Split([| '/' |], StringSplitOptions.RemoveEmptyEntries)

        if parts.Length >= 3 && parts.[0] = "work" then
            parts.[1]
        else
            "unknown-work"

    // FS.GG.SDD#560: an evidence ref whose value fails ITS field's id class but is a well-formed id
    // of ANOTHER class is MISFILED, not malformed — the prefix already names the field it belongs in.
    // The retrospective's case: `tasks.yml` lists `sourceIds: [CR-008, PD-010]` together, so the
    // author copied CR-### checklist-result ids into `clarificationDecisionRefs` and got a generic
    // "not a well-formed decision id" that named neither the id class nor the field it belonged in.
    // Classify the raw value against every id class that HAS an evidence ref field; a match to a
    // DIFFERENT field is the misfile.
    let private evidenceRefField (value: string) =
        // Each row collapses its `Result<'a, _>` to a bool so the list is homogeneous — the id classes
        // are distinct types, but here we only care whether the value parses as that class.
        [ Identifiers.createTaskId value |> Result.isOk, "task", "taskRefs"
          Identifiers.createRequirementId value |> Result.isOk, "requirement", "requirementRefs"
          Identifiers.createAcceptanceScenarioId value |> Result.isOk, "acceptance-scenario", "acceptanceScenarioRefs"
          Identifiers.createDecisionId value |> Result.isOk, "clarification decision", "clarificationDecisionRefs"
          Identifiers.createChecklistResultId value |> Result.isOk, "checklist-result", "checklistResultRefs"
          Identifiers.createPlanDecisionId value |> Result.isOk, "plan-decision", "planDecisionRefs" ]
        |> List.tryPick (fun (parses, kind, field) -> if parses then Some(kind, field) else None)

    // Emit `misfiledReference` naming the right field when the value is a well-formed id of another
    // evidence-ref class; otherwise the generic `malformedReference` (a genuine typo, not a misfile,
    // so the message stays byte-identical for those).
    let private evidenceRefDiagnostic artifact (expectedKind: string) (expectedField: string) (value: string) =
        match evidenceRefField value with
        | Some(actualKind, actualField) when actualField <> expectedField ->
            Diagnostics.create
                "misfiledReference"
                DiagnosticError
                (Some artifact)
                None
                $"Reference '{value}' is a {actualKind} id; put it in `{actualField}`, not `{expectedField}`."
                $"Move '{value}' to `{actualField}`, or remove the reference."
                [ value ]
        | _ -> Diagnostics.malformedReference artifact expectedKind value

    let parseEvidenceArtifact (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Evidence

        match yamlRoot artifact "Evidence file is empty." 0 snapshot.Text with
        | Error diagnostics -> Error diagnostics
        | Ok root ->
            let version, versionDiagnostics = schemaVersion artifact root

            let workIdValue =
                tryScalarAt [ "workId" ] root
                |> Option.defaultValue (workIdFromEvidencePath snapshot.Path)

            let workId = Identifiers.createWorkId workIdValue

            let stage =
                tryScalarAt [ "stage" ] root
                |> Option.bind (Identifiers.parseStage >> Result.toOption)
                |> Option.defaultValue LifecycleStage.Evidence

            // Each evidence node yields (declaration option, diagnostics). Malformed cross-
            // references and a whole entry skipped for a malformed id are surfaced as blocking
            // diagnostics instead of being silently dropped by the parse*Ids helpers (#70/§2.5).
            let evidenceParse =
                trySequenceAt [ "evidence" ] root
                |> Option.map (fun sequence ->
                    sequence.Children
                    |> Seq.mapi (fun index node ->
                        match node |> tryMapping with
                        | None -> None, []
                        | Some mapping ->
                            match tryScalarAt [ "id" ] mapping with
                            | None -> None, []
                            | Some rawId ->
                                // Both cited-path buckets, read RAW from the YAML — `artifacts:` because
                                // the codec drops what it cannot contain, and `sourceRefs[].path`
                                // because it is never turned into an `ArtifactRef` at all. Reading the
                                // authored text is what lets the malformed value be NAMED back to the
                                // author instead of vanishing (#359/#365).
                                let citedPathDiagnostics =
                                    [ yield! scalarList [ "artifacts" ] mapping; yield! sourceRefPaths mapping ]
                                    |> List.filter (fun path -> not (String.IsNullOrWhiteSpace path))
                                    |> List.filter (citedPathIsContained >> not)
                                    |> List.distinct
                                    |> List.map (Diagnostics.malformedArtifactPath artifact)

                                let refDiagnostics =
                                    [ scalarList [ "taskRefs" ] mapping
                                      |> malformedRefs Identifiers.createTaskId
                                      |> List.map (evidenceRefDiagnostic artifact "task" "taskRefs")
                                      scalarList [ "requirementRefs" ] mapping
                                      |> malformedRefs Identifiers.createRequirementId
                                      |> List.map (evidenceRefDiagnostic artifact "requirement" "requirementRefs")
                                      scalarList [ "clarificationDecisionRefs" ] mapping
                                      |> malformedRefs Identifiers.createDecisionId
                                      |> List.map (
                                          evidenceRefDiagnostic artifact "decision" "clarificationDecisionRefs"
                                      )
                                      citedPathDiagnostics ]
                                    |> List.concat

                                match Identifiers.createEvidenceId rawId with
                                | Error _ ->
                                    None, (Diagnostics.malformedReference artifact "evidence" rawId :: refDiagnostics)
                                | Ok id ->
                                    // The shared `declarationFields` codec decodes every authored field
                                    // (FR-007); the semantic layer here owns what is NOT serialization:
                                    // the parse-assigned `Id`/`Source`/`SourceLocation`, and the
                                    // subject-type ref merge — a `task`/`requirement` subject prepends
                                    // its id into the corresponding ref list. Malformed-ref diagnostics
                                    // are computed above (`refDiagnostics`); the codec read is lenient.
                                    let decoded =
                                        match
                                            ArtifactCodec.foldInto
                                                EvidenceCodec.declarationFields
                                                EvidenceCodec.declarationSeed
                                                mapping
                                        with
                                        | Ok value -> value
                                        | Error _ -> EvidenceCodec.declarationSeed

                                    let taskRefs =
                                        match decoded.Subject.SubjectType with
                                        | "task" ->
                                            (Identifiers.createTaskId decoded.Subject.Id
                                             |> Result.toOption
                                             |> Option.toList)
                                            @ decoded.TaskRefs
                                        | _ -> decoded.TaskRefs

                                    let requirementRefs =
                                        match decoded.Subject.SubjectType with
                                        | "requirement" ->
                                            (Identifiers.createRequirementId decoded.Subject.Id
                                             |> Result.toOption
                                             |> Option.toList)
                                            @ decoded.RequirementRefs
                                        | _ -> decoded.RequirementRefs

                                    Some
                                        { decoded with
                                            Id = id
                                            TaskRefs = taskRefs
                                            RequirementRefs = requirementRefs
                                            Source = artifact
                                            SourceLocation = sourceLocation (index + 1) },
                                    refDiagnostics)
                    |> Seq.toList)
                |> Option.defaultValue []

            let evidence = evidenceParse |> List.choose fst
            let referenceDiagnostics = evidenceParse |> List.collect snd

            let duplicateDiagnostics =
                evidence
                |> List.groupBy (fun declaration -> declaration.Id.Value)
                |> List.choose (fun (id, declarations) ->
                    if List.length declarations > 1 then
                        Some(
                            Diagnostics.duplicateIdentifier
                                artifact
                                id
                                (declarations |> List.choose (fun declaration -> declaration.SourceLocation))
                        )
                    else
                        None)

            let artifactDiagnostics =
                [ if stage <> LifecycleStage.Evidence then
                      Diagnostics.workModelInconsistent
                          artifact
                          $"Evidence stage '{Identifiers.stageValue stage}' is not 'evidence'."
                          "Set stage: evidence before rerunning."
                          [ Identifiers.stageValue stage ] ]

            match version, workId, versionDiagnostics with
            | Some schema, Ok workId, [] ->
                Ok
                    { SchemaVersion = schema
                      WorkId = workId
                      Stage = stage
                      Status = tryScalarAt [ "status" ] root |> Option.defaultValue "draft"
                      SourceSpec =
                        tryScalarAt [ "sourceSpec" ] root
                        |> Option.defaultValue $"work/{workId.Value}/spec.md"
                      SourceClarifications =
                        tryScalarAt [ "sourceClarifications" ] root
                        |> Option.defaultValue $"work/{workId.Value}/clarifications.md"
                      SourceChecklist =
                        tryScalarAt [ "sourceChecklist" ] root
                        |> Option.defaultValue $"work/{workId.Value}/checklist.md"
                      SourcePlan =
                        tryScalarAt [ "sourcePlan" ] root
                        |> Option.defaultValue $"work/{workId.Value}/plan.md"
                      SourceTasks =
                        tryScalarAt [ "sourceTasks" ] root
                        |> Option.defaultValue $"work/{workId.Value}/tasks.yml"
                      SourceAnalysis =
                        tryScalarAt [ "sourceAnalysis" ] root
                        |> Option.defaultValue $"readiness/{workId.Value}/analysis.json"
                      SourceSnapshots = parseEvidenceSourceSnapshots root
                      Evidence = evidence |> List.sortBy (fun declaration -> declaration.Id.Value)
                      LifecycleNotes = scalarList [ "lifecycleNotes" ] root
                      Diagnostics =
                        duplicateDiagnostics @ artifactDiagnostics @ referenceDiagnostics
                        |> Diagnostics.sort }
            | _ ->
                let workIdDiagnostics =
                    match workId with
                    | Error message ->
                        [ Diagnostics.workModelInconsistent
                              artifact
                              message
                              "Use a valid work id in evidence.yml."
                              [ workIdValue ] ]
                    | Ok _ -> []

                Error(versionDiagnostics @ duplicateDiagnostics @ workIdDiagnostics)

    let parseEvidence (snapshot: FileSnapshot) =
        parseEvidenceArtifact snapshot |> Result.map (fun artifact -> artifact.Evidence)
