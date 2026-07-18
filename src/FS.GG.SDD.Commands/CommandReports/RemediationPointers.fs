namespace FS.GG.SDD.Commands.Internal

// Feature 078 (#125): every authoring-grammar *blocking* (error-severity) diagnostic points to
// the shipped authoring surface so an author can self-unblock. This module is the single source of
// truth for that covered set: it maps each covered diagnostic id to the vendored process skill that
// resolves it, and renders the deterministic pointer sentence appended to the diagnostic's
// Correction by `DiagnosticConstructors`.
//
// The pointer targets the **vendored `fs-gg-sdd-*` skills**, not tool-repo-only docs. Every
// scaffolded product vendors the process skills under `.claude/`, `.codex/`, and the neutral
// `.agents/` skill roots (byte-identical, drift-guarded), so a skill reference resolves in a
// scaffold's tree — whereas `docs/examples/` and `docs/reference/` live only in this tool's own
// repo and left a scaffold operator following a `fix:` pointer into a dead end (FS.GG.SDD#539).
// The reference is by skill **name** (never a `.claude`/`.codex`/`.agents` path), so generic SDD
// embeds no agent-runtime literal and the same sentence is correct for a Claude, Codex, or neutral
// `.agents` runtime.
//
// Two targets: `Skill` is the per-stage skill that shows the artifact plus its stage-specific ids
// and required headings; `Grammar` is a section anchor into the cross-cutting
// `fs-gg-sdd-authoring-contracts` skill for the five load-bearing gating grammars. Stable-id rules
// are stage-specific (each stage skill documents its own id prefixes), so stable-id blocks carry no
// grammar anchor and point at the stage skill alone.
//
// The pointer strings are constants (no filesystem I/O at construction). The
// RemediationPointersTests guard recomputes the live authoring-contracts skill anchor slugs and
// checks each cited skill is present on disk, so the guidance cannot rot. See
// specs/078-diagnostic-remediation-pointers/contracts/remediation-pointer.md.
//
// Kept `module internal` with no separate .fsi, matching the sibling `DiagnosticConstructors`
// (Constitution III's .fsi requirement is for public modules; this is reached only via
// InternalsVisibleTo("FS.GG.SDD.Commands.Tests")).
module internal RemediationPointers =

    /// Where a covered diagnostic's correction sends the author. At least one field is `Some`
    /// for every registry entry (guard-enforced).
    type RemediationPointer =
        { Skill: string option // vendored stage skill name, e.g. "fs-gg-sdd-specify"
          Grammar: string option } // section anchor slug into the fs-gg-sdd-authoring-contracts skill

    let skill name = Some $"fs-gg-sdd-{name}"

    let grammar slug = Some slug

    // GitHub heading slugs of the five gating-grammar sections in the vendored
    // fs-gg-sdd-authoring-contracts skill (its numbered `## N. …` headings). Stable-id declarations
    // have no cross-cutting section — they are stage-specific — so `stableIds` is deliberately absent
    // and those diagnostics cite the stage skill alone.
    let frontMatter = grammar "5-per-stage-front-matter-used-by-every-authored-stage"
    let coverageLine = grammar "1-checklist-coverage-line-used-by-checklist"
    let decisionTag = grammar "4-clarify-decision-tag-resolution-used-by-clarify"
    let evidenceDecl = grammar "2-evidenceyml-satisfaction-used-by-evidenceverify"
    let specifyFacts = grammar "3-specify---input-intent-facts-used-by-specify"

    let at sk gr = { Skill = sk; Grammar = gr }

    let charterSkill = skill "charter"
    let specifySkill = skill "specify"
    let clarifySkill = skill "clarify"
    let checklistSkill = skill "checklist"
    let planSkill = skill "plan"
    let tasksSkill = skill "tasks"
    let evidenceSkill = skill "evidence"

    /// The enumerated authoring-grammar covered set (FR-001): the error-severity blocking
    /// diagnostics whose Correction gains a resolving pointer, keyed by diagnostic id. Includes
    /// the grammar-rooted aggregate readiness blocks (clarify Q1). Excludes pure
    /// sequencing/config/tool-defect blocks and the non-blocking `stale*` warnings.
    //
    // A stable-id-class block (`duplicate*Id`, `missing*Id`, `unknown*Reference`, dependency/cycle
    // blocks) cites the stage skill alone (grammar `None`) — the id prefixes and required headings
    // it names live in that stage skill, and no authoring-contracts section is cross-cutting for
    // them.
    let registry: Map<string, RemediationPointer> =
        [ // charter
          "malformedCharterFrontMatter", at charterSkill frontMatter
          "charterIdentityMismatch", at charterSkill frontMatter
          // specify
          "missingSpecificationIntent", at specifySkill specifyFacts
          "malformedSpecificationFrontMatter", at specifySkill frontMatter
          "malformedSpecificationFacts", at specifySkill None
          "duplicateSpecificationId", at specifySkill None
          "missingSpecificationId", at specifySkill None
          "unknownSpecificationReference", at specifySkill None
          "specificationIdentityMismatch", at specifySkill frontMatter
          // clarify
          "missingClarificationAnswer", at clarifySkill decisionTag
          "unresolvedBlockingAmbiguity", at clarifySkill decisionTag
          "malformedClarificationFrontMatter", at clarifySkill frontMatter
          "duplicateClarificationId", at clarifySkill None
          "unknownClarificationReference", at clarifySkill None
          "unsafeDecisionChange", at clarifySkill decisionTag
          "clarificationIdentityMismatch", at clarifySkill frontMatter
          // checklist
          "malformedChecklistFrontMatter", at checklistSkill frontMatter
          "missingChecklistBackReference", at checklistSkill None
          "duplicateChecklistId", at checklistSkill None
          "unknownChecklistSourceReference", at checklistSkill None
          "failedChecklistPrerequisite", at checklistSkill coverageLine
          "checklistIdentityMismatch", at checklistSkill frontMatter
          // plan
          "malformedPlanFrontMatter", at planSkill frontMatter
          "duplicatePlanId", at planSkill None
          "unknownPlanSourceReference", at planSkill None
          "failedPlanPrerequisite", at planSkill None
          "planIdentityMismatch", at planSkill frontMatter
          // tasks
          "malformedTasksArtifact", at tasksSkill frontMatter
          "duplicateTaskId", at tasksSkill None
          "unknownTaskSourceReference", at tasksSkill None
          "unknownTaskDependency", at tasksSkill None
          "taskDependencyCycle", at tasksSkill None
          "doneTaskMissingEvidence", at tasksSkill None
          "skippedTaskMissingRationale", at tasksSkill None
          "failedTasksPrerequisite", at tasksSkill None
          "tasksIdentityMismatch", at tasksSkill frontMatter
          // evidence
          "evidence.malformedEvidenceArtifact", at evidenceSkill evidenceDecl
          "evidence.duplicateEvidenceId", at evidenceSkill evidenceDecl
          "evidence.unknownReference", at evidenceSkill evidenceDecl
          "evidence.missingRequiredEvidence", at evidenceSkill evidenceDecl
          "evidence.undisclosedSyntheticEvidence", at evidenceSkill evidenceDecl
          "evidence.missingDeferralRationale", at evidenceSkill evidenceDecl
          "evidence.missingRequiredSkill", at evidenceSkill evidenceDecl
          "evidence.artifactNotFound", at evidenceSkill evidenceDecl
          "evidence.unsupportedResultState", at evidenceSkill evidenceDecl
          "evidence.unsafeUpdate", at evidenceSkill evidenceDecl
          "evidence.identityMismatch", at evidenceSkill frontMatter
          // verify
          "verify.missingRequiredTest", at evidenceSkill evidenceDecl
          "verify.unobservedRequiredTest", at evidenceSkill evidenceDecl
          // ship
          "ship.unobservedEvidence", at evidenceSkill evidenceDecl ]
        |> Map.ofList

    /// The deterministic pointer sentence appended to a covered diagnostic's Correction, or `""`
    /// for a non-covered id (so appending it to a non-covered correction is a no-op — FR-008).
    let suffixFor (id: string) : string =
        match Map.tryFind id registry with
        | None -> ""
        | Some pointer ->
            match pointer.Skill, pointer.Grammar with
            | Some sk, Some gr ->
                $"See the {sk} skill and the grammar under the fs-gg-sdd-authoring-contracts skill (#{gr})."
            | Some sk, None -> $"See the {sk} skill."
            | None, Some gr -> $"See the grammar under the fs-gg-sdd-authoring-contracts skill (#{gr})."
            | None, None -> ""
