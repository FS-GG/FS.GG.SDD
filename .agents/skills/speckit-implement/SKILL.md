---
name: speckit-implement
description: Implement tasks with Elmish/MVU boundaries and real-evidence discipline.
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: github-spec-kit
  source: preset:fsharp-opinionated
user-invocable: true
disable-model-invocation: false
---

# Speckit Implement Skill

# /speckit.implement

Execute the feature's tasks against the plan. Update `tasks.md` as you go.

## Status marking discipline

Use the status legend from the template exactly:

- `[ ]` — not started.
- `[X]` — **done.** The code paths that will run in production were exercised.
  Tests used real dependencies (real filesystem, real process, real network)
  where safe. Synthetic evidence is permitted only when disclosed per Principle V
  (see below); a task resting on undisclosed synthetic evidence is not `[X]`.
- `[-]` — **skipped.** Requires written rationale on the task line.

**Never mark a task `[X]` if it failed, or if its "pass" rests on synthetic
evidence that has not been disclosed.** Never weaken an assertion to green a
build — narrow the scope and document it instead. Dishonest status undermines the
whole point of test evidence (Principle V).

## Vertical-slice rule (US phases)

A task tagged `[US*]` may only be marked `[X]` when the user-facing surface was
actually exercised end-to-end. "Exercised" means one of:

- An FSI transcript captured under `readiness/` that drives the new behavior
  through its public entry point — not through internal helpers.
- A smoke run of the host application (CLI invocation, GUI launch, HTTP request)
  that touches the new code path, with the artifact (log, screenshot, response
  body) saved under `readiness/`.
- A semantic test that loads the **packed** library or runs the host binary and
  exercises the user-reachable path — not a unit test against domain modules in
  isolation.

A diff that touches only `Domain/`, `Core/`, `Models/`, or equivalent internal
layers is **never** sufficient evidence for `[X]` on a `[US*]` task. The story
isn't done when the model compiles; it's done when the user can reach it. If
wire-up to the UI / CLI / API surface is missing, the honest status is `[ ]`
(keep working). If the path can only be exercised with synthetic evidence for
now, disclose it per Principle V and open a tracking issue for the real wire-up.

## Elmish/MVU discipline (Principle IV)

For any task whose spec, plan, or task line identifies stateful workflow or I/O,
implement through the Elmish/MVU boundary:

- `Model` captures owned workflow state.
- `Msg` captures user actions, external responses, and internal transitions.
- `Effect` or `Cmd<Msg>` captures requested I/O.
- `init` returns initial state plus startup effects.
- `update` is pure: it may inspect `Msg` and `Model`, but it MUST NOT touch
  filesystem, network, database, process state, wall clock, random source, or
  mutable global state.
- An interpreter at the edge executes effects and maps results back to `Msg`.

Before marking an MVU-bearing `[US*]` task `[X]`, verify all of the following:

- FSI or packed-library tests exercise public `init` / `update` paths.
- Tests assert both next `Model` and emitted effects for representative messages.
- The interpreter path has real evidence where safe (real filesystem, process,
  network, database, or host entry point). If it can only run against a fake,
  in-memory substitute, canned response, or unconnected interpreter, disclose
  that synthetic evidence per Principle V and track the real-evidence path.
- The user-facing entry point is wired through the interpreter boundary, not
  around it.

Simple pure functions do not need an MVU shell. If a task does not involve
stateful workflow or I/O, note that Principle IV is not applicable and use the
ordinary spec → FSI → semantic tests → implementation path.

## Synthetic-evidence disclosure (Principle V)

Synthetic evidence — mocks, stubs, fakes, hardcoded fixtures, in-memory
substitutes, canned responses — MAY be used when real evidence is unavailable or
prohibitively expensive AND a real-evidence path is planned or documented as
infeasible. When you rely on it, disclose it at every surface:

1. **Code-level.** Add a `// SYNTHETIC:` comment at the use site naming the fact
   and the reason (and the real-evidence path if known). Example:
   ```fsharp
   let userRepo = InMemoryUserRepo()  // SYNTHETIC: staging DB not provisioned; real repo in US-17
   ```
   Prefer explicit, ugly literals over clever factories that make synthetic data
   feel real.
2. **Test-level.** Test names exercising the synthetic surface contain the token
   `Synthetic`. Example:
   ```fsharp
   [<Test>] let ``Signup.createUser_Synthetic_persists in-memory`` () = ...
   ```
3. **PR-level.** List every synthetic dependency in the PR description with its
   reason and real-evidence path (or why it is infeasible).

There is no `[S]` task marker, no propagated `[S*]`, and no evidence audit in
this repository: disclosure is the discipline, not a gate.

## Workflow, per task

1. Confirm the task's prerequisites (earlier tasks it depends on) are complete.
   If a prerequisite is unmet, stop and raise it.
2. Implement the task per the plan.
3. Run the verification appropriate for the phase (tests, baseline check, FSI
   exercise, …). For tasks tagged `[US*]`, the verification MUST include a
   user-reachable exercise — see the Vertical-slice rule above. For MVU-bearing
   tasks, include transition/effect assertions and interpreter evidence. A green
   unit test on the domain layer is not enough.
4. Update the status in `tasks.md`. Before writing `[X]` on a `[US*]` task,
   confirm the vertical-slice rule is satisfied; if not, the honest status is
   `[ ]`. If the pass rests on synthetic evidence, add the code-level, test-level,
   and PR-level disclosures before moving on.
5. Move to the next task.

## Visibility discipline (Principle II)

- Never write `private`, `internal`, or `public` on a top-level F# binding.
  Visibility lives in the `.fsi` signature file.
- If a task needs to change the public surface, the `.fsi` update is part of the
  same task — not a follow-up.

## Simplicity discipline (Principle III)

Before reaching for a "clever" F# feature (custom operators, SRTP, reflection,
non-trivial computation expressions, type providers, non-obvious active
patterns), confirm the feature is justified in the spec or plan. If it isn't,
either simplify or stop and raise the justification gap.

## Stop conditions

Stop and ask the user when:

- A task's spec guidance conflicts with its code. The spec wins (Principle III:
  complex features require justification).
- A test fails in a way that would require weakening an assertion or adding
  `[<Skip>]` to pass. Never weaken; surface the failure.
- A task depends on earlier work that is incomplete or missing. Resolve the
  ordering before proceeding.
