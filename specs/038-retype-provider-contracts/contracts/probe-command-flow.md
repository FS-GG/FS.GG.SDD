# Contract: Descriptor → Acceptance Probe Declared-Command Flow

Defines how the opt-in composition acceptance harness feeds a resolved provider
descriptor's declared commands into the build/run probes. The probe resolver
semantics are owned by feature 035 and are unchanged; this feature only changes the
**source** of the declared command from a hard-coded `None` to
`descriptor.Build` / `descriptor.Run`.

## Probe signatures (after type unification, D5)

```fsharp
// AcceptanceSupport.fs — local DeclaredCommand removed; canonical type adopted
open Fsgg.Provider   // DeclaredCommand = { Executable: string; Arguments: string list }

val buildProbe : declared: DeclaredCommand option -> root: string -> ProbeOutcome
val runProbe   : declared: DeclaredCommand option -> root: string -> RunOutcome
```

`resolveBuildCommand` / `resolveRunCommand` keep their feature-035 logic verbatim
(the canonical `DeclaredCommand` is field-identical to the retired local copy).

## Resolution rule (unchanged from feature 035)

| `declared` | Build probe invokes | Run probe invokes |
|------------|--------------------|--------------------|
| `None` | `dotnet build` at `root` | `dotnet run --project <discovered>` at `root` |
| `Some c` where `isMalformed c` (blank exe) | `dotnet build` (default) | `dotnet run` (default) |
| `Some c` (valid) | `c.Executable c.Arguments` at `root` | `c.Executable c.Arguments` at `root` |

Bounded-execution windows are unchanged: build = 300 s completion timeout; run =
10 s grace + 60 s overall. A cannot-start / non-zero / timeout outcome is each a
diagnosed non-zero outcome (FR-010).

## Harness wiring (CompositionAcceptanceTests.fs, success path)

Before (feature 035, staged-idle):

```fsharp
let build = buildProbe None root
let run   = if appBuilds then runProbe None root else <skipped>
```

After (this feature, FR-008):

```fsharp
open FS.GG.SDD.Artifacts   // module Config — the registry parser's real home

// Resolve the descriptor the run scaffolded from the registry copied to <root>.
let descriptor =
    snapshotAt root ".fsgg/providers.yml"
    |> Config.parseProviderRegistry
    |> Result.toOption
    |> Option.bind (List.tryFind (fun d -> d.Name = providerName))

let declaredBuild = descriptor |> Option.bind (fun d -> d.Build)
let declaredRun   = descriptor |> Option.bind (fun d -> d.Run)

let build = buildProbe declaredBuild root
let run   = if appBuilds then runProbe declaredRun root else <skipped>
```

The parser lives in `module Config` under namespace `FS.GG.SDD.Artifacts`
(`Config.parseProviderRegistry`). `HandlersScaffold.fs` reaches it through a local
alias (`module ConfigModule = FS.GG.SDD.Artifacts.Config`); the acceptance harness has
no such alias, so it references `Config` directly. (`providerName` is the `--provider`
value the harness already scaffolds with; no provider identity is hard-coded — D7.)

## Invariants

- **FR-009 / SC-004**: the reference provider declares no `build`/`run`, so
  `declaredBuild = declaredRun = None`, the `dotnet` defaults run, and the
  `composition-acceptance-result` verdict is identical in pass/fail to the
  pre-change harness.
- **FR-010 / SC-005**: a descriptor declaring a valid `build`/`run` command makes
  the corresponding probe invoke that command (offline-provable with a synthetic
  descriptor that never starts a `dotnet` process).
- **FR-011 / SC-006 (T021a)**: referencing `FS.GG.Contracts` and using
  `Fsgg.Provider` introduces no Governance/rendering identity and no
  provider-specific package id / template id / path / command / docs URL; the
  invariant scan still passes (D6).
