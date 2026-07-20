---
title: Migrate the spec-kit lane to sdd
category: SDD
categoryindex: 6
index: 16
description: Move a scaffolded workspace off the legacy spec-kit lifecycle lane onto the sdd lane before the spec-kit lane is removed. Additive, non-destructive, and safe to re-apply.
---

# Migrate the spec-kit lane to the sdd lane

Under [ADR-0056](https://github.com/FS-GG/.github/blob/main/docs/adr/0056-sdd-is-the-default-lifecycle-spec-kit-is-legacy-and-scheduled-for-removal.md),
`sdd` is the one default lifecycle lane and the `spec-kit` lane is **legacy and
scheduled for removal**. If your workspace was scaffolded on the `spec-kit` lane
(its lifecycle parameter was `spec-kit`), it keeps working today, but the lane —
and the Spec Kit hook fabric that rides on it — will be removed on a published
milestone. This guide moves such a workspace onto the `sdd` lane **before** that
milestone, so nothing you depend on disappears with the lane.

> This is a **different** task from
> [Migration from Spec Kit](migration-from-spec-kit.md). That guide is about
> *additively adopting* native SDD artifacts alongside a standard Spec Kit
> project you intend to keep — Spec Kit stays a valid workflow. This guide is
> about *leaving the retiring `spec-kit` lane* for the `sdd` lane before a
> deadline. The two have opposite premises; read the one that matches your
> situation.

## Who this is for

You scaffolded with the lifecycle lane set to `spec-kit`, so your workspace
carries the Spec Kit lane fabric (a `.specify/` tree, its `after_*` hooks, and the
lane-gated feedback/sample machinery). ADR-0056 puts that fabric on a removal
schedule. Moving to the `sdd` lane means making sure your workspace has the
SDD lifecycle skeleton the `sdd` lane provides — the `fs-gg-sdd-*` process skills,
the `.fsgg/constitution.md`, and the `.fsgg/early-stage-guidance.md` — so your
lifecycle keeps working once the `spec-kit` fabric is gone.

If you scaffolded on the `sdd` lane already, or with lifecycle `none`, you do not
need this guide.

## The guarantee, up front

Both paths below are **additive, non-destructive, and safe to re-apply**:

- Nothing under `specs/`, `.specify/`, or any authored source is deleted,
  rewritten, reordered, or normalized.
- Only **missing** SDD skeleton artifacts are materialized; an existing,
  possibly author-edited artifact is never overwritten.
- Every step is safe to run again — re-running reports what changed (usually
  nothing) rather than clobbering.

You are adding the `sdd`-lane skeleton to a workspace that lacks part of it, not
converting or discarding what you have.

## Path A — re-supply the SDD skeleton in place (recommended)

Use this when you want to keep your current workspace and simply ensure it has
the `sdd`-lane lifecycle skeleton. The reconciliation verb is `fsgg-sdd upgrade`,
whose skeleton re-seed step **no-clobber re-materializes only the missing**
seeded artifacts via `init`'s seeding — see
[the doctor/upgrade reference](reference/doctor-upgrade.md) for the full step
model and safety invariants.

First, see what is missing without changing anything — `doctor` is strictly
read-only:

```text
fsgg-sdd doctor
```

`doctor` reports which seeded `fs-gg-sdd-*` skills and
`.fsgg/early-stage-guidance.md` are present versus expected, and previews exactly
what `upgrade` would re-seed. When the preview looks right, apply it:

```text
fsgg-sdd upgrade
```

`upgrade` re-seeds **only the missing** skeleton artifacts, each behind its own
confirmation (or all at once under `--yes`). It never touches your `.specify/`
tree, your `specs/`, or any authored content. Re-run `doctor` afterward to
confirm no skeleton drift remains.

Your existing Spec Kit content stays exactly where it is. If you also want to
represent that content as native SDD sources, follow
[Migration from Spec Kit](migration-from-spec-kit.md) — that step is additive and
independent of this lane move.

## Path B — re-scaffold on the sdd lane

Use this when you would rather start from a clean `sdd`-lane workspace and bring
your authored content across — for example, if your `spec-kit`-lane tree has
drifted far from a current skeleton.

Scaffold a fresh workspace with the lifecycle lane set to `sdd` (the default), in
a new directory, using your provider and its parameters as before:

```text
fsgg-sdd scaffold --provider <name>
```

The lifecycle lane is a provider-declared parameter; with no override it resolves
to the provider's declared default, which is `sdd`. To be explicit you may pass
`--param lifecycle=sdd`. Then copy your authored sources (`specs/`, `.specify/`,
and any authored SDD sources under `work/`) into the new workspace. Nothing from
the old workspace is deleted by scaffolding a new one; you retire the old tree
only once you have verified the new one.

## After the move

- Re-run `fsgg-sdd doctor` — it should report no skeleton drift.
- Your lifecycle now rides the `sdd` lane and no longer depends on the retiring
  `spec-kit` fabric, so it is unaffected when that lane is removed.
- Governance remains optional either way; see
  [Adopting Governance](adopting-governance.md) if you want that layer.

## Coexistence and re-applying

- The move is safe to re-apply. Re-running `doctor`/`upgrade`, or re-authoring a
  source through the `fsgg-sdd` commands, does not damage existing content; the
  commands refuse unsafe overwrites and report what changed.
- Until the removal milestone, a `spec-kit`-lane workspace keeps working. Moving
  early simply means you are not racing the deadline.
