# Q1 qualification report

## Verdict

Accept the exact pinned bundle as a **producer candidate**. Do not grant production authority yet.
Cross-repository acceptance requires the exact S.I.R. interpreter replay in EHotwagner/S.I.R.#353,
two independent review acceptances, and the post-Q1 amendment to ADR-0077. Moving versions, different
digests, additional Quint language features, or a changed compiled-contract shape are new candidates.

## Demonstrated slices

The requirements slice binds `REQ-AUDIT-001` acceptance to observed `EV-VERIFY-001` evidence. The S.I.R.
slice defines saturating damage, named action observations, a reviewed deterministic witness, and a
consumer replay envelope. The coordination slice covers stale observations, a bounded lost response,
retry, refusal, ordering, at-most-once apply, receipts, safety, deadlock freedom within the finite model,
and eventual completion under explicit weak fairness.

The three canonical Markdown sources total 14,250 bytes and generate 10,819 bytes of Quint. Generated files
are extracted twice in isolated directories and compared byte-for-byte; they are not committed authority.

## Verification and negative controls

The repository harness performs exact tool/source/guidance-tree checks, source-located fence validation,
Draft 2020-12 plus semantic contract validation, triple extraction, six typechecks, eight explicit named
examples across the pinned-kit and minimal workflows, one deterministic ITF witness, four seeded Rust
simulations, three bounded Apalache safety checks, and one TLC temporal check. A fresh `QUINT_HOME` links
only the verified Rust evaluator and complete Apalache distribution, and a dedicated endpoint must be
unused before checking. On the recorded host a complete repaired run took 25.9 seconds. This is a single
observation, not a service-level promise.

Nineteen independent mutations must fail: missing, reordered/unexpected, and duplicate fences; stale
source; edited generated output; missing evidence guard; non-saturating combat damage; double apply on
retry; lost-update revision bypass; stale-refusal apply; completion-before-apply without receipt;
unbounded lost-response liveness; arbitrary-expression lowering; invalid contract ID; escaping source
path; reversed line range; malformed digest; duplicate action; and a moving/latest guidance substitution.
Every refusal must match its intended binding and report a source or JSON instance location.

The temporal check found a real design defect during qualification: an unbounded `LoseResponse -> Retry`
cycle could run forever. Bounding loss alone was insufficient because aggregate fairness still admitted a
stale-refusal cycle. The repaired model separates apply from stale refusal, bounds revision, records loss
as a finite event, and applies fairness over the complete state tuple. TLC then found no temporal violation;
the original form remains a required failing mutation.

The deterministic witness check also exposed host-path leakage in ITF metadata. The harness now strips
timestamp/description fields and canonicalizes the source path to its basename before byte comparison.
An independent review then exposed that a bare `quint test` did not execute the `run` examples; all named
examples now use explicit `--match` selection, and their machine receipt records each case.

## Supply chain and dependency cost

All executables are digest-pinned in `candidate-manifest.json`. The main local footprint is Quint
(125.7 MB), a Temurin JRE (164.8 MB installed), and Apalache (136.0 MB installed); `lmt` and the Rust
evaluator are about 2.8 MB and 2.6 MB. The pinned guidance checkout is 37.7 MB. `lmt` lacks a tagged
release and Go module declaration, so Q2 must package the exact reviewed source and dependency closure or
refuse it. A moving installer is not an admissible substitute.

Draft 2020-12 validation uses Ajv 8.17.1 in a 1.29 MB content-receipted closure under the pinned 62.8 MB
Node.js 26.7.0 binary. Schema validation is followed by repository semantic checks for canonical paths,
line bounds, sorted uniqueness, valid references, and exact source/generated digests.

The Apache-2.0 `quint-llm-kit` corpus is guidance only. Q1 adopts typecheck-first, executable
decomposition, seeded simulation, witnesses, and divergent-prefix explanations; adapts action labels,
coverage, and implementation correspondence to explicit FS-GG catalogue IDs; rejects moving installers,
runtime-specific orchestration, and Choreo for this shared-state slice. Detailed dispositions and exact
attribution are in `quint-llm-kit-evaluation.md`; executable comparison, semantic-diff usefulness,
diagnostics, and first-review readability findings are in `workflow-comparison.md`.

## Scope boundary

This experiment changes no production backend, package version, public API, lifecycle default, provider
floor, or historical P0-P4 evidence. Q2 owns implementation of an accepted profile; Q3 owns publication.
