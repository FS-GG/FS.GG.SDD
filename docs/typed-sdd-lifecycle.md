---
title: Typed SDD Lifecycle
category: Design
categoryindex: 4
index: 7
description: Authority, provenance, migration, and failure contracts for the Typed SDD lifecycle backend.
---

# Typed SDD lifecycle

Typed SDD is an additive representation backend for the existing SDD lifecycle. It keeps the same
charter-through-ship stage sequence and skills, but makes an F# script the canonical specification
authority. Standard SDD remains the omitted default until the separately governed P5 flip.

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
                    typed-authority.json
```

`cref:T:FS.GG.SDD.Artifacts.TypedSpecifications.LifecycleLane` represents the four accepted tokens:
`none`, `sdd`, `typed-sdd`, and legacy `spec-kit`. They never alias. An omitted value resolves to
`sdd`. `cref:T:FS.GG.SDD.Artifacts.TypedSpecifications.TypedAuthorityManifest` binds the selected
backend to the compiler/package/extension identity, canonical source digest, projection digests,
authoring receipt, and optional rollback source digest.

The F# script embeds a deterministic schema-v1 model and compiles it through
`cref:T:FS.GG.SDD.Artifacts.TypedSpecifications.SpecificationCompiler` and the published requirements
extension. Markdown is generated for review; it is never accepted as Typed SDD authority.

## Installed operations

```console
fsgg-sdd typed-sdd author --work demo --title "Demo" --agent tern-001 --session session-1
fsgg-sdd typed-sdd inspect --work demo
```

After editing an existing `specification.fsx`, repeat `author` with `--accept` and a fresh agent/session
receipt. The command compiles the edited authority before atomically replacing its projections and
manifest. The seeded `fs-gg-sdd-typed-author`, `fs-gg-sdd-typed-inspect`, and
`fs-gg-sdd-typed-migrate` skills are embedded in the Commands assembly and materialized into every
configured agent-skill root by init, scaffold, refresh, and upgrade.

## Standard SDD migration

Migration is intentionally two-step. First analyze without writing:

```console
fsgg-sdd typed-sdd migrate --work demo --source work/demo/spec.md
```

The classification is `Migrated` for losslessly representable content, `Ambiguous` when an authored
reference requires a decision, or `Unsupported` for constructs outside the published extension. The
report includes locations or a semantic diff and the rollback source digest. Only after reviewing a
`Migrated` report should the command be repeated with `--accept`. Acceptance preserves the original
bytes at `work/<id>/spec.standard-sdd.rollback.md` in the same recoverable write transaction. Restore
them explicitly with:

```console
fsgg-sdd typed-sdd rollback --work demo --accept
```

## Failure identities

Automation should branch on diagnostic IDs, not message text. Wrong lifecycle, unavailable compiler,
package identity mismatch, unsupported extension, direct canonical edit, stale projection, and an
unavailable authoring agent have separate IDs and corrections. Doctor, readiness, and ship consumers
must retain these identities rather than converting them to a generic lifecycle failure.

Work ids are single path segments, and migration sources must be project-relative paths contained by
the selected root. Unknown or incomplete options fail closed.

## Refresh and upgrade

Refresh and upgrade resolve the backend from scaffold provenance. For `typed-sdd`, they preserve the
canonical script and supported extension nodes, then regenerate only projections and receipts. A
package/compiler identity change is an explicit upgrade boundary; it is never silently accepted during
refresh. Standard SDD and `none` retain their existing behavior.

## Design consequences

One ordered lifecycle and skill corpus avoids two processes drifting apart. A backend-specific authority
manifest still allows strong freshness and identity checks. The tradeoff is that clean consumers need a
compatible .NET/F# compiler and must retain the exact package pin recorded by provenance.
