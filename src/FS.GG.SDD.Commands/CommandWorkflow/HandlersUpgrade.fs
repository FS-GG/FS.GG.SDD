namespace FS.GG.SDD.Commands.Internal

open Fsgg
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal.Foundation
open FS.GG.SDD.Commands.Internal.HandlersDoctor

/// `fsgg-sdd upgrade` handler (feature 053, US2–US4). The reconciliation verb: it re-derives
/// its next step from the interpreted-effect log each `nextLifecycleEffects` tick (like
/// `HandlersScaffold`, no new model state field). Only the *actionable* preview steps
/// (`Outcome = "wouldApply"`) are reconciled; each is shown as a diff and applied only after
/// its own `Confirm` (interactive) or under `--yes`. `upgrade` is the **only** command that
/// mutates for remediation (FR-006/FR-008): the CLI self-update `RunProcess`, the no-clobber
/// re-seed `WriteFile`s (init's `AgentGuidanceTarget` effects for the *missing* paths only,
/// R8/FR-010), and (value-agnostically inert here, R6) the consumer-only re-pin.
module internal HandlersUpgrade =

    // SDD's own tool package id (not a provider/template/rendering literal). The self-update
    // is orchestrated at the same `RunProcess` edge scaffold uses; the updated binary takes
    // effect on the next invocation (spec Assumption / R4).
    let selfUpdatePackageId = "FS.GG.SDD.Cli"

    let selfUpdateEffect =
        RunProcess("dotnet", [ "tool"; "update"; selfUpdatePackageId; "--global" ], "")

    let private isSelfUpdate effect =
        match effect with
        | RunProcess("dotnet", "tool" :: "update" :: _, _) -> true
        | _ -> false

    // No-clobber re-seed: the init `AgentGuidanceTarget` WriteFile effects for the MISSING
    // seeded paths only (R8/FR-010). Filtering `initEffects` to the missing set — and the
    // interpreter's `canOverwrite AgentGuidanceTarget` refusal on a present file — doubly
    // guarantees a present, author-edited artifact is never overwritten (US4-AC2).
    let reSeedEffects (request: CommandRequest) (missing: string list) =
        let missingSet = missing |> List.map normalizeRelativePath |> Set.ofList

        initEffects request
        |> List.filter (fun effect ->
            match effect with
            | WriteFile(path, _, _) -> Set.contains (normalizeRelativePath path) missingSet
            | _ -> false)

    let private driverIdOfPath (path: string) =
        Fsgg.Schemas.agentSkillRoots
        |> List.tryPick (fun root ->
            let prefix = root + "/skills/"

            if path.StartsWith(prefix, System.StringComparison.Ordinal) then
                path.Substring(prefix.Length).Split('/')
                |> Array.tryHead
                |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
            else
                None)

    /// Before a v1/SKILL-only directory can be completed and recorded as a schema-v2 driver tree,
    /// read every preserved file that the new provenance record would attest. The missing targets
    /// themselves are supplied from the verified embedded plan; these are the complementary files
    /// already on disk and therefore outside the write set.
    let private ownerBackfillPreservedFiles model (targets: string list) =
        match resolveProvenance model with
        | Some record ->
            let targetSet = targets |> List.map normalizeRelativePath |> Set.ofList

            let presentIds =
                Set.ofList (
                    SeededSkills.skillNames
                    @ (Drift.productSkillEntries (Some record) |> List.map fst)
                )

            let driver = DriverSkills.plan presentIds
            let affectedDriverIds = targets |> List.choose driverIdOfPath |> Set.ofList

            driver.ProvenancePaths
            |> List.filter (fun (path, _) ->
                driverIdOfPath path |> Option.exists affectedDriverIds.Contains
                && not (Set.contains (normalizeRelativePath path) targetSet))
        | None -> []

    // FS-GG/FS.GG.SDD#752 AC4. `DriverSkills.ProvenancePaths` and `GameSkills.ProvenancePaths` both
    // record `Fsgg.SkillMirror.sha256` of the body written, so that is the one function that
    // reproduces the value here — this used to hash `UTF8.GetBytes file.Text` un-normalized, which
    // was the OTHER domain and disagreed with `Drift`'s verification of the very same field.
    //
    // `file.Text` arrives from `CommandEffects.readFile`, i.e. through `SkillMirror.decodeBody`, so
    // it is BOM-free; `SkillMirror.sha256` then folds `\r\n`. That composition is precisely the
    // domain the digest was recorded in, so the comparison is exact rather than approximately right
    // on the content that happens to ship.
    let private preservedFilesVerified model preservedFiles =
        preservedFiles
        |> List.forall (fun (path, expectedSha256) ->
            snapshot path model
            |> Option.exists (fun file -> Fsgg.SkillMirror.sha256 file.Text = expectedSha256))

    // ADR-0063 / FS-GG/FS.GG.SDD#624: the owner-sourced (driver + product classes) skill copies among a
    // re-seed step's targets, reconstructed as no-clobber writes from the SAME embedded, content-
    // addressed plan the drift preview used (`Drift.ownerSourcedBackfill`, filtered to the step's
    // declared targets), so the applied bytes are exactly the verified bytes previewed — a backfill
    // can never write an unverified body (ADR-0014). Empty when the scaffold has no provenance or no
    // owner package is embedded. These are disjoint from `reSeedEffects` (which reconstructs only the
    // SEEDED init writes among the targets), so the two together cover every re-seed target once.
    let ownerBackfillEffects model (targets: string list) =
        match resolveProvenance model with
        | Some record ->
            let targetSet = targets |> Set.ofList

            let presentIds =
                Set.ofList (
                    SeededSkills.skillNames
                    @ (Drift.productSkillEntries (Some record) |> List.map fst)
                )

            let driver = DriverSkills.plan presentIds
            let product = GameSkills.plan (record.EffectiveParameters |> Map.ofList)

            let writes =
                driver.Writes @ product.Writes
                |> List.filter (fun effect -> effectPath effect |> Option.exists targetSet.Contains)

            let affectedDriverIds = targets |> List.choose driverIdOfPath |> Set.ofList

            let newDriverPaths =
                driver.ProvenancePaths
                |> List.filter (fun (path, _) -> driverIdOfPath path |> Option.exists affectedDriverIds.Contains)
                |> List.map (fun (path, sha256) ->
                    { Path = path
                      Owner = ArtifactOwner.Driver
                      Sha256 = Some sha256 }
                    : ScaffoldProvenance.ScaffoldProducedPath)

            let provenanceWrite =
                if List.isEmpty newDriverPaths then
                    []
                else
                    let driverPaths =
                        record.DriverPaths @ newDriverPaths
                        |> List.groupBy (fun path -> path.Path)
                        |> List.map (fun (_, paths) -> List.last paths)
                        |> List.sortBy (fun path -> path.Path)

                    let updated =
                        { record with
                            DriverPaths = driverPaths }

                    [ WriteFile(ScaffoldProvenance.provenancePath, ScaffoldProvenance.serialize updated, GeneratedView) ]

            writes @ provenanceWrite
        | None -> []

    let applyEffectsFor model (request: CommandRequest) (step: ReconciliationStep) =
        match step.StepId with
        | ReconciliationStepId.CliSelfUpdate -> [ selfUpdateEffect ]
        | ReconciliationStepId.ArtifactReSeed ->
            reSeedEffects request step.TargetPaths
            @ ownerBackfillEffects model step.TargetPaths
        // templateRePin is `noTarget` in this feature (R6) and never actionable.
        | ReconciliationStepId.TemplateRePin -> []

    // FS-GG/FS.GG.SDD#736: the advisory that closes an `upgrade` over un-repaired skill drift must
    // name the condition it actually found. There were three conditions and ONE sentence, and the
    // sentence described the wrong one for the commonest of them: a file present in one root only —
    // a `.DS_Store`, a `SKILL.md.orig` from a merge, a provider file dropped from one root — is
    // reported at the roots that LACK it, under "some copies diverge from their canonical body —
    // re-scaffold or restore the canonical skill sources", which reads as an instruction to CREATE
    // `.DS_Store` in the other two roots.
    //
    // No class is repairable by this lane, and #736 AC2 requires that be said plainly rather than
    // letting `ResidualDrift` recur forever under a hint describing a different failure. The reason
    // is structural, not a gap waiting on a flag: the re-seed reconstructs only the init `WriteFile`s
    // whose paths are in `MissingArtifactPaths` (`reSeedEffects`, the SEEDED skeleton set), the owner
    // backfill only paths in the embedded verified plan, and there is no delete effect anywhere in
    // the model. So `upgrade` can neither add an auxiliary copy, remove a stray one, nor rewrite a
    // divergent body — and what it CAN repair (a missing seeded copy, and since #733 a missing
    // owner-sourced copy) is already subtracted before these fire, so a sentence saying "re-running
    // will not clear it" is true of what is left.

    // FS-GG/FS.GG.SDD#747, AC3 condition 3: "the ignored set is stated where a reader of the
    // advisory finds it, so 'this tree is converged' is never quietly conditional on an invisible
    // exclusion."
    //
    // `NextActionHint` is that place. `DoctorSummary` carries no prose field at all — it reports
    // paths and a coherence bit — so `upgrade`'s hint is the ONLY advisory channel this lane has,
    // and it is rendered in both projections (`upgradeNextAction:` and `nextActionHint`).
    //
    // It fires on WHAT WAS EXCLUDED, not on the rule: an empty list adds nothing, so a workspace
    // with no junk reads byte-identically to the pre-#747 text (which is what the exact-wording
    // regression on byte divergence pins). When it does fire it names the files AND the complete
    // rule, because a reader who is told only "some files were ignored" has been told the tree is
    // converged without being told what that is conditional on.
    let private ignoredJunkStatement (ignored: string list) =
        let names = String.concat ", " Drift.junkFileNames
        let suffixes = String.concat ", " Drift.junkFileSuffixes
        let excluded = String.concat ", " ignored

        $"Excluded from the comparison as OS/VCS junk: {excluded}. "
        + $"That exclusion is a fixed list built into the tool and is not configurable — these exact names ({names}), and these exact suffixes ({suffixes}). "
        + "A file recorded in this workspace's scaffold provenance is never excluded, whatever it is called, so a producer that genuinely ships one of these names is still compared and still reported."

    /// Append the #747 exclusion to an advisory when — and only when — it actually fired.
    let private withIgnoredJunk (drift: Drift.DriftReport) (hint: string) =
        match drift.IgnoredSkillJunkPaths with
        | [] -> hint
        | ignored -> hint + " " + ignoredJunkStatement ignored

    let private notMirroredHint =
        "Skill copies are not mirrored (advisory): each reported path is a file another root carries and that root does not. "
        + "`fsgg-sdd upgrade` cannot repair this class and re-running it will not clear it — its re-seed writes only MISSING files it has a canonical source for (the seeded skeleton, and the owner-sourced copies recorded in provenance) and the lane has no delete step. "
        // FS-GG/FS.GG.SDD#747: the old tail illustrated this class with `.DS_Store`, an editor
        // backup and a merge `.orig` — the three shapes that are now EXCLUDED from the comparison
        // and so can never reach this sentence. Left as it was it would tell an operator to go
        // delete a file that is not on the list they were handed. AC5: the wording stays truthful
        // for what was chosen, which means it now illustrates the class with what actually
        // survives the filter — a real file one root is missing.
        + "Reconcile by hand: copy the file into the roots that lack it if it belongs to the skill, or delete it from the root that has it if it does not (e.g. a scratch note, or a provider file dropped from a later release and left behind in the other roots). Recognised OS and merge junk is already excluded and is not reported here."

    // The third condition, and the reason `notMirroredHint` cannot simply absorb it: when NO root
    // carries the skill there is no sibling to mirror from, so "another root carries it" would be a
    // false statement and "copy it from the root that has it" an impossible instruction.
    let private lostHint =
        "Skill copies are missing entirely (advisory): the reported skills are absent from every declared root, so there is nothing to mirror from — restore the canonical skill sources or re-scaffold."

    // Unchanged wording for the condition it always described correctly: every reported root HAS the
    // file and the bytes disagree.
    let private divergentHint =
        "Skill content drift detected (advisory); some copies diverge from their canonical body — re-scaffold or restore the canonical skill sources."

    // FS-GG/FS.GG.SDD#750: the FOURTH condition, and the reason none of the three above can carry
    // it. It is reported at the roots that HAVE the file, so `notMirroredHint` ("another root
    // carries this file", reported at the roots that LACK it) is false of it in both halves — and
    // its "copy it into the roots that lack it" arm would instruct an operator to SPREAD a file the
    // producer never declared. `lostHint` asserts the skill is absent everywhere; it is present.
    // `divergentHint` asserts the bytes disagree; under the all-roots case they agree perfectly.
    //
    // The repair is genuinely a fourth one, and it has two arms because the tool cannot tell which
    // applies FROM THE FILE ALONE — the same limit #747 recorded for the junk rule. Either the file
    // does not belong to the skill (delete it), or it does and the recorded declaration is stale
    // (re-scaffold, so tree and declaration are regenerated together). Naming only one would be a
    // guess stated as an instruction.
    //
    // #747's LESSON APPLIES TO THE ILLUSTRATION, and it is why this sentence names neither an editor
    // backup nor a merge leftover. Those are exactly `junkFileSuffixes` (`.bak`, `.orig`, `.swp`,
    // `~`, …), subtracted from the compared set before any class is computed, and the junk filter
    // spares only files the provenance DECLARES — which an undeclared file, by definition, is not.
    // So no editor artefact can ever reach this sentence, and illustrating the class with one would
    // rebuild the defect #747 removed from `notMirroredHint` above (see the comment at its tail):
    // an advisory telling an operator to go delete a file that is not on the list they were handed.
    // What survives the filter is an ordinarily-named file — a scratch note, a renamed auxiliary, a
    // file a later release dropped from the declaration — so those are what it names.
    let private undeclaredHint =
        "Skill copies carry undeclared files (advisory): each reported path is a file that root HAS, under a skill whose producer records a complete file set, and this file is not in it. "
        + "`fsgg-sdd upgrade` cannot repair this class and re-running it will not clear it — the lane has no delete step, and there is no canonical body to write for a file no declaration names. "
        + "Reconcile by hand: delete the file if it does not belong to the skill (a scratch note, or an auxiliary renamed without updating the declaration), or re-scaffold if it does, so the tree and the recorded declaration are regenerated together. "
        + "The other roots are NOT reported here and must not be made to match — an undeclared file is not repaired by copying it further. "
        + "Recognised OS and merge junk never reaches this class: it is excluded from the comparison before any of it is computed."

    /// The advisory for un-repaired skill drift: every condition actually present, in that order,
    /// and nothing else. Divergence alone ⇒ byte-identical to the pre-#736 text, so the wording that
    /// was always correct is preserved rather than reworded along with the wording that was not.
    /// Nothing present at all ⇒ the divergence text, which is where this lane's only caller —
    /// guarded on a non-empty drift list — behaved before, so an unclassified path can never leave
    /// the operator with no advisory at all.
    /// FS.GG.SDD#760: the advisory for a run holding subjects it could not READ. Its repair is the
    /// one the read edge's own `unreadableFile` / `unlistableDirectory` warning names, and it is
    /// pointedly NOT "re-run `upgrade`": this lane cannot open the file either, so a re-run clears
    /// nothing and the operator would keep taking the same advice forever.
    let private unreadableSubjectHint =
        "Some subjects could not be read, so this run cannot report the workspace coherent (advisory). `fsgg-sdd upgrade` cannot repair a file it cannot open — no re-run will clear this. See the `unreadableFile` / `unlistableDirectory` findings above for the exact paths, restore access to them (e.g. `chmod +r`), then re-run to get a verdict over the whole set."

    let skillDriftHint
        (notMirrored: string list)
        (lost: string list)
        (divergent: string list)
        (undeclared: string list)
        =
        match
            [ if not (List.isEmpty notMirrored) then
                  notMirroredHint
              if not (List.isEmpty lost) then
                  lostHint
              if not (List.isEmpty divergent) then
                  divergentHint
              if not (List.isEmpty undeclared) then
                  undeclaredHint ]
        with
        | [] -> divergentHint
        | sentences -> String.concat " " sentences

    let private confirmPrompt (step: ReconciliationStep) =
        $"Apply {reconciliationStepIdValue step.StepId}?\n{step.DiffPreview}\n[y/N] "

    // A confirm decision from the interpreted-effect log for one step, or None when the
    // step's Confirm has not been interpreted yet.
    let private confirmDecision model stepId : bool option =
        model.InterpretedEffects
        |> List.tryPick (fun result ->
            match result.Effect with
            | Confirm(s, _) when s = stepId -> result.Confirmed
            | _ -> None)

    let private confirmPlanned model stepId =
        model.PendingEffects
        |> List.exists (fun effect ->
            match effect with
            | Confirm(s, _) when s = stepId -> true
            | _ -> false)

    // The applied/failed outcome of a step's apply effects, read from the interpreted log:
    // a process step is `applied` iff it started and exited 0; a write step iff it succeeded.
    let private applyOutcome model (effects: CommandEffect list) =
        let ok =
            effects
            |> List.forall (fun effect ->
                match
                    model.InterpretedEffects
                    |> List.tryFind (fun result -> effectKey result.Effect = effectKey effect)
                with
                | Some result ->
                    match result.Effect with
                    | RunProcess _ ->
                        match result.Process with
                        | Some { Started = true; ExitCode = 0 } -> true
                        | _ -> false
                    | _ -> result.Succeeded
                | None -> false)

        if ok then
            ReconciliationOutcome.Applied
        else
            ReconciliationOutcome.Failed

    type private StepProgress =
        | Resolved of ReconciliationOutcome
        | EmitEffects of CommandEffect list
        | Awaiting

    let private applyStage model (request: CommandRequest) (step: ReconciliationStep) =
        let preservedFiles =
            match step.StepId with
            | ReconciliationStepId.ArtifactReSeed -> ownerBackfillPreservedFiles model step.TargetPaths
            | _ -> []

        let verificationReads = preservedFiles |> List.map (fst >> ReadFile)

        let readsInterpreted =
            verificationReads
            |> List.forall (fun effect -> hasInterpreted (effectKey effect) model)

        let readsPlanned =
            verificationReads
            |> List.exists (fun effect -> hasPlanned (effectKey effect) model)

        if not readsInterpreted then
            if readsPlanned then
                Awaiting
            else
                EmitEffects verificationReads
        elif not (preservedFilesVerified model preservedFiles) then
            // A missing/unreadable/tampered preserved file must never be laundered into a fresh
            // canonical digest in provenance. Fail before emitting any recovery write.
            Resolved ReconciliationOutcome.Failed
        else
            let effects = applyEffectsFor model request step

            let allInterpreted =
                effects |> List.forall (fun effect -> hasInterpreted (effectKey effect) model)

            let anyPlanned =
                effects |> List.exists (fun effect -> hasPlanned (effectKey effect) model)

            if List.isEmpty effects then
                Resolved ReconciliationOutcome.Applied
            elif allInterpreted then
                Resolved(applyOutcome model effects)
            elif anyPlanned then
                Awaiting
            else
                EmitEffects effects

    let private stepProgress model (request: CommandRequest) (step: ReconciliationStep) =
        if request.AssumeYes then
            // `--yes`: apply each step directly — no `Confirm` emitted (FR-011).
            applyStage model request step
        else
            match confirmDecision model (reconciliationStepIdValue step.StepId) with
            | Some true -> applyStage model request step
            | Some false -> Resolved ReconciliationOutcome.Skipped
            | None ->
                if confirmPlanned model (reconciliationStepIdValue step.StepId) then
                    Awaiting
                else
                    EmitEffects [ Confirm(reconciliationStepIdValue step.StepId, confirmPrompt step) ]

    let private selfUpdateExitCode model =
        model.InterpretedEffects
        |> List.tryPick (fun result -> if isSelfUpdate result.Effect then result.Process else None)
        |> function
            | Some processResult -> processResult.ExitCode
            | None -> -1

    let private noOpSummary (request: CommandRequest) (drift: Drift.DriftReport) hint : UpgradeSummary =
        { HasProvenance = drift.HasProvenance
          Mode = if request.AssumeYes then "assumeYes" else "interactive"
          AlreadyCoherent = true
          Steps = drift.Steps
          AppliedStepIds = []
          SkippedStepIds = []
          FailedStepIds = []
          SkillDriftPaths = drift.SkillDriftPaths
          ResidualDrift = false
          // #747: the no-op summaries are exactly where the exclusion MUST be stated — this is the
          // "Already coherent — nothing to reconcile." close, and after #747 that sentence can be
          // true only because something was subtracted.
          NextActionHint = withIgnoredJunk drift hint }

    let private finalizeApply
        model
        (request: CommandRequest)
        (drift: Drift.DriftReport)
        (actionable: ReconciliationStep list)
        =
        let outcomes =
            actionable
            |> List.map (fun step ->
                let outcome =
                    match stepProgress model request step with
                    | Resolved value -> value
                    | _ -> ReconciliationOutcome.Failed

                step.StepId, outcome)

        let ofOutcome value =
            outcomes |> List.filter (fun (_, o) -> o = value) |> List.map fst |> List.sort

        let applied = ofOutcome ReconciliationOutcome.Applied
        let skipped = ofOutcome ReconciliationOutcome.Skipped
        let failed = ofOutcome ReconciliationOutcome.Failed

        let resolvedMap = Map.ofList outcomes

        let steps =
            drift.Steps
            |> List.map (fun step ->
                match Map.tryFind step.StepId resolvedMap with
                | Some outcome -> { step with Outcome = outcome }
                | None -> step)

        // 058/ADR-0014 P1: the content drift `upgrade` does NOT repair in this phase — a
        // present-but-divergent or hash-mismatched copy, and product-skill loss (advisory
        // first, no clobber). Missing *seeded* copies are excluded ONLY when the `artifactReSeed`
        // step actually applied (a skipped/failed re-seed leaves them outstanding, so they stay
        // in the advisory surface).
        //
        // FS-GG/FS.GG.SDD#733: the OWNER-SOURCED backfill paths are subtracted too. They are
        // deliberately NOT in `MissingArtifactPaths` (that field is the SEEDED-skeleton axis and its
        // goldens), but the SAME `artifactReSeed` step writes them — `ownerBackfillEffects` — so a
        // copy this run just restored must not close the run as residual drift. Before #733 the
        // omission was inert, because the owner-sourced class produced no `SkillDriftPaths` at all;
        // now it does, and without this the commonest owner-sourced repair reports itself unrepaired
        // under a hint saying the lane cannot repair it — both false, and FR-013 inverted.
        let repairedMissing =
            if List.contains ReconciliationStepId.ArtifactReSeed applied then
                drift.MissingArtifactPaths @ drift.OwnerSkillBackfillPaths
            else
                []

        let unrepaired (paths: string list) =
            paths |> List.filter (fun path -> not (List.contains path repairedMissing))

        let unrepairedSkillDrift = unrepaired drift.SkillDriftPaths

        // #736: the same subtraction, per condition, so the closing advisory names what actually
        // survived the run rather than what the fold found before the re-seed ran.
        let unrepairedNotMirrored = unrepaired drift.SkillNotMirroredPaths
        let unrepairedLost = unrepaired drift.SkillLostPaths
        let unrepairedDivergent = unrepaired drift.SkillDivergentPaths
        // #750: the fourth class takes the same subtraction, applied rather than special-cased so
        // the four classes and the union they sum to keep being filtered by one rule.
        //
        // It is inert TODAY, and the reason is worth stating exactly rather than as "the re-seed
        // writes only declared paths", which is not true of this lane. `ownerBackfillEffects` writes
        // from the EMBEDDED plan (`driver.Writes @ product.Writes`), not from the record, and it
        // re-declares only the DRIVER rows it wrote — `GameSkillPaths` is written by `scaffold`
        // alone. A GameSkill is single-file at the pinned package's schema v1 (`SKILL.md`, no
        // `files` array), so a backfilled GameSkill path is either the whole skill (no recorded
        // rows ⇒ the id is not in `ownerSourcedSkillFiles` ⇒ nothing is asserted about it) or
        // already declared. Nothing can currently land here. That is a property of the PACKAGE
        // SCHEMA, not of this lane, and a GameSkill manifest that gained a `files` array would
        // make an `upgrade`-written file report `Undeclared` permanently — filed as
        // FS-GG/FS.GG.SDD#798 against the recording asymmetry, which is its cause.
        let unrepairedUndeclared = unrepaired drift.SkillUndeclaredPaths

        // FR-013: never report an incomplete reconciliation as complete. A skipped or
        // failed step leaves residual drift; so does a self-update that "applied" (the
        // running binary is unchanged until the next invocation, R4); so does un-repaired
        // content drift (advisory in P1).
        let residualDrift =
            not (List.isEmpty skipped)
            || not (List.isEmpty failed)
            || List.contains ReconciliationStepId.CliSelfUpdate applied
            || not (List.isEmpty unrepairedSkillDrift)

        // FS.GG.SDD#745 AC3. A step whose apply effects were refused because a TARGET could not be
        // READ is not a tool defect, and `upgradeStepFailed` is `markToolDefect` — i.e. exit 2,
        // "the tool itself failed", over a mode bit. The measured shape was three `toolDefect`s
        // sitting beside three warnings whose correction read "Nothing about the tool is broken."
        //
        // The run still BLOCKS: the write edge emitted a blocking `unreadableWriteTarget` naming
        // the file and the reason, which is strictly more informative than a step id, and
        // `FailedStepIds` / `residualDrift` still report the step as failed. What is dropped is
        // only the second, less specific diagnostic — and with it the exit-2 escalation. Exit 1.
        let failedOnUnreadableTarget stepId =
            actionable
            |> List.filter (fun step -> step.StepId = stepId)
            |> List.collect (applyEffectsFor model request)
            |> List.exists (fun effect ->
                model.InterpretedEffects
                |> List.exists (fun result ->
                    effectKey result.Effect = effectKey effect
                    && not result.Succeeded
                    && (match result.Read with
                        | Unreadable _ -> true
                        | Bytes _
                        | Absent
                        // #743: `false`, and not merely because it is unreachable (this ranges
                        // over an APPLY step's effects, and `upgrade` never applies through an
                        // enumeration). A truncated LISTING is not a refused write target, so it
                        // could not be why a step failed, and claiming it was would suppress a
                        // `upgradeStepFailed` that is genuinely owed.
                        | Truncated _ -> false)))

        let diagnostics =
            if not (List.isEmpty failed) then
                failed
                |> List.choose (fun stepId ->
                    if stepId = ReconciliationStepId.CliSelfUpdate then
                        Some(upgradeSelfUpdateFailed (selfUpdateExitCode model))
                    elif failedOnUnreadableTarget stepId then
                        None
                    else
                        Some(upgradeStepFailed (reconciliationStepIdValue stepId)))
            elif not (List.isEmpty skipped) then
                [ upgradeResidualDrift (skipped |> List.map reconciliationStepIdValue) ]
            else
                []

        let hint =
            if not (List.isEmpty failed) then
                "A confirmed step failed; inspect the failure and re-run `fsgg-sdd upgrade`."
            elif not (List.isEmpty skipped) then
                "Re-run `fsgg-sdd upgrade` and confirm the skipped step(s) to finish reconciling."
            elif not (List.isEmpty unrepairedSkillDrift) then
                skillDriftHint unrepairedNotMirrored unrepairedLost unrepairedDivergent unrepairedUndeclared
            elif residualDrift then
                "The CLI self-update takes effect on the next invocation; re-run `fsgg-sdd doctor` afterwards to confirm coherence."
            else
                "Reconciliation complete; run `fsgg-sdd doctor` to confirm coherence."

        let summary: UpgradeSummary =
            { HasProvenance = drift.HasProvenance
              Mode = if request.AssumeYes then "assumeYes" else "interactive"
              AlreadyCoherent = false
              Steps = steps
              AppliedStepIds = applied
              SkippedStepIds = skipped
              FailedStepIds = failed
              SkillDriftPaths = unrepairedSkillDrift
              ResidualDrift = residualDrift
              NextActionHint = withIgnoredJunk drift hint }

        { model with
            Upgrade = Some summary
            Diagnostics = model.Diagnostics @ diagnostics },
        []

    let computeUpgradeNext model =
        match model.Upgrade with
        | Some _ -> model, []
        | None ->

            // 058/ADR-0014 P1: bring the provider product-skill copies into snapshots (read-only)
            // before the content-addressed drift is computed — the same provenance-driven gate
            // `doctor` uses, shared via `HandlersDoctor.skillReadGate`.
            match skillReadGate model with
            | Some effects ->
                if List.isEmpty effects then
                    model, []
                else
                    { model with
                        PendingEffects = model.PendingEffects @ effects },
                    effects
            | None ->
                let request = model.Request
                let drift = computeDrift model

                let actionable =
                    drift.Steps
                    |> List.filter (fun step -> step.Outcome = ReconciliationOutcome.WouldApply)

                // FS.GG.SDD#745 / decision #754, in the lane that never carried the rule and only
                // ever obeyed it BY ACCIDENT.
                //
                // `doctor` recomputes its own verdict as `drift.IsCoherent && List.isEmpty
                // unreadable`, because a verdict may never report coherent over a subject it did not
                // read. `upgrade` has no such line — and did not need one, because an unobservable
                // skill copy reached `computeDrift` as phantom *not mirrored* drift, so
                // `drift.IsCoherent` was already false. FS.GG.SDD#760 removes that phantom, which is
                // correct and which removes the accident with it.
                //
                // Measured on the `productCoherentFixture` with one `chmod 000` on a skill auxiliary,
                // with #760's fold change and WITHOUT this guard: `alreadyCoherent: true`,
                // `residualDrift: false`, "Already coherent — nothing to reconcile." — over a file
                // the run could not open. Trading a false FINDING in `doctor` for a false PASS in
                // `upgrade` is not a fix; it is the same defect moved (epic FS-GG/.github#266).
                //
                // So a run holding unreadable subjects may take neither nothing-to-reconcile close.
                // It falls to the ADVISORY residual arm below: exit 0, no extra write, no prompt,
                // `residualDrift: true`, and a hint naming the repair the read edge already named.
                // The narrower claims are untouched — an APPLY summary sets `AlreadyCoherent = false`
                // already, and its `ResidualDrift` describes what the reconciliation left rather than
                // whether the workspace is whole.
                let unreadable = unreadableSubjects model

                // #313: provenance-less no longer implies nothing-to-reconcile. The CLI axis is a
                // fact about the installed tool, so an unmet `sdd.minToolVersion` floor makes the
                // self-update step actionable even here. Absent a floor there is no actionable
                // step and this is the unchanged "nothing to reconcile" no-op.
                //
                // #760 adds the `unreadable` conjunct for the reason above, and it belongs on THIS
                // arm too: an unreadable `.fsgg/scaffold-provenance.json` resolves to `None` exactly
                // as an absent one does, so "No scaffold provenance" would be asserted about a
                // workspace whose provenance is right there and merely unopenable.
                if not drift.HasProvenance && List.isEmpty actionable && List.isEmpty unreadable then
                    { model with
                        Upgrade = Some(noOpSummary request drift "No scaffold provenance — nothing to reconcile.") },
                    []
                elif drift.IsCoherent && List.isEmpty unreadable then
                    { model with
                        Upgrade = Some(noOpSummary request drift "Already coherent — nothing to reconcile.") },
                    []
                elif List.isEmpty actionable then
                    // 058/ADR-0014 P1: the only drift is advisory content drift (a divergent/
                    // hash-mismatched copy or product-skill loss) with NO applicable step. There is
                    // nothing to confirm or `--yes`, so this is NOT a non-interactive refusal — it is
                    // reported advisory (exit 0, residual), so CI's `upgrade` doesn't dead-end at exit 1.
                    let summary: UpgradeSummary =
                        { HasProvenance = true
                          Mode = if request.AssumeYes then "assumeYes" else "interactive"
                          AlreadyCoherent = false
                          Steps = drift.Steps
                          AppliedStepIds = []
                          SkippedStepIds = []
                          FailedStepIds = []
                          SkillDriftPaths = drift.SkillDriftPaths
                          ResidualDrift = true
                          NextActionHint =
                            // #760: `skillDriftHint` describes DRIFT, and its no-condition fallback
                            // is the divergence sentence — which would tell an operator whose only
                            // problem is a permissions bit that "some copies diverge from their
                            // canonical body", about copies nothing compared. When the reason this
                            // arm was reached is unreadable subjects rather than drift, say so; when
                            // both are present, say both, drift first, exactly as the three drift
                            // conditions are already concatenated in a fixed order.
                            [ if not (List.isEmpty drift.SkillDriftPaths) then
                                  skillDriftHint
                                      drift.SkillNotMirroredPaths
                                      drift.SkillLostPaths
                                      drift.SkillDivergentPaths
                                      drift.SkillUndeclaredPaths
                              if not (List.isEmpty unreadable) then
                                  unreadableSubjectHint ]
                            |> function
                                | [] ->
                                    skillDriftHint
                                        drift.SkillNotMirroredPaths
                                        drift.SkillLostPaths
                                        drift.SkillDivergentPaths
                                        drift.SkillUndeclaredPaths
                                | sentences -> String.concat " " sentences
                            |> withIgnoredJunk drift }

                    { model with Upgrade = Some summary }, []
                elif not request.AssumeYes && not request.IsInteractive then
                    // FR-012 / SC-004: non-interactive without `--yes` refuses up front when there IS
                    // actionable reconciliation — zero writes, no `Confirm`, no prompt-hang (exit 1).
                    let summary: UpgradeSummary =
                        { HasProvenance = drift.HasProvenance
                          Mode = "refusedNonInteractive"
                          AlreadyCoherent = false
                          Steps = drift.Steps
                          AppliedStepIds = []
                          SkippedStepIds = []
                          FailedStepIds = []
                          SkillDriftPaths = drift.SkillDriftPaths
                          ResidualDrift = true
                          NextActionHint =
                            "Re-run `fsgg-sdd upgrade` interactively, or pass `--yes` to apply without prompting."
                            |> withIgnoredJunk drift }

                    { model with
                        Upgrade = Some summary
                        Diagnostics = model.Diagnostics @ [ upgradeNonInteractiveNoYes () ] },
                    []
                else
                    let rec walk steps =
                        match steps with
                        | [] -> None
                        | step :: rest ->
                            match stepProgress model request step with
                            | Resolved _ -> walk rest
                            | EmitEffects effects -> Some(Choice1Of2 effects)
                            | Awaiting -> Some(Choice2Of2())

                    match walk actionable with
                    | Some(Choice1Of2 effects) ->
                        { model with
                            PendingEffects = model.PendingEffects @ effects },
                        effects
                    | Some(Choice2Of2()) -> model, []
                    | None -> finalizeApply model request drift actionable
