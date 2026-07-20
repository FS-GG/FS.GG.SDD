---
title: Migration from Spec Kit
category: SDD
categoryindex: 6
index: 14
description: Additively adopt native FS.GG.SDD artifacts from an existing standard Spec Kit project without removing or rewriting specs or .specify content.
---

# Migration from Spec Kit

This guide maps an existing **standard Spec Kit** project onto native FS.GG.SDD
artifacts. The migration is **additive and non-destructive**: it adds SDD sources
through the `fsgg-sdd` commands and never deletes, rewrites, reorders, or
normalizes your existing `specs/` or `.specify/` content. Standard Spec Kit
remains a valid workflow after migration, and every step is safe to re-apply.

There is **no migration command**. Migration is `fsgg-sdd init` plus authoring
the native sources through the ordinary lifecycle commands.

> Looking to move a workspace **off** the retiring `spec-kit` lifecycle lane onto
> the `sdd` lane before it is removed (ADR-0056)? That is a different task — see
> [Migrate the spec-kit lane to sdd](migrate-spec-kit-lane-to-sdd.md). This guide
> keeps Spec Kit as a valid workflow; that one is about leaving the deprecated
> lane before a deadline.

## Starting point

An existing Spec Kit project with:

- `specs/<feature>/` — `spec.md`, `plan.md`, clarifications, `checklist.md`,
  `tasks.md`, and any evidence;
- `.specify/` — Spec Kit configuration and templates.

Leave both as they are. Nothing below touches them.

## Additive setup

Run `init` at the project root:

```text
fsgg-sdd init --root .
```

This creates `.fsgg/`, `work/`, and `readiness/` alongside your existing
`specs/` and `.specify/` directories. It does not read, move, or modify Spec Kit
content. If the SDD skeleton already exists, re-running `init` is safe.

## Artifact mapping

For each Spec Kit feature you want to track natively, author the equivalent SDD
source **through the `fsgg-sdd` commands** (see the [Quickstart](quickstart.md)
for the full command-by-command walkthrough). Use the feature's existing Spec Kit
content as the input you transcribe; do not delete the originals.

| Spec Kit artifact (`specs/<feature>/`) | Native SDD authored source (`work/<id>/`) | Authored through |
|---|---|---|
| `spec.md` | `work/<id>/spec.md` | `fsgg-sdd specify` |
| clarifications | `work/<id>/clarifications.md` | `fsgg-sdd clarify` |
| `checklist.md` | `work/<id>/checklist.md` | `fsgg-sdd checklist` |
| `plan.md` (+ contracts) | `work/<id>/plan.md` (+ `work/<id>/contracts/`) | `fsgg-sdd plan` |
| `tasks.md` | `work/<id>/tasks.yml` | `fsgg-sdd tasks` |
| evidence | `work/<id>/evidence.yml` | `fsgg-sdd evidence` |

Begin with `fsgg-sdd charter` to establish the work item identity, then proceed
in canonical lifecycle order. The generated readiness views under
`readiness/<id>/` (`work-model.json`, `analysis.json`, `verify.json`,
`ship.json`, `summary.md`, `agent-commands/`) are produced by the lifecycle and
the cross-cutting `fsgg-sdd refresh` / `fsgg-sdd agents` generators — their
currency comes from re-running those generators, not from file presence.

## No-equivalent handling

When a Spec Kit artifact has no direct native equivalent:

- **Represent it** in the nearest SDD authored source (for example, fold a free
  -form note into the relevant `spec.md` or `plan.md` section), or
- **Explicitly defer it** and record the deferral, leaving the original Spec Kit
  content in place.

Never delete authored Spec Kit content to force a mapping. If something does not
map cleanly, deferring is the correct, safe choice.

## Coexistence

- Standard Spec Kit remains a valid workflow after migration. The `specs/` and
  `.specify/` directories continue to work exactly as before.
- The migration steps are safe to re-apply. Re-running `init` and re-authoring a
  source through the `fsgg-sdd` commands does not damage existing content; the
  commands refuse unsafe overwrites and report what changed.
- Governance remains optional. Adopting native SDD artifacts does not require any
  Governance files; see [Adopting Governance](adopting-governance.md) if you
  later want that optional layer.
