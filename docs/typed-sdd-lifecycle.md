---
title: Typed SDD Lifecycle
category: Design
categoryindex: 4
index: 7
description: Authority, provenance, migration, and failure contracts for the Typed SDD lifecycle backend.
---

# Typed SDD lifecycle

Typed SDD has one Quint-backed workspace lifecycle. Manifest v2 is the omitted backend and selects
the hermetic `quint-specification-v1` authority; manifest v1 keeps the F# authority available only by
explicit compatibility selection during the 2.x window. Both use the same charter-through-ship stages.
Provider lifecycle tokens remain distinct; their downstream omitted default changes only after the
separately evidenced `OperatingV2` cutover.

## Authority flow

```text
provider lifecycle parameter
          |
          v
scaffold provenance -----> representation backend
                                |
                     work/<id>/specification.fsx
                                |
                         package compiler
                         /              \
        normalized JSON                  Markdown projection
                         \              /
                    typed-authority.json (manifest v1)

explicit --backend quint-specification-v1 + --cache
          |
          v
exact lmt + Quint twice --> typed-effect-bound Q2 compiler
          |                         |
          +---- complete artifacts-+
                         |
              typed-authority.json (manifest v2)
```

`cref:T:FS.GG.SDD.Artifacts.TypedSpecifications.LifecycleLane` represents the four accepted tokens:
`none`, `sdd`, `typed-sdd`, and legacy `spec-kit`. They never alias. An omitted Typed SDD backend
resolves to Quint; an omitted provider lifecycle still resolves to `sdd` until the `OperatingV2`
rollout. `cref:T:FS.GG.SDD.Artifacts.TypedSpecifications.TypedAuthorityManifest` binds the selected
backend to the compiler/package/extension identity, canonical source digest, projection digests,
authoring receipt, and optional rollback source digest.

The F# script embeds a deterministic schema-v1 model and compiles it through
`cref:T:FS.GG.SDD.Artifacts.TypedSpecifications.SpecificationCompiler` and the published requirements
extension. Markdown is generated for review; it is never accepted as Typed SDD authority.

Manifest v2 is a separate additive type, not growth of the v1 F# record. Its closed inventory binds
canonical Markdown, the fence manifest, generated `.qnt`, exact typed-effect JSON, source map,
compiled contract, generated bindings, and compilation receipt. Inspection recomputes semantic
closure; matching manifest hashes alone are insufficient.

Two explicit Quint profiles share the same pinned binaries. `fsgg-quint-profile/1` remains the
digest-qualified requirements slice. `fsgg-quint-profile/2` admits consumer-defined bounded models
through structural Quint 0.32.0 validation and a retained `fsgg.quint.general-bindings/v1` selector
manifest. That manifest names exports and actions plus their literate ranges; it carries no semantic
values. All exported values are authored in Quint and cross the boundary only as bool, signed int64,
string, tuple, record, variant, list, set, or map values.

## Installed operations

```console
# Validate locally acquired exact tool objects and stage them in the content-addressed cache.
fsgg-sdd typed-sdd provision --cache /qualified/quint-cache \
  --quint /acquired/quint-linux-amd64 --lmt /built/lmt

fsgg-sdd typed-sdd author --work demo --title "Demo" --agent tern-001 --session session-1 \
  --cache /preseeded/quint-cache
fsgg-sdd typed-sdd inspect --work demo

# Pure three-way reconciliation over one accepted workspace model and two exact-base proposals.
fsgg-sdd typed-sdd reconcile --accepted workspace.json \
  --left proposal-a.json --right proposal-b.json --rich

# Fingerprint-bound correspondence; omit --changed for the complete report.
fsgg-sdd typed-sdd correspond --accepted workspace.json \
  --contract compiled-contract.json --observations observations.json \
  --changed PROD-001,DECIS-002 --json

# Explicit spelling is equivalent to the omitted Quint backend.
fsgg-sdd typed-sdd author --work demo --title "Demo" \
  --agent tern-001 --session session-1 \
  --backend quint-specification-v1 --cache /preseeded/quint-cache

# Consumer-defined profile: both paths are contained, project-relative inputs.
fsgg-sdd typed-sdd author --work combat --title "Combat model" \
  --agent tern-001 --session session-2 \
  --backend quint-specification-v1 --profile fsgg-quint-profile/2 \
  --source docs/rules/combat.md --bindings docs/rules/combat.bindings.json \
  --cache /preseeded/quint-cache
```

Profile 2 is deliberately bounded: typed/effect JSON is limited to 16 MiB; declarations, effect
rows, and bindings to 4,096; exports to 256; exported values to 100,000 aggregate nodes, depth 32,
and 64 KiB per string. Tests, seeded simulation, and optional model checking remain explicit evidence
rungs. A registered external algorithm is modeled as inert catalogue data plus an implementation
correspondence test—the generic host never executes a domain algorithm hidden inside a value export.

After editing an existing `specification.fsx`, repeat `author` with `--accept` and a fresh agent/session
receipt. The command compiles the edited authority before atomically replacing its projections and
manifest. The seeded `fs-gg-sdd-typed-author`, `fs-gg-sdd-typed-inspect`,
`fs-gg-sdd-typed-migrate`, `fs-gg-sdd-typed-reconcile`, and
`fs-gg-sdd-typed-correspond` skills are embedded in the Commands assembly and materialized into every
configured agent-skill root by init, scaffold, refresh, and upgrade.

## Reconciliation and correspondence

`WorkspaceLifecycle` encodes and decodes strict schema-v1 workspace models and change proposals.
Reconciliation is an order-independent pure three-way merge against the exact accepted fingerprint.
Compatible disjoint proposals yield one candidate and sorted semantic changes. Stale bases,
overlapping or duplicate declarations, conflicting assumptions, rename/delete pairs, dangling
identities, and non-reducible dispositions return every deterministic diagnostic and structurally
cannot carry a candidate. Producing a candidate is not human acceptance and does not mutate authority.

`WorkspaceCorrespondence` derives one report from the accepted model, a validated profile-2 compiled
contract, and canonical fingerprint-bound observations. Each accepted evidence obligation remains
distinctly `satisfied`, `missing`, `stale`, `contradicted`, `ambiguous`, `unsupported`, or
`unobserved`; these states are not collapsed into a percentage. Selective checking follows compiled
relationships and impact subjects from `--changed`, while global catalogue, schema, duplicate,
binding, and fingerprint checks always run first. A forged or incomplete global input blocks the
report. JSON, plain, and rich output are projections of the same typed report; none is editable
coverage authority.

The v2 cache layout is `objects/<sha256>`. Follow the exact acquisition and build recipe in
[Typed SDD tool provisioning](reference/typed-sdd-provisioning.md), then let the installed `provision`
operation verify both inputs and stage the complete object set. The accepted Linux/amd64 objects are Quint 0.32.0
`939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f` and the qualified `lmt`
binary `37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10`.
The host retains verified bytes, copies them into each isolated run, clears ambient process state,
poisons network proxies, and executes the same requests it records for the pure Q2 compiler.
Quint LLM Kit guidance is pinned separately at `cc75369f741af7d490936f82002c2d28e3b3d78d`;
guidance helps authors and agents but is never compiler authority.

## Standard SDD migration

Migration is intentionally two-step. First analyze without writing:

```console
fsgg-sdd typed-sdd migrate --work demo --source work/demo/spec.md
```

The classification is `Migrated` for losslessly representable content, `Ambiguous` when an authored
reference requires a decision, or `Unsupported` for constructs outside the published extension. The
report includes locations or a semantic diff and the rollback source digest. Only after reviewing a
`Migrated` report should the command be repeated with `--accept`. For Quint v2, add the cache, agent,
and session arguments used by authoring; the backend may be omitted. The qualified migration accepts only
the closed requirements/evidence extension. Every v1 identity is lowered into the bounded compiled
catalogue, references become relationships and acceptance action effects, and semantic text is retained
as non-executable compatibility metadata. The raw Q1 Quint module remains the fixed executable slice;
other semantics are `Unsupported`, never approximated.
Acceptance snapshots every original v1 path and byte under
`.fsgg/typed-sdd-rollback/v1/<id>/`, authenticates the inventory from manifest v2, then commits.
Direct v1 replacement through `author --accept` is refused. Restore explicitly with:

```console
fsgg-sdd typed-sdd rollback --work demo --accept
```

## Failure identities

Automation should branch on diagnostic IDs, not message text. Wrong lifecycle, unavailable compiler,
package identity mismatch, unsupported extension, direct canonical edit, stale projection, and an
unavailable authoring agent have separate IDs and corrections. Doctor, readiness, and ship consumers
must retain these identities rather than converting them to a generic lifecycle failure.

V2 additionally distinguishes absent, unreadable, aliased, noncanonical, stale source/fence/map,
typed-effect, binding, receipt, tool/cache, transaction, and rollback failures. Unknown change classes
select the full verification corpus.

Author, migration, and rollback decisions and commits use the same exclusive authority lock and
canonical recovery journal. Migration additionally compares the accepted v1 source and normalized
payload with its preflight observation before it may commit.
The journal records every target, whether it existed, and its prior bytes before the first live move.
Inspection takes that lock and recovers an interrupted prepared transaction before reading the
manifest. A committed journal retains the new tree; a prepared journal restores the old tree.
Package acceptance hard-kills every author move, kills rollback after replacement begins, and holds a
prepared commit open during a concurrent inspect.

Work ids are single path segments, and migration sources must be project-relative paths contained by
the selected root. Unknown or incomplete options fail closed.

## Refresh and upgrade

Shared lifecycle commands resolve the authority from manifest version/backend after scaffold
provenance selects `typed-sdd`. V1 executes the F# compiler. V2 reads and validates its declared
artifact inventory and never falls back to F#. A package, profile, or toolchain identity change is an
explicit upgrade boundary; it is never silently accepted. Standard SDD and `none` are unchanged.

## Design consequences

One ordered lifecycle and skill corpus avoids two processes drifting apart. A backend-specific authority
manifest still allows strong freshness and identity checks. F# v1 requires a compatible compiler;
Quint v2 requires the exact preseeded cache. Neither backend performs network acquisition.
The Linux-qualified effect edge clears ambient state, supplies only the environment declared in the
Q2 process request, and executes the exact retained tools inside a fresh user/network namespace. Its
launcher, platform, arguments, and namespace primitive are recorded as the canonical
`fsgg.quint.os-sandbox/v1` artifact; inspection requires those exact bytes and binds their digest into
the compiled contract.
