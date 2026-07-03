namespace FS.GG.SDD.Commands

open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.CommandTypes

module CommandHelp =
    let private flag name argument description =
        { Name = name
          Argument = argument
          Description = description }

    let globalFlags =
        [ flag "--root" (Some "<path>") "Project root directory (default: the current directory)."
          flag "--json" None "Emit the deterministic JSON report (default)."
          flag "--text" None "Emit a portable plain-text summary."
          flag
              "--rich"
              None
              "Emit human-oriented rich output; degrades to plain text when non-interactive or color-disabled."
          flag "--help, -h" None "Show usage and flags for the command." ]

    let commandEntries =
        [ { Name = "init"
            Description = "Seed the SDD skeleton in the current directory." }
          { Name = "charter"
            Description = "Create the work item charter." }
          { Name = "specify"
            Description = "Draft the specification from labeled intent." }
          { Name = "clarify"
            Description = "Record clarification questions, answers, and decisions." }
          { Name = "checklist"
            Description = "Review requirements quality before planning." }
          { Name = "plan"
            Description = "Author the implementation plan." }
          { Name = "tasks"
            Description = "Generate the dependency-ordered task breakdown." }
          { Name = "analyze"
            Description = "Analyze cross-artifact lifecycle readiness." }
          { Name = "evidence"
            Description = "Declare authored verification evidence." }
          { Name = "verify"
            Description = "Evaluate verification readiness and emit verify.json." }
          { Name = "ship"
            Description = "Aggregate merge-boundary ship readiness and emit ship.json." }
          { Name = "agents"
            Description = "Generate per-target agent command and skill guidance." }
          { Name = "refresh"
            Description = "Regenerate SDD-owned generated views to currency." }
          { Name = "scaffold"
            Description = "Take an empty directory to a buildable SDD product via a provider." }
          { Name = "doctor"
            Description = "Report how a scaffolded product has drifted from its coherent set (read-only)." }
          { Name = "upgrade"
            Description = "Reconcile a behind scaffold across confirmable per-step diffs." }
          { Name = "version"
            Description = "Print the CLI/generator version." }
          { Name = "validate"
            Description = "Run the cross-cutting validation harness." }
          { Name = "registry"
            Description = "Validate a registry document against the schema." } ]

    let private work = flag "--work" (Some "<id>") "Target work item id."
    let private title = flag "--title" (Some "<text>") "Human-readable work item title."

    let private input =
        flag "--input" (Some "<text>") "Labeled intent lines used to seed the artifact."

    let private dryRun =
        flag "--dry-run" None "Report proposed changes without writing."

    let commandFlags (command: SddCommand) =
        match command with
        | Init -> []
        | Charter -> [ work; title ]
        | Specify
        | Clarify -> [ work; title; input; dryRun ]
        | Checklist
        | Plan
        | Tasks
        | Analyze
        | Evidence
        | Verify
        | Ship
        | Agents
        | Refresh -> [ work; title; dryRun ]
        | Scaffold ->
            [ flag "--provider" (Some "<name>") "Template provider declared in .fsgg/providers.yml (required)."
              flag "--param" (Some "<key=value>") "Provider parameter (repeatable)."
              flag "--force" None "Pass the provider's force flag where the contract supports it."
              flag "--no-update" None "Skip the managed template and agent-context updates." ]
        // doctor takes only the global flags (`--root`, format selection).
        | Doctor -> []
        | Upgrade ->
            [ flag "--yes" None "Apply the reconciliation without prompting (explicit non-interactive apply)." ]

    let topLevelHelp (generator: GeneratorVersion) =
        { Scope = TopLevel
          Usage = $"fsgg-sdd <command> [options]  (generator {generator.Version})"
          Commands = commandEntries
          GlobalFlags = globalFlags
          CommandFlags = [] }

    let commandHelp (command: SddCommand) =
        { Scope = Command(commandName command)
          Usage = $"fsgg-sdd {commandName command} [options]"
          Commands = []
          GlobalFlags = globalFlags
          CommandFlags = commandFlags command }
