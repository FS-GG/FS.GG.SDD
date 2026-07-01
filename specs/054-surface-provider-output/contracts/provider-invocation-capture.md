# Contract: provider-output capture at the `RunProcess` edge

**Feature**: `054-surface-provider-output` | Governs `runProcess` in
`src/FS.GG.SDD.Commands/CommandEffects.fs:70-115` and the `ProcessRunResult` it returns.

## Capture protocol

On invoking the provider process, both streams are **captured under a per-stream bound**
instead of drained-and-discarded (today `ReadToEnd() |> ignore`, lines 97-98):

1. Read up to `providerOutputCapChars` (65 536) characters per stream into the retained
   buffer; continue reading (and discarding) the remainder so the child's pipes never block
   (deadlock-safe drain retained). If any remainder was discarded, set that stream's
   `…Truncated` flag.
2. Read **stdout and stderr concurrently** (one task per stream) before `WaitForExit`, so a
   child that fills one pipe while the parent bounds the other cannot deadlock.
3. Decode through the process `StreamReader` (UTF-8 with replacement) so non-UTF-8 / binary
   bytes become replacement characters — never a crash or corrupted JSON (edge case).
4. Record the fully-resolved `Command` (program + args as executed) on the result.

## Result mapping

```
Started = true , exit code c :  { Started=true;  ExitCode=c;  Command; StdOut; StdErr; …Truncated }
launched, then non-UTF-8/large:  truncation flags set; content decoded defensively
Process.Start returns null    :  { Started=false; ExitCode=-1; Command; StdOut=""; StdErr="" }
launch throws (engine absent) :  { Started=false; ExitCode=-1; Command; StdOut="";
                                   StdErr = exception.Message }   // R4: surface the launch error
DryRun                        :  unchanged — success effect None, process never spawned
```

The handler derives the report `ExitCode: int option` as `Some ExitCode` when `Started`
else `None` (FR-003), and `CommandLine` from `Command`.

## Invariants

- **CAP-1**: retained content per stream ≤ `providerOutputCapChars`; truncation flagged
  (FR-005).
- **CAP-2**: capture never changes exit-code observation, outcome classification, or the
  `Started` discriminator — the handler branches (`HandlersScaffold.fs:341-373`) are unchanged
  in control flow; they only additionally read the captured fields (FR-007).
- **CAP-3**: `DryRun` still spawns nothing and captures nothing.
- **CAP-4**: no capture occurs for user-input blocks — they never reach the `RunProcess`
  edge (the create effect is never planned) (FR-006).
- **CAP-5**: defensive decode ⇒ a binary blob on a stream cannot throw or emit invalid JSON.
