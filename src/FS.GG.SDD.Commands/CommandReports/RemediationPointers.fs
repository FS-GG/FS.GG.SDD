namespace FS.GG.SDD.Commands.Internal

// Feature 078 (#125): every authoring-grammar *blocking* (error-severity) diagnostic points to
// its shipped example and grammar section so an author can self-unblock. This module is the single
// source of truth for that covered set: it maps each covered diagnostic id to the shipped example
// (docs/examples/lifecycle-artifacts/<stage>) and/or the grammar section anchor
// (docs/reference/authoring-contracts.md#<slug>) that resolves it, and renders the deterministic
// pointer sentence appended to the diagnostic's Correction by `DiagnosticConstructors`.
//
// The pointer strings are constants (no filesystem I/O at construction). The
// RemediationPointersTests guard recomputes the live anchor slugs and the on-disk example paths and
// fails if any cited target no longer resolves, so the guidance cannot rot. See
// specs/078-diagnostic-remediation-pointers/contracts/remediation-pointer.md.
//
// Kept `module internal` with no separate .fsi, matching the sibling `DiagnosticConstructors`
// (Constitution III's .fsi requirement is for public modules; this is reached only via
// InternalsVisibleTo("FS.GG.SDD.Commands.Tests")).
module internal RemediationPointers =

    /// Where a covered diagnostic's correction sends the author. At least one field is `Some`
    /// for every registry entry (guard-enforced).
    type RemediationPointer =
        { Example: string option // repo-relative POSIX path under docs/examples/lifecycle-artifacts/
          Anchor: string option } // docs/reference/authoring-contracts.md#<slug>

    let example name =
        Some $"docs/examples/lifecycle-artifacts/{name}"

    let anchor slug =
        Some $"docs/reference/authoring-contracts.md#{slug}"

    // GitHub heading slugs of the grammar sections in docs/reference/authoring-contracts.md.
    let frontMatter = anchor "per-stage-front-matter"
    let stableIds = anchor "stable-id-declarations"
    let coverageLine = anchor "acceptance-coverage-line"
    let decisionTag = anchor "clarify-decision-tag-resolution"
    let evidenceDecl = anchor "evidenceyml-declarations"
    let specifyFacts = anchor "specify---input-intent-facts"

    let at ex an = { Example = ex; Anchor = an }

    let charterEx = example "charter.md"
    let specEx = example "spec.md"
    let clarifyEx = example "clarifications.md"
    let checklistEx = example "checklist.md"
    let planEx = example "plan.md"
    let tasksEx = example "tasks.yml"
    let evidenceEx = example "evidence.yml"

    /// The enumerated authoring-grammar covered set (FR-001): the error-severity blocking
    /// diagnostics whose Correction gains a resolving pointer, keyed by diagnostic id. Includes
    /// the grammar-rooted aggregate readiness blocks (clarify Q1). Excludes pure
    /// sequencing/config/tool-defect blocks and the non-blocking `stale*` warnings.
    let registry: Map<string, RemediationPointer> =
        [ // charter
          "malformedCharterFrontMatter", at charterEx frontMatter
          "charterIdentityMismatch", at charterEx frontMatter
          // specify
          "missingSpecificationIntent", at specEx specifyFacts
          "malformedSpecificationFrontMatter", at specEx frontMatter
          "malformedSpecificationFacts", at specEx stableIds
          "duplicateSpecificationId", at specEx stableIds
          "missingSpecificationId", at specEx stableIds
          "unknownSpecificationReference", at specEx stableIds
          "specificationIdentityMismatch", at specEx frontMatter
          // clarify
          "missingClarificationAnswer", at clarifyEx decisionTag
          "unresolvedBlockingAmbiguity", at clarifyEx decisionTag
          "malformedClarificationFrontMatter", at clarifyEx frontMatter
          "duplicateClarificationId", at clarifyEx stableIds
          "unknownClarificationReference", at clarifyEx stableIds
          "unsafeDecisionChange", at clarifyEx decisionTag
          "clarificationIdentityMismatch", at clarifyEx frontMatter
          // checklist
          "malformedChecklistFrontMatter", at checklistEx frontMatter
          "duplicateChecklistId", at checklistEx stableIds
          "unknownChecklistSourceReference", at checklistEx stableIds
          "failedChecklistPrerequisite", at checklistEx coverageLine
          "checklistIdentityMismatch", at checklistEx frontMatter
          // plan
          "malformedPlanFrontMatter", at planEx frontMatter
          "duplicatePlanId", at planEx stableIds
          "unknownPlanSourceReference", at planEx stableIds
          "failedPlanPrerequisite", at planEx stableIds
          "planIdentityMismatch", at planEx frontMatter
          // tasks
          "malformedTasksArtifact", at tasksEx frontMatter
          "duplicateTaskId", at tasksEx stableIds
          "unknownTaskSourceReference", at tasksEx stableIds
          "unknownTaskDependency", at tasksEx stableIds
          "taskDependencyCycle", at tasksEx stableIds
          "doneTaskMissingEvidence", at tasksEx stableIds
          "skippedTaskMissingRationale", at tasksEx stableIds
          "tasksIdentityMismatch", at tasksEx frontMatter
          // evidence
          "evidence.malformedEvidenceArtifact", at evidenceEx evidenceDecl
          "evidence.duplicateEvidenceId", at evidenceEx evidenceDecl
          "evidence.unknownReference", at evidenceEx evidenceDecl
          "evidence.missingRequiredEvidence", at evidenceEx evidenceDecl
          "evidence.undisclosedSyntheticEvidence", at evidenceEx evidenceDecl
          "evidence.missingDeferralRationale", at evidenceEx evidenceDecl
          "evidence.missingRequiredSkill", at evidenceEx evidenceDecl
          "evidence.unsupportedResultState", at evidenceEx evidenceDecl
          "evidence.unsafeUpdate", at evidenceEx evidenceDecl
          "evidence.identityMismatch", at evidenceEx frontMatter
          // verify
          "verify.missingRequiredTest", at evidenceEx evidenceDecl ]
        |> Map.ofList

    /// The deterministic pointer sentence appended to a covered diagnostic's Correction, or `""`
    /// for a non-covered id (so appending it to a non-covered correction is a no-op — FR-008).
    let suffixFor (id: string) : string =
        match Map.tryFind id registry with
        | None -> ""
        | Some pointer ->
            match pointer.Example, pointer.Anchor with
            | Some ex, Some an -> $"See the shipped example {ex} and the grammar at {an}."
            | Some ex, None -> $"See the shipped example {ex}."
            | None, Some an -> $"See the grammar at {an}."
            | None, None -> ""
