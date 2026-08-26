---
schemaVersion: 1
workId: 923-quint-profile-compiler
title: Build the hermetic Quint profile and compiled-contract boundary
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

# Build the hermetic Quint profile and compiled-contract boundary Charter

## Identity
- Work id: `923-quint-profile-compiler`
- Lifecycle stage: charter
- Status: chartered

## Principles
- Implement only the exact Q1-accepted `fsgg-quint-profile/1`; every version or feature expansion is a new candidate.
- Keep literate Markdown authoritative, generated `.qnt` modules disposable, raw Quint IR private to the adapter, and the small compiled contract language-neutral.
- Make every tool and dependency observation content-addressed. An absent/unreadable tool, incomplete cache, or network fallback is not a passing verification result.
- Preserve the producer/consumer split: FS.GG.SDD owns compilation, stable codecs, generic replay, and evidence identity; product repositories own business adapters and model-observable projections.
- Follow `.fsi`-first development with deterministic golden fixtures, independent mutations, package-only acceptance, and Fable parity over shared contracts.

## Scope Boundaries
- In scope: exact toolchain/cache manifests, literate extraction receipts, deterministic source maps, closed-profile validation over the pinned Quint surface, canonical compiled-contract and ITF/replay codecs, semantic fingerprints/diff, generated F#/Fable bindings, diagnostics, and Q1 golden fixtures.
- In scope: package the reviewed `lmt` source identity and require an offline content-addressed Go/tool cache; do not execute a moving installer or download during compilation.
- Out of scope: the `quint-specification-v1` lifecycle backend and manifest-v2 command integration (Q3), package publication, registry/provider/default changes, product-specific adapters, and canonical consumer migration.
- The Q1 experiment remains immutable evidence. Q2 promotes its accepted boundary into production library contracts without rewriting Q1 receipts.

## Policy Pointers
- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.
- Governance files are optional compatibility pointers and are not evaluated by this command.

## Lifecycle Notes
- Governing authority: ADR-0077 plus `FS-GG/.github`'s accepted 2026-08-26 Q1 qualification amendment.
- Exact prerequisites: FS.GG.SDD merge `60351fd0614a5c8e4bdf286c21f185196116fd69` and S.I.R. merge `77e56d11867a5e2e7ad99f4d61b0f0c9fff61a5f`.
- Next lifecycle action: `fsgg-sdd specify --work 923-quint-profile-compiler`.
