# Contract: Declared-or-Default Probe Command Resolution

The behavioral contract for how the composition acceptance **build probe** and
**run probe** choose the command they invoke. It adds **no** new lifecycle
interface and **no** public SDD schema. It refines the build/run-probe section of
[`../../034-scaffold-composition-acceptance/contracts/acceptance-protocol.md`](../../034-scaffold-composition-acceptance/contracts/acceptance-protocol.md)
(§"The build/run probe") and is forward-compatible with the H2
`ProviderDescriptor` build/run fields (FS-GG/FS.GG.SDD#8).

## Declared command (optional input)

A probe accepts an **optional** declared command:

```
DeclaredCommand = { Executable: string; Arguments: string list }   // consumed as option
```

- **Read shape (FR-004 / SC-004)**: `{ Executable, Arguments }` is the structured,
  pre-tokenized form the process edge consumes directly. When H2 lands, the
  descriptor's build/run command field maps into this record with no resolver
  change — only the call site passes `Some` instead of `None`.
- **Working directory**: always the **product root** (FR-003); not part of the
  declared shape in this feature.
- **Blank normalization (FR-010)**: a `Some` whose `Executable` is
  null/empty/whitespace is treated identically to `None` (no declared command).

## Resolution

| | Declared command present (non-blank) | Absent / blank (default) |
|---|---|---|
| **Build** (FR-001/FR-003) | invoke `Executable Arguments` at the product root | `dotnet build` at the product root |
| **Run** (FR-002/FR-003) | invoke `Executable Arguments` at the product root | `dotnet run --project <discovered>` at the product root |

- **Run-project discovery (FR-008)**: `<discovered>` is selected
  **deterministically** — enumerate `*.fsproj`/`*.csproj` under the product root,
  ordinal-sort the forward-slash relative paths, take the first. Same product ⇒
  same target.
- Defaults reference **only** `dotnet` — never a provider/template/package/path or
  docs URL (FR-009).

## Bounded execution (FR-006 / FR-007)

Declared and default commands run through the **same** bounds:

- **Build**: 300 s completion timeout.
- **Run**: 10 s grace window (pass iff clean exit) + 60 s overall cap; a process
  that survives the grace window without a non-zero exit counts as started.
- A process exceeding its bound is **killed** and reported non-zero.

Three distinct diagnosed, non-zero outcomes — never a silent pass, never a hang:

| Failure mode | `ProbeResult` |
|---|---|
| Could not start (declared or default exe) | `{ Started=false; ExitCode=-1; Diagnostic="could not start `<exe>`." }` |
| Exited non-zero | `{ Started=true; ExitCode=<n≠0>; Diagnostic=<surfaced output> }` |
| Timed out | `{ Started=true; ExitCode=-1; Diagnostic="… timed out …" }` |
| Run default, no runnable project discovered | `{ Started=false; ExitCode=-1; Diagnostic="no runnable project discovered." }` |

## Guarantees

- **No observable change today (FR-005 / SC-001)**: the only reachable state is "no
  declared command", which resolves to `dotnet build` and
  `dotnet run --project <discovered>` over the single runnable reference product —
  the same facts and the same `composition-acceptance-result` pass/fail verdict.
- **Declared beats default (SC-002)**: given a synthetic declared command, the
  probe invokes it, never `dotnet` — provable on the **pure resolver** with no
  process spawned.
- **H2 is pure wiring (SC-004)**: adopting the descriptor's build/run fields
  requires only passing them into the existing optional parameter.
- **Provider-agnostic (FR-009 / SC-003)**: no Governance/rendering identity and no
  provider-specific id/template/path/command/docs URL in the harness or defaults.
- **No hang (SC-005)**: every failure mode yields a diagnosed non-zero outcome
  within its bound.

## Non-goals

- No change to the scaffold command, the `scaffold-provider` schema, the
  `scaffold-provenance` record, or any lifecycle artifact.
- H2 itself (the extended `ProviderDescriptor` fields, FS-GG/FS.GG.SDD#8/#9) is out
  of scope; this feature only makes the probes ready to consume it.
- The network-gated real-provider path is unchanged; the new declared-command
  coverage runs in the default offline inner loop.
