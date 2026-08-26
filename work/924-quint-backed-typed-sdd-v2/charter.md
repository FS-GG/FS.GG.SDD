---
schemaVersion: 1
workId: 924-quint-backed-typed-sdd-v2
title: Publish the Quint-backed Typed SDD v2 migration
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

# Publish the Quint-backed Typed SDD v2 migration Charter

## Identity
- Work id: `924-quint-backed-typed-sdd-v2`
- Lifecycle stage: charter
- Status: chartered

## Principles
- Preserve manifest-v1 and `fsharp-specification-v1` inspection byte-for-byte while adding an explicit manifest-v2/`quint-specification-v1` authority; never infer a backend from files on disk.
- Treat Q2's compiler as a pure package boundary. Q3 owns the hermetic effect edge that resolves exact cached tools, executes extraction/typecheck, constructs observations, and invokes that boundary without network or moving installers.
- Migration and rollback are transactions over exact bytes: preflight writes nothing, accepted migration is atomic, and rollback restores the complete v1 authority or fails without a partial state.
- Publish one coherent stable package set with exact source and artifact anchors, prove both feeds from clean installed consumers, and compare feed payloads excluding only nuget.org's feed-added signature.
- Keep Q6 authority out of Q3: no provider floor, registry row, consumer pin, workspace lifecycle default, or coordinated rollout flips in this feature.

## Scope Boundaries
- In scope: versioned Typed SDD authority manifests, explicit backend dispatch, the Q2 compiler host, deterministic Quint authoring, v1 inspection, v1-to-v2 preflight/migration/rollback, shared lifecycle command dispatch, fail-safe selective-CI routing, installed-package acceptance, skill/docs updates, compatibility baselines, and stable dual-feed publication.
- In scope: exact Q1/Q2 Quint, LLM-kit, Go/`lmt`, Fable, profile, contract, source-map, cache and receipt identities; absence or unreadability remains distinct from mismatch and never falls back to a network path.
- Out of scope: product-specific adapters or semantics, arbitrary Quint surface growth, provider/registry/consumer/default changes, historical Q1/Q2 receipt rewrites, and downstream GS2 adoption.
- The accepted Q1 captures and Q2 package contract are immutable prerequisites; Q3 composes them rather than redefining either.

## Policy Pointers
- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.
- Governance files are optional compatibility pointers and are not evaluated by this command.

## Lifecycle Notes
- Governing authority: ADR-0077, the live GitHub Substrate v2 roadmap Q3 tranche, FS.GG.SDD#924, and Q2 merge `b803089dd576495df2bd1a4d00d9b8920c99d613`.
- Exact release boundary: stable Artifacts and CLI packages are obligations of this work; tag, feed receipts and public install verification must bind the reviewed merge commit.
- Next lifecycle action: `fsgg-sdd specify --work 924-quint-backed-typed-sdd-v2`.
