# Contract: Post-instantiation staging (MVU tick sequence)

Pins how the scaffold staged driver (`computeScaffoldNext`, `HandlersScaffold.fs:294-324`)
sequences the post-instantiation effects. Stateless recomputation from `InterpretedEffects`
(Decision 3); no new `CommandModel` field.

## Tick sequence (success create outcome)

| Tick | Precondition (from interpreted-effect log) | Plans | Sets `Scaffold`? |
|---|---|---|---|
| (existing) invoke | create not interpreted/planned | install/update/skeleton/create | no |
| **A** | create interpreted, success, probe not yet planned | provenance `WriteFile` + `git rev-parse` probe + one `SetExecutable` per produced `.sh` | no |
| **B** | probe interpreted, init not yet decided | `git init` iff `Started ∧ ExitCode≠0`; else nothing | no |
| **C** | `git init` interpreted **or** init skipped in B | — | **yes** (terminal) |

## Invariants

- **I1 (FR-009)**: a non-success create outcome (`ProviderFailed`, intrusion, `Started=false`)
  short-circuits to the existing one-tick terminal finalize — **no** tick A/B/C, no probe,
  init, or exec.
- **I2 (FR-002/003)**: `git init` is planned only in tick B and only when the probe says
  not-in-a-tree and git is available.
- **I3 (FR-010)**: `Scaffold` is set exactly once, in tick C, after every post-instantiation
  effect is interpreted — so a partially-applied scaffold is never reported complete, and a
  skipped convenience step never reports failure.
- **I4 (FR-004)**: tick A plans the provenance write **before** the `git init` of tick B, so
  the initialized work tree captures provenance.
- **I5**: the MVU loop (`CommandWorkflow.fs:129-133`) and the test loop
  (`ScaffoldCommandTests.fs:35-54`) re-tick while new effects are returned; no driver change.
- **I6 (FR-008)**: under dry-run, finalize returns immediately with a planned summary
  describing the post-instantiation steps and performs none (existing dry-run branch,
  extended to mention repo-init + chmod in the hint).

## Marker effects (phase detection)

- Probe: `RunProcess("git", ["rev-parse"; "--is-inside-work-tree"], _)`.
- Init: `RunProcess("git", ["init"], _)`.
- Exec batch: `SetExecutable _`.

These are disjoint from the create marker (`RunProcess("dotnet", args, _)` containing `-o`,
`isCreateProcess`), so phase detection is unambiguous.
</content>
