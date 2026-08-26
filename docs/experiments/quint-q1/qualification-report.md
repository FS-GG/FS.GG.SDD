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

The three canonical Markdown sources total 12,535 bytes and generate 9,206 bytes of Quint. Generated files
are extracted twice in isolated directories and compared byte-for-byte; they are not committed authority.

## Verification and negative controls

The repository harness performs exact tool/source digest checks, fence inventory validation, JSON schema
and contract checks, double extraction, three typechecks, three named test suites, one deterministic ITF
witness, three seeded Rust simulations, three bounded Apalache safety checks, and one TLC temporal check.
On the recorded host a complete final run took 21.1 seconds. This is a single observation, not a service
level promise.

Ten independent mutations must fail: missing, reordered/unexpected, and duplicate fences; stale source;
edited generated output; missing evidence guard; non-saturating combat damage; double apply on retry;
unbounded lost-response liveness; and arbitrary-expression lowering into the compiled contract.

The temporal check found a real design defect during qualification: an unbounded `LoseResponse -> Retry`
cycle could run forever. Bounding loss alone was insufficient because aggregate fairness still admitted a
stale-refusal cycle. The repaired model separates apply from stale refusal, bounds revision, records loss
as a finite event, and applies fairness over the complete state tuple. TLC then found no temporal violation;
the original form remains a required failing mutation.

The deterministic witness check also exposed host-path leakage in ITF metadata. The harness now strips
timestamp/description fields and canonicalizes the source path to its basename before byte comparison.

## Supply chain and dependency cost

All executables are digest-pinned in `candidate-manifest.json`. The main local footprint is Quint
(125.7 MB), a Temurin JRE (164.8 MB installed), and Apalache (136.0 MB installed); `lmt` and the Rust
evaluator are about 2.8 MB and 2.6 MB. The pinned guidance checkout is 37.7 MB. `lmt` lacks a tagged
release and Go module declaration, so Q2 must package the exact reviewed source and dependency closure or
refuse it. A moving installer is not an admissible substitute.

The Apache-2.0 `quint-llm-kit` corpus is guidance only. Q1 adopts typecheck-first, executable
decomposition, seeded simulation, witnesses, and divergent-prefix explanations; adapts action labels,
coverage, and implementation correspondence to explicit FS-GG catalogue IDs; rejects moving installers,
runtime-specific orchestration, and Choreo for this shared-state slice. Detailed dispositions and exact
attribution are in `quint-llm-kit-evaluation.md`.

## Scope boundary

This experiment changes no production backend, package version, public API, lifecycle default, provider
floor, or historical P0-P4 evidence. Q2 owns implementation of an accepted profile; Q3 owns publication.
