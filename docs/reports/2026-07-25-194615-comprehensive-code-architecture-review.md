# Comprehensive code and architecture review

- Repository: `FS-GG/FS.GG.SDD`
- Reviewed revision: `77a9f8e16f6ae1c29425684e1c854761ea4a2c7f`
- Review completed: 2026-07-25 19:46:15 UTC (21:46:15 CEST)
- Scope: artifacts, command model, CLI, validation, acceptance/contracts, public signatures, workflows, and cross-repository role

## Executive assessment

SDD is a mature contract engine with strong separation between artifact models, command semantics, CLI composition, and validation. The current revision is green and 2,021 Release tests passed. No new correctness failure was found. The main risks are complexity concentration in command/scaffolding handlers and an external acceptance layer that is legitimately environment-dependent but therefore absent from ordinary local and some scheduled runs.

Overall risk: **low to medium**. The implementation is well tested and previous parser hardening is present; future risk is dominated by public-contract breadth and integration coverage rather than known broken behavior.

## Architecture

The repository uses artifact and command libraries as the semantic core, with validation and CLI projects composing those contracts. `.fsi` files make much of the F# public surface explicit. Acceptance and contract projects then verify executable behavior and cross-repository composition. This is an appropriate structure for a tool that acts as a producer contract for Templates and the wider FS-GG workflow.

## Evidence

| Suite, Release | Result |
|---|---|
| Artifacts | 508 passed |
| Commands | 1,100 passed |
| CLI | 212 passed |
| Validation | 35 passed |
| Acceptance | 39 passed, 5 skipped |
| Contracts | 127 passed |
| Total | 2,021 passed, 5 skipped |
| Current-revision GitHub checks | 9 succeeded, 0 failed |

The five skipped acceptance cases require a real composition-provider registry and clearly report that the environment variable is absent. This review was not a production registry availability or adversarial-input fuzzing exercise.

## Findings

### 1. Medium — command and scaffolding behavior is concentrated in very large modules

`DiagnosticConstructors`, `TaskGraphAuthoring`, `HandlersEvidence`, `HandlersScaffold`, `ReleaseContract`, and `WorkModel` are all roughly 1,100–1,600 lines; some associated tests are larger still. These modules encode many policy branches and artifact transformations.

The large test suite limits regression risk, but changes have a broad review radius and make it difficult to isolate invariants.

Recommendation: extract small domain services around evidence, scaffold mutation, and task-graph authoring. Preserve public command types and add golden/characterization cases before each extraction.

### 2. Medium — the public command contract is broad and costly to evolve

The `CommandTypes.fs`/`.fsi` pair is each about a thousand lines. Explicit signatures are a strength, but the breadth makes versioning and semantic review expensive and increases the chance that unrelated commands share public types.

Recommendation: group the public surface into cohesive command-family modules and publish a compatibility policy for additions, deprecations, and serialized representations. Keep `.fsi` parity checks as a gate.

### 3. Medium — real-provider acceptance coverage is opt-in

Five composition-provider tests skip when `FSGG_SDD_ACCEPTANCE_REGISTRY` is unavailable. The scheduled workflow can also omit them when the corresponding secret is not configured. This is honest capability detection, not a test defect, but it leaves provider drift outside the default signal.

Recommendation: provision a read-only acceptance registry for at least one scheduled job, fail that job when the prerequisite silently disappears, and retain local skip behavior for contributors without credentials.

### 4. Low — complexity tests should continue to protect parser resource bounds

Previous review work hardened YAML handling with depth/size pre-scans after a stack-overflow class failure. That remediation is present and the suite is green.

Recommendation: keep those adversarial fixtures as permanent regression gates and add allocation/time ceilings for representative maximum-size artifacts.

## Strengths

- The architecture separates artifact semantics, commands, validation, and CLI concerns.
- Public F# contracts are made explicit with signature files and contract tests.
- The Release suite is broad and current GitHub checks are fully green.
- External acceptance prerequisites are reported explicitly.
- Parser resource-bound hardening addresses a historically high-impact failure mode.

## Recommended order

1. Ensure one scheduled run exercises the real provider registry.
2. Decompose evidence and scaffold handlers behind stable command contracts.
3. Partition the public command surface by command family.
4. Add resource-ceiling assertions beside the existing adversarial parser tests.
