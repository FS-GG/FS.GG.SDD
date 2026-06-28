# Phase 1 Data Model: Declared-or-Default Acceptance Build/Run Probes

These are harness-internal F# types in
`tests/FS.GG.SDD.Acceptance.Tests/AcceptanceSupport.fs`. No public
`FS.GG.SDD.*` package type, schema, or emitted-document shape changes (the
`composition-acceptance-result` record is untouched).

## Entities

### `DeclaredCommand` (new) — the optional provider-declared command

| Field | Type | Notes |
|---|---|---|
| `Executable` | `string` | The program to launch. Blank (null/empty/whitespace) ⇒ treated as "no declared command" (FR-010). |
| `Arguments` | `string list` | Arguments passed verbatim to the executable, in order. |

Consumed as `DeclaredCommand option`. Shaped as a **1:1 forward-compatible read**
of the H2 `ProviderDescriptor` build/run fields (FS-GG/FS.GG.SDD#8): adopting H2
maps the descriptor's command field into this record with no resolver change
(FR-004 / SC-004). Working directory is **not** a field — FR-003 fixes it at the
product root.

### `ProbeCommand` (new) — the resolved command a probe actually invokes

| Field | Type | Notes |
|---|---|---|
| `Executable` | `string` | Either the declared executable or the default (`dotnet`). |
| `Arguments` | `string list` | Declared args, or the default args (`["build"]` / `["run"; "--project"; <discovered>]`). |
| `WorkingDirectory` | `string` | Always the product root in this feature. |

The single value handed to the existing process-shell edge
(`startProcess`/`runToCompletion`). It is the spec's **"Probe command"** entity.

### `ProbeResult` (unchanged) — the probe outcome

| Field | Type | Notes |
|---|---|---|
| `Started` | `bool` | Whether the process launched. |
| `ExitCode` | `int` | Process exit code; `-1` for not-started / timed-out. |
| `Diagnostic` | `string` | Surfaced stdout/stderr or a diagnostic message; empty on clean pass. |

Existing record (`AcceptanceSupport.fs:177`). Shape unchanged (spec "Probe
outcome" entity), so the `composition-acceptance-result` facts it feeds are
unchanged.

## Functions (public surface within the harness)

```fsharp
// Deterministic runnable-project discovery (FR-008). None ⇒ no runnable project.
val discoverRunnableProject : root: string -> string option

// Pure resolvers: declared-or-default → ProbeCommand. Blank executable ⇒ default (FR-010).
val resolveBuildCommand : declared: DeclaredCommand option -> root: string -> ProbeCommand
val resolveRunCommand   : declared: DeclaredCommand option -> root: string -> ProbeCommand option
//   resolveRunCommand returns None when no declared command is given AND no runnable
//   project is discoverable, so the run probe can emit the diagnosed not-started outcome.

// Probes: explicit declared-command parameter; passing `None` preserves today's behavior.
val buildProbe : declared: DeclaredCommand option -> root: string -> ProbeResult
val runProbe   : declared: DeclaredCommand option -> root: string -> ProbeResult
```

> Signatures are advisory for the plan, with one hard F# constraint:
> `buildProbe`/`runProbe` are module-level `let` functions, and F# optional
> parameters (`?p`) are permitted only on type members — **not** on `let`-bound
> functions. The declared command is therefore an **explicit** `DeclaredCommand option`
> curried parameter, not a `?declared` optional parameter. The existing
> `CompositionAcceptanceTests.fs` call sites are updated to `buildProbe None root` /
> `runProbe None root` (T009), which is `declared = None` — today's behavior.

## Resolution rules

| Probe | Declared (non-blank) | No declared / blank (default) |
|---|---|---|
| Build | `{ Executable=declared.Executable; Arguments=declared.Arguments; WorkingDirectory=root }` | `{ Executable="dotnet"; Arguments=["build"]; WorkingDirectory=root }` |
| Run | `{ Executable=declared.Executable; Arguments=declared.Arguments; WorkingDirectory=root }` | `discoverRunnableProject root` ⇒ `Some p` → `{ Executable="dotnet"; Arguments=["run";"--project";p]; WorkingDirectory=root }`; `None` → diagnosed not-started `ProbeResult` |

## Bounded-execution invariants (FR-006 / FR-007 / SC-005)

Both probes route the resolved `ProbeCommand` through the existing edge, so:

- Build: 300 s completion timeout; timeout ⇒ killed, `{ Started=true; ExitCode=-1; Diagnostic="… timed out …" }`.
- Run: 10 s grace (pass iff clean exit) + 60 s overall cap; survives-grace ⇒ `{ Started=true; ExitCode=0 }`; exceeds cap ⇒ killed.
- Cannot start (declared or default) ⇒ `{ Started=false; ExitCode=-1; Diagnostic="could not start `<exe>`." }`.
- Run default with no runnable project ⇒ `{ Started=false; ExitCode=-1; Diagnostic="no runnable project discovered." }`.

## Provider-agnostic invariant (FR-009 / SC-003 / T021a)

`DeclaredCommand`, `ProbeCommand`, the resolvers, and the defaults reference only
`dotnet`. No Governance/rendering identity or provider-specific package id,
template id, path, command, or docs URL is introduced. The existing
"acceptance project carries no Governance reference" test continues to scan
`AcceptanceSupport.fs` and pass.
