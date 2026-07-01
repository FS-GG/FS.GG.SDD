# Contract: the `Confirm` effect + confirmation edge protocol

Feature `053-upgrade-doctor-remediation` · reads FR-007, FR-011, FR-012; Constitution V. See
research R7.

## Why a new effect

The CLI is today a batch pipeline: `init → interpretUntilIdle → BuildReport → render → exit`. The
edge interpreter (`CommandEffects.interpret`) never reads stdin. ADR-0009 requires each `upgrade`
step to be **shown as a diff and confirmed** before it mutates. Modelling confirmation as an
`Effect` keeps the constitutional MVU boundary: `Effect` requests the I/O, the edge performs it, the
pure `update` transitions on the result.

## Type additions (data-model E3)

```fsharp
// CommandEffect (additive case)
| Confirm of stepId: string * prompt: string

// CommandEffectResult (additive field; None for every existing result)
Confirmed: bool option
```

## Edge interpreter rules (`CommandEffects.interpret`)

For `Confirm(stepId, prompt)`:

| Condition | Behavior | `Confirmed` |
|-----------|----------|-------------|
| `DryRun = true` | never mutate, never read stdin | `Some false` |
| `IsInteractive = true` | write `prompt` (the step diff + a `[y/N]`), read one line from `Console.In`; `y`/`yes` (case-insensitive) → confirmed | `Some true` / `Some false` |
| `IsInteractive = false` | never reached for a genuine confirm: the pure core refuses up front (see below) | n/a |

The `prompt` text is presentation only and excluded from the deterministic json contract (like
process stdout/stderr). The **decision** (`Confirmed`) is the contract-relevant fact and is recorded
in `UpgradeSummary` step outcomes.

## Interactivity & `--yes` threading

- `--yes` → `CommandRequest.AssumeYes = true`, parsed in `Program.fs`.
- `IsInteractive` is computed at the edge in `Program.fs`/`Rendering.detectCapabilities` from
  `Console.IsInputRedirected` (a **new** input-interactivity signal; the existing capability struct
  only tracks output redirection for rich degradation) and threaded into `CommandRequest`.

## Pure-core decision table (`HandlersUpgrade`)

| `AssumeYes` | `IsInteractive` | Behavior |
|-------------|-----------------|----------|
| `true` | any | Apply each step directly — **no `Confirm` emitted** (short-circuit); `Mode = "assumeYes"`. |
| `false` | `true` | Emit one `Confirm` per step; apply the step iff `Confirmed = Some true`; `Mode = "interactive"`. |
| `false` | `false` | **Refuse**: emit `upgrade.nonInteractiveNoYes`, zero writes, no `Confirm`, no prompt-hang; `Mode = "refusedNonInteractive"`, exit 1 (FR-012 / SC-004). |

## Staging (re-derive from the interpreted-effect log)

`HandlersUpgrade` computes the next step from `model.InterpretedEffects` (the `Confirmed` outcomes
and any applied-step results), exactly like `HandlersScaffold` re-derives its tick — **no new model
state field**. Each tick: if the current step is unconfirmed, emit its `Confirm`; once confirmed,
emit the step's apply effect(s); once the apply result is in the log, advance to the next step;
after the last, finalize the `UpgradeSummary`.

## Test posture

- A scripted-stdin harness drives the interactive confirm-each path (synthetic stdin disclosed in the
  test name, per Constitution VI) — confirm-all, decline-one, decline-all.
- A non-interactive (`IsInteractive = false`, no `--yes`) test asserts zero writes, no hang, exit 1.
- A `DryRun` test asserts `Confirm` resolves `Some false` and nothing mutates.
- An additive-field regression: every existing effect result carries `Confirmed = None`; existing
  goldens unchanged.
