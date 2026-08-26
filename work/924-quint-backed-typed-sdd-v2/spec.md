---
schemaVersion: 1
workId: 924-quint-backed-typed-sdd-v2
title: Publish the Quint-backed Typed SDD v2 migration
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Publish the Quint-backed Typed SDD v2 migration Specification

Prose status: specified

## User Value
Package consumers can explicitly author, inspect, migrate, and roll back a deterministic Quint-backed Typed SDD v2 authority while existing v1 inspection remains unchanged.

## Scope
- SB-001: Add an explicit manifest-v2 authority and `quint-specification-v1` backend beside the existing manifest-v1/`fsharp-specification-v1` contract.
- SB-002: Host the Q2 pure compiler through a hermetic effect edge that resolves exact cache objects, executes the pinned extractor and Quint toolchain, records complete observations, and never downloads or chooses a moving version.
- SB-003: Add deterministic new-author, inspect, v1-to-v2 preflight/migrate, rollback, lifecycle-command dispatch, selective-CI routing, package-only verification, guidance, compatibility baselines, and stable coherent-set publication.

## Non-Goals
- SB-004: Do not change v1 authority bytes or diagnostics, infer a backend from file presence, remove the `sdd` omitted-lifecycle default, or silently fall back from Quint to F#.
- SB-005: Do not flip provider floors, package registry rows, consumer pins, workspaces, or organization lifecycle defaults; Q6 owns coordinated adoption.
- SB-006: Do not add product semantics, arbitrary Quint features, an online installer, a second compiler model, or rewrite Q1/Q2 historical evidence.

## User Stories
- US-001 (P1): As an author, I can create an explicit Quint-backed Typed SDD authority whose generated source, compiled contract, source map, bindings, and receipt are deterministic and inspectable.
- US-002 (P1): As an existing consumer, I can inspect an unchanged v1 authority and migrate it transactionally to v2 with a no-write preflight and byte-exact rollback.
- US-003 (P1): As a package consumer, I can run the lifecycle from installed stable packages with a preseeded cache and no repository sibling, source load, or network path.
- US-004 (P2): As a CI maintainer, I can select the minimum safe Quint verification class from compiled-contract impacts while unknown or compiler-sensitive inputs broaden to the full corpus.
- US-005 (P1): As a release reviewer, I can prove one reviewed source commit produced coherent stable Artifacts/CLI packages whose non-signature payloads are identical on both feeds.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001] [FR-002] [FR-003]: Given the accepted Q1 slices and complete exact cache, when explicit Quint authoring runs twice in isolated directories, then every authority artifact and report byte agrees and the manifest names v2 plus `quint-specification-v1`.
- AC-002 [US-002] [FR-001] [FR-004] [FR-005]: Given a golden v1 authority, when inspect, preflight, migration and rollback run, then v1 inspect is unchanged, preflight writes nothing, migration is atomic and deterministic, and rollback restores every original byte while removing v2 outputs.
- AC-003 [US-003] [FR-002] [FR-006] [FR-009]: Given only installed nupkgs/CLI and a preseeded content-addressed cache, when author/inspect/migrate/rollback run with network poisoned, then all positive routes pass without project references and every missing, unreadable, incomplete, wrong or moving identity fails distinctly.
- AC-004 [US-004] [FR-007]: Given representative prose, catalogue, action, temporal/bounds, adapter/runtime, compiler/profile/toolchain and unknown changes, when selection runs, then it chooses the documented increasing check class and unknown or selector-sensitive input chooses the full corpus; inverted mappings fail.
- AC-005 [US-001] [US-002] [FR-003] [FR-004] [FR-008] [FR-009]: Given hand-edited outputs, an inferred backend, a partial-write fault, changed rollback bytes, stale receipts, wrong pins, replay removal, or an unexpected tool process, when the lifecycle runs, then each route fails closed with its stable source-located diagnostic and leaves no partial authority.
- AC-006 [US-005] [FR-010] [FR-011] [FR-012]: Given the independently accepted merge commit, when the coherent stable release runs, then repository/source metadata bind that commit, both feeds return equivalent non-signature payloads, clean public installs pass, and publication produces durable tag/package/feed receipts before completion.

## Functional Requirements
- FR-001: Typed authority decoding MUST discriminate manifest v1 and v2 explicitly, preserve exact v1 validation/inspection, accept only the declared backend for each version, and reject unknown fields, versions, backends, or file-presence inference. (Stories: US-001, US-002; Acceptance: AC-001, AC-002)
- FR-002: The Quint host MUST resolve the exact Q1/Q2 toolchain and profile identities from a local content-addressed cache, execute no network or moving installer, build or validate the qualified `lmt`, run pinned Quint extraction/typecheck twice, and construct every Q2 observation before calling the pure compiler API. (Stories: US-001, US-003; Acceptance: AC-001, AC-003)
- FR-003: Explicit Quint authoring MUST write canonical literate Markdown, ordered fence manifest, generated `.qnt`, source map, compiled contract, bindings, compilation receipt, tool/profile/package fingerprints and author/session bindings through one atomic transaction. (Stories: US-001; Acceptance: AC-001, AC-005)
- FR-004: Migration MUST decode the embedded v1 authority, render canonical literate Quint, classify preflight as Migrated/Ambiguous/Unsupported with zero writes, and only commit a complete v2 authority after the same hermetic compiler checks pass. (Stories: US-002; Acceptance: AC-002, AC-005)
- FR-005: Rollback MUST retain and authenticate every original v1 byte and path, restore the complete v1 authority, remove every v2 output, and recover the pre-operation tree on any failure. (Stories: US-002; Acceptance: AC-002, AC-005)
- FR-006: Shared author/inspect/migrate/rollback, stage, refresh, doctor, upgrade, readiness and related command paths MUST dispatch by explicit manifest/backend without an F# fallback; omitted lifecycle selection MUST remain `sdd`. (Stories: US-001, US-002, US-003; Acceptance: AC-001, AC-003)
- FR-007: A closed deterministic selector MUST map compiled-contract impacts and changed authority surfaces to prose-only, structural/typecheck, test/simulation, model-check, or full-corpus verification; unknown/schema/profile/selector/compiler/toolchain input MUST choose full-corpus. (Stories: US-004; Acceptance: AC-004)
- FR-008: Diagnostics MUST distinguish absence, unreadability, incompleteness, digest mismatch, unsupported input, ambiguity, tool failure, stale generation, partial-write recovery and rollback mismatch with deterministic ordering and actionable source/path bindings. (Stories: US-001, US-002; Acceptance: AC-005)
- FR-009: Package-only acceptance MUST use fresh NuGet and HTTP caches, a complete local feed, poisoned proxies, installed CLI/Artifacts packages, exact Q1 fixtures, replay and Fable/Node parity, and no `ProjectReference`, source `#load`, nuget.org restore, or sibling checkout. (Stories: US-003; Acceptance: AC-003, AC-005)
- FR-010: All new public APIs MUST be `.fsi`-declared with native semantic tests, current API-surface baselines, additive compatibility classification and coherent CLI/Artifacts stable versions. (Stories: US-005; Acceptance: AC-006)
- FR-011: Release MUST build once from the accepted merge, assert tag/head/nuspec repository commit and source revision, publish in declared feed order, download both feeds with retry, compare every non-signature ZIP payload byte, and run clean public installs. (Stories: US-005; Acceptance: AC-006)
- FR-012: The feature MUST update author/inspect/migrate guidance and lifecycle/reference/release documentation with the exact pinned Q1/Q2 subset and attribution while making no provider, registry, consumer-pin or workspace-default mutation. (Stories: US-001, US-005; Acceptance: AC-006)

## Ambiguities
- AMB-001: Which additive API shape preserves binary compatibility for the existing public F# manifest-v1 record while exposing v2 authority?
- AMB-002: How does the CLI locate or build the exact qualified tools without packaging an executable or permitting acquisition during the command?
- AMB-003: What complete rollback material is retained, and which manifest authenticates it without allowing migrated files to masquerade as v1?
- AMB-004: Which change classes select each verification rung, and what is the fail-safe answer when inputs do not classify?
- AMB-005: How are nuget.org feed-added signatures excluded without weakening byte-identity claims over package payloads?
- AMB-006: Does stable publication happen in the feature PR or only after its reviewed merge, and how are those obligations completed before the item is Done?

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 924-quint-backed-typed-sdd-v2`.
