namespace FS.GG.SDD.Commands.Internal

open System
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.CommandTypes

[<AutoOpen>]
module internal DiagnosticConstructors =
    module ArtifactRefModule = FS.GG.SDD.Artifacts.ArtifactRef
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    let artifactForPath (path: string) =
        match ArtifactRefModule.create path (ArtifactKind.Other "command") ArtifactOwner.Sdd true with
        | Ok artifact -> Some artifact
        | Error _ -> None

    // Feature 078 (#125): append the remediation pointer for the authoring-grammar blocking
    // diagnostics. `suffixFor` returns "" for every non-covered id, so non-covered corrections stay
    // byte-identical (FR-008). Every constructor funnels through here, so this is the only wiring.
    let commandDiagnostic id severity path message correction relatedIds =
        let correction =
            match RemediationPointers.suffixFor id with
            | "" -> correction
            | suffix -> correction + " " + suffix

        DiagnosticsModule.create id severity (path |> Option.bind artifactForPath) None message correction relatedIds

    let errorDiagnostic id path message correction relatedIds =
        commandDiagnostic id DiagnosticSeverity.DiagnosticError path message correction relatedIds

    let warningDiagnostic id path message correction relatedIds =
        commandDiagnostic id DiagnosticSeverity.DiagnosticWarning path message correction relatedIds

    // Family-shape helpers: the common Some-path skeletons whose relatedIds are
    // derived from the call (the named artifact, or a single referenced id).
    let errorForPath id path message correction =
        errorDiagnostic id (Some path) message correction [ path ]

    let warningForPath id path message correction =
        warningDiagnostic id (Some path) message correction [ path ]

    let errorForRef id path message correction relatedId =
        errorDiagnostic id (Some path) message correction [ relatedId ]

    // Work-id identity-mismatch family: every lifecycle artifact/view raises the same shape —
    // an error over the artifact path whose message names the artifact `noun` and whose relatedIds
    // are the expected/actual work ids. Only the `id`, the `noun`, and the `correction` vary per
    // artifact, so the message wording and relatedIds order stay byte-identical across all callers.
    let identityMismatch id noun correction path expectedWorkId actualWorkId =
        errorDiagnostic
            id
            (Some path)
            $"{noun} work id '{actualWorkId}' does not match selected work id '{expectedWorkId}'."
            correction
            [ expectedWorkId; actualWorkId ]

    let unknownCommand (value: string) =
        errorDiagnostic
            "unknownCommand"
            None
            $"Unknown SDD command '{value}'."
            "Use one of: init, charter, specify, clarify, checklist, plan, tasks, analyze, evidence, verify, ship, agents, refresh, scaffold, doctor, upgrade, validate, registry."
            []

    // Sibling of `unknownCommand` (FS-GG/FS.GG.SDD#196): an unrecognized *option* used to fall
    // through silently, so `init --project-root /tmp/b` seeded the current directory and reported
    // `succeeded`. `recognized` is supplied by the caller's option table rather than re-spelled
    // here, so the correction cannot drift from what the parser accepts.
    let unknownOption (command: SddCommand) (value: string) (recognized: string list) (suggestion: string option) =
        let options = String.Join(", ", recognized)

        let hint =
            match suggestion with
            | Some candidate -> $" Did you mean '{candidate}'?"
            | None -> ""

        errorDiagnostic
            "unknownOption"
            None
            $"Unknown option '{value}' for command '{commandName command}'."
            $"Use one of: {options}.{hint}"
            [ value ]

    // Sibling of `unknownOption` (FS-GG/FS.GG.SDD#264, ADR-0002 Gap C finding 6): a value-taking
    // option supplied with no following value used to read as `None` and silently fall back to its
    // default — `charter --work` (trailing) ran with no work id, `evidence --from-tests` mapped no
    // proving test, a trailing `--root` defaulted to `.`. This is the dangling-value half of the
    // "malformed argv silently accepted" class #196 closed for unknown *tokens*; it now blocks the
    // same way — a `DiagnosticError`, exit 1, zero writes — rather than running against a defaulted
    // input. Reported before the request is read, so nothing is seeded.
    let missingOptionValue (command: SddCommand) (option: string) =
        errorDiagnostic
            "missingOptionValue"
            None
            $"Option '{option}' for command '{commandName command}' requires a value, but none was given."
            $"Supply a value after '{option}' (for example `{option} <value>`)."
            [ option ]

    // Top-level exception backstop (FS-GG/FS.GG.SDD#250, Gap C finding 7): a throw that escapes the
    // pure plan/update/serialize pipeline used to print a raw CLR stack trace and exit with the
    // default unhandled code. This is the CLI-edge sibling of `unknownCommand`/`unknownOption`,
    // classifying the escape as a *tool defect* (exit 2 via `IsToolDefect`) rather than malformed
    // input — the "never a raw stack trace; distinguish malformed input from tool defect" doctrine.
    // Carries the exception message (not the stack) so the failure is diagnosable without a leak.
    let unhandledException (message: string) =
        errorDiagnostic
            "unhandledException"
            None
            $"The CLI failed with an unexpected internal error: {message}"
            "This is a tool defect, not a problem with your input. Re-run with the same arguments; if it recurs, report it to FS.GG.SDD with the command line that triggered it."
            []
        |> DiagnosticsModule.markToolDefect

    let malformedWorkId (value: string) =
        errorDiagnostic
            "malformedWorkId"
            None
            $"Work id '{value}' is malformed."
            "Use a stable lowercase work id such as 003-native-sdd-lifecycle-commands."
            [ value ]

    let missingWorkId (command: SddCommand) =
        errorDiagnostic
            "missingWorkId"
            None
            $"Command '{commandName command}' requires --work."
            "Pass --work <id> for work-item lifecycle commands."
            [ commandName command ]

    let lintMissingArtifact () =
        errorDiagnostic
            "lintMissingArtifact"
            None
            "Command 'lint' requires an <artifact> path."
            "Run `fsgg-sdd lint <artifact>` with a path to an authored SDD artifact."
            []

    let explainUnsupported (command: SddCommand) =
        errorDiagnostic
            "explainUnsupported"
            None
            $"`--explain` is not supported for '{commandName command}'."
            "Use `--explain` on an authoring stage (charter/specify/clarify/checklist/plan/tasks/evidence), or run `fsgg-sdd lint <artifact>`."
            [ commandName command ]

    let unsupportedCommand (command: SddCommand) =
        errorDiagnostic
            "unsupportedLifecycleCommand"
            None
            $"Command '{commandName command}' is declared but not implemented in the current MVP slice."
            "Use an implemented lifecycle command in this slice; later lifecycle commands remain pending in tasks.md."
            [ commandName command ]

    let outsideProject () =
        errorDiagnostic
            "outsideProject"
            (Some ".fsgg/project.yml")
            "The current directory is not an initialized FS.GG.SDD project."
            "Run fsgg-sdd init or pass --root for an initialized SDD project."
            []

    let missingProjectConfig path =
        errorForPath
            "missingProjectConfig"
            path
            $"Required project config '{path}' is missing."
            "Run fsgg-sdd init or restore the SDD project configuration."

    let malformedProjectConfig path =
        errorForPath
            "malformedProjectConfig"
            path
            $"Project config '{path}' is malformed."
            "Fix schemaVersion, project.id, project.defaultWorkRoot, sdd.config, and sdd.agents before authoring a charter."

    let missingSddConfig path =
        errorForPath
            "missingSddConfig"
            path
            $"Required SDD config '{path}' is missing."
            "Restore .fsgg/sdd.yml before authoring a charter."

    let malformedSddConfig path =
        errorForPath
            "malformedSddConfig"
            path
            $"SDD config '{path}' is malformed."
            "Fix the SDD lifecycle policy before authoring a charter."

    let missingAgentsConfig path =
        errorForPath
            "missingAgentsConfig"
            path
            $"Required agent config '{path}' is missing."
            "Restore .fsgg/agents.yml before authoring a charter."

    let malformedAgentsConfig path =
        errorForPath
            "malformedAgentsConfig"
            path
            $"Agent config '{path}' is malformed."
            "Fix .fsgg/agents.yml before authoring a charter."

    let duplicateWorkId workId paths =
        errorDiagnostic
            "duplicateWorkId"
            None
            $"Work id '{workId}' is declared by more than one work artifact."
            "Keep one authored source for the selected work id and move or rename the duplicate."
            (workId :: (paths |> List.sort))

    let missingCharterPrerequisite path message =
        errorForPath
            "missingCharterPrerequisite"
            path
            message
            "Run fsgg-sdd charter for the selected work item before running fsgg-sdd specify."

    let charterIdentityMismatch path expectedWorkId actualWorkId =
        identityMismatch
            "charterIdentityMismatch"
            "Charter"
            "Move the charter under the matching work id or update its front matter before rerunning."
            path
            expectedWorkId
            actualWorkId

    let malformedCharterFrontMatter path message =
        errorForPath
            "malformedCharterFrontMatter"
            path
            message
            "Add schemaVersion, workId, title, stage, changeTier, and status front matter before rerunning."

    let missingSpecificationIntent path missingFacts =
        let missingText = String.concat ", " missingFacts

        errorDiagnostic
            "missingSpecificationIntent"
            (Some path)
            $"Specification intent is missing required facts: {missingText}."
            "Provide --input with labeled facts, one per line: \"value: <user value>\", \"scope: <scope>\", \"requirement: <measurable requirement>\"."
            missingFacts

    let missingSpecificationPrerequisite path message =
        errorForPath
            "missingSpecificationPrerequisite"
            path
            message
            "Run fsgg-sdd specify for the selected work item before running fsgg-sdd clarify."

    let specificationIdentityMismatch path expectedWorkId actualWorkId =
        identityMismatch
            "specificationIdentityMismatch"
            "Specification"
            "Move the specification under the matching work id or update its front matter before rerunning."
            path
            expectedWorkId
            actualWorkId

    let malformedSpecificationFrontMatter path message =
        errorForPath
            "malformedSpecificationFrontMatter"
            path
            message
            "Add schemaVersion, workId, title, stage: specify, changeTier, and status front matter before rerunning."

    let malformedSpecificationFacts path message =
        errorForPath
            "malformedSpecificationFacts"
            path
            message
            "Fix specification ids, references, and required sections before recording clarification decisions."

    let duplicateSpecificationId path id =
        errorForRef
            "duplicateSpecificationId"
            path
            $"Specification identifier '{id}' is declared more than once."
            "Rename one duplicate identifier and update all structured references before rerunning."
            id

    let missingSpecificationId path idFamily =
        errorForRef
            "missingSpecificationId"
            path
            $"Specification content is missing a required {idFamily} stable id."
            "Add stable story, requirement, scenario, scope, or ambiguity ids before rerunning."
            idFamily

    let unknownSpecificationReference path id =
        errorForRef
            "unknownSpecificationReference"
            path
            $"Specification reference '{id}' does not resolve."
            "Declare the referenced specification id or remove the stale structured link before rerunning."
            id

    let missingClarificationAnswer path missingIds =
        let missingText = String.concat ", " (missingIds |> List.sort)

        errorDiagnostic
            "missingClarificationAnswer"
            (Some path)
            $"Clarification input is missing answers for blocking ambiguity: {missingText}."
            "Provide an answer, accepted deferral, or explicit still-open note for each blocking ambiguity."
            missingIds

    let missingClarificationPrerequisite path message =
        errorForPath
            "missingClarificationPrerequisite"
            path
            message
            "Run fsgg-sdd clarify for the selected work item before running fsgg-sdd checklist."

    let clarificationIdentityMismatch path expectedWorkId actualWorkId =
        identityMismatch
            "clarificationIdentityMismatch"
            "Clarification"
            "Move clarifications.md under the matching work id or update its front matter before rerunning."
            path
            expectedWorkId
            actualWorkId

    let malformedClarificationFrontMatter path message =
        errorForPath
            "malformedClarificationFrontMatter"
            path
            message
            "Add schemaVersion, workId, title, stage: clarify, changeTier, status, and sourceSpec front matter before rerunning."

    let duplicateClarificationId path id =
        errorForRef
            "duplicateClarificationId"
            path
            $"Clarification identifier '{id}' is declared more than once."
            "Rename one duplicate clarification question or decision id and update references before rerunning."
            id

    let unknownClarificationReference path id =
        errorForRef
            "unknownClarificationReference"
            path
            $"Clarification reference '{id}' does not resolve in the selected specification or clarification artifact."
            "Reference a known AMB, CQ, FR, US, or AC id, or remove the stale clarification link."
            id

    let unsafeDecisionChange path id =
        errorForRef
            "unsafeDecisionChange"
            path
            $"Clarification decision '{id}' would be changed by this rerun."
            "Preserve existing decisions and add a new decision id for a replacement path."
            id

    let unresolvedBlockingAmbiguity path ids =
        errorDiagnostic
            "unresolvedBlockingAmbiguity"
            (Some path)
            "Blocking ambiguity remains unresolved after clarification planning."
            // Name the recognized grammar: any AMB-### under `## Remaining Ambiguity` counts as
            // unresolved unless its line is a `None…`/`No …` disclaimer or is marked
            // `deferred`/`non-blocking`. Resolve each with a concrete decision or accepted
            // deferral; to state none remain, write a `None.`/`No remaining ambiguities.`
            // disclaimer rather than re-listing the resolved AMB ids as bullets.
            "Resolve each blocking ambiguity with a concrete decision or accepted deferral before moving to checklist. Under '## Remaining Ambiguity', an AMB-### is counted as blocking unless its line is a 'None.'/'No remaining ambiguities.' disclaimer or is marked 'deferred'/'non-blocking'."
            ids

    let failedRequirementsQuality path message correction relatedIds =
        warningDiagnostic "failedRequirementsQuality" (Some path) message correction relatedIds

    let checklistIdentityMismatch path expectedWorkId actualWorkId =
        identityMismatch
            "checklistIdentityMismatch"
            "Checklist"
            "Move checklist.md under the matching work id or update its front matter before rerunning."
            path
            expectedWorkId
            actualWorkId

    let malformedChecklistFrontMatter path message =
        errorForPath
            "malformedChecklistFrontMatter"
            path
            message
            "Add schemaVersion, workId, title, stage: checklist, changeTier, status, sourceSpec, and sourceClarifications front matter before rerunning."

    // Feature 081 (#144): a review result (`CR-###`) missing its `[CHK:CHK-###]` back-reference
    // gets its own diagnostic that names the real cause, split out from the front-matter diagnostic.
    let missingChecklistBackReference path id =
        errorForRef
            "missingChecklistBackReference"
            path
            $"Checklist review result '{id}' is missing its [CHK:CHK-###] item back-reference."
            "Add [CHK:CHK-###] naming the checklist item this review result covers (e.g. `- CR-001 [CHK:CHK-001] pass: …`)."
            id

    let duplicateChecklistId path id =
        errorForRef
            "duplicateChecklistId"
            path
            $"Checklist identifier '{id}' is declared more than once."
            "Rename one duplicate checklist item or result id and update references before rerunning."
            id

    let unknownChecklistSourceReference path id =
        errorForRef
            "unknownChecklistSourceReference"
            path
            $"Checklist reference '{id}' does not resolve in the selected specification, clarification, or checklist item set."
            "Reference a known FR, US, AC, SB, AMB, CQ, DEC, or CHK id, or remove the stale checklist link."
            id

    let staleChecklistResult path resultIds =
        warningDiagnostic
            "staleChecklistResult"
            (Some path)
            "One or more checklist results were reviewed against older source snapshots."
            "Review the stale checklist results against the current specification and clarification sources."
            resultIds

    let missingChecklistPrerequisite path message =
        errorForPath
            "missingChecklistPrerequisite"
            path
            message
            "Run fsgg-sdd checklist for the selected work item before running fsgg-sdd plan."

    let failedChecklistPrerequisite path message relatedIds =
        errorDiagnostic
            "failedChecklistPrerequisite"
            (Some path)
            message
            // A clean `fsgg-sdd checklist` review writes `status: checklistReady` automatically —
            // there is no manual transition to author and hand-editing the status is not the fix.
            // Clear the blocking findings / stale reviews / unresolved deferrals, then re-run
            // `fsgg-sdd checklist` to have it re-promote the status.
            "Correct blocking checklist findings, stale review results, or unresolved deferrals, then re-run 'fsgg-sdd checklist' — a clean review writes status: checklistReady automatically (do not hand-edit the status)."
            relatedIds

    let planIdentityMismatch path expectedWorkId actualWorkId =
        identityMismatch
            "planIdentityMismatch"
            "Plan"
            "Move plan.md under the matching work id or update its front matter before rerunning."
            path
            expectedWorkId
            actualWorkId

    let malformedPlanFrontMatter path message =
        errorForPath
            "malformedPlanFrontMatter"
            path
            message
            "Add schemaVersion, workId, title, stage: plan, status, sourceSpec, sourceClarifications, and sourceChecklist front matter before rerunning."

    let duplicatePlanId path id =
        errorForRef
            "duplicatePlanId"
            path
            $"Plan identifier '{id}' is declared more than once."
            "Rename one duplicate planning identifier and update all structured references before rerunning."
            id

    let unknownPlanSourceReference path id =
        errorForRef
            "unknownPlanSourceReference"
            path
            $"Plan reference '{id}' does not resolve in the selected specification, clarification, checklist, or plan artifact."
            "Reference a known FR, US, AC, SB, AMB, CQ, DEC, CHK, CR, PD, PC, VO, PM, or GV id, or remove the stale plan link."
            id

    // Feature 090: retained for the *authored* case only — a plan whose `## Plan Decisions` prose
    // carries an operator-written `stale:` marker (FR-009). `plan` no longer emits this on digest
    // drift, and no longer writes such a line into the authored plan; see `stalePlanSnapshot`.
    let stalePlanDecision path decisionIds =
        warningDiagnostic
            "stalePlanDecision"
            (Some path)
            "One or more plan decisions were recorded against older source snapshots."
            "Review the stale plan decisions before treating the plan as ready for task generation."
            decisionIds

    // Feature 090 (#163). The plan's recorded source digests no longer match the sources they name.
    // An ERROR, not a warning: `runHandler`'s effect gate discards every write effect when any
    // diagnostic is a DiagnosticError, which is exactly how `plan` keeps its hands off the authored
    // `plan.md` (FR-001/FR-003). The prior `stalePlanDecision` warning let the mutated text through.
    // Deliberately NOT `markToolDefect` — a stale upstream is workspace state, not a tool defect, so
    // the blocked exit stays 1. `changedPaths` arrive ordinally sorted (FR-002/FR-014) and become
    // `RelatedIds`.
    //
    // The `--accept-upstream` remediation lives in the `Correction` string below, NOT in
    // `RemediationPointers`: that registry is the authoring-grammar docs index (its header states it
    // "Excludes pure sequencing/config/tool-defect blocks"), and a stale snapshot is a sequencing
    // block with no grammar section to cite. The `plan.acceptUpstream` NextAction carries the
    // machine-readable half. Do not move this sentence out of `Correction` expecting the registry to
    // re-add it — `suffixFor` returns "" for unregistered ids.
    let stalePlanSnapshot path (changedPaths: string list) =
        let count = List.length changedPaths
        let sources = if count = 1 then "source" else "sources"

        errorDiagnostic
            "stalePlanSnapshot"
            (Some path)
            $"Plan snapshot is stale: {count} {sources} changed since the plan was recorded."
            "Review the recorded plan decisions against the changed sources, then re-run with --accept-upstream."
            changedPaths

    // Feature 090 (#163), FR-011. Advancing to `plan` freezes the spec/clarify/checklist authoring
    // window: the plan records their digests, and a later upstream edit needs an explicit
    // re-baseline. Nothing said so before — operators discovered it by tripping over it. A
    // DiagnosticInfo, so `ReportAssembly.outcome` (which inspects only Error and Warning) leaves the
    // outcome, exit code, and changedArtifacts untouched. It adds a fact, not an outcome.
    let planAuthoringWindow path (snapshottedSources: string list) =
        commandDiagnostic
            "planAuthoringWindow"
            DiagnosticSeverity.DiagnosticInfo
            (Some path)
            "Plan snapshotted its sources; later edits to them require a re-baseline."
            "Re-run fsgg-sdd plan --accept-upstream after editing the specification, clarifications, or checklist."
            snapshottedSources

    let missingPlanPrerequisite path message =
        errorForPath
            "missingPlanPrerequisite"
            path
            message
            "Run fsgg-sdd plan for the selected work item before running fsgg-sdd tasks."

    let failedPlanPrerequisite path message relatedIds =
        errorDiagnostic
            "failedPlanPrerequisite"
            (Some path)
            message
            "Correct blocking planning findings, stale decisions, or malformed plan data before task generation."
            relatedIds

    let tasksIdentityMismatch path expectedWorkId actualWorkId =
        identityMismatch
            "tasksIdentityMismatch"
            "Tasks"
            "Move tasks.yml under the matching work id or update its work.id before rerunning."
            path
            expectedWorkId
            actualWorkId

    let malformedTasksArtifact path message =
        errorForPath
            "malformedTasksArtifact"
            path
            message
            "Fix schemaVersion, work identity, source links, task ids, dependencies, and status fields before rerunning."

    let duplicateTaskId path id =
        errorForRef
            "duplicateTaskId"
            path
            $"Task id '{id}' is declared more than once."
            "Rename one duplicate task id and update dependency and evidence references before rerunning."
            id

    let unknownTaskSourceReference path id =
        errorForRef
            "unknownTaskSourceReference"
            path
            $"Task source reference '{id}' does not resolve in the selected lifecycle artifacts."
            "Reference a known FR, AC, DEC, PD, PC, VO, PM, GV, CHK, or CR id, or remove the stale task link."
            id

    let unknownTaskDependency path id =
        errorForRef
            "unknownTaskDependency"
            path
            $"Task dependency '{id}' does not resolve."
            "Declare the dependency task id or remove the dependency edge."
            id

    let taskDependencyCycle path ids =
        let cycleText = String.concat " -> " ids

        errorDiagnostic
            "taskDependencyCycle"
            (Some path)
            $"Task dependency cycle detected: {cycleText}."
            "Remove one dependency edge so the task graph is acyclic."
            ids

    let staleTask path taskIds =
        warningDiagnostic
            "staleTask"
            (Some path)
            "One or more task entries were recorded against older source snapshots."
            "Review stale tasks and rerun fsgg-sdd tasks after updating their source links."
            taskIds

    let doneTaskMissingEvidence path ids =
        errorDiagnostic
            "doneTaskMissingEvidence"
            (Some path)
            "One or more completed tasks are missing required evidence declarations."
            "Add work/<id>/evidence.yml entries for completed tasks or move the tasks back to pending."
            ids

    let skippedTaskMissingRationale path ids =
        errorDiagnostic
            "skippedTaskMissingRationale"
            (Some path)
            "One or more skipped tasks are missing skip rationale."
            "Add skipRationale for every skipped task before treating the task graph as ready."
            ids

    let missingTasksPrerequisite path message =
        errorForPath
            "missingTasksPrerequisite"
            path
            message
            "Run fsgg-sdd tasks for the selected work item before running fsgg-sdd analyze."

    let failedTasksPrerequisite path message relatedIds =
        errorDiagnostic
            "failedTasksPrerequisite"
            (Some path)
            message
            "Correct tasks.yml or rerun fsgg-sdd tasks before treating the work item as implementation-ready."
            relatedIds

    let analysisIdentityMismatch path expectedWorkId actualWorkId =
        identityMismatch
            "analysisIdentityMismatch"
            "Analysis view"
            "Regenerate the analysis view for the selected work id."
            path
            expectedWorkId
            actualWorkId

    let malformedAnalysisView path message =
        warningForPath
            "malformedAnalysisView"
            path
            message
            "Regenerate readiness/<id>/analysis.json from current lifecycle sources."

    let missingAnalysisPrerequisite path message =
        errorForPath
            "evidence.missingAnalysisPrerequisite"
            path
            message
            "Run fsgg-sdd analyze for the selected work item before recording evidence."

    // FS-GG/FS.GG.SDD#351 (epic .github#417): a scaffolded plan entry is not a plan entry. `plan`
    // seeds a decision per requirement carrying that requirement's own refs BY CONSTRUCTION, so the
    // traceability chain closes with zero human authorship — the gates check that the ids line up,
    // and the scaffold generates ids that line up. A plausible-looking filling is worse than an empty
    // one because it does not itch. This blocks while the tool's own prose is still sitting there.
    let unauthoredScaffoldContent path ids =
        errorDiagnostic
            "unauthoredScaffoldContent"
            (Some path)
            "Plan entries still hold the prose the scaffold wrote — an unauthored decision is a missing decision."
            "Replace each listed entry's text with the real decision, contract, obligation, migration note, or generated-view impact. The scaffold gives you the id and the refs; the judgement is yours to write."
            ids

    let analysisNotReady path readiness =
        errorForRef
            "evidence.analysisNotReady"
            path
            $"Analysis readiness '{readiness}' is not implementationReady."
            "Correct analysis findings and rerun fsgg-sdd analyze before recording evidence."
            readiness

    let evidenceIdentityMismatch path expectedWorkId actualWorkId =
        identityMismatch
            "evidence.identityMismatch"
            "Evidence"
            "Move evidence.yml under the matching work id or update its structured work id before rerunning."
            path
            expectedWorkId
            actualWorkId

    let malformedEvidenceArtifact path message =
        errorForPath
            "evidence.malformedEvidenceArtifact"
            path
            message
            "Fix schemaVersion, workId, stage, status, source links, evidence ids, result states, and disclosure fields before rerunning."

    let duplicateEvidenceId path id =
        errorForRef
            "evidence.duplicateEvidenceId"
            path
            $"Evidence id '{id}' is declared more than once."
            "Rename duplicate evidence declarations and keep stable ids unique within the selected evidence artifact."
            id

    let unknownEvidenceReference path id =
        errorForRef
            "evidence.unknownReference"
            path
            $"Evidence reference '{id}' does not resolve in the selected lifecycle artifacts."
            "Reference a known task, requirement, decision, obligation, source artifact, or generated view, or remove the stale evidence link."
            id

    let missingRequiredEvidence path ids =
        errorDiagnostic
            "evidence.missingRequiredEvidence"
            (Some path)
            "One or more required evidence obligations are missing current evidence or accepted deferral."
            "For each missing obligation id, add a matching evidence declaration with result: pass and synthetic: false (a synthetic pass does not satisfy it), or an accepted deferral."
            ids

    let staleEvidence path ids =
        warningDiagnostic
            "evidence.staleEvidence"
            (Some path)
            "One or more evidence declarations need review against current lifecycle facts."
            "Review stale evidence declarations and record a compatible update before verification."
            ids

    let staleEvidenceSource path ids =
        warningDiagnostic
            "evidence.staleEvidenceSource"
            (Some path)
            "One or more evidence source snapshots no longer match current source digests."
            "Rerun the evidence command after reviewing the changed source artifacts."
            ids

    let undisclosedSyntheticEvidence path ids =
        errorDiagnostic
            "evidence.undisclosedSyntheticEvidence"
            (Some path)
            "Synthetic evidence is missing disclosure of the real path it stands in for."
            "Add syntheticDisclosure.standsInFor and syntheticDisclosure.reason to every synthetic declaration."
            ids

    let missingDeferralRationale path ids =
        errorDiagnostic
            "evidence.missingDeferralRationale"
            (Some path)
            "Accepted deferral evidence is missing rationale, owner, scope, or later lifecycle visibility."
            "Add rationale, owner, scope, and laterLifecycleVisibility to every deferral declaration."
            ids

    // FS-GG/FS.GG.SDD#306: a visual-inspection obligation is discharged by a rendered artifact plus an
    // explicit disposition. A `pass` that names no artifact claims someone looked at a frame that does
    // not exist — the exact shape of the green-suite-over-a-visual-bug the obligation was added to catch.
    let missingVisualInspectionArtifact path ids =
        errorDiagnostic
            "evidence.missingVisualInspectionArtifact"
            (Some path)
            "Visual-inspection evidence passes without naming a rendered artifact."
            "Render one representative frame, look at it, and record the produced image in artifacts or a sourceRefs path/uri — or defer the obligation and say why."
            ids

    // FS-GG/FS.GG.SDD#349 (epic .github#266, instance (j)): a cited artifact path is a pointer to a
    // file on disk, and nothing ever followed it. A `pass` citing an artifact that is not there is
    // evidence whose only support is a string — the gate compared against a record of reality
    // instead of reality. Both buckets are probed (`artifacts:` and `sourceRefs[].path`); a `uri` is
    // not a local file and is never probed.
    let evidenceArtifactNotFound path paths =
        errorDiagnostic
            "evidence.artifactNotFound"
            (Some path)
            "Evidence passes while citing an artifact that does not exist."
            "Produce the cited artifact, correct the path, or stop claiming a pass — a cited path that is not on disk proves nothing."
            paths

    /// FS.GG.SDD#359 / #365. The sibling of `artifactNotFound`, one step earlier: that path is legal
    /// but absent; this path is not legal at all. It is the author's typo, not a tool defect, and it
    /// is refused BEFORE any filesystem probe is planned for it — a `..` chain used to be resolved
    /// straight out of the workspace, so a file outside the repository could discharge the gate.
    let malformedCitedArtifactPath path values =
        // Name the offending path IN THE MESSAGE, not only in relatedIds. #359's whole complaint is
        // that the author was told to go file a bug against SDD and never told which of their paths
        // was wrong — so the text projection has to carry the one fact they need to act.
        let named = values |> List.map (fun value -> $"'{value}'") |> String.concat ", "

        errorDiagnostic
            "evidence.malformedArtifactPath"
            (Some path)
            $"Evidence cites an artifact path that is not repository-relative: {named}."
            "Cite artifacts by a repository-relative path with no '..' segment. A path outside the workspace proves nothing, and is refused before it is ever read."
            values

    // FS.GG.SDD#350 / ADR-0035. The three ways a run receipt can fail to be believable. All three are
    // BLOCKING, and all three record nothing: a gate that degraded to "no receipt" on a malformed
    // report would fail open in a brand-new place, which is the exact class epic .github#266 exists
    // to close ("a gate must fail closed when its subject is absent, stale, or unreachable").

    /// The report is there but SDD cannot get a believable run out of it: not XML, no root, a root
    /// that is neither a TRX `<TestRun>` nor a JUnit `<testsuites>`/`<testsuite>` — or one that parsed
    /// perfectly and records **no executed tests**, which is not a run and must never become a passing
    /// receipt. The parser's own reason is carried through verbatim, because the author has to know
    /// WHICH of those it was to fix it.
    let testReportUnparseable path (reason: string) =
        errorDiagnostic
            "evidence.testReportUnparseable"
            (Some path)
            $"The test report yielded no usable run, so nothing was observed: {reason}"
            "Point --from-test-report at a TRX or JUnit XML report from a run that actually executed tests. SDD records a receipt from a report it can read; it never runs the suite itself."
            [ path ]

    /// The report is not on disk. Deliberately NOT silent: `--from-tests` naming a report that is not
    /// there is the author believing a run was observed when none was, and answering that with a
    /// quiet no-op would leave the obligation self-attested while looking recorded.
    let testReportNotFound path (report: string) =
        errorDiagnostic
            "evidence.testReportNotFound"
            (Some path)
            $"The test report '{report}' does not exist, so no run was observed."
            "Run the suite to produce the report, or correct the --from-tests path. A report that is not on disk proves nothing."
            [ report ]

    /// The `--from-tests` path escapes the repository (absolute, or carrying a `..` segment). Refused
    /// LEXICALLY, before any read effect is planned — the same rule, and the same reason, as #365:
    /// a `..` chain resolved at the edge would let a report outside the workspace discharge the gate.
    /// Nothing is read, so nothing can be recorded.
    let testReportPathEscape path (report: string) =
        errorDiagnostic
            "evidence.testReportPathEscape"
            (Some path)
            $"The test report path '{report}' is not repository-relative — it must stay inside the repository and contain no '..' segment."
            "Pass --from-tests a repository-relative path. A report outside the workspace proves nothing, and is refused before it is ever read."
            [ report ]

    /// The report parsed, and it records failures — while an obligation claims `result: pass`. The
    /// artifact and the claim contradict each other, and the artifact is the one nobody typed.
    ///
    /// Nothing is recorded: a failing run is not a receipt anybody wants, and stamping it would leave
    /// a declaration carrying a receipt that `isObserved` rejects — indistinguishable, downstream,
    /// from having no receipt at all. The author fixes the suite and re-records.
    let observedRunFailed path (report: string) (failed: int) (ids: string list) =
        errorDiagnostic
            "evidence.observedRunFailed"
            (Some path)
            $"The observed run in '{report}' recorded {failed} failing test(s), but evidence claims a pass."
            "Fix the failing tests and re-run `evidence --from-test-report`, or stop claiming a pass. A run that failed does not support an obligation that says it passed."
            ids

    /// A receipt that contradicts itself. `TestReport.parse` DERIVES `outcome` from the counts, so it
    /// cannot produce one of these — this is reserved for a receipt somebody hand-wrote into
    /// `evidence.yml`, which is exactly the move the receipt exists to stop being worth making.
    let observedRunInconsistent path (ids: string list) (reason: string) =
        errorDiagnostic
            "evidence.observedRunInconsistent"
            (Some path)
            $"An observedRun receipt contradicts itself: {reason}"
            "Do not hand-write observedRun. Record it with `fsgg-sdd evidence --from-tests <report>`, which derives every field from a report it read."
            ids

    let missingRequiredSkill path ids =
        errorDiagnostic
            "evidence.missingRequiredSkill"
            (Some path)
            "Completed work references required skills without visible evidence support."
            "Add evidence linked to the required task skill or move the task back to pending until the skill-backed work is complete."
            ids

    let unsupportedEvidenceResultState path states =
        errorDiagnostic
            "evidence.unsupportedResultState"
            (Some path)
            "Evidence contains unsupported result states."
            "Use pass, fail, deferred, missing, stale, advisory, or blocked for evidence result."
            states

    let unsafeEvidenceUpdate path ids =
        errorDiagnostic
            "evidence.unsafeUpdate"
            (Some path)
            "The proposed evidence update would change existing declaration meaning."
            "Preserve existing declaration ids and meanings; append a compatible new declaration instead."
            ids

    // The fix must name an authored line that disposes the id, not tasks.yml: tasks.yml is
    // regenerated, so editing it is inert, and rerunning `tasks` over unchanged sources
    // reproduces the same gap. Those were the old text's only two suggestions (#311).
    //
    // Tagging a `## Plan Decisions` PD-### line with the id is the general route, and it is
    // id-class agnostic: `Plan.planSourceIdsInLine` matches AC/DEC/FR/… alike, lifting every id
    // on the line into the decision's `SourceIds`, which `TaskGraphAuthoring.planDecisionTasks`
    // forwards into the generated task's `sourceIds` — the set `allTaskDispositionIds` reads.
    // An orphan AC-### is the case that actually reaches an author (every other required class
    // has its own task generator), so the text names its spec.md route too.
    let missingDisposition path ids =
        errorDiagnostic
            "missingDisposition"
            (Some path)
            "One or more lifecycle facts have no current task disposition."
            "Tag a plan.md '## Plan Decisions' line with each id so fsgg-sdd tasks derives a disposing task, for example '- PD-005 [AC-012] [DEC-003] complete: ...'; an orphan AC-### may instead be referenced from a spec.md requirement. Editing tasks.yml has no effect, because tasks regenerates it."
            ids

    let unsafeOverwrite (path: string) =
        errorForPath
            "unsafeOverwrite"
            path
            "The command would overwrite existing authored content."
            "Review the existing file and choose an explicit safe update path before rerunning."

    let malformedGeneratedView path =
        warningForPath
            "malformedGeneratedView"
            path
            $"Generated view '{path}' is malformed and will be refreshed when source data is valid."
            "Regenerate readiness/<id>/work-model.json from current lifecycle sources."

    let blockedGeneratedViewRefresh path relatedIds =
        warningDiagnostic
            "blockedGeneratedViewRefresh"
            (Some path)
            $"Generated view '{path}' cannot be refreshed from the current lifecycle sources."
            "Fix the named lifecycle diagnostics before treating the generated view as current."
            (path :: relatedIds)

    // The counterpart to `blockedGeneratedViewRefresh` for the case where the view was never
    // written at all: the derived work model carries blocking diagnostics and no prior view
    // exists on disk. Previously that arm dropped its diagnostics and the command reported
    // success with the view silently absent (FS.GG.SDD#191). This is a distinct id — not
    // `blockedGeneratedViewRefresh` — precisely because "the work model could not be
    // generated" is not "an existing generated view is stale"; keeping them separate stops
    // it from being read as a `staleSource`/`generatedView` analysis finding that would
    // falsely mark analysis as needing a refresh.
    let workModelNotGenerated path relatedIds =
        warningDiagnostic
            "workModelNotGenerated"
            (Some path)
            $"Generated view '{path}' was not written: the derived work model has blocking diagnostics."
            "Resolve the named lifecycle diagnostics, then re-run to generate the work model view."
            (path :: relatedIds)

    let missingEvidencePrerequisite path message =
        errorForPath
            "verify.missingEvidencePrerequisite"
            path
            message
            "Run fsgg-sdd evidence for the selected work item before running fsgg-sdd verify."

    let verifyIdentityMismatch path expectedWorkId actualWorkId =
        identityMismatch
            "verify.identityMismatch"
            "Verification view"
            "Regenerate the verification view for the selected work id."
            path
            expectedWorkId
            actualWorkId

    let malformedVerificationView path message =
        errorForPath
            "verify.malformedVerificationView"
            path
            message
            "Remove or repair the malformed readiness/<id>/verify.json before refreshing the verification view."

    let missingRequiredTest path ids =
        errorDiagnostic
            "verify.missingRequiredTest"
            (Some path)
            "One or more required test obligations are missing satisfying evidence or an accepted deferral."
            "Add verification evidence or an accepted deferral linked to the missing required test obligations."
            ids

    /// FS.GG.SDD#350 / ADR-0035 stage 3, raised only under `verify --require-observed`.
    ///
    /// The obligation is not missing, not stale, and not a disclosed synthetic: someone wrote
    /// `result: pass` and meant it. What is absent is any evidence that a run *happened* — so this
    /// says exactly that, and nothing stronger. It is not an accusation of lying; it is the tool
    /// declining to certify a pass it never observed.
    let unobservedRequiredTest path ids =
        errorDiagnostic
            "verify.unobservedRequiredTest"
            (Some path)
            "One or more required test obligations are satisfied only by an authored 'result: pass' — no observedRun receipt records a run SDD actually read."
            "Run the suite, then record the receipt with 'fsgg-sdd evidence --from-test-report <trx-or-junit>'. SDD never runs the suite itself; it reads the report a runner produced."
            ids

    /// FS.GG.SDD#350 / ADR-0035 stage 3, raised only under `ship --require-observed`.
    ///
    /// The merge-boundary twin of `unobservedRequiredTest`. It fires on the record `verify` wrote,
    /// which is the whole point: a `verify.json` that went green BEFORE the receipt policy was asked
    /// for is still sitting on disk, still digest-current, and would otherwise certify a pass nobody
    /// observed. Ship refuses it rather than inheriting a verdict that predates the question.
    let unobservedShipEvidence path (ids: string list) =
        errorDiagnostic
            "ship.unobservedEvidence"
            (Some path)
            $"{ids.Length} supported evidence obligation(s) carry no observedRun receipt — the recorded verification is a self-attestation, not an observed run."
            "Re-run 'fsgg-sdd verify --require-observed' after recording receipts with 'fsgg-sdd evidence --from-test-report <trx-or-junit>'. A verify.json produced before the receipt policy does not satisfy it."
            ids

    let staleRequiredTest path ids =
        warningDiagnostic
            "verify.staleRequiredTest"
            (Some path)
            "One or more required test obligations were satisfied against older lifecycle sources."
            "Re-run the verifying tests and record current evidence before treating the work item as verification-ready."
            ids

    let toolDefect (path: string option) (message: string) =
        errorDiagnostic
            "toolDefect"
            path
            message
            "Inspect the command failure and fix the tool or environment before rerunning."
            []
        |> DiagnosticsModule.markToolDefect

    let missingVerificationPrerequisite path message =
        errorForPath
            "ship.missingVerificationPrerequisite"
            path
            message
            "Run fsgg-sdd verify for the selected work item before running fsgg-sdd ship."

    let verificationNotReady path (status: string) =
        errorForPath
            "ship.verificationNotReady"
            path
            $"Verification view reports '{status}' rather than a verification-ready status."
            "Resolve the verification findings and rerun fsgg-sdd verify before ship."

    let failedVerification path ids =
        errorDiagnostic
            "ship.failedVerification"
            (Some path)
            "Verification view reports unresolved blocking findings that must be corrected before ship."
            "Correct the underlying verification findings and rerun fsgg-sdd verify before ship."
            ids

    let shipIdentityMismatch path expectedWorkId actualWorkId =
        identityMismatch
            "ship.identityMismatch"
            "Ship view"
            "Regenerate the ship view for the selected work id."
            path
            expectedWorkId
            actualWorkId

    let malformedShipView path message =
        errorForPath
            "ship.malformedShipView"
            path
            message
            "Remove or repair the malformed readiness/<id>/ship.json before refreshing the ship view."

    let agentsNoTargets path =
        errorForPath
            "agents.noTargets"
            path
            "Agent guidance configuration declares no agent targets."
            "Declare at least one agent target (for example claude or codex) in .fsgg/agents.yml."

    let agentsInvalidGeneratedRoot path targetId =
        errorForRef
            "agents.invalidGeneratedRoot"
            path
            $"Agent guidance target '{targetId}' has a work-model path or generated root that does not resolve within the project."
            "Point the work-model path and each target generated root at a location inside the project."
            targetId

    let agentsWorkModelIdentityMismatch path expectedWorkId actualWorkId =
        identityMismatch
            "agents.workModelIdentityMismatch"
            "Work model"
            "Select the work id that matches the normalized work model, or regenerate the work model."
            path
            expectedWorkId
            actualWorkId

    // Early-stage (FR-010b): a *missing* work model at a pre-work-model stage is not a
    // defect — it is the expected early-stage state. This advisory (DiagnosticInfo, so it
    // never blocks) carries the best-effort facts derived only from artifacts that exist
    // (the present pre-work-model stages, in relatedIds) and points the author at the
    // seeded static guidance. It writes no view and is never digest-stamped (FR-008/FR-011).
    let agentsEarlyStageGuidance (presentStages: string list) =
        commandDiagnostic
            "agents.earlyStageGuidance"
            DiagnosticSeverity.DiagnosticInfo
            (Some ".fsgg/early-stage-guidance.md")
            "No normalized work model exists yet for this work item; this is the expected early-stage state. Full agent guidance is generated once verify or ship builds the work model."
            "Author the pre-work-model stages (charter, specify, clarify, checklist) following .fsgg/early-stage-guidance.md; run fsgg-sdd verify or ship to build the work model before generating full agent guidance."
            (presentStages |> List.distinct |> List.sort)

    let agentsMalformedWorkModel path message =
        errorForPath
            "agents.malformedWorkModel"
            path
            message
            "Regenerate readiness/<id>/work-model.json from current lifecycle sources before generating agent guidance."

    let agentsStaleWorkModel path =
        errorForPath
            "agents.staleWorkModel"
            path
            "Normalized work model source digests no longer match the current lifecycle sources."
            "Rerun fsgg-sdd verify or ship to refresh the work model before generating agent guidance."

    let agentsBlockedWorkModel path relatedIds =
        errorDiagnostic
            "agents.blockedWorkModel"
            (Some path)
            "Normalized work model is blocked by invalid lifecycle source data."
            "Resolve the work-model diagnostics and refresh the work model before generating agent guidance."
            relatedIds

    let agentsUnknownSourceReference path id =
        errorForRef
            "agents.unknownSourceReference"
            path
            $"Work model references unknown lifecycle fact '{id}'."
            "Correct the lifecycle source or regenerate the work model so all references resolve."
            id

    let agentsMalformedGeneratedGuidance path message =
        errorForPath
            "agents.malformedGeneratedGuidance"
            path
            message
            "Remove or repair the malformed generated guidance.json before refreshing agent guidance."

    let agentsStaleGeneratedGuidance path targetId =
        warningDiagnostic
            "agents.staleGeneratedGuidance"
            (Some path)
            $"Generated agent guidance for target '{targetId}' no longer matches the current normalized work model."
            "Regenerate agent guidance so the generated view matches the current work model."
            [ targetId ]

    // Advisory, not a gate. A recorded digest cannot distinguish "rendered from an older work model"
    // from "edited out of band": an interrupted `agents` run leaves exactly the state a tampered file
    // does. Blocking here refused to regenerate the very view whose regeneration is the remedy, and
    // never self-healed (FS.GG.SDD#197). Regeneration resolves every one of those states.
    let agentsBehaviorDivergence path targetIds =
        warningDiagnostic
            "agents.behaviorDivergence"
            (Some path)
            "Configured agent targets record different workflow behavior digests for the same lifecycle model, so at least one target's guidance was not rendered from the current normalized work model."
            "Regenerate agent guidance so every target is rendered from the current normalized work model."
            targetIds

    let refreshMissingSource viewPath sourcePath =
        errorForRef
            "refresh.missingSource"
            viewPath
            $"Generated view '{viewPath}' cannot be refreshed because declared source '{sourcePath}' is missing."
            "Restore or author the missing declared source before refreshing the generated view."
            sourcePath

    let refreshMalformedSource viewPath sourcePath message =
        errorForRef
            "refresh.malformedSource"
            viewPath
            message
            "Repair the malformed or schema-incompatible declared source before refreshing the generated view."
            sourcePath

    let refreshStaleView viewPath sourcePaths =
        warningDiagnostic
            "refresh.staleView"
            (Some viewPath)
            $"Generated view '{viewPath}' no longer matches its current declared sources."
            "Refresh the generated view from its current declared sources."
            sourcePaths

    let refreshMalformedGeneratedView viewPath message =
        warningForPath
            "refresh.malformedGeneratedView"
            viewPath
            message
            "Regenerate the malformed generated view from its current declared sources."

    let refreshBlockedUpstreamView viewPath upstreamViewPath =
        errorForRef
            "refresh.blockedUpstreamView"
            viewPath
            $"Generated view '{viewPath}' cannot be refreshed until upstream view '{upstreamViewPath}' is current."
            "Bring the named upstream generated view to currency before refreshing this dependent view."
            upstreamViewPath

    // Early-stage (FR-010b): refresh has nothing to bring to currency until the
    // pre-work-model authoring stages exist. When the work model is absent *and* its
    // authored sources have not been written yet, this advisory (non-blocking) reports the
    // navigable early-stage state and points to the seeded static guidance. relatedIds
    // carries the present pre-work-model stages; no view is written (FR-005/008/011).
    let refreshEarlyStageGuidance (presentStages: string list) =
        commandDiagnostic
            "refresh.earlyStageGuidance"
            DiagnosticSeverity.DiagnosticInfo
            (Some ".fsgg/early-stage-guidance.md")
            "No normalized work model exists yet for this work item; refresh has no generated views to bring to currency at this early stage."
            "Author the pre-work-model stages (charter, specify, clarify, checklist) following .fsgg/early-stage-guidance.md; the generated views are refreshed once verify or ship builds the work model."
            (presentStages |> List.distinct |> List.sort)

    let refreshUnrenderableSummary summaryPath relatedIds =
        errorDiagnostic
            "refresh.unrenderableSummary"
            (Some summaryPath)
            $"Readiness summary '{summaryPath}' cannot be rendered because its required structured readiness data is missing, stale, or blocked."
            "Bring the structured readiness views the summary projects to currency before rendering the summary."
            relatedIds

    let changeFromEffectResult (request: CommandRequest) (result: CommandEffectResult) =
        match result.Effect with
        | CreateDirectory path ->
            let operation =
                if result.Succeeded then
                    match result.Snapshot with
                    | Some _ -> ArtifactOperation.NoChange
                    | None -> ArtifactOperation.Create
                else
                    ArtifactOperation.Refuse

            Some
                { Path = path
                  Kind = "directory"
                  Ownership = "sdd"
                  Operation = operation
                  BeforeDigest = None
                  AfterDigest = None
                  SafeWriteDecision =
                    if not result.Succeeded then
                        "refused"
                    elif request.DryRun && operation <> ArtifactOperation.NoChange then
                        "dryRunOnly"
                    elif operation = ArtifactOperation.NoChange then
                        "preserveExisting"
                    else
                        "safe"
                  DiagnosticIds = result.Diagnostic |> Option.map (fun d -> [ d.Id ]) |> Option.defaultValue [] }
        | WriteFile(path, text, kind) ->
            let operation =
                if result.Succeeded then
                    match result.Snapshot with
                    | Some snapshot when snapshot.Text = text -> ArtifactOperation.NoChange
                    | Some _ -> ArtifactOperation.Update
                    | None -> ArtifactOperation.Create
                else
                    ArtifactOperation.Refuse

            let beforeDigest =
                result.Snapshot
                |> Option.map (fun snapshot -> SchemaVersionModule.sha256Text snapshot.Text)

            let afterDigest =
                if result.Succeeded then
                    Some(SchemaVersionModule.sha256Text text)
                else
                    None

            Some
                { Path = path
                  Kind = writeKindValue kind
                  Ownership =
                    match kind with
                    | GeneratedView -> "generated"
                    | HybridArtifact _ -> "hybrid"
                    | _ -> "authored"
                  Operation = operation
                  BeforeDigest = beforeDigest
                  AfterDigest = afterDigest
                  SafeWriteDecision =
                    if not result.Succeeded then
                        "refused"
                    elif request.DryRun && operation <> ArtifactOperation.NoChange then
                        "dryRunOnly"
                    elif operation = ArtifactOperation.NoChange then
                        "preserveExisting"
                    elif kind = GeneratedView then
                        "refreshGeneratedView"
                    else
                        "safe"
                  DiagnosticIds = result.Diagnostic |> Option.map (fun d -> [ d.Id ]) |> Option.defaultValue [] }
        | ReadFile _
        | EnumerateDirectory _
        | RunProcess _
        | SetExecutable _
        | Confirm _ -> None

    let governanceCompatibility: GovernanceCompatibilityFact list =
        [ { Path = ".fsgg/policy.yml"
            Relationship = "optionalGovernancePolicy"
            RequiredBySdd = false
            State = "notEvaluated"
            DiagnosticIds = [] }
          { Path = ".fsgg/capabilities.yml"
            Relationship = "optionalGovernanceCapabilities"
            RequiredBySdd = false
            State = "notEvaluated"
            DiagnosticIds = [] }
          { Path = ".fsgg/tooling.yml"
            Relationship = "optionalGovernanceTooling"
            RequiredBySdd = false
            State = "notEvaluated"
            DiagnosticIds = [] } ]

    let planCorrectionCommand (diagnostics: Diagnostic list) =
        let ids = diagnostics |> List.map _.Id |> Set.ofList

        if
            Set.contains "missingSpecificationPrerequisite" ids
            || Set.contains "malformedSpecificationFacts" ids
            || Set.contains "specificationIdentityMismatch" ids
        then
            Some Specify
        elif
            Set.contains "missingClarificationPrerequisite" ids
            || Set.contains "malformedClarificationFrontMatter" ids
            || Set.contains "clarificationIdentityMismatch" ids
        then
            Some Clarify
        elif
            Set.contains "missingChecklistPrerequisite" ids
            || Set.contains "failedChecklistPrerequisite" ids
            || Set.contains "checklistIdentityMismatch" ids
            || Set.contains "malformedChecklistFrontMatter" ids
            || Set.contains "missingChecklistBackReference" ids
            || Set.contains "duplicateChecklistId" ids
            || Set.contains "unknownChecklistSourceReference" ids
        then
            Some Checklist
        elif
            Set.contains "planIdentityMismatch" ids
            || Set.contains "malformedPlanFrontMatter" ids
            || Set.contains "duplicatePlanId" ids
            || Set.contains "unknownPlanSourceReference" ids
            || Set.contains "stalePlanDecision" ids
        then
            Some Plan
        else
            None

    let tasksCorrectionCommand (diagnostics: Diagnostic list) =
        let ids = diagnostics |> List.map _.Id |> Set.ofList

        if
            Set.contains "missingSpecificationPrerequisite" ids
            || Set.contains "malformedSpecificationFacts" ids
            || Set.contains "specificationIdentityMismatch" ids
        then
            Some Specify
        elif
            Set.contains "missingClarificationPrerequisite" ids
            || Set.contains "malformedClarificationFrontMatter" ids
            || Set.contains "clarificationIdentityMismatch" ids
        then
            Some Clarify
        elif
            Set.contains "missingChecklistPrerequisite" ids
            || Set.contains "failedChecklistPrerequisite" ids
            || Set.contains "checklistIdentityMismatch" ids
            || Set.contains "malformedChecklistFrontMatter" ids
            || Set.contains "missingChecklistBackReference" ids
            || Set.contains "duplicateChecklistId" ids
            || Set.contains "unknownChecklistSourceReference" ids
        then
            Some Checklist
        elif
            Set.contains "missingPlanPrerequisite" ids
            || Set.contains "failedPlanPrerequisite" ids
            || Set.contains "planIdentityMismatch" ids
            || Set.contains "malformedPlanFrontMatter" ids
            || Set.contains "duplicatePlanId" ids
            || Set.contains "unknownPlanSourceReference" ids
        then
            Some Plan
        elif
            Set.contains "tasksIdentityMismatch" ids
            || Set.contains "malformedTasksArtifact" ids
            || Set.contains "duplicateTaskId" ids
            || Set.contains "unknownTaskSourceReference" ids
            || Set.contains "unknownTaskDependency" ids
            || Set.contains "taskDependencyCycle" ids
            || Set.contains "doneTaskMissingEvidence" ids
            || Set.contains "skippedTaskMissingRationale" ids
            || Set.contains "missingTasksPrerequisite" ids
            || Set.contains "failedTasksPrerequisite" ids
            || Set.contains "missingDisposition" ids
        then
            Some Tasks
        elif
            Set.contains "malformedAnalysisView" ids
            || Set.contains "analysisIdentityMismatch" ids
            || Set.contains "blockedGeneratedViewRefresh" ids
            || Set.contains "malformedGeneratedView" ids
        then
            None
        else
            None

    let verifyCorrectionCommand (diagnostics: Diagnostic list) =
        let ids = diagnostics |> List.map _.Id |> Set.ofList

        if
            ids |> Set.contains "evidence.missingAnalysisPrerequisite"
            || ids |> Set.contains "evidence.analysisNotReady"
            || ids |> Set.contains "malformedAnalysisView"
            || ids |> Set.contains "analysisIdentityMismatch"
        then
            Some Analyze
        elif
            ids |> Set.contains "missingTasksPrerequisite"
            || ids |> Set.contains "malformedTasksArtifact"
            || ids |> Set.contains "tasksIdentityMismatch"
            || ids |> Set.contains "duplicateTaskId"
            || ids |> Set.contains "unknownTaskDependency"
            || ids |> Set.contains "taskDependencyCycle"
            || ids |> Set.contains "evidence.missingRequiredSkill"
        then
            Some Tasks
        elif
            ids
            |> Set.exists (fun id ->
                id.StartsWith("evidence.", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("verify.", StringComparison.OrdinalIgnoreCase))
        then
            Some Evidence
        else
            None

    let shipCorrectionCommand (diagnostics: Diagnostic list) =
        let ids = diagnostics |> List.map _.Id |> Set.ofList

        if
            ids |> Set.contains "ship.missingVerificationPrerequisite"
            || ids |> Set.contains "ship.verificationNotReady"
            || ids |> Set.contains "ship.failedVerification"
            || ids |> Set.contains "verify.identityMismatch"
            || ids |> Set.contains "verify.malformedVerificationView"
        then
            Some Verify
        elif
            ids |> Set.contains "evidence.missingAnalysisPrerequisite"
            || ids |> Set.contains "evidence.analysisNotReady"
            || ids |> Set.contains "malformedAnalysisView"
            || ids |> Set.contains "analysisIdentityMismatch"
        then
            Some Analyze
        elif
            ids
            |> Set.exists (fun id -> id.StartsWith("evidence.", StringComparison.OrdinalIgnoreCase))
        then
            Some Evidence
        else
            None

    // FS-GG/FS.GG.SDD#305: a stale toolchain is invisible in the artifacts it emits, so three separate
    // consumers independently rediscovered one already-fixed defect. A workspace declares its floor as
    // `sdd.minToolVersion` in .fsgg/project.yml; running below it warns on every command that reads the
    // config. Warning, not error: the floor states what the workspace expects, and a stale tool still
    // produces usable output — it just cannot be trusted as fresh signal.
    let projectToolVersionBelowMinimum (installed: string) (minimum: string) =
        warningDiagnostic
            "project.toolVersionBelowMinimum"
            (Some ".fsgg/project.yml")
            $"fsgg-sdd {installed} is older than the workspace floor sdd.minToolVersion {minimum}; reports from this run may reflect already-fixed defects."
            "Update the CLI (dotnet tool update --global FS.GG.SDD.Cli), or lower sdd.minToolVersion in .fsgg/project.yml if the floor is wrong."
            [ installed; minimum ]

    // A floor that cannot be parsed is a silent no-op unless it is surfaced — exactly the failure mode
    // FS-GG/FS.GG.SDD#305 exists to close. Warn rather than block, so a config typo never wedges a
    // workspace out of every lifecycle command.
    let projectMinToolVersionUnparseable (value: string) =
        warningDiagnostic
            "project.minToolVersionUnparseable"
            (Some ".fsgg/project.yml")
            $"sdd.minToolVersion '{value}' is not a major.minor.patch version; the tool-version floor is not enforced."
            "Set sdd.minToolVersion to a major.minor.patch version (for example 0.9.0), or remove it to declare no floor."
            [ value ]
