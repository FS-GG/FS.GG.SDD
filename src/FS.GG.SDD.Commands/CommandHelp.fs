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
          { Name = "surface"
            Description = "Check or refresh the committed docs/api-surface .fsi baselines (read-only by default)." }
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
        | Evidence ->
            [ work
              title
              // Feature 077: pre-map each newly scaffolded obligation to a proving test file.
              flag
                  "--from-tests"
                  (Some "<path>")
                  "Seed each scaffolded obligation with a verification source pointing at this test path."
              // FS.GG.SDD#350 / ADR-0035: record a receipt for a run SDD actually read. Note the two
              // flags name DIFFERENT things — where the tests live, versus a report of a run.
              // FS.GG.SDD#542: the flag ENRICHES typed obligations; it does not type them. Say so, so
              // a fresh all-missing scaffold does not read the resulting evidenceBlocking count as a
              // test-visibility failure.
              flag
                  "--from-test-report"
                  (Some "<path>")
                  "Record an observedRun receipt from a TRX or JUnit report onto obligations already typed kind: verification: SDD parses it and hashes its bytes. It enriches typed obligations only — it does not type or bootstrap a freshly-scaffolded obligation (kind: missing) — and never runs the suite itself."
              // FS.GG.SDD#550: the maintenance complement to --from-test-report. When a TRX is
              // regenerated, receipts pinned to it go stale; this re-stamps them in place.
              flag
                  "--sync-observed-run"
                  (Some "<trx>")
                  "Re-stamp every obligation already carrying an observedRun receipt sourced from this report, recomputing its digest and passed/failed/skipped counts from the report's current bytes. The maintenance complement to --from-test-report for a regenerated TRX; receipts sourced from another report are left untouched. Mutually exclusive with --from-test-report."
              dryRun ]
        | Plan ->
            [ work
              title
              // Feature 090: the explicit gesture that re-baselines the plan's `## Source Snapshot`
              // after an upstream edit. Without it a moved digest blocks and writes nothing.
              flag
                  "--accept-upstream"
                  None
                  "Re-baseline the plan's Source Snapshot against the current spec, clarifications, and checklist."
              dryRun ]
        // FS.GG.SDD#350 / ADR-0035 stage 3b (FS.GG.SDD#497): fail-closed on an unobserved pass is
        // now the DEFAULT. `--no-require-observed` is the opt-out for a migration window; the legacy
        // `--require-observed` stays a recognized, now-redundant explicit accept.
        | Verify
        | Ship ->
            [ work
              title
              flag
                  "--no-require-observed"
                  None
                  "Opt out of the default receipt requirement: let an obligation whose result:pass carries no observedRun receipt satisfy, as it did before the ADR-0035 stage 3b flip. Applies to BOTH verify and ship."
              flag
                  "--require-observed"
                  None
                  "Now the default and redundant: fail closed on an unobserved pass. Kept recognized so pre-flip invocations that passed it still work."
              dryRun ]
        | Checklist
        | Tasks
        | Analyze
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
        | Lint ->
            [ flag "--explain" None "Run the same pre-flight checks against the stage's own artifact (non-blocking)." ]
        // surface: `--check` (default, read-only, exit 1 on drift) or `--update` (refresh baselines);
        // roots default to src/ and docs/api-surface/ and are overridable via --param. Feature 094
        // adds the version axis the bump prompt reads (never writes) — also --param, also defaulted.
        | Surface ->
            [ flag "--check" None "Report API-surface baseline drift; read-only, exits 1 on drift (default)."
              flag
                  "--update"
                  None
                  "Refresh the docs/api-surface .fsi baselines from the authored signatures (takes precedence)."
              flag "--param" (Some "<key=value>") "Root override: sourceRoot=<dir> / baselineRoot=<dir>."
              flag
                  "--param"
                  (Some "versionAxisFile=<file>")
                  "File the coherent-set version axis is read from (default Directory.Build.props); never written."
              flag
                  "--param"
                  (Some "versionAxisProperty=<name>")
                  "MSBuild property holding the coherent-set version (default Version)." ]
        // Feature 105, Phase 2: capture/check a framework package's real restored surface against a
        // committed docs/dependency-surface/** baseline. baselineRoot defaults, no package literal.
        | DependencySurface ->
            [ flag
                  "--check"
                  None
                  "Restore and check every authored/committed dependency-surface target; writes no captures, exits 1 on missing capture or drift (default)."
              flag
                  "--update"
                  None
                  "Refresh/create the docs/dependency-surface captures from the restored packages (takes precedence)."
              flag
                  "--param"
                  (Some "packageId=<id>")
                  "Explicit capture target package id (with version=); default discovers authored references and committed captures."
              flag "--param" (Some "version=<ver>") "Explicit capture target version (with packageId=)."
              flag
                  "--param"
                  (Some "baselineRoot=<dir>")
                  "Committed-capture root override (default docs/dependency-surface)." ]
        // `Help` is the scope a help report is stamped with, not an invocable command, so it has
        // no flags of its own — `commandHelp Help` is unreachable from argv.
        | Help -> []

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
