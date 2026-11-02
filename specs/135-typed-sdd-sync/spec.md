# Typed SDD synchronization

Feature 135 · FS.GG.SDD#1164 · routine source window. The existing literate Quint source remains authority. This feature joins an accepted profile-2 contract to the existing `WorkspaceLifecycle`, `WorkspaceCorrespondence`, and `QuintReplay` APIs. It does not change their v1 schemas or the F# backend.

## User journey

An agent edits the literate source and declares finite checks and executable bindings. Authoring validates its model before the existing explicit acceptance. After the accepted transaction commits, `typed-sdd sync --work ID --cache PATH` checks the accepted revision against the qualified model tools and the built implementation. A failed or interrupted sync leaves acceptance intact but blocks verify and ship. A repair repeats sync against the same accepted meaning. A source or implementation change also triggers a fresh check without relying on a supplied correspondence JSON document.

## Contract

- The versioned sync declaration names accepted module IDs, their WorkspaceModuleKind, references, assumptions, evidence obligations, executable selector, required scenarios and invariants, and finite bounds. It contains no expected result or parallel transition semantics. Every module ID resolves to an exported or promoted catalogue identity. Every evidence ID resolves to a promoted evidence row. Missing or conflicting declarations refuse. The first window supports two modules in one generated target; imports remain unsupported.
- The authority join creates a deterministic `WorkspaceModel` at revision zero or the exact prior accepted revision plus one, using canonical export values for content digests. The generated projection is digest bound inside the existing accepted transaction. Semantic differences and actual source/test/build input changes select impacted obligations; unknown input or incomplete dependency coverage selects All. Empty selected behavioral obligations never mean success.
- Structural validation checks schema, types/effects, references, assumptions, and selected profile/bounds. Domain consistency is limited to explicitly declared properties, scenarios, and observed finite bounds. Test discovery/execution counts and observable actions must be positive. A failing scenario, invariant, unreachable witness, unavailable verifier, timeout, or unsupported mode remains a failure with its diagnostic and counterexample.
- The execution host resolves exact tool and implementation identities, bounds each process, captures output and digests, and invokes a consumer adapter that calls the built `WorkspaceLifecycle.reduce`. The adapter maps selected actions to production calls and projects actual state; it does not accept caller expected states. Replay compares those observations with model-produced traces. `correspond --observations` remains a pure diagnostic API.
- A sync result has its own schema and complete accepted/source/generated-contract, implementation/adapter/test/build, tool/dependency/configuration/seed/bounds, run identity, count, status and output-digest fields. Hashes identify bytes but do not authenticate a producer. Verify and ship run a fresh controlled sync or independently authenticated exact-input result; a dropped report is never sufficient. Recheck authority and source identity after execution. Interrupted accepted commits recover through the existing transaction journal and rerun.
- Existing JSON/plain/rich output projections convey the same typed result. No implicit default backend/provider change, spec weakening, or claim of general equivalence is permitted.

## Acceptance

The exact matrix and five milestones in the approved bounded plan for #1164 govern this feature. At minimum the ordinary command path demonstrates a positive model witness, symbolic verification, real reducer replay, a planted exact-base defect that fails, repair without weakening expected model behavior, and a new property already satisfied by unchanged implementation. Include forged/stale evidence, unknown impact, missing verifier, interrupted revision, retained workspace and package-only fresh workspace controls. Source merge alone leaves #1164 open until publication and both installations pass.

Prior foundations: #927, #934, and #122. Broader Rendering and fleet adoption remain with their own owners.
