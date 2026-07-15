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
open FS.GG.SDD.Commands.Internal.EarlyStageAuthoring

module internal CharterAuthoring =

    let charterTemplate request workId =
        let title = requestTitle request workId

        $"""---
schemaVersion: 1
workId: {workId}
title: {yamlFrontMatterScalar title}
stage: charter
changeTier: tier1
status: chartered
policyPointers:
  - .fsgg/sdd.yml
  - .fsgg/agents.yml
  - .fsgg/policy.yml
  - .fsgg/capabilities.yml
  - .fsgg/tooling.yml
---

# {title} Charter

## Identity
- Work id: `{workId}`
- Lifecycle stage: charter
- Status: chartered

## Principles
- Capture the work item's local principles before specification begins.

## Scope Boundaries
- Keep SDD lifecycle ownership separate from optional Governance enforcement.

## Policy Pointers
- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.
- Governance files are optional compatibility pointers and are not evaluated by this command.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd specify --work {workId}`.
"""

    let sectionText workId heading =
        match heading with
        | "Identity" -> $"## Identity\n- Work id: `{workId}`\n- Lifecycle stage: charter\n- Status: chartered\n"
        | "Principles" -> "## Principles\n- Capture the work item's local principles before specification begins.\n"
        | "Scope Boundaries" ->
            "## Scope Boundaries\n- Keep SDD lifecycle ownership separate from optional Governance enforcement.\n"
        | "Policy Pointers" ->
            "## Policy Pointers\n- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.\n- Governance files are optional compatibility pointers and are not evaluated by this command.\n"
        | "Lifecycle Notes" -> $"## Lifecycle Notes\n- Next lifecycle action: `fsgg-sdd specify --work {workId}`.\n"
        | _ -> $"## {heading}\n"

    let ensureStandardSections workId text =
        ensureSections MergePolicies.charter (sectionText workId) text

    let charterDiagnosticsAndText request workId model =
        let path = charterPath workId

        match snapshot path model with
        | None -> [], charterTemplate request workId
        | Some existing ->
            match parseCharterFrontMatter path existing.Text with
            | Error diagnostic -> [ diagnostic ], existing.Text
            | Ok frontMatter when frontMatter.SchemaVersion <> "1" ->
                [ malformedCharterFrontMatter
                      path
                      $"Charter schemaVersion '{frontMatter.SchemaVersion}' is not supported." ],
                existing.Text
            | Ok frontMatter when not (String.Equals(frontMatter.WorkId, workId, StringComparison.OrdinalIgnoreCase)) ->
                [ charterIdentityMismatch path workId frontMatter.WorkId ], existing.Text
            | Ok frontMatter when not (String.Equals(frontMatter.Stage, "charter", StringComparison.OrdinalIgnoreCase)) ->
                [ malformedCharterFrontMatter path $"Charter stage '{frontMatter.Stage}' is not 'charter'." ],
                existing.Text
            | Ok _ when
                existing.Text.Contains("<!-- fsgg-sdd: unsafe-overwrite -->", StringComparison.OrdinalIgnoreCase)
                ->
                [ unsafeOverwrite path ], existing.Text
            | Ok _ -> [], ensureStandardSections workId existing.Text

    let charterPrerequisiteDiagnosticsAndText workId model =
        let path = charterPath workId

        match snapshot path model with
        | None -> [ missingCharterPrerequisite path $"Charter prerequisite '{path}' is missing." ], None
        | Some existing ->
            match parseCharterFrontMatter path existing.Text with
            | Error _ ->
                [ missingCharterPrerequisite path "Charter prerequisite front matter is malformed." ],
                Some existing.Text
            | Ok frontMatter when frontMatter.SchemaVersion <> "1" ->
                [ missingCharterPrerequisite
                      path
                      $"Charter schemaVersion '{frontMatter.SchemaVersion}' is not supported." ],
                Some existing.Text
            | Ok frontMatter when not (String.Equals(frontMatter.WorkId, workId, StringComparison.OrdinalIgnoreCase)) ->
                [ charterIdentityMismatch path workId frontMatter.WorkId ], Some existing.Text
            | Ok frontMatter when not (String.Equals(frontMatter.Stage, "charter", StringComparison.OrdinalIgnoreCase)) ->
                [ missingCharterPrerequisite path $"Charter stage '{frontMatter.Stage}' is not 'charter'." ],
                Some existing.Text
            | Ok _ -> [], Some existing.Text
