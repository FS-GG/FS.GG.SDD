---
schemaVersion: 1
workId: 922-quint-first-q1
title: "Qualify Quint-first authoring across requirements, S.I.R., and coordination"
stage: charter
changeTier: tier1
status: chartered
policyPointers:
  - .fsgg/sdd.yml
  - .fsgg/agents.yml
  - .fsgg/policy.yml
  - .fsgg/capabilities.yml
  - .fsgg/tooling.yml
---

# Qualify Quint-first authoring across requirements, S.I.R., and coordination Charter

## Identity
- Work id: `922-quint-first-q1`
- Lifecycle stage: charter
- Status: chartered

## Principles
- Keep `fsharp-specification-v1` as the sole production authority; Q1 is a non-production qualification.
- Treat the literate Markdown bytes and ordered named `quint` blocks as the only authored candidate; extracted `.qnt` files are disposable generated views.
- Pin every external source, compiler, extractor, and guidance input by immutable identity and digest.
- Keep FS.GG.SDD generic: the producer owns toolchain, profile, ITF codec, replay protocol, and evidence envelope; consumers own business adapters and observable-state projections.
- Require independent readability and architecture/tooling review over all three slices, plus independently failing mutations.

## Scope Boundaries
- In scope: three literate vertical slices, deterministic extraction controls, pinned Quint and `quint-llm-kit` evaluation, proposed closed profile/compiled-contract/replay contracts, cross-domain measurements, and exact fingerprints.
- In scope through the separately owned child `EHotwagner/S.I.R.#353`: test-only replay against the real combat interpreter.
- Out of scope: production backend/API/package/default/provider changes; source-project references to S.I.R.; canonical S.I.R. rule migration; Q2 toolchain implementation; Q3 publication; GS2 protocol implementation.
- No downloaded source, generated module, model result, or author-scored readability assertion is acceptance by itself.

## Policy Pointers
- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.
- Governance files are optional compatibility pointers and are not evaluated by this command.

## Lifecycle Notes
- Governing decision: `FS-GG/.github` ADR-0077 and the Quint-first migration design, Q1.
- Cross-repository prerequisites: accepted Q0 (`FS-GG/.github#2953`) and the test-only correspondence response (`EHotwagner/S.I.R.#353`).
- Exit is an explicit accept-or-refuse verdict. Acceptance additionally requires a separately reviewed ADR-0077 amendment before Q2 begins.
- Next lifecycle action: `fsgg-sdd specify --work 922-quint-first-q1`.
