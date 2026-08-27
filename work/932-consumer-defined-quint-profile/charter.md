---
schemaVersion: 1
workId: 932-consumer-defined-quint-profile
title: General consumer-defined Quint model profile
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

# General consumer-defined Quint model profile Charter

## Identity
- Work id: `932-consumer-defined-quint-profile`
- Lifecycle stage: charter
- Status: chartered

## Principles
- Preserve `fsgg-quint-profile/1`, compiled-contract v1, and every existing Q1 byte and diagnostic as a frozen compatibility surface.
- Keep consumer semantics in the literate Quint authority; source maps may bind locations, but no semantic sidecar may substitute for model content.
- Accept only a closed, versioned, resource-bounded projection of Quint 0.32.0 typed/effect output and fail closed on unknown structure.
- Declare additive public F# signatures before implementation, prove native/Fable parity, and publish one coherent Artifacts/CLI set.

## Scope Boundaries
- In: a general consumer-defined profile, generic compiled model data, deterministic generated bindings, hermetic host dispatch, package-only acceptance, compatibility and release evidence.
- In: a real complete S.I.R. combat model as the cross-domain acceptance consumer, including catalogue facts, formulas, a registered external algorithm contract, state transitions, invariants, and executable witnesses.
- Out: changing frozen profile 1, accepting arbitrary raw compiler IR as a public contract, downloading tools at runtime, generating domain behavior, or making Governance mandatory.

## Policy Pointers
- Constitution principles I/III require `.fsi`-first public design; V keeps host I/O at the effect edge; VI requires semantic and mutation evidence.
- `.fsgg/sdd.yml` and `.fsgg/agents.yml` govern lifecycle ownership and equivalent Claude/Codex behavior.
- FS.GG.SDD#924 is the frozen v2 lifecycle foundation; EHotwagner/S.I.R.#352 is the blocking consumer and FS.GG.SDD#927 is the parent Quint roadmap.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd specify --work 932-consumer-defined-quint-profile`.
