namespace FS.GG.SDD.Artifacts

open System
open System.Globalization
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
        {
            Source: string
            Digest: string
            /// `exact-bytes-v1` is the only current receipt contract. Missing fields parse as the
            /// legacy normalized-text contract and are deliberately rejected until re-synced.
            DigestContract: string
            /// Immutable Git commit whose source tree produced the report. A receipt-only
            /// descendant may carry the receipt without invalidating this binding.
            CandidateCommit: string
            Outcome: string
            Passed: int
            Failed: int
            Skipped: int
        }

    /// A durable **record** the obligation rests on, rather than a run (FS.GG.SDD#865).
    ///
    /// `ObservedRun` above answers "did a test run discharge this?", and for an obligation discharged by
    /// filing a row, recording a decision, or performing a routing the honest answer is that no run ever
    /// could. Before this type existed that obligation could not reach `Observed: true` by ANY route, so
    /// `verify.unobservedRequiredTest` fired forever and `ship` was unreachable — measured twice, on
    /// `.github#2380` and `.github#2545`, both of which merged with `verify` blocked and said so.
    ///
    /// This is the second observable channel, deliberately shaped like the first rather than weaker:
    ///
    ///   * `Kind` names WHAT class of durable artifact backs the claim, from a closed set.
    ///   * `Locator` names the artifact itself, in a form checked against that kind — so a later reader
    ///     can go and look. The locator is committed into `verify.json` and the ship verdict verbatim,
    ///     which is what "re-checkable" means operationally.
    ///   * `Digest` binds the artifact's EXACT BYTES when the record is repository-local
    ///     (`kind: decision`) — the same `sha256:` binding `ObservedRun` uses, and the reason a local
    ///     record is strictly stronger evidence than a remote one. It is empty for `issue`/`commit`,
    ///     where there are no local bytes to hash.
    ///   * `Statement` says what the record is asserted to establish, so a reader knows what to check
    ///     the artifact AGAINST rather than merely that it exists.
    ///   * `RecordedAt` dates the claim.
    ///
    /// **SDD never dereferences `Locator`** (DEC-001). Judging a remote record live would put a network
    /// call inside a lifecycle gate that must reproduce offline in every consuming repository —
    /// `.github#2545` measured that cost concretely, its check script containing no network call of any
    /// kind — and it would buy less than it looks: resolving a URL proves the locator resolves, not that
    /// what it resolves to says what `Statement` claims. The reader still has to look.
    ///
    /// **This does not make a record unforgeable, and must not be sold as if it did** — the same
    /// disclaimer `ObservedRun` carries, for the same reason. It moves the bar from an unattributed
    /// `result: pass` to a named, dated, classified artifact that a reader can refute. What it must not
    /// do is let the author's word back in, and it does not: a record-class obligation with no receipt,
    /// or with an incoherent one, does not satisfy.
    type RecordReceipt =
        {
            /// One of `recordReceiptKinds`: `decision`, `issue`, or `commit`.
            Kind: string
            /// The durable reference, in the form `Kind` requires.
            Locator: string
            /// `durable-locator-v1` is the only current contract. Anything else is rejected rather than
            /// reinterpreted — the lesson `DigestContract` already learned.
            LocatorContract: string
            /// `sha256:<64 hex>` of the record's exact bytes for a repository-local `decision` record;
            /// empty for `issue`/`commit`, which have no local bytes.
            Digest: string
            /// What the record is asserted to establish.
            Statement: string
            /// When the record was made, as an ISO-8601 instant.
            RecordedAt: string
        }

    /// FS.GG.SDD#709 / ADR-0065. The producer-issued, schema-versioned proof that a passing test
    /// traversed the real producer-owned production composition. Every value is imported from the
    /// producer's
    /// `journeyReceipt` map; none is inferred from a test name or authored boolean.
    type JourneyReceipt =
        { SchemaVersion: int
          RunnerIdentity: string
          RunnerVersion: string
          Origin: string
          RouteId: string
          ScenarioId: string
          TestId: string
          InputKind: string
          InputDigest: string
          ReplayDigest: string
          TraceDigest: string
          InitialFingerprint: string
          TerminalFingerprint: string
          TerminalPredicateReached: bool
          Outcome: string
          MaximumSteps: int
          ActualSteps: int
          ObservedReportSource: string
          ObservedReportDigest: string
          ObservedTestName: string
          ObservedTestOutcome: string }

    type PerformanceIntentDeclaration = Fsgg.Schemas.PerformanceIntentDeclaration

    type PerformanceBudgetDeclaration =
        { ArtifactPath: string
          Intent: PerformanceIntentDeclaration option
          TargetFps: int
          WorkloadIds: string list
          StressWorkloadIds: string list
          WorkloadDefinitionDigests: string list
          CurrencyToken: string
          CapturedAfterUtc: string
          MaxP95Ms: decimal
          MaxP99Ms: decimal
          MaxCatchUpFrames: int
          MeasurementScope: string
          RequiredCapability: string
          LiveCompositorRequired: bool
          DeferralIssue: string option }

    type PerformanceBudgetState =
        | PerformancePassed
        | PerformanceFailed
        | PerformanceDeferred
        | PerformanceMalformed

    type PerformanceEvidenceSampleSet = Fsgg.Schemas.PerformanceEvidenceSampleSet
    type PerformanceEvidenceArtifact = Fsgg.Schemas.PerformanceEvidenceArtifact
    type PerformanceEvidenceMeasurement = Fsgg.Schemas.PerformanceEvidenceMeasurement

    type PerformanceBudgetEvaluation =
        { DeclarationId: string
          ArtifactPath: string
          State: PerformanceBudgetState
          WorkloadIds: string list
          Reasons: string list
          DeferralIssue: string option
          Artifact: PerformanceEvidenceArtifact option
          Measurements: PerformanceEvidenceMeasurement list }

    let isPerformanceDebtIssueReference (value: string) =
        not (String.IsNullOrWhiteSpace value)
        && Regex.IsMatch(
            value.Trim(),
            @"^(?:https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/issues/[1-9][0-9]*|[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+#[1-9][0-9]*)$",
            RegexOptions.CultureInvariant
        )

    let requiresPerformanceIntentProfile (profile: string option) =
        let renderLoopProfile = String.Concat("g", "ame")

        profile
        |> Option.exists (fun value ->
            value.Equals("interactive", StringComparison.OrdinalIgnoreCase)
            || value.Equals(renderLoopProfile, StringComparison.OrdinalIgnoreCase))

    let performanceIntentProblems (intent: PerformanceIntentDeclaration) =
        let disposition = intent.Disposition.Trim().ToLowerInvariant()

        let bindings =
            intent.WorkloadDefinitionDigests
            |> List.map (fun entry ->
                let separator = entry.IndexOf('=')

                if separator <= 0 || separator = entry.Length - 1 then
                    None
                else
                    Some(entry.Substring(0, separator).Trim(), entry.Substring(separator + 1).Trim()))

        let validBindings = bindings |> List.choose id

        let placeholders =
            validBindings
            |> List.filter (fun (_, digest) ->
                let lowered = digest.ToLowerInvariant()

                lowered.Contains("placeholder")
                || lowered.Contains("todo")
                || lowered.Contains("tbd"))

        [ if String.IsNullOrWhiteSpace intent.Id then
              "id is required"
          match disposition with
          | "active" ->
              if intent.TargetFps <= 0 then
                  "targetFps must be positive"

              if List.isEmpty intent.WorkloadIds then
                  "workloadIds must name at least one normal workload"

              if bindings |> List.exists Option.isNone then
                  "workloadDefinitionDigests entries must use '<workloadId>=<digest>'"

              for workloadId in intent.WorkloadIds |> List.distinct do
                  let matches = validBindings |> List.filter (fun (id, _) -> id = workloadId)

                  if List.isEmpty matches then
                      $"workloadDefinitionDigests must bind '{workloadId}'"
                  elif matches.Length <> 1 then
                      $"workloadDefinitionDigests must bind '{workloadId}' exactly once"

              for workloadId, digest in validBindings do
                  if not (List.contains workloadId intent.WorkloadIds) then
                      $"workloadDefinitionDigests binds undeclared workload '{workloadId}'"

                  if not (Regex.IsMatch(digest, @"^sha256:[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)) then
                      $"workloadDefinitionDigests for '{workloadId}' must use a nonblank sha256 digest token"

              if not (List.isEmpty placeholders) then
                  "workloadDefinitionDigests cannot contain placeholder/TODO/TBD values"

              if String.IsNullOrWhiteSpace intent.MaximumExpectedScale then
                  "maximumExpectedScale is required"

              if intent.MaxP95Ms <= 0m || intent.MaxP99Ms <= 0m || intent.MaxCatchUpFrames < 0 then
                  "timing thresholds must contain positive p95/p99 and non-negative catch-up limits"

              if List.isEmpty intent.StructuralCostBudgets then
                  "structuralCostBudgets must declare at least one structural limit"

              if String.IsNullOrWhiteSpace intent.RequiredCapability then
                  "requiredCapability is required"
          | "not-applicable" ->
              if List.isEmpty intent.EvidenceRefs then
                  "not-applicable intent requires evidenceRefs"

              if intent.Rationale |> Option.forall String.IsNullOrWhiteSpace then
                  "not-applicable intent requires rationale"
          | "deferred" ->
              if List.isEmpty intent.EvidenceRefs then
                  "deferred intent requires evidenceRefs"

              match intent.DeferralIssue with
              | Some issue when isPerformanceDebtIssueReference issue -> ()
              | _ -> "deferred intent requires an owner/repo#N or GitHub issue URL"
          | _ -> $"unknown disposition '{intent.Disposition}'" ]

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
            /// FS.GG.SDD#865: the receipt, when the obligation rests on a durable record rather than a
            /// run. `None` is the honest state for a record-class obligation nobody has recorded yet —
            /// and, for a test-class obligation, the ordinary state, since a record does not discharge
            /// a test (DEC-002).
            RecordReceipt: RecordReceipt option
            JourneyReceipt: JourneyReceipt option
            PerformanceBudget: PerformanceBudgetDeclaration option
            Rationale: string option
            Owner: string option
            Scope: string option
            LaterLifecycleVisibility: string option
            Notes: string list
            Source: ArtifactRef
            SourceLocation: SourceLocation option
        }

    type EvidenceObligation =
        {
            ObligationId: string
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
            // The required test-kind gate a classified gameplay FR obligation carries. Non-empty
            // means a passing declaration must use one of these kinds; observation is checked later.
            // one of these. Empty (every other obligation) ⇒ no kind restriction — additive and
            // backward-compatible.
            RequiredEvidenceKinds: string list
            /// FS.GG.SDD#865: WHAT CLASS OF EVIDENCE COULD EVER DISCHARGE THIS — one of
            /// `dischargeClasses`. `testDischargeClass` (the default, and what every obligation minted
            /// before this field existed meant) says a test run discharges it; `recordDischargeClass`
            /// says a durable record does, and that no run ever will.
            ///
            /// This is the axis the type was missing. `RequiredEvidenceKinds` above restricts WHICH
            /// declaration kind satisfies; `State` records HOW WELL the evidence stands up. Neither can
            /// express "no runner report will ever exist for this obligation", so `Observed` had exactly
            /// one true-maker and a record obligation blocked `verify` permanently.
            DischargeClass: string
            RequiredSkillOrCapabilityTags: string list
            Blocking: bool
            Correction: string
        }

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

    // The FR classification facet that carries the per-FR observed-test
    // obligation. It is one of `RequirementModel.recognizedRequirementClasses` (currently the only
    // one); named here because it is *this* class — not the vocabulary at large — that the task
    // generator maps to a gameplay-test obligation.
    let gameplayClassification = "gameplay"

    // WI-4 (ADR-0048): the capability tag marking a task — and the obligation minted from it — as a
    // per-classified-FR gameplay test obligation, discharged only by an observed test. It
    // lives here, in Artifacts, for the same reason as `visualInspectionSkill`: the task generator
    // that stamps it and the evidence/verify handlers that read it back off the obligation sit in
    // different modules of `Commands` and must agree on one literal.
    let gameplayTestCapability = "gameplay-test"

    let productionJourneyClassification = "production-journey"
    let productionJourneyCapability = "production-journey"

    /// The evidence kinds that count as a *real test* for a classified-FR obligation (ADR-0048). A
    /// gameplay obligation is satisfied only by one of these kinds with a passing observed run—the
    /// single source of truth for the derived obligation's `RequiredEvidenceKinds`.
    let realTestEvidenceKinds = [ "verification" ]

    /// Does this obligation's skill/capability tag set mark it a classified-FR gameplay obligation?
    let isGameplayTestTagged (tags: string list) =
        tags
        |> List.exists (fun tag -> String.Equals(tag, gameplayTestCapability, StringComparison.OrdinalIgnoreCase))

    let isProductionJourneyTagged (tags: string list) =
        tags
        |> List.exists (fun tag -> String.Equals(tag, productionJourneyCapability, StringComparison.OrdinalIgnoreCase))

    // FS.GG.SDD#865: the capability tag marking a task — and the obligation minted from it — as
    // discharged by a durable RECORD rather than by a test run. It lives here, in Artifacts, for the
    // same reason as `visualInspectionSkill` and `gameplayTestCapability`: the modules that stamp it
    // and read it back sit in different parts of `Commands` and must agree on one literal.
    //
    // DEC-003: the class rides this AUTHORED task tag rather than a new spec classification facet.
    // `requiredSkills` is authored state the task generator unions across regeneration
    // (FS.GG.SDD#310, AC7), so the tag survives a `tasks` re-run — which means declaring it costs the
    // author one token in the artifact where they already declare what a task needs, and costs the
    // task generator nothing.
    let recordDischargeCapability = "record-discharge"

    /// The obligation discharge classes (FS.GG.SDD#865). `test` is the default and the only thing any
    /// obligation minted before this axis existed could have meant.
    let testDischargeClass = "test"

    let recordDischargeClass = "record"

    let dischargeClasses = [ testDischargeClass; recordDischargeClass ]

    /// Does this obligation's skill/capability tag set mark it a record-discharged obligation?
    let isRecordDischargeTagged (tags: string list) =
        tags
        |> List.exists (fun tag -> String.Equals(tag, recordDischargeCapability, StringComparison.OrdinalIgnoreCase))

    /// The discharge class these tags declare — the one place the tag is turned into the class, so the
    /// obligation minter and any later reader cannot disagree about what the tag means.
    let dischargeClassFromTags (tags: string list) =
        if isRecordDischargeTagged tags then
            recordDischargeClass
        else
            testDischargeClass

    /// Is this obligation record-class? Read from the class STRING rather than re-derived from tags,
    /// because the class is what `verify` persists and `ship` reads back. An unrecognized or absent
    /// value reads as test-class — the fail-closed direction, since a test-class obligation is held to
    /// the stricter, longer-standing `observedRun` requirement.
    let isRecordDischargeClass (dischargeClass: string) =
        String.Equals(dischargeClass, recordDischargeClass, StringComparison.OrdinalIgnoreCase)

    /// The closed set of durable-record kinds a `RecordReceipt` may name (FS.GG.SDD#865).
    ///
    ///   * `decision` — a record committed in THIS repository: an ADR, a spec section, a lifecycle
    ///     note. Locator is a contained repository-relative path, and the receipt binds its bytes.
    ///   * `issue`    — a row filed in a tracker. Locator is an absolute `https` URI.
    ///   * `commit`   — a landed commit. Locator is a 40-character hex object name.
    let recordReceiptKinds = [ "decision"; "issue"; "commit" ]

    /// The only current `RecordReceipt.LocatorContract`.
    let recordLocatorContract = "durable-locator-v1"

    let private sha256Digest value =
        not (String.IsNullOrWhiteSpace value)
        && Regex.IsMatch(value, @"^sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant)

    /// The complete schema-v1 producer contract and its same-execution observed-report binding.
    /// Returns stable, actionable reasons; an empty list is the only valid verdict.
    let journeyReceiptProblems (declaration: EvidenceDeclaration) =
        match declaration.JourneyReceipt with
        | None -> [ "journey receipt is missing" ]
        | Some receipt ->
            [ if receipt.SchemaVersion <> 1 then
                  $"unsupported journey receipt schemaVersion {receipt.SchemaVersion}"
              if String.IsNullOrWhiteSpace receipt.RunnerIdentity then
                  "runner.identity is required"
              if String.IsNullOrWhiteSpace receipt.RunnerVersion then
                  "runner.version is required"
              if not (receipt.Origin.Equals("production-journey", StringComparison.OrdinalIgnoreCase)) then
                  $"origin '{receipt.Origin}' is not production-journey"
              for label, value in
                  [ "routeId", receipt.RouteId
                    "scenarioId", receipt.ScenarioId
                    "testId", receipt.TestId
                    "observedTestReport.source", receipt.ObservedReportSource
                    "observedTestReport.testName", receipt.ObservedTestName ] do
                  if String.IsNullOrWhiteSpace value then
                      $"{label} is required"
              if
                  not (
                      receipt.InputKind.Equals("fixed-script", StringComparison.OrdinalIgnoreCase)
                      || receipt.InputKind.Equals("seeded-policy", StringComparison.OrdinalIgnoreCase)
                  )
              then
                  $"input.kind '{receipt.InputKind}' is not fixed-script or seeded-policy"
              for label, digest in
                  [ "input.digest", receipt.InputDigest
                    "replayDigest", receipt.ReplayDigest
                    "traceDigest", receipt.TraceDigest
                    "initialFingerprint", receipt.InitialFingerprint
                    "terminalFingerprint", receipt.TerminalFingerprint
                    "observedTestReport.digest", receipt.ObservedReportDigest ] do
                  if not (sha256Digest digest) then
                      $"{label} is not a sha256:<hex> digest"
              if not receipt.TerminalPredicateReached then
                  "terminal predicate was not reached"
              if not (receipt.Outcome.Equals("passed", StringComparison.OrdinalIgnoreCase)) then
                  $"journey outcome '{receipt.Outcome}' is not passed"
              if receipt.MaximumSteps <= 0 then
                  "maximumSteps must be positive"
              if receipt.ActualSteps <= 0 then
                  "actualSteps must be positive"
              if receipt.ActualSteps > receipt.MaximumSteps then
                  $"actualSteps {receipt.ActualSteps} exceeds maximumSteps {receipt.MaximumSteps}"
              if not (receipt.ObservedTestOutcome.Equals("passed", StringComparison.OrdinalIgnoreCase)) then
                  $"observed test outcome '{receipt.ObservedTestOutcome}' is not passed"
              match declaration.ObservedRun with
              | None -> "matching observedRun is missing"
              | Some run ->
                  if not (receipt.ObservedReportSource.Equals(run.Source, StringComparison.Ordinal)) then
                      "journey receipt report source does not match observedRun.source"

                  if not (receipt.ObservedReportDigest.Equals(run.Digest, StringComparison.OrdinalIgnoreCase)) then
                      "journey receipt report digest does not match observedRun.digest"

                  if
                      not (run.Outcome.Equals("passed", StringComparison.OrdinalIgnoreCase))
                      || run.Failed <> 0
                  then
                      "observedRun is not passing" ]

    let hasValidJourneyReceipt declaration =
        List.isEmpty (journeyReceiptProblems declaration)

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

    let private decimalInvariant (fallback: decimal) (value: string) =
        match Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture) with
        | true, parsed -> parsed
        | _ -> fallback

    let private decimalText (value: decimal) =
        value.ToString("0.############################", CultureInfo.InvariantCulture)

    let private scalarFacts (line: string) =
        Regex.Matches(line, @"(?<key>[A-Za-z0-9-]+)=(?<value>[^\s]+)")
        |> Seq.cast<Match>
        |> Seq.map (fun matched -> matched.Groups["key"].Value, matched.Groups["value"].Value)
        |> Map.ofSeq

    let private tryDecimal key facts =
        facts
        |> Map.tryFind key
        |> Option.bind (fun (value: string) ->
            match Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | _ -> None)

    let private tryInt key facts =
        facts
        |> Map.tryFind key
        |> Option.bind (fun (value: string) ->
            match Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | _ -> None)

    let private performanceArtifactFacts (text: string) =
        let lines =
            text.Replace("\r\n", "\n").Split('\n')
            |> Array.map _.Trim()
            |> Array.filter (String.IsNullOrWhiteSpace >> not)

        let valueAfter prefix =
            lines
            |> Array.tryPick (fun line ->
                if line.StartsWith(prefix, StringComparison.Ordinal) then
                    Some(line.Substring(prefix.Length).Trim())
                else
                    None)

        let workloads =
            lines
            |> Array.choose (fun line ->
                if line.StartsWith("scenario=", StringComparison.Ordinal) then
                    let facts = scalarFacts line
                    Map.tryFind "scenario" facts |> Option.map (fun id -> id, facts)
                else
                    None)
            |> Map.ofArray

        valueAfter "target-normal-play-p95-ms<="
        |> Option.bind (fun value ->
            match Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture) with
            | true, p95 ->
                valueAfter "target-normal-play-p99-ms<="
                |> Option.bind (fun p99Text ->
                    match Decimal.TryParse(p99Text, NumberStyles.Number, CultureInfo.InvariantCulture) with
                    | true, p99 ->
                        valueAfter "target-sustained-catch-up-frames="
                        |> Option.bind (fun catchUpText ->
                            match
                                Int32.TryParse(catchUpText, NumberStyles.Integer, CultureInfo.InvariantCulture)
                            with
                            | true, catchUp ->
                                Some(
                                    p95,
                                    p99,
                                    catchUp,
                                    valueAfter "target-scope=" |> Option.defaultValue "",
                                    valueAfter "measurement-mode=" |> Option.defaultValue "",
                                    valueAfter "live-compositor-proof="
                                    |> Option.map (fun value ->
                                        String.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                                    |> Option.defaultValue false,
                                    workloads
                                )
                            | _ -> None)
                    | _ -> None)
            | _ -> None)

    let private evaluateLegacyPerformanceBudgets
        (artifactText: string -> string option)
        (declarations: EvidenceDeclaration list)
        : PerformanceBudgetEvaluation list =
        declarations
        |> List.choose (fun declaration ->
            declaration.PerformanceBudget
            |> Option.map (fun budget ->
                let malformed =
                    [ if String.IsNullOrWhiteSpace budget.ArtifactPath then
                          "artifactPath is required"
                      if budget.TargetFps <= 0 then
                          "targetFps must be positive"
                      if List.isEmpty budget.WorkloadIds then
                          "workloadIds must name at least one active normal-play workload"
                      if budget.MaxP95Ms <= 0m then
                          "maxP95Ms must be positive"
                      if budget.MaxP99Ms <= 0m then
                          "maxP99Ms must be positive"
                      if budget.MaxCatchUpFrames < 0 then
                          "maxCatchUpFrames cannot be negative"
                      if String.IsNullOrWhiteSpace budget.MeasurementScope then
                          "measurementScope is required"
                      if String.IsNullOrWhiteSpace budget.RequiredCapability then
                          "requiredCapability is required"
                      let overlap =
                          Set.intersect (Set.ofList budget.WorkloadIds) (Set.ofList budget.StressWorkloadIds)

                      if not (Set.isEmpty overlap) then
                          let overlappingIds = String.concat ", " (Set.toList overlap)
                          $"normal and stress workload ids overlap: {overlappingIds}" ]

                let state, reasons =
                    if not (List.isEmpty malformed) then
                        PerformanceMalformed, malformed
                    else
                        match artifactText budget.ArtifactPath with
                        | None -> PerformanceMalformed, [ $"performance artifact '{budget.ArtifactPath}' is absent" ]
                        | Some text ->
                            match performanceArtifactFacts text with
                            | None ->
                                PerformanceMalformed,
                                [ $"performance artifact '{budget.ArtifactPath}' is missing or has malformed targets" ]
                            | Some(artifactP95,
                                   artifactP99,
                                   artifactCatchUp,
                                   artifactScope,
                                   capability,
                                   liveProof,
                                   workloads) ->
                                let bindingFailures =
                                    [ if artifactP95 <> budget.MaxP95Ms then
                                          $"artifact p95 target {decimalText artifactP95} does not match declared {decimalText budget.MaxP95Ms}"
                                      if artifactP99 <> budget.MaxP99Ms then
                                          $"artifact p99 target {decimalText artifactP99} does not match declared {decimalText budget.MaxP99Ms}"
                                      if artifactCatchUp <> budget.MaxCatchUpFrames then
                                          $"artifact catch-up target {artifactCatchUp} does not match declared {budget.MaxCatchUpFrames}"
                                      if
                                          not (
                                              String.Equals(
                                                  artifactScope,
                                                  budget.MeasurementScope,
                                                  StringComparison.Ordinal
                                              )
                                          )
                                      then
                                          $"artifact scope '{artifactScope}' does not match declared '{budget.MeasurementScope}'"
                                      if
                                          not (
                                              String.Equals(
                                                  capability,
                                                  budget.RequiredCapability,
                                                  StringComparison.Ordinal
                                              )
                                          )
                                      then
                                          $"artifact capability '{capability}' does not match required '{budget.RequiredCapability}'"
                                      if budget.LiveCompositorRequired && not liveProof then
                                          "live compositor proof is required but the artifact declares live-compositor-proof=false"
                                      for workloadId in budget.WorkloadIds do
                                          match Map.tryFind workloadId workloads with
                                          | None -> $"normal-play workload '{workloadId}' is absent"
                                          | Some facts ->
                                              match tryDecimal "p95-ms" facts with
                                              | Some actual when actual <= budget.MaxP95Ms -> ()
                                              | Some actual ->
                                                  $"{workloadId} p95 {decimalText actual} ms exceeds {decimalText budget.MaxP95Ms} ms"
                                              | None -> $"{workloadId} p95-ms is missing or malformed"

                                              match tryDecimal "p99-ms" facts with
                                              | Some actual when actual <= budget.MaxP99Ms -> ()
                                              | Some actual ->
                                                  $"{workloadId} p99 {decimalText actual} ms exceeds {decimalText budget.MaxP99Ms} ms"
                                              | None -> $"{workloadId} p99-ms is missing or malformed"

                                              match tryInt "catch-up-frames" facts with
                                              | Some actual when actual <= budget.MaxCatchUpFrames -> ()
                                              | Some actual ->
                                                  $"{workloadId} catch-up frames {actual} exceeds {budget.MaxCatchUpFrames}"
                                              | None -> $"{workloadId} catch-up-frames is missing or malformed" ]

                                if List.isEmpty bindingFailures then
                                    PerformancePassed, []
                                elif Option.isSome budget.DeferralIssue then
                                    PerformanceDeferred, bindingFailures
                                else
                                    PerformanceFailed, bindingFailures

                { DeclarationId = declaration.Id.Value
                  ArtifactPath = budget.ArtifactPath
                  State = state
                  WorkloadIds = budget.WorkloadIds |> List.distinct |> List.sort
                  Reasons = reasons
                  DeferralIssue = budget.DeferralIssue
                  Artifact = None
                  Measurements = [] }))
        |> List.sortBy _.DeclarationId

    let private tryProperty (name: string) (element: JsonElement) =
        match element.TryGetProperty name with
        | true, value -> Some value
        | _ -> None

    let private jsonString (name: string) (element: JsonElement) =
        tryProperty name element
        |> Option.filter (fun value -> value.ValueKind = JsonValueKind.String)
        |> Option.bind (fun value -> value.GetString() |> Option.ofObj)
        |> Option.defaultValue ""

    let private jsonInt (name: string) (element: JsonElement) =
        tryProperty name element
        |> Option.bind (fun value ->
            match value.TryGetInt32() with
            | true, parsed -> Some parsed
            | _ -> None)
        |> Option.defaultValue Int32.MinValue

    let private jsonDecimal (name: string) (element: JsonElement) =
        tryProperty name element
        |> Option.bind (fun value ->
            match value.TryGetDecimal() with
            | true, parsed -> Some parsed
            | _ -> None)
        |> Option.defaultValue -1m

    let private jsonBool (name: string) (element: JsonElement) =
        tryProperty name element
        |> Option.filter (fun value -> value.ValueKind = JsonValueKind.True || value.ValueKind = JsonValueKind.False)
        |> Option.map _.GetBoolean()
        |> Option.defaultValue false

    let private jsonList (read: JsonElement -> 'a option) (name: string) (element: JsonElement) =
        tryProperty name element
        |> Option.filter (fun value -> value.ValueKind = JsonValueKind.Array)
        |> Option.map (fun value -> value.EnumerateArray() |> Seq.choose read |> List.ofSeq)
        |> Option.defaultValue []

    let private jsonStrings name element =
        jsonList
            (fun item ->
                if item.ValueKind = JsonValueKind.String then
                    item.GetString() |> Option.ofObj
                else
                    None)
            name
            element

    let private jsonDecimals name element =
        jsonList
            (fun item ->
                match item.TryGetDecimal() with
                | true, value -> Some value
                | _ -> None)
            name
            element

    let private jsonInts name element =
        jsonList
            (fun item ->
                match item.TryGetInt32() with
                | true, value -> Some value
                | _ -> None)
            name
            element

    let parsePerformanceEvidence (text: string) : Result<PerformanceEvidenceArtifact, string list> =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement

            if root.ValueKind <> JsonValueKind.Object then
                Error [ "performance evidence root must be a JSON object" ]
            else
                let contractVersion = jsonString "contractVersion" root

                let claimed =
                    tryProperty "claimedBudgetPassed" root
                    |> Option.bind (fun value ->
                        if value.ValueKind = JsonValueKind.True || value.ValueKind = JsonValueKind.False then
                            Some(value.GetBoolean())
                        else
                            None)

                let parsedSampleSets =
                    tryProperty "sampleSets" root
                    |> Option.filter (fun value -> value.ValueKind = JsonValueKind.Array)
                    |> Option.map (fun value ->
                        value.EnumerateArray()
                        |> Seq.map (fun item ->
                            let sample: Fsgg.Schemas.PerformanceEvidenceSampleSet =
                                { WorkloadId = jsonString "workloadId" item
                                  WorkloadDefinitionDigest = jsonString "workloadDefinitionDigest" item
                                  WorkloadClass = jsonString "workloadClass" item
                                  TargetFps = jsonInt "targetFps" item
                                  MaxP95Ms = jsonDecimal "maxP95Ms" item
                                  MaxP99Ms = jsonDecimal "maxP99Ms" item
                                  MaxCatchUpFrames = jsonInt "maxCatchUpFrames" item
                                  MeasurementScope = jsonString "measurementScope" item
                                  RequiredCapability = jsonString "requiredCapability" item
                                  HostProfile = jsonString "hostProfile" item
                                  PackageVersions = jsonStrings "packageVersions" item
                                  MeasurementMode = jsonString "measurementMode" item
                                  Capabilities = jsonStrings "capabilities" item
                                  WarmupPolicy = jsonString "warmupPolicy" item
                                  SamplePolicy = jsonString "samplePolicy" item
                                  CapturedAtUtc = jsonString "capturedAtUtc" item
                                  CurrencyToken = jsonString "currencyToken" item
                                  ProbeReadbackContaminated = jsonBool "probeReadbackContaminated" item
                                  DurationSamplesMs = jsonDecimals "durationSamplesMs" item
                                  CatchUpFrames = jsonInts "catchUpFrames" item }

                            let itemErrors =
                                [ match tryProperty "probeReadbackContaminated" item with
                                  | Some property when
                                      property.ValueKind = JsonValueKind.True
                                      || property.ValueKind = JsonValueKind.False
                                      ->
                                      ()
                                  | _ ->
                                      let id =
                                          if String.IsNullOrWhiteSpace sample.WorkloadId then
                                              "<missing workloadId>"
                                          else
                                              sample.WorkloadId

                                      $"{id} probeReadbackContaminated must be present and boolean" ]

                            sample, itemErrors)
                        |> List.ofSeq)
                    |> Option.defaultValue []

                let sampleSets = parsedSampleSets |> List.map fst

                let errors =
                    [ if contractVersion <> "performance-evidence-v1" then
                          "contractVersion must be 'performance-evidence-v1'"
                      if List.isEmpty sampleSets then
                          "sampleSets must contain at least one independently verifiable sample set"
                      yield! parsedSampleSets |> List.collect snd ]

                if List.isEmpty errors then
                    Ok
                        { ContractVersion = contractVersion
                          ClaimedBudgetPassed = claimed
                          SampleSets = sampleSets }
                else
                    Error errors
        with :? JsonException as ex ->
            Error [ $"performance evidence is not valid JSON: {ex.Message}" ]

    let private nearestRank percentile samples =
        let ordered = samples |> List.sort
        ordered.[max 0 (int (Math.Ceiling(percentile * float ordered.Length)) - 1)]

    let private sampleBinding (sample: PerformanceEvidenceSampleSet) =
        sample.WorkloadDefinitionDigest,
        sample.HostProfile,
        (List.sort sample.PackageVersions),
        sample.MeasurementMode,
        sample.MeasurementScope,
        sample.RequiredCapability,
        (List.sort sample.Capabilities),
        sample.WarmupPolicy,
        sample.SamplePolicy,
        sample.CapturedAtUtc,
        sample.CurrencyToken,
        sample.ProbeReadbackContaminated

    let private workloadDefinitionBindings (entries: string list) =
        entries
        |> List.choose (fun entry ->
            let separator = entry.IndexOf('=')

            if separator <= 0 || separator = entry.Length - 1 then
                None
            else
                Some(entry.Substring(0, separator).Trim(), entry.Substring(separator + 1).Trim()))

    let private tryIsoTimestamp (value: string) =
        if
            String.IsNullOrWhiteSpace value
            || not (
                Regex.IsMatch(
                    value,
                    @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$",
                    RegexOptions.CultureInvariant
                )
            )
        then
            None
        else
            match DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | true, parsed -> Some parsed
            | _ -> None

    let evaluatePerformanceBudgets
        (artifactText: string -> string option)
        (declarations: EvidenceDeclaration list)
        : PerformanceBudgetEvaluation list =
        declarations
        |> List.choose (fun declaration ->
            declaration.PerformanceBudget
            |> Option.map (fun budget ->
                let declarationErrors =
                    let workloadDefinitions =
                        workloadDefinitionBindings budget.WorkloadDefinitionDigests

                    let declaredIds = budget.WorkloadIds @ budget.StressWorkloadIds |> List.distinct

                    [ if String.IsNullOrWhiteSpace budget.ArtifactPath then
                          "artifactPath is required"
                      match budget.Intent with
                      | Some intent ->
                          yield!
                              performanceIntentProblems intent
                              |> List.map (fun problem -> $"intent.{problem}")

                          if
                              not (String.Equals(intent.Disposition, "active", StringComparison.OrdinalIgnoreCase))
                          then
                              "a performanceBudget may bind only an active performance intent"

                          if String.IsNullOrWhiteSpace intent.Id then
                              "intent.id is required"

                          if intent.TargetFps <> budget.TargetFps then
                              "intent.targetFps must equal performanceBudget.targetFps"

                          if Set.ofList intent.WorkloadIds <> Set.ofList budget.WorkloadIds then
                              "intent.workloadIds must equal performanceBudget.workloadIds"

                          if
                              (workloadDefinitionBindings intent.WorkloadDefinitionDigests |> Map.ofList)
                              <> (workloadDefinitions
                                  |> List.filter (fun (id, _) -> List.contains id intent.WorkloadIds)
                                  |> Map.ofList)
                          then
                              "intent.workloadDefinitionDigests must equal the performanceBudget normal-workload bindings"

                          if intent.MaxP95Ms <> budget.MaxP95Ms || intent.MaxP99Ms <> budget.MaxP99Ms then
                              "intent timing thresholds must equal the performanceBudget thresholds"

                          if intent.MaxCatchUpFrames <> budget.MaxCatchUpFrames then
                              "intent.maxCatchUpFrames must equal performanceBudget.maxCatchUpFrames"

                          if
                              not (
                                  String.Equals(
                                      intent.RequiredCapability,
                                      budget.RequiredCapability,
                                      StringComparison.Ordinal
                                  )
                              )
                          then
                              "intent.requiredCapability must equal performanceBudget.requiredCapability"

                          if intent.LiveCompositorRequired <> budget.LiveCompositorRequired then
                              "intent.liveCompositorRequired must equal performanceBudget.liveCompositorRequired"

                          if String.IsNullOrWhiteSpace intent.MaximumExpectedScale then
                              "intent.maximumExpectedScale is required"

                          if List.isEmpty intent.StructuralCostBudgets then
                              "intent.structuralCostBudgets must declare at least one structural limit"
                      | None -> ()
                      match budget.DeferralIssue with
                      | Some issue when not (isPerformanceDebtIssueReference issue) ->
                          "deferralIssue must use owner/repo#N or a GitHub issue URL"
                      | _ -> ()
                      if budget.TargetFps <= 0 then
                          "targetFps must be positive"
                      if List.isEmpty budget.WorkloadIds then
                          "workloadIds must name at least one active normal-play workload"
                      if workloadDefinitions.Length <> budget.WorkloadDefinitionDigests.Length then
                          "workloadDefinitionDigests entries must use '<workloadId>=<digest>'"
                      for workloadId in declaredIds do
                          let matches = workloadDefinitions |> List.filter (fun (id, _) -> id = workloadId)

                          if List.isEmpty matches then
                              $"workloadDefinitionDigests must bind '{workloadId}'"
                          elif matches.Length <> 1 then
                              $"workloadDefinitionDigests must bind '{workloadId}' exactly once"
                      for workloadId, _ in workloadDefinitions do
                          if not (List.contains workloadId declaredIds) then
                              $"workloadDefinitionDigests binds undeclared workload '{workloadId}'"
                      if String.IsNullOrWhiteSpace budget.CurrencyToken then
                          "currencyToken is required"
                      if tryIsoTimestamp budget.CapturedAfterUtc |> Option.isNone then
                          "capturedAfterUtc must be an ISO-8601 timestamp"
                      if budget.MaxP95Ms <= 0m then
                          "maxP95Ms must be positive"
                      if budget.MaxP99Ms <= 0m then
                          "maxP99Ms must be positive"
                      if budget.MaxCatchUpFrames < 0 then
                          "maxCatchUpFrames cannot be negative"
                      if String.IsNullOrWhiteSpace budget.MeasurementScope then
                          "measurementScope is required"
                      if String.IsNullOrWhiteSpace budget.RequiredCapability then
                          "requiredCapability is required"
                      let overlap =
                          Set.intersect (Set.ofList budget.WorkloadIds) (Set.ofList budget.StressWorkloadIds)

                      if not (Set.isEmpty overlap) then
                          let names = String.concat ", " (Set.toList overlap)
                          $"normal and stress workload ids overlap: {names}" ]

                let finish state reasons artifact measurements =
                    { DeclarationId = declaration.Id.Value
                      ArtifactPath = budget.ArtifactPath
                      State = state
                      WorkloadIds = budget.WorkloadIds |> List.distinct |> List.sort
                      Reasons = reasons
                      DeferralIssue = budget.DeferralIssue
                      Artifact = artifact
                      Measurements = measurements }

                if not (List.isEmpty declarationErrors) then
                    finish PerformanceMalformed declarationErrors None []
                else
                    match artifactText budget.ArtifactPath with
                    | None ->
                        finish
                            PerformanceMalformed
                            [ $"performance artifact '{budget.ArtifactPath}' is absent" ]
                            None
                            []
                    | Some text ->
                        match parsePerformanceEvidence text with
                        | Error errors -> finish PerformanceMalformed errors None []
                        | Ok artifact ->
                            let declaredIds = Set.ofList (budget.WorkloadIds @ budget.StressWorkloadIds)

                            let expectedDefinitions =
                                workloadDefinitionBindings budget.WorkloadDefinitionDigests |> Map.ofList

                            let capturedAfter = tryIsoTimestamp budget.CapturedAfterUtc |> Option.get

                            let bindingErrors =
                                [ for sample in artifact.SampleSets do
                                      let id =
                                          if String.IsNullOrWhiteSpace sample.WorkloadId then
                                              "<missing workloadId>"
                                          else
                                              sample.WorkloadId

                                      if not (Set.contains sample.WorkloadId declaredIds) then
                                          $"{id} is not a declared workload"

                                      if String.IsNullOrWhiteSpace sample.WorkloadDefinitionDigest then
                                          $"{id} workloadDefinitionDigest is required"
                                      elif
                                          Map.tryFind sample.WorkloadId expectedDefinitions
                                          <> Some sample.WorkloadDefinitionDigest
                                      then
                                          $"{id} workloadDefinitionDigest does not match the declaration"

                                      if String.IsNullOrWhiteSpace sample.HostProfile then
                                          $"{id} hostProfile is required"

                                      if List.isEmpty sample.PackageVersions then
                                          $"{id} packageVersions must not be empty"

                                      if String.IsNullOrWhiteSpace sample.WarmupPolicy then
                                          $"{id} warmupPolicy is required"

                                      if String.IsNullOrWhiteSpace sample.SamplePolicy then
                                          $"{id} samplePolicy is required"

                                      if String.IsNullOrWhiteSpace sample.CapturedAtUtc then
                                          $"{id} capturedAtUtc is required"
                                      else
                                          match tryIsoTimestamp sample.CapturedAtUtc with
                                          | None -> $"{id} capturedAtUtc must be an ISO-8601 timestamp"
                                          | Some captured when captured < capturedAfter ->
                                              $"{id} capturedAtUtc predates declared capturedAfterUtc"
                                          | Some _ -> ()

                                      if String.IsNullOrWhiteSpace sample.CurrencyToken then
                                          $"{id} currencyToken is required"
                                      elif sample.CurrencyToken <> budget.CurrencyToken then
                                          $"{id} currencyToken does not match the declaration"

                                      if List.isEmpty sample.DurationSamplesMs then
                                          $"{id} durationSamplesMs must not be empty"

                                      if sample.DurationSamplesMs |> List.exists (fun value -> value < 0m) then
                                          $"{id} durationSamplesMs cannot contain negative values"

                                      if List.isEmpty sample.CatchUpFrames then
                                          $"{id} catchUpFrames must not be empty"

                                      if sample.CatchUpFrames |> List.exists (fun value -> value < 0) then
                                          $"{id} catchUpFrames cannot contain negative values"

                                      if sample.TargetFps <> budget.TargetFps then
                                          $"{id} targetFps {sample.TargetFps} does not match declared {budget.TargetFps}"

                                      if sample.MaxP95Ms <> budget.MaxP95Ms then
                                          $"{id} maxP95Ms does not match the declaration"

                                      if sample.MaxP99Ms <> budget.MaxP99Ms then
                                          $"{id} maxP99Ms does not match the declaration"

                                      if sample.MaxCatchUpFrames <> budget.MaxCatchUpFrames then
                                          $"{id} maxCatchUpFrames does not match the declaration"

                                      if sample.MeasurementScope <> budget.MeasurementScope then
                                          $"{id} measurementScope does not match the declaration"

                                      if
                                          sample.RequiredCapability <> budget.RequiredCapability
                                          || not (List.contains budget.RequiredCapability sample.Capabilities)
                                      then
                                          $"{id} does not bind the required capability '{budget.RequiredCapability}'"

                                      if
                                          sample.MeasurementMode <> "headless"
                                          && sample.MeasurementMode <> "live-compositor"
                                      then
                                          $"{id} measurementMode '{sample.MeasurementMode}' is unsupported"

                                      if
                                          budget.LiveCompositorRequired && sample.MeasurementMode <> "live-compositor"
                                      then
                                          $"{id} uses '{sample.MeasurementMode}' but live-compositor evidence is required"

                                      if budget.LiveCompositorRequired && sample.ProbeReadbackContaminated then
                                          $"{id} is probe/readback contaminated and cannot prove live-compositor performance"

                                  for workloadId in budget.WorkloadIds do
                                      let sets =
                                          artifact.SampleSets |> List.filter (fun set -> set.WorkloadId = workloadId)

                                      if List.isEmpty sets then
                                          $"normal-play workload '{workloadId}' is absent"
                                      elif sets |> List.exists (fun set -> set.WorkloadClass <> "normal-play") then
                                          $"{workloadId} must be classified as normal-play"
                                      elif sets |> List.map sampleBinding |> List.distinct |> List.length > 1 then
                                          $"{workloadId} sample sets have mixed digest, host, package, mode, scope, capability, policy, capture-time, currency, or contamination bindings"

                                  for workloadId in budget.StressWorkloadIds do
                                      let sets =
                                          artifact.SampleSets |> List.filter (fun set -> set.WorkloadId = workloadId)

                                      if
                                          sets |> List.exists (fun set -> set.WorkloadClass <> "stress-throughput")
                                      then
                                          $"{workloadId} must be classified as stress-throughput"
                                      elif sets |> List.map sampleBinding |> List.distinct |> List.length > 1 then
                                          $"{workloadId} sample sets have mixed digest, host, package, mode, scope, capability, policy, capture-time, currency, or contamination bindings" ]

                            let measurements =
                                budget.WorkloadIds @ budget.StressWorkloadIds
                                |> List.distinct
                                |> List.choose (fun workloadId ->
                                    let sets =
                                        artifact.SampleSets |> List.filter (fun set -> set.WorkloadId = workloadId)

                                    let durations = sets |> List.collect _.DurationSamplesMs
                                    let catchUps = sets |> List.collect _.CatchUpFrames

                                    if List.isEmpty durations || List.isEmpty catchUps then
                                        None
                                    else
                                        let measured: Fsgg.Schemas.PerformanceEvidenceMeasurement =
                                            { WorkloadId = workloadId
                                              P95Ms = nearestRank 0.95 durations
                                              P99Ms = nearestRank 0.99 durations
                                              MaxCatchUpFrames = List.max catchUps }

                                        Some measured)

                            let measurementFailures =
                                [ for measured in measurements do
                                      if
                                          List.contains measured.WorkloadId budget.WorkloadIds
                                          && measured.P95Ms > budget.MaxP95Ms
                                      then
                                          $"{measured.WorkloadId} recomputed p95 {decimalText measured.P95Ms} ms exceeds {decimalText budget.MaxP95Ms} ms"

                                      if
                                          List.contains measured.WorkloadId budget.WorkloadIds
                                          && measured.P99Ms > budget.MaxP99Ms
                                      then
                                          $"{measured.WorkloadId} recomputed p99 {decimalText measured.P99Ms} ms exceeds {decimalText budget.MaxP99Ms} ms"

                                      if
                                          List.contains measured.WorkloadId budget.WorkloadIds
                                          && measured.MaxCatchUpFrames > budget.MaxCatchUpFrames
                                      then
                                          $"{measured.WorkloadId} recomputed catch-up frames {measured.MaxCatchUpFrames} exceeds {budget.MaxCatchUpFrames}" ]

                            let failures =
                                [ yield! measurementFailures
                                  if
                                      artifact.ClaimedBudgetPassed = Some true
                                      && not (List.isEmpty measurementFailures)
                                  then
                                      "claimedBudgetPassed=true disagrees with the raw samples" ]

                            if not (List.isEmpty bindingErrors) then
                                finish PerformanceMalformed bindingErrors (Some artifact) measurements
                            elif List.isEmpty failures then
                                finish PerformancePassed [] (Some artifact) measurements
                            elif Option.isSome budget.DeferralIssue then
                                finish PerformanceDeferred failures (Some artifact) measurements
                            else
                                finish PerformanceFailed failures (Some artifact) measurements))
        |> List.sortBy _.DeclarationId

    /// The visual-inspection artifact rule (FS.GG.SDD#306, FR-004), stated once. A declaration that
    /// claims a pass while naming no rendered artifact asserts that someone
    /// looked at a frame that does not exist. Three call sites read this — the `evidence` pre-write
    /// gate, the `ED-` disposition cascade, and the `TD-` mirror — so the rule cannot drift between
    /// what blocks and what the readiness view records.
    ///
    /// Provenance does not exempt a passing claim from naming the artifact it says was inspected.
    let passesWithoutRenderedArtifact (declaration: EvidenceDeclaration) =
        normalizedEvidenceResult declaration.Result = "pass"
        && not (namesRenderedArtifact declaration)

    /// Does this declaration satisfy a required-evidence-kind gate — a passing claim whose kind is
    /// one of `requiredKinds`? Provenance does not change the kind or observed outcome. A non-test
    /// kind (e.g. `implementation`) still cannot discharge a test obligation. Stated once so the
    /// `ED-` disposition cascade and its
    /// `TD-` verify mirror cannot drift on what discharges a classified-FR obligation.
    let satisfiesRequiredEvidenceKinds (requiredKinds: string list) (declaration: EvidenceDeclaration) =
        normalizedEvidenceResult declaration.Result = "pass"
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
          | _ -> ()
          // FS.GG.SDD#865, the exact analogue one field along. A `decision` receipt names a record
          // committed in THIS repository, so its locator IS a cited local path and belongs in the same
          // bucket — and then the #349 cascade probes it for free: a decision record deleted after the
          // receipt was written turns its obligation `invalid` at `verify`, with no new gate. Only
          // `decision` is included; an `issue` URI and a `commit` object name are not local files and
          // are never probed, the same line `sourceRefs[].uri` already sits on.
          // The `:// ` clause mirrors `recordReceiptInconsistency`'s decision-locator rule exactly, and
          // is not redundant with it. Without it a `decision` receipt carrying a URI would be cited as a
          // local path, `exists` would report it absent, and the ladder's `artifactNotFound` arm — which
          // sits ABOVE the receipt arms — would report "artifact not found" for what is really "your
          // locator is the wrong kind for this receipt". The author would be sent looking for a file
          // they never meant to name.
          match declaration.RecordReceipt with
          | Some receipt when
              String.Equals(receipt.Kind.Trim(), "decision", StringComparison.OrdinalIgnoreCase)
              && named receipt.Locator
              && citedPathIsContained receipt.Locator
              && not (receipt.Locator.Contains "://")
              ->
              receipt.Locator
          | _ -> ()
          match declaration.JourneyReceipt with
          | Some receipt when
              not (String.IsNullOrWhiteSpace receipt.ObservedReportSource)
              && citedPathIsContained receipt.ObservedReportSource
              ->
              receipt.ObservedReportSource
          | _ -> ()
          match declaration.PerformanceBudget with
          | Some budget when named budget.ArtifactPath && citedPathIsContained budget.ArtifactPath ->
              budget.ArtifactPath
          | _ -> () ]
        |> List.distinct
        |> List.sort

    /// The cited-artifact existence rule (FS.GG.SDD#349, FR-006/FR-007), stated once. A declaration
    /// that claims a pass while citing a file that is not on disk asserts that
    /// something was proven by an artifact nobody can open.
    ///
    /// Gated on the passing claim inside the rule, so that the three
    /// call sites — the `evidence` pre-write gate, the `ED-` cascade, and the `TD-` mirror — cannot
    /// drift on which declarations are held to it. A deferral legitimately cites an artifact that
    /// does not exist yet; blocking it would teach authors to stop deferring.
    let missingCitedArtifacts (exists: string -> bool) (declaration: EvidenceDeclaration) =
        if normalizedEvidenceResult declaration.Result <> "pass" then
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

            run.DigestContract = "exact-bytes-v1"
            && Regex.IsMatch(run.CandidateCommit, "^[a-fA-F0-9]{40,64}$", RegexOptions.CultureInvariant)
            && outcome = "passed"
            && run.Failed = 0
            && run.Passed > 0)

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
        elif not (String.Equals(run.DigestContract, "exact-bytes-v1", StringComparison.Ordinal)) then
            Some
                $"digestContract '{run.DigestContract}' is legacy; re-run evidence --sync-observed-run to migrate it to exact-bytes-v1"
        elif not (Regex.IsMatch(run.CandidateCommit, "^[a-fA-F0-9]{40,64}$", RegexOptions.CultureInvariant)) then
            Some "candidateCommit is missing or is not an immutable Git commit id; re-run evidence --sync-observed-run"
        elif String.IsNullOrWhiteSpace run.Source then
            Some "source names no report"
        else
            None

    /// Does this declaration claim a pass? Provenance is metadata; observation decides whether the
    /// claim is usable at a protected boundary.
    let claimsRealPass (declaration: EvidenceDeclaration) =
        normalizedEvidenceResult declaration.Result = "pass"

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

    /// The record receipt's internal-consistency rule (FS.GG.SDD#865, FR-003) — the exact shape of
    /// `observedRunInconsistency` above, and for the same reason. A record receipt is *authored*: unlike
    /// `ObservedRun`, which `--from-test-report` derives from a file, there is no runner to read a record
    /// from, so every field here is user input. Judging the receipt's form is therefore the whole of what
    /// stops it becoming a second, roomier place to type `pass`.
    ///
    /// Returns the reason, or `None` when the receipt is coherent. Total and I/O-free: this decides form,
    /// never existence or content. Existence of a `decision` record is the #349 cited-artifact cascade's
    /// job (via `citedArtifactPaths`), byte-currency is `recordReceiptIsCurrent`'s, and the CONTENT of a
    /// remote record is a later reader's — deliberately, because reading it here would mean dereferencing
    /// it, which DEC-001 refused.
    let recordReceiptInconsistency (receipt: RecordReceipt) : string option =
        // Normalised, NOT compared raw — the same agreement `isObserved` and `observedRunInconsistency`
        // reached over `outcome`: two rules reading one field must read it the same way, or the author
        // is told a receipt is fine by one and silently ignored by the other.
        let kind = receipt.Kind.Trim().ToLowerInvariant()
        let locator = receipt.Locator.Trim()

        // The recorded form, identical to `ObservedRun.Digest`: `sha256:` + a 64-hex digest.
        let wellFormedDigest =
            Regex.IsMatch(receipt.Digest, @"^sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant)

        let knownKinds = String.Join(", ", recordReceiptKinds)

        if not (List.contains kind recordReceiptKinds) then
            Some $"kind '{receipt.Kind}' is not one of {knownKinds}"
        elif not (String.Equals(receipt.LocatorContract, recordLocatorContract, StringComparison.Ordinal)) then
            Some $"locatorContract '{receipt.LocatorContract}' is not '{recordLocatorContract}'"
        elif String.IsNullOrWhiteSpace locator then
            Some "locator names no record"
        // Each kind's locator is checked against the form THAT kind must take, because a locator
        // checked only for non-emptiness is a free-text field wearing a schema. The forms are
        // deliberately generic: no tracker host, no repository, no provider literal appears here.
        // `citedPathIsContained` answers "does this path escape the repository?", and a URI does not —
        // `https://host/x` carries no `..` and is not rooted, so containment alone ACCEPTS it. That is
        // right for what containment is for and wrong for what a `decision` locator means: a decision
        // record is a file in this repository whose bytes the receipt binds, and a URI names no such
        // file. Both conditions are therefore required, and the scheme check is the one that stops a
        // remote locator from being smuggled in under the strongest kind — which would let it claim the
        // byte-binding it can never have.
        elif
            kind = "decision"
            && (not (citedPathIsContained locator) || locator.Contains "://")
        then
            Some $"decision locator '{locator}' is not a contained repository-relative path"
        elif
            kind = "issue"
            && not (Regex.IsMatch(locator, @"^https://[^\s]+$", RegexOptions.CultureInvariant))
        then
            Some $"issue locator '{locator}' is not an absolute https URI"
        elif
            kind = "commit"
            && not (Regex.IsMatch(locator, @"^[a-f0-9]{40}$", RegexOptions.CultureInvariant))
        then
            Some $"commit locator '{locator}' is not a 40-character hex object name"
        // A repository-local record is byte-bound; a remote one has no local bytes to bind, and a
        // digest offered for it would be an unverifiable number. Both directions are errors: the
        // missing binding weakens the receipt, the impossible one misrepresents it.
        elif kind = "decision" && not wellFormedDigest then
            Some $"decision receipt digest '{receipt.Digest}' is not a sha256:<hex> digest"
        elif kind <> "decision" && not (String.IsNullOrWhiteSpace receipt.Digest) then
            Some $"a {kind} receipt has no local bytes to digest, but carries digest '{receipt.Digest}'"
        elif String.IsNullOrWhiteSpace receipt.Statement then
            // Without this the receipt says only "a record exists", leaving a reader who opens the
            // locator nothing to check the record AGAINST. The statement is what makes it refutable.
            Some "statement says nothing the record is asserted to establish"
        elif String.IsNullOrWhiteSpace receipt.RecordedAt then
            Some "recordedAt names no date"
        else
            match
                DateTimeOffset.TryParse(
                    receipt.RecordedAt,
                    Globalization.CultureInfo.InvariantCulture,
                    Globalization.DateTimeStyles.RoundtripKind
                )
            with
            | true, _ -> None
            | _ -> Some $"recordedAt '{receipt.RecordedAt}' is not an ISO-8601 instant"

    /// Does this declaration rest on a durable record the tool can hand a later reader?
    /// (FS.GG.SDD#865, FR-002/FR-005.) The record twin of `isObserved`, and deliberately NOT folded
    /// into it: keeping the two rules separate is what makes DEC-002 hold — a record receipt cannot
    /// discharge a test obligation, because nothing that asks "was a run observed?" consults this.
    ///
    /// Total and I/O-free, like `isObserved`. A coherent receipt is the whole condition here; whether
    /// the record it names is still on disk and still says the same bytes is decided by the cited-path
    /// cascade and by `recordReceiptIsCurrent` at the effect edge, exactly as for `ObservedRun`.
    let isRecorded (declaration: EvidenceDeclaration) =
        declaration.RecordReceipt
        |> Option.exists (fun receipt -> Option.isNone (recordReceiptInconsistency receipt))

    /// Was an *obligation* — matched by these declarations — discharged by a durable record?
    /// (FS.GG.SDD#865, FR-005.) The record twin of `obligationIsObserved`, sharing its two load-bearing
    /// decisions rather than restating them: only the declarations claiming a real pass are consulted,
    /// and **all** of them must be recorded. One receipt must not launder a bare `result: pass` sitting
    /// beside it — the same fail-open defect class, one channel over.
    let obligationIsRecorded (declarations: EvidenceDeclaration list) =
        let passes = declarations |> List.filter claimsRealPass

        not (List.isEmpty passes) && passes |> List.forall isRecorded

    /// **The one kind-directed discharge rule** (FS.GG.SDD#865, FR-008 / DEC-002), consumed by the
    /// `ED-` ladder, the `TD-` ladder, `ship`, and the committed verdict.
    ///
    /// It exists so those four cannot drift on what discharges an obligation — the same discipline
    /// `obligationIsObserved` already imposed on what "observed" means, and `missingCitedArtifacts` on
    /// what "cited" means. A caller that branched on the class itself would be one edit away from a
    /// `verify` that certifies what `ship` refuses.
    ///
    /// Fail-closed in both directions: a record receipt never discharges a test-class obligation, and an
    /// observed run never discharges a record-class one. The alternative — accept either — would let an
    /// author attach whichever receipt they happen to hold to whichever obligation is blocking, which is
    /// precisely the laundering the `forall` above exists to prevent.
    let obligationDischarged (dischargeClass: string) (declarations: EvidenceDeclaration list) =
        if isRecordDischargeClass dischargeClass then
            obligationIsRecorded declarations
        else
            obligationIsObserved declarations

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
              DigestContract: string option
              CandidateCommit: string option
              Outcome: string option
              Passed: int
              Failed: int
              Skipped: int }

        let observedRunDraftSeed =
            { Source = None
              Digest = None
              DigestContract = None
              CandidateCommit = None
              Outcome = None
              Passed = 0
              Failed = 0
              Skipped = 0 }

        let observedRunFields: ArtifactCodec.FieldCodec<ObservedRunDraft> list =
            [ ArtifactCodec.optionalScalar "source" (fun r -> r.Source) (fun v r -> { r with Source = v })
              ArtifactCodec.optionalScalar "digest" (fun r -> r.Digest) (fun v r -> { r with Digest = v })
              ArtifactCodec.optionalScalar "digestContract" (fun r -> r.DigestContract) (fun v r ->
                  { r with DigestContract = v })
              ArtifactCodec.optionalScalar "candidateCommit" (fun r -> r.CandidateCommit) (fun v r ->
                  { r with CandidateCommit = v })
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
                      DigestContract = draft.DigestContract |> Option.defaultValue "normalized-text-v1"
                      CandidateCommit = draft.CandidateCommit |> Option.defaultValue ""
                      Outcome = draft.Outcome |> Option.defaultValue ""
                      Passed = draft.Passed
                      Failed = draft.Failed
                      Skipped = draft.Skipped }
            | _ -> None

        let lowerObservedRun (run: ObservedRun) : ObservedRunDraft =
            { Source = Some run.Source
              Digest = Some run.Digest
              DigestContract = Some run.DigestContract
              CandidateCommit = Some run.CandidateCommit
              Outcome = Some run.Outcome
              Passed = run.Passed
              Failed = run.Failed
              Skipped = run.Skipped }

        // FS.GG.SDD#865. The record receipt reads through a draft for exactly the reason the observed-run
        // receipt does: its identifying scalars are null-aware, and a partial or blank mapping must lift
        // to `None` (no receipt) rather than to a receipt made of empty strings. A receipt of empty
        // strings that still counted as "recorded" would be the fail-open this channel exists to avoid
        // opening.
        //
        // `digest` is option-carrying but lifts to a plain string, because absence is MEANINGFUL and
        // legal here (an `issue`/`commit` record has no local bytes) rather than a defect —
        // `recordReceiptInconsistency` decides per kind whether the absence is right, instead of the
        // codec dropping the receipt over it.
        type RecordReceiptDraft =
            { Kind: string option
              Locator: string option
              LocatorContract: string option
              Digest: string option
              Statement: string option
              RecordedAt: string option }

        let recordReceiptDraftSeed =
            { Kind = None
              Locator = None
              LocatorContract = None
              Digest = None
              Statement = None
              RecordedAt = None }

        let recordReceiptFields: ArtifactCodec.FieldCodec<RecordReceiptDraft> list =
            [ ArtifactCodec.optionalScalar "kind" (fun r -> r.Kind) (fun v r -> { r with Kind = v })
              ArtifactCodec.optionalScalar "locator" (fun r -> r.Locator) (fun v r -> { r with Locator = v })
              ArtifactCodec.optionalScalar "locatorContract" (fun r -> r.LocatorContract) (fun v r ->
                  { r with LocatorContract = v })
              ArtifactCodec.optionalScalar "digest" (fun r -> r.Digest) (fun v r -> { r with Digest = v })
              ArtifactCodec.optionalScalar "statement" (fun r -> r.Statement) (fun v r -> { r with Statement = v })
              ArtifactCodec.optionalScalar "recordedAt" (fun r -> r.RecordedAt) (fun v r -> { r with RecordedAt = v }) ]

        /// A record receipt exists only if it names BOTH what class of record backs the claim and the
        /// record itself. Either alone is not a receipt: a kind with no locator is a category, and a
        /// locator with no kind is a string nobody knows how to check. Both blank/absent → `None`, and
        /// the obligation is unrecorded.
        ///
        /// Everything the draft does NOT gate on is lifted verbatim — an empty `locatorContract`,
        /// `statement` or `recordedAt` survives into the receipt and is REFUSED by
        /// `recordReceiptInconsistency` with a reason naming the field. That is the deliberate split:
        /// the codec decides whether a receipt was written, the coherence rule decides whether it is
        /// any good, and a malformed receipt is never silently downgraded to "no receipt at all".
        let liftRecordReceipt (draft: RecordReceiptDraft) : RecordReceipt option =
            match draft.Kind, draft.Locator with
            | Some kind, Some locator when
                not (String.IsNullOrWhiteSpace kind) && not (String.IsNullOrWhiteSpace locator)
                ->
                Some
                    { Kind = kind
                      Locator = locator
                      LocatorContract = draft.LocatorContract |> Option.defaultValue ""
                      Digest = draft.Digest |> Option.defaultValue ""
                      Statement = draft.Statement |> Option.defaultValue ""
                      RecordedAt = draft.RecordedAt |> Option.defaultValue "" }
            | _ -> None

        let lowerRecordReceipt (receipt: RecordReceipt) : RecordReceiptDraft =
            { Kind = Some receipt.Kind
              Locator = Some receipt.Locator
              LocatorContract = Some receipt.LocatorContract
              // Rendered only when present, so a round-trip of an `issue`/`commit` receipt does not
              // grow a `digest: ` line the reader would then have to explain.
              Digest =
                (if String.IsNullOrWhiteSpace receipt.Digest then
                     None
                 else
                     Some receipt.Digest)
              Statement = Some receipt.Statement
              RecordedAt = Some receipt.RecordedAt }

        let journeyReceiptSeed: JourneyReceipt =
            { SchemaVersion = 0
              RunnerIdentity = ""
              RunnerVersion = ""
              Origin = ""
              RouteId = ""
              ScenarioId = ""
              TestId = ""
              InputKind = ""
              InputDigest = ""
              ReplayDigest = ""
              TraceDigest = ""
              InitialFingerprint = ""
              TerminalFingerprint = ""
              TerminalPredicateReached = false
              Outcome = ""
              MaximumSteps = 0
              ActualSteps = 0
              ObservedReportSource = ""
              ObservedReportDigest = ""
              ObservedTestName = ""
              ObservedTestOutcome = "" }

        let private journeyRunnerFields: ArtifactCodec.FieldCodec<JourneyReceipt> list =
            [ ArtifactCodec.requiredScalar "identity" _.RunnerIdentity (fun value receipt ->
                  { receipt with RunnerIdentity = value })
              ArtifactCodec.requiredScalar "version" _.RunnerVersion (fun value receipt ->
                  { receipt with RunnerVersion = value }) ]

        let private journeyInputFields: ArtifactCodec.FieldCodec<JourneyReceipt> list =
            [ ArtifactCodec.requiredScalar "kind" _.InputKind (fun value receipt -> { receipt with InputKind = value })
              ArtifactCodec.requiredScalar "digest" _.InputDigest (fun value receipt ->
                  { receipt with InputDigest = value }) ]

        let private journeyTerminalFields: ArtifactCodec.FieldCodec<JourneyReceipt> list =
            [ ArtifactCodec.boolScalar "reached" false _.TerminalPredicateReached (fun value receipt ->
                  { receipt with
                      TerminalPredicateReached = value }) ]

        let private journeyObservedReportFields: ArtifactCodec.FieldCodec<JourneyReceipt> list =
            [ ArtifactCodec.requiredScalar "source" _.ObservedReportSource (fun value receipt ->
                  { receipt with
                      ObservedReportSource = value })
              ArtifactCodec.requiredScalar "digest" _.ObservedReportDigest (fun value receipt ->
                  { receipt with
                      ObservedReportDigest = value })
              ArtifactCodec.requiredScalar "testName" _.ObservedTestName (fun value receipt ->
                  { receipt with
                      ObservedTestName = value })
              ArtifactCodec.requiredScalar "outcome" _.ObservedTestOutcome (fun value receipt ->
                  { receipt with
                      ObservedTestOutcome = value }) ]

        let journeyReceiptFields: ArtifactCodec.FieldCodec<JourneyReceipt> list =
            [ ArtifactCodec.intScalar "schemaVersion" 0 _.SchemaVersion (fun value receipt ->
                  { receipt with SchemaVersion = value })
              ArtifactCodec.nested "runner" journeyRunnerFields journeyReceiptSeed id (fun value receipt ->
                  { receipt with
                      RunnerIdentity = value.RunnerIdentity
                      RunnerVersion = value.RunnerVersion })
              ArtifactCodec.requiredScalar "origin" _.Origin (fun value receipt -> { receipt with Origin = value })
              ArtifactCodec.requiredScalar "routeId" _.RouteId (fun value receipt -> { receipt with RouteId = value })
              ArtifactCodec.requiredScalar "scenarioId" _.ScenarioId (fun value receipt ->
                  { receipt with ScenarioId = value })
              ArtifactCodec.requiredScalar "testId" _.TestId (fun value receipt -> { receipt with TestId = value })
              ArtifactCodec.nested "input" journeyInputFields journeyReceiptSeed id (fun value receipt ->
                  { receipt with
                      InputKind = value.InputKind
                      InputDigest = value.InputDigest })
              ArtifactCodec.requiredScalar "replayDigest" _.ReplayDigest (fun value receipt ->
                  { receipt with ReplayDigest = value })
              ArtifactCodec.requiredScalar "traceDigest" _.TraceDigest (fun value receipt ->
                  { receipt with TraceDigest = value })
              ArtifactCodec.requiredScalar "initialFingerprint" _.InitialFingerprint (fun value receipt ->
                  { receipt with
                      InitialFingerprint = value })
              ArtifactCodec.requiredScalar "terminalFingerprint" _.TerminalFingerprint (fun value receipt ->
                  { receipt with
                      TerminalFingerprint = value })
              ArtifactCodec.nested "terminalPredicate" journeyTerminalFields journeyReceiptSeed id (fun value receipt ->
                  { receipt with
                      TerminalPredicateReached = value.TerminalPredicateReached })
              ArtifactCodec.requiredScalar "outcome" _.Outcome (fun value receipt -> { receipt with Outcome = value })
              ArtifactCodec.intScalar "maximumSteps" 0 _.MaximumSteps (fun value receipt ->
                  { receipt with MaximumSteps = value })
              ArtifactCodec.intScalar "actualSteps" 0 _.ActualSteps (fun value receipt ->
                  { receipt with ActualSteps = value })
              ArtifactCodec.nested
                  "observedTestReport"
                  journeyObservedReportFields
                  journeyReceiptSeed
                  id
                  (fun value receipt ->
                      { receipt with
                          ObservedReportSource = value.ObservedReportSource
                          ObservedReportDigest = value.ObservedReportDigest
                          ObservedTestName = value.ObservedTestName
                          ObservedTestOutcome = value.ObservedTestOutcome }) ]

        let performanceBudgetSeed: PerformanceBudgetDeclaration =
            { ArtifactPath = ""
              Intent = None
              TargetFps = 0
              WorkloadIds = []
              StressWorkloadIds = []
              WorkloadDefinitionDigests = []
              CurrencyToken = ""
              CapturedAfterUtc = ""
              MaxP95Ms = -1m
              MaxP99Ms = -1m
              MaxCatchUpFrames = -1
              MeasurementScope = ""
              RequiredCapability = ""
              LiveCompositorRequired = false
              DeferralIssue = None }

        let performanceIntentSeed: PerformanceIntentDeclaration =
            { Id = ""
              Disposition = ""
              TargetFps = 0
              WorkloadIds = []
              WorkloadDefinitionDigests = []
              MaximumExpectedScale = ""
              MaxP95Ms = -1m
              MaxP99Ms = -1m
              MaxCatchUpFrames = -1
              StructuralCostBudgets = []
              RequiredCapability = ""
              LiveCompositorRequired = false
              DeferralIssue = None
              EvidenceRefs = []
              Rationale = None }

        let performanceIntentFields: ArtifactCodec.FieldCodec<PerformanceIntentDeclaration> list =
            [ ArtifactCodec.requiredScalar "id" _.Id (fun value intent -> { intent with Id = value })
              ArtifactCodec.requiredScalar "disposition" _.Disposition (fun value intent ->
                  { intent with Disposition = value })
              ArtifactCodec.intScalar "targetFps" 0 _.TargetFps (fun value intent -> { intent with TargetFps = value })
              ArtifactCodec.alwaysInlineList "workloadIds" _.WorkloadIds (fun value intent ->
                  { intent with WorkloadIds = value })
              ArtifactCodec.alwaysInlineList
                  "workloadDefinitionDigests"
                  _.WorkloadDefinitionDigests
                  (fun value intent ->
                      { intent with
                          WorkloadDefinitionDigests = value })
              ArtifactCodec.requiredScalar "maximumExpectedScale" _.MaximumExpectedScale (fun value intent ->
                  { intent with
                      MaximumExpectedScale = value })
              ArtifactCodec.mappedScalar "maxP95Ms" decimalText (decimalInvariant -1m) _.MaxP95Ms (fun value intent ->
                  { intent with MaxP95Ms = value })
              ArtifactCodec.mappedScalar "maxP99Ms" decimalText (decimalInvariant -1m) _.MaxP99Ms (fun value intent ->
                  { intent with MaxP99Ms = value })
              ArtifactCodec.intScalar "maxCatchUpFrames" -1 _.MaxCatchUpFrames (fun value intent ->
                  { intent with MaxCatchUpFrames = value })
              ArtifactCodec.alwaysInlineList "structuralCostBudgets" _.StructuralCostBudgets (fun value intent ->
                  { intent with
                      StructuralCostBudgets = value })
              ArtifactCodec.requiredScalar "requiredCapability" _.RequiredCapability (fun value intent ->
                  { intent with
                      RequiredCapability = value })
              ArtifactCodec.boolScalar "liveCompositorRequired" false _.LiveCompositorRequired (fun value intent ->
                  { intent with
                      LiveCompositorRequired = value })
              ArtifactCodec.optionalScalar "deferralIssue" _.DeferralIssue (fun value intent ->
                  { intent with DeferralIssue = value })
              ArtifactCodec.alwaysInlineList "evidenceRefs" _.EvidenceRefs (fun value intent ->
                  { intent with EvidenceRefs = value })
              ArtifactCodec.optionalScalar "rationale" _.Rationale (fun value intent ->
                  { intent with Rationale = value }) ]

        let performanceBudgetFields: ArtifactCodec.FieldCodec<PerformanceBudgetDeclaration> list =
            [ ArtifactCodec.requiredScalar "artifactPath" _.ArtifactPath (fun value budget ->
                  { budget with ArtifactPath = value })
              ArtifactCodec.optionalNestedVia
                  "intent"
                  performanceIntentFields
                  performanceIntentSeed
                  Some
                  id
                  _.Intent
                  (fun value budget -> { budget with Intent = value })
              ArtifactCodec.intScalar "targetFps" 0 _.TargetFps (fun value budget -> { budget with TargetFps = value })
              ArtifactCodec.alwaysInlineList "workloadIds" _.WorkloadIds (fun value budget ->
                  { budget with WorkloadIds = value })
              ArtifactCodec.alwaysInlineList "stressWorkloadIds" _.StressWorkloadIds (fun value budget ->
                  { budget with
                      StressWorkloadIds = value })
              ArtifactCodec.alwaysInlineList
                  "workloadDefinitionDigests"
                  _.WorkloadDefinitionDigests
                  (fun value budget ->
                      { budget with
                          WorkloadDefinitionDigests = value })
              ArtifactCodec.requiredScalar "currencyToken" _.CurrencyToken (fun value budget ->
                  { budget with CurrencyToken = value })
              ArtifactCodec.requiredScalar "capturedAfterUtc" _.CapturedAfterUtc (fun value budget ->
                  { budget with CapturedAfterUtc = value })
              ArtifactCodec.mappedScalar "maxP95Ms" decimalText (decimalInvariant -1m) _.MaxP95Ms (fun value budget ->
                  { budget with MaxP95Ms = value })
              ArtifactCodec.mappedScalar "maxP99Ms" decimalText (decimalInvariant -1m) _.MaxP99Ms (fun value budget ->
                  { budget with MaxP99Ms = value })
              ArtifactCodec.intScalar "maxCatchUpFrames" -1 _.MaxCatchUpFrames (fun value budget ->
                  { budget with MaxCatchUpFrames = value })
              ArtifactCodec.requiredScalar "measurementScope" _.MeasurementScope (fun value budget ->
                  { budget with MeasurementScope = value })
              ArtifactCodec.requiredScalar "requiredCapability" _.RequiredCapability (fun value budget ->
                  { budget with
                      RequiredCapability = value })
              ArtifactCodec.boolScalar "liveCompositorRequired" false _.LiveCompositorRequired (fun value budget ->
                  { budget with
                      LiveCompositorRequired = value })
              ArtifactCodec.optionalScalar "deferralIssue" _.DeferralIssue (fun value budget ->
                  { budget with DeferralIssue = value }) ]

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
              RecordReceipt = None
              JourneyReceipt = None
              PerformanceBudget = None
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
              // FS.GG.SDD#865. AUTHORED, unlike `observedRun` — there is no runner to read a record
              // from, which is the whole reason this channel exists — but round-tripped through the
              // same shared field list for the same reason: a receipt the renderer emitted and the
              // reader dropped would silently un-record every obligation on the next `evidence` run.
              ArtifactCodec.optionalNestedVia
                  "recordReceipt"
                  recordReceiptFields
                  recordReceiptDraftSeed
                  liftRecordReceipt
                  lowerRecordReceipt
                  (fun d -> d.RecordReceipt)
                  (fun v d -> { d with RecordReceipt = v })
              ArtifactCodec.optionalNestedVia
                  "journeyReceipt"
                  journeyReceiptFields
                  journeyReceiptSeed
                  Some
                  id
                  (fun d -> d.JourneyReceipt)
                  (fun v d -> { d with JourneyReceipt = v })
              ArtifactCodec.optionalNestedVia
                  "performanceBudget"
                  performanceBudgetFields
                  performanceBudgetSeed
                  Some
                  id
                  (fun d -> d.PerformanceBudget)
                  (fun v d -> { d with PerformanceBudget = v })
              ArtifactCodec.optionalScalar "rationale" (fun d -> d.Rationale) (fun v d -> { d with Rationale = v })
              ArtifactCodec.optionalScalar "owner" (fun d -> d.Owner) (fun v d -> { d with Owner = v })
              ArtifactCodec.optionalScalar "scope" (fun d -> d.Scope) (fun v d -> { d with Scope = v })
              ArtifactCodec.optionalScalar "laterLifecycleVisibility" (fun d -> d.LaterLifecycleVisibility) (fun v d ->
                  { d with LaterLifecycleVisibility = v })
              ArtifactCodec.alwaysInlineList "notes" (fun d -> d.Notes) (fun v d -> { d with Notes = v }) ]

    // `parseEvidenceSourceRefs`/`parseSyntheticDisclosure` were retired when the declaration moved onto
    // `declarationFields` (FS.GG.SDD#260): its `recordList "sourceRefs"` and
    // `optionalNestedVia "syntheticDisclosure"` now own both directions for those records.

    let parsePerformanceIntentYaml (yaml: string) =
        match parseYamlDocument yaml with
        | YamlRoot root ->
            match tryNodeAt [ "performanceIntent" ] root |> Option.bind tryMapping with
            | None -> Ok None
            | Some mapping ->
                ArtifactCodec.foldInto EvidenceCodec.performanceIntentFields EvidenceCodec.performanceIntentSeed mapping
                |> Result.map Some
        | YamlEmpty -> Error "front matter is empty"
        | YamlMalformed(message, line, column) -> Error $"YAML syntax error at line {line}, column {column}: {message}"

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
