namespace FS.GG.SDD.Commands.Internal

open System
open System.IO
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

[<AutoOpen>]
module internal Foundation =
    module GenerationManifestModule = FS.GG.SDD.Artifacts.GenerationManifest
    module IdentifiersModule = FS.GG.SDD.Artifacts.Identifiers

    let normalizeRoot (path: string) =
        if String.IsNullOrWhiteSpace path then "." else path.Trim()

    let normalizeRelativePath (path: string) =
        (if String.IsNullOrEmpty path then "" else path.Trim().Replace('\\', '/')).TrimStart('/')

    let projectIdFromRoot (root: string) =
        let name = DirectoryInfo(normalizeRoot root).Name
        if String.IsNullOrWhiteSpace name then "sdd-project" else name.ToLowerInvariant()

    let projectConfigText (projectId: string) =
        $"""schemaVersion: 1
project:
  id: {projectId}
  defaultWorkRoot: work
sdd:
  config: .fsgg/sdd.yml
  agents: .fsgg/agents.yml
"""

    let sddConfigText =
        """schemaVersion: 1
lifecycle:
  stages: [charter, specify, clarify, checklist, plan, tasks, analyze]
artifacts:
  workRoot: work
  readinessRoot: readiness
generatedViews:
  requireSourceDigests: true
  requireGeneratorVersion: true
  staleBehavior: diagnostic
"""

    let agentsConfigText =
        """schemaVersion: 1
agents:
  - id: claude
    guidancePath: CLAUDE.md
    generatedRoot: readiness/{workId}/agent-commands/claude
  - id: codex
    guidancePath: AGENTS.md
    generatedRoot: readiness/{workId}/agent-commands/codex
sourceModel:
  workModel: readiness/{workId}/work-model.json
policy:
  generatedGuidanceIsAuthority: false
  requireEquivalentClaudeAndCodexBehavior: true
"""

    let agentGuidance (name: string) =
        $"""# {name} SDD guidance

This file is an SDD lifecycle guidance target. Generated agent guidance is a
projection over `.fsgg/agents.yml` and readiness data; it is not a second source
of truth.
"""

    // The generic, deterministic lifecycle constitution the SDD skeleton seeds at
    // .fsgg/constitution.md. Authoritative body lives in the feature contract
    // (specs/033-skeleton-constitution/contracts/constitution-content.md); this literal
    // transcribes it verbatim. No date/timestamp/randomness keeps `init` byte-identical
    // (FR-007); no repo/provider/template/rendering token keeps it generic (FR-003).
    let constitutionText =
        """# Product Constitution

This constitution governs spec-driven development for this product. It is the
highest-precedence engineering authority here: it overrides conflicting habits,
prompts, and generated plans. It was seeded by the SDD skeleton as a populated
baseline; ratify or amend it to fit this product, then treat it as the contract
every change is measured against.

## Core Principles

### I. Specify Before Implementing

Every non-trivial change MUST start from a written specification: the
user-visible outcome, the scope boundary, the change tier, the public-surface
impact, and how the change will be verified. Sketch the public shape and exercise
it interactively before the implementation hardens it. Specs precede code so that
humans, scripts, and agents can agree on the contract while it is still cheap to
change.

### II. Structured Artifacts Are the Machine Contract

Markdown is an authoring surface for humans. Schema-versioned structured artifacts
are the contract tools and gates rely on. Each lifecycle stage MUST declare which
data is authoritative; when prose and structured data disagree, the plan MUST say
which wins, how the conflict is reported, and which view records it. Avoid
replacing prose drift with schema drift: keep typed contracts stable and
versioned.

### III. Public Surface Is Declared, Not Incidental

The public surface of a module MUST be declared explicitly — in signature files
where the language supports them — rather than left as a side effect of
implementation. Maintain a surface baseline once code exists. A contracted change
that does not update signatures, baselines, tests, and docs together is
incomplete.

### IV. Idiomatic Simplicity Is the Default

Prefer the plain, idiomatic form: functions over classes, records and discriminated
unions over hierarchies, simple modules over frameworks, and the standard library
over clever abstractions. Reach for advanced or metaprogramming features only with
a justification recorded in the plan. Mutation and loops are allowed where they are
clearer or measurably necessary; say so in a short comment.

### V. Model–Update–Effect Is the Boundary for State and I/O

Any workflow with multi-step state or external I/O MUST expose or clearly wrap a
Model–Update–Effect boundary: durable state, explicit messages, requested effects,
a pure transition, and an edge interpreter that performs the real I/O. Pure
parsers, data models, and validators need no such ceremony. Keep I/O out of pure
transitions so behavior stays testable and deterministic.

### VI. Test Evidence Is Mandatory

Behavior-changing code MUST ship with automated tests that fail before the change
and pass after. Prefer real filesystem, process, and schema fixtures over mocks;
when synthetic data is unavoidable, disclose it near the test and say what real
path it stands in for. Generated views, schema migrations, and output contracts
need snapshot or golden coverage once they are tool-facing.

### VII. Agents and Humans Share One Contract

Command-line users, automation, and coding agents MUST operate over the same
artifacts. Agent prompts and skills may help author files, but they are never a
second source of truth. If an agent writes an authoring surface, the corresponding
structured model and views are refreshed by the workflow or report a stale-view
diagnostic.

### VIII. Observability and Safe Failure

Operationally significant events MUST produce actionable diagnostics: parse
failures, missing artifacts, stale views, conflicting state, and integration
failures. Distinguish malformed user input from tool defects. Critical paths fail
fast and visibly; optional integrations degrade explicitly rather than silently.

## Change Classification

Every change declares a tier in its spec:

- **Tier 1 (contracted change):** public surface, schema, generated view, command,
  artifact layout, agent-skill contract, or external integration. Requires a spec,
  a plan, tasks, signatures where code exists, tests, docs, and migration notes
  when applicable.
- **Tier 2 (internal change):** implementation cleanup with no externally visible
  contract change. Requires a spec and tests; signatures and baselines stay
  unchanged.

## Development Workflow

Use the spec-driven loop: specify, clarify as needed, plan, break into tasks,
implement, and analyze before merge. For lifecycle features, the plan identifies
the authored artifacts, the structured contracts, the generated views, the schema
and migration posture, the agent-facing behavior, any optional external governance
integration, and the tests and fixtures that cover stale or conflicting artifacts.

## Governance

This constitution overrides conflicting local habits, prompts, and generated plans.
Amendments require a change with a stated rationale and migration impact. When the
constitution and a template disagree, the constitution wins and the template is
defective until synchronized.

Versioning policy:

- MAJOR: backward-incompatible principle or governance changes.
- MINOR: new principles or materially expanded obligations.
- PATCH: clarifications that do not change obligations.

This baseline is unratified. Record your product's ratification once the team has
reviewed and adopted these principles.
"""

    // The generic, deterministic early-stage authoring guidance the SDD skeleton seeds at
    // .fsgg/early-stage-guidance.md (FR-010a, feature 049). It covers the four
    // pre-work-model stages — charter, specify, clarify, checklist — that exist before
    // readiness/<id>/work-model.json (and therefore before `fsgg-sdd agents`/`refresh` can
    // generate per-work-item guidance). It is an authoring SURFACE, not a machine contract:
    // every heading list, id prefix, command, path, and authoring rule it names is a
    // read-only mirror of the live SDD contract, pinned by EarlyStageGuidanceContractTests
    // so the prose can never drift from the parser. No date/timestamp/randomness keeps
    // `init` byte-identical (FR-007); no repo/provider/template/rendering token keeps it
    // generic. The fenced blocks carry drift-guard tags (headings:/ids:/coverage:/evidence:).
    let earlyStageGuidanceText =
        """# Early-Stage Authoring Guidance

This file is seeded by the SDD skeleton. It gives an author (or their coding agent)
self-contained guidance for the **pre-work-model** lifecycle stages — `charter`,
`specify`, `clarify`, and `checklist` — the stages that run **before**
`readiness/<id>/work-model.json` exists. Until that work model is built (by
`fsgg-sdd verify` or `fsgg-sdd ship`), `fsgg-sdd agents` and `fsgg-sdd refresh` cannot
generate per-work-item agent guidance, so this static guidance is what you author from.

It is an authoring surface, not a machine contract. The authoritative definitions live
in the SDD tool; this file restates them so you never have to read or decompile the CLI.

## Lifecycle order

The pre-work-model stages run in this order:

`fsgg-sdd charter` → `fsgg-sdd specify` → `fsgg-sdd clarify` → `fsgg-sdd checklist`

Each stage authors one Markdown artifact under `work/<id>/`. Author the required section
headings (verbatim, as `## <heading>`) and use the stable-id formats listed below.

## `fsgg-sdd charter`

Authors `work/<id>/charter.md`. Required section headings:

```text headings:charter
Identity
Principles
Scope Boundaries
Policy Pointers
Lifecycle Notes
```

Stable ids: the charter declares no scoped ids. It carries front-matter fields
(`workId`, `stage`, `changeTier`, `status`).

## `fsgg-sdd specify`

Authors `work/<id>/spec.md`. Required section headings:

```text headings:specify
User Value
Scope
Non-Goals
User Stories
Acceptance Scenarios
Functional Requirements
Ambiguities
Public Or Tool-Facing Impact
Lifecycle Notes
```

Stable-id formats (each is `PREFIX-` followed by three or more digits, e.g. `FR-001`):

```text ids:specify
US
AC
FR
SB
AMB
```

## `fsgg-sdd clarify`

Authors `work/<id>/clarifications.md`. Required section headings:

```text headings:clarify
Source Specification
Clarification Questions
Answers
Decisions
Accepted Deferrals
Remaining Ambiguity
Lifecycle Notes
```

Stable-id formats (each is `PREFIX-` followed by three or more digits):

```text ids:clarify
CQ
DEC
AMB
```

## `fsgg-sdd checklist`

Authors `work/<id>/checklist.md`. Required section headings:

```text headings:checklist
Source Specification
Source Clarifications
Source Snapshot
Checklist Items
Review Results
Accepted Deferrals
Blocking Findings
Advisory Notes
Lifecycle Notes
```

Stable-id formats (each is `PREFIX-` followed by three or more digits):

```text ids:checklist
CHK
CR
```

## §1.1 Acceptance coverage line

`fsgg-sdd checklist` marks a functional requirement **covered** only when a strict-scan
parser finds a list item that leads with `- FR-###:` and carries an acceptance reference
(`AC-###`) **on the same line**:

- the item starts with a literal `- `, then the id, then a literal `:`;
- the requirement id is `FR-` followed by three or more digits (case-insensitive);
- the acceptance reference(s) sit on that same line;
- there is prose after the colon.

A bold id (`**FR-001**`), a colon-less id (`- FR-001 — …`), or an acceptance reference on
a different line does **not** establish coverage.

Copyable accepted form (establishes coverage):

```text coverage:accepted
- FR-001: The system records one outcome per request. (covers AC-001)
```

## §1.2 `evidence.yml` satisfaction

Each entry under `evidence:` declares a `kind` and a `result`. An obligation is
**satisfied** only by a matching declaration whose `result` is `pass` **and** whose
`synthetic` is `false`.

- `synthetic: true` with `result: pass` discloses a stand-in and does **not** satisfy.
- `result: deferred` (or `kind: deferral`) is an accepted deferral, not a satisfaction.
- `result: fail`, `missing`, `stale`, or `blocked` does not satisfy.

Copyable declaration that satisfies its obligation:

```yaml evidence:satisfied
schemaVersion: 1
evidence:
  - id: EV001
    kind: verification
    subject:
      type: task
      id: T001
    result: pass
    synthetic: false
```

## Once the work model exists

After `fsgg-sdd verify` or `fsgg-sdd ship` builds `readiness/<id>/work-model.json`, the
generated per-work-item views under `readiness/<id>/agent-commands/<target>/` become the
authoritative agent guidance. This static early-stage guidance covers only the
pre-work-model window; it does not shadow or duplicate those generated views.

For the full authoring contracts, see `docs/reference/authoring-contracts.md`.
"""

    let earlyStageGuidancePath = ".fsgg/early-stage-guidance.md"

    let initEffects (request: CommandRequest) =
        let projectId = projectIdFromRoot request.ProjectRoot

        [ CreateDirectory ".fsgg"
          CreateDirectory "work"
          CreateDirectory "readiness"
          WriteFile(".fsgg/project.yml", projectConfigText projectId, StructuredSource)
          WriteFile(".fsgg/sdd.yml", sddConfigText, StructuredSource)
          WriteFile(".fsgg/agents.yml", agentsConfigText, StructuredSource)
          WriteFile(".fsgg/constitution.md", constitutionText, AgentGuidanceTarget)
          WriteFile(earlyStageGuidancePath, earlyStageGuidanceText, AgentGuidanceTarget)
          WriteFile("AGENTS.md", agentGuidance "Codex", AgentGuidanceTarget)
          WriteFile("CLAUDE.md", agentGuidance "Claude", AgentGuidanceTarget) ]

    let charterPath workId = $"work/{workId}/charter.md"
    let specPath workId = $"work/{workId}/spec.md"
    let clarificationPath workId = $"work/{workId}/clarifications.md"
    let checklistPath workId = $"work/{workId}/checklist.md"
    let planPath workId = $"work/{workId}/plan.md"
    let tasksPath workId = $"work/{workId}/tasks.yml"
    let evidencePath workId = $"work/{workId}/evidence.yml"
    let workModelPath workId = GenerationManifestModule.expectedWorkModelOutputPath workId
    let analysisPath workId = $"readiness/{workId}/analysis.json"
    let verifyPath workId = $"readiness/{workId}/verify.json"
    let shipPath workId = $"readiness/{workId}/ship.json"
    let readinessDirectory workId = $"readiness/{workId}"

    let charterReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(charterPath workId)
          ReadFile(specPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(workModelPath workId)
          EnumerateDirectory "work" ]

    let clarifyReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(charterPath workId)
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(workModelPath workId)
          EnumerateDirectory "work" ]

    let checklistReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(charterPath workId)
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(workModelPath workId)
          EnumerateDirectory "work" ]

    let planReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(charterPath workId)
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(planPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(workModelPath workId)
          EnumerateDirectory "work" ]

    let tasksReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(charterPath workId)
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(planPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(workModelPath workId)
          EnumerateDirectory "work" ]

    let analyzeReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(planPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(workModelPath workId)
          ReadFile(analysisPath workId)
          EnumerateDirectory "work" ]

    let evidenceReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(planPath workId)
          ReadFile(tasksPath workId)
          ReadFile(workModelPath workId)
          ReadFile(analysisPath workId)
          ReadFile(evidencePath workId)
          EnumerateDirectory "work" ]

    let verifyReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(planPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(analysisPath workId)
          ReadFile(workModelPath workId)
          ReadFile(verifyPath workId)
          EnumerateDirectory "work" ]

    let shipReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          // Optional Governance config: presence-only detection for the handoff (FR-011).
          ReadFile ".fsgg/policy.yml"
          ReadFile ".fsgg/capabilities.yml"
          ReadFile ".fsgg/tooling.yml"
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(planPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(analysisPath workId)
          ReadFile(workModelPath workId)
          ReadFile(verifyPath workId)
          ReadFile(shipPath workId)
          EnumerateDirectory "work" ]

    let agentsReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(workModelPath workId)
          EnumerateDirectory "work" ]

    let refreshReadEffects workId =
        // NOTE: charter.md is intentionally not read. The reused analyze/verify/ship
        // generators do not read charter standalone, so reading it here would make
        // refresh regenerate a different work model than the lifecycle, breaking
        // idempotency. Refresh never writes charter regardless.
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          // Optional Governance config: presence-only detection for the handoff (FR-011).
          ReadFile ".fsgg/policy.yml"
          ReadFile ".fsgg/capabilities.yml"
          ReadFile ".fsgg/tooling.yml"
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(planPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(analysisPath workId)
          ReadFile(workModelPath workId)
          ReadFile(verifyPath workId)
          ReadFile(shipPath workId)
          // Scaffold provenance: provider-produced paths are excluded from refresh.
          ReadFile ".fsgg/scaffold-provenance.json"
          ReadFile(GenerationManifestModule.expectedGovernanceHandoffOutputPath workId)
          ReadFile(GenerationManifestModule.expectedSummaryOutputPath workId)
          EnumerateDirectory "work" ]

    let scaffoldReadEffects =
        // Provider registry + a before-snapshot of the target root (for the produced
        // diff and the non-empty-target collision guard).
        [ ReadFile ".fsgg/providers.yml"
          EnumerateDirectory "" ]

    let workIdDiagnostics (request: CommandRequest) =
        match request.Command, request.WorkId with
        | Init, _ -> []
        // Scaffold is cross-cutting and operates on --root, not a work item.
        | Scaffold, _ -> []
        | _, None -> [ missingWorkId request.Command ]
        | _, Some value ->
            match IdentifiersModule.createWorkId value with
            | Ok _ -> []
            | Error _ -> [ malformedWorkId value ]

    let plan (request: CommandRequest) =
        let diagnostics = workIdDiagnostics request

        if not (List.isEmpty diagnostics) then
            diagnostics, []
        else
            match request.Command, request.WorkId with
            | Init, _ -> [], initEffects request
            | Charter, Some workId
            | Specify, Some workId -> [], charterReadEffects workId
            | Clarify, Some workId -> [], clarifyReadEffects workId
            | Checklist, Some workId -> [], checklistReadEffects workId
            | Plan, Some workId -> [], planReadEffects workId
            | Tasks, Some workId -> [], tasksReadEffects workId
            | Analyze, Some workId -> [], analyzeReadEffects workId
            | Evidence, Some workId -> [], evidenceReadEffects workId
            | Verify, Some workId -> [], verifyReadEffects workId
            | Ship, Some workId -> [], shipReadEffects workId
            | Agents, Some workId -> [], agentsReadEffects workId
            | Refresh, Some workId -> [], refreshReadEffects workId
            | Scaffold, _ -> [], scaffoldReadEffects
            | command, _ -> [ unsupportedCommand command ], []

    let effectKey effect =
        match effect with
        | ReadFile path -> "read:" + normalizeRelativePath path
        | EnumerateDirectory path -> "enumerate:" + normalizeRelativePath path
        | CreateDirectory path -> "mkdir:" + normalizeRelativePath path
        | WriteFile(path, _, kind) -> $"write:{normalizeRelativePath path}:{writeKindValue kind}"
        | RunProcess(command, args, workingDir) ->
            let renderedArgs = String.concat " " args
            $"run:{command} {renderedArgs}@{normalizeRelativePath workingDir}"
        | SetExecutable path -> "setexec:" + normalizeRelativePath path
        | EmitStdout text -> "stdout:" + text
        | EmitStderr text -> "stderr:" + text
        | SetExitCode code -> "exit:" + string code

    let readEffectKey path = "read:" + normalizeRelativePath path

    let hasPlanned key model =
        model.PendingEffects |> List.exists (fun effect -> effectKey effect = key)

    let hasInterpreted key model =
        model.InterpretedEffects |> List.exists (fun result -> effectKey result.Effect = key)

    let hasPlannedWrite model =
        model.PendingEffects
        |> List.exists (function
            | CreateDirectory _
            | WriteFile _ -> true
            | _ -> false)

    let appendNewEffects effects model =
        let existing = model.PendingEffects |> List.map effectKey |> Set.ofList

        effects
        |> List.filter (fun effect -> not (Set.contains (effectKey effect) existing))

    let snapshot path model =
        let key = readEffectKey path

        model.InterpretedEffects
        |> List.tryPick (fun result ->
            if effectKey result.Effect = key then
                result.Snapshot
            else
                None)

    let directoryListing path model =
        let key = "enumerate:" + normalizeRelativePath path

        model.InterpretedEffects
        |> List.tryPick (fun result ->
            if effectKey result.Effect = key then
                result.Snapshot |> Option.map _.Text
            else
                None)
        |> Option.defaultValue ""

    let plannedReadPaths model =
        model.PendingEffects
        |> List.choose (function
            | ReadFile path -> Some(normalizeRelativePath path)
            | _ -> None)

    let allPlannedReadsInterpreted model =
        model.PendingEffects
        |> List.filter (function
            | ReadFile _
            | EnumerateDirectory _ -> true
            | _ -> false)
        |> List.forall (fun effect -> hasInterpreted (effectKey effect) model)

    let duplicateCandidateReadEffects workId model =
        let selectedPrefix = $"work/{workId}/"
        let already = plannedReadPaths model |> Set.ofList

        directoryListing "work" model
        |> fun text -> text.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map normalizeRelativePath
        |> Array.filter (fun path ->
            (path.EndsWith("/charter.md", StringComparison.OrdinalIgnoreCase)
             || path.EndsWith("/spec.md", StringComparison.OrdinalIgnoreCase))
            && not (path.StartsWith(selectedPrefix, StringComparison.OrdinalIgnoreCase))
            && not (Set.contains path already))
        |> Array.sort
        |> Array.map ReadFile
        |> Array.toList

    let stripQuotes (value: string) =
        let value = if String.IsNullOrEmpty value then "" else value.Trim()
        value.Trim([| '"'; '\'' |])

    let tryScalar key (yaml: string) =
        let pattern = $"(?m)^\\s*{Regex.Escape key}\\s*:\\s*(.*?)\\s*$"
        let m = Regex.Match(yaml, pattern)

        if m.Success then
            let value = stripQuotes m.Groups.[1].Value
            if String.IsNullOrWhiteSpace value then None else Some value
        else
            None

    let splitFrontMatter (text: string) =
        let normalized = (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")
        let lines = normalized.Split('\n')

        if lines.Length > 0 && lines.[0].Trim() = "---" then
            lines
            |> Array.mapi (fun index line -> index, line)
            |> Array.tryFind (fun (index, line) -> index > 0 && line.Trim() = "---")
            |> Option.map (fun (index, _) ->
                let yaml = lines.[1 .. index - 1] |> String.concat "\n"
                let body = lines.[index + 1 ..] |> String.concat "\n"
                yaml, body)
        else
            None

    type CharterFrontMatter =
        { SchemaVersion: string
          WorkId: string
          Title: string
          Stage: string
          ChangeTier: string
          Status: string }

    let generatedViewState
        (path: string)
        (kind: string)
        (generator: GeneratorVersion)
        (sources: GeneratedViewSource list)
        (outputDigest: OutputDigest option)
        (currency: GeneratedViewCurrency)
        (diagnosticIds: string list)
        : GeneratedViewState
        =
        { Path = path
          Kind = kind
          SchemaVersion = Some 1
          Generator = Some generator
          Sources = sources |> List.sortBy _.Path
          OutputDigest = outputDigest
          Currency = currency
          DiagnosticIds = diagnosticIds |> List.distinct |> List.sort }

    let blockingDiagnosticIds (diagnostics: Diagnostic list) : string list =
        diagnostics
        |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
        |> List.map _.Id

    let blockedWorkModelView (path: string) (generator: GeneratorVersion) (blockingIds: string list) : GeneratedViewState =
        generatedViewState path "workModel" generator [] None GeneratedViewCurrency.Blocked blockingIds

    // The pre-work-model stages whose authored artifact already exists for the selected
    // work item, derived only from the enumerated `work` listing (FR-011: best-effort
    // facts from artifacts that actually exist, never fabricated). Stage tokens match the
    // lifecycle command names so the early-stage NextAction can compute the next command.
    let earlyStagePresentStages workId model =
        let listing = directoryListing "work" model

        let present path stage =
            if listing.Contains(normalizeRelativePath path) then [ stage ] else []

        (present (charterPath workId) "charter")
        @ (present (specPath workId) "specify")
        @ (present (clarificationPath workId) "clarify")
        @ (present (checklistPath workId) "checklist")

