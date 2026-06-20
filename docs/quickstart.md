---
title: Quickstart
category: SDD
categoryindex: 6
index: 13
description: Take a new project from fsgg-sdd init through fsgg-sdd ship for one work item with no Governance gate runtime installed.
---

# Quickstart

This quickstart walks a new user from `fsgg-sdd init` through `fsgg-sdd ship` for
a single work item. It uses **only** the SDD command surface and produces the
SDD-owned readiness artifacts without any Governance gate runtime.

Each lifecycle stage names the **authored source** it writes under `work/<id>/`
and the **generated readiness view** it refreshes or reports under
`readiness/<id>/`. Generated views are outputs: their currency comes from
re-running the generators (`fsgg-sdd refresh` and `fsgg-sdd agents`), not from
file presence alone.

## Prerequisites

- The FS.GG.SDD CLI (`fsgg-sdd`).

That is all. You do **not** need:

- a Governance gate runtime (`.fsgg/policy.yml`, `.fsgg/capabilities.yml`,
  `.fsgg/tooling.yml`) — these are optional and never required by any SDD
  command;
- the FS.GG.Rendering package or any runtime product template;
- a monorepo checkout.

Throughout, `<id>` is your work item id (for example `001-my-first-feature`).

## `fsgg-sdd init`

```text
fsgg-sdd init --root .
```

`init` creates the lifecycle skeleton:

- `.fsgg/` — project configuration (`project.yml`, `sdd.yml`, `agents.yml`) and
  the optional Governance compatibility surface;
- `work/` — where authored lifecycle sources will live, one directory per work
  item;
- `readiness/` — where generated readiness views will be written;
- `CLAUDE.md` / `AGENTS.md` and the configured agent guidance targets
  (`claude`, `codex`).

**Next action:** `charter`.

## The lifecycle, in canonical order

Run the stages in this order. For each, the table lists the authored source
written, the generated readiness view refreshed or reported, and the next action
the command emits.

| Stage | Command | Authored source | Generated readiness view | Next action |
|---|---|---|---|---|
| charter | `fsgg-sdd charter --work <id> --title "<title>"` | `work/<id>/charter.md` | `readiness/<id>/work-model.json` | `specify` |
| specify | `fsgg-sdd specify --work <id> --input "<intent>"` | `work/<id>/spec.md` | `work-model.json` | `clarify` |
| clarify | `fsgg-sdd clarify --work <id>` | `work/<id>/clarifications.md` | `work-model.json` | `checklist` |
| checklist | `fsgg-sdd checklist --work <id>` | `work/<id>/checklist.md` | `work-model.json` | `plan` |
| plan | `fsgg-sdd plan --work <id>` | `work/<id>/plan.md` (and `work/<id>/contracts/` when contracts are authored) | `work-model.json` | `tasks` |
| tasks | `fsgg-sdd tasks --work <id>` | `work/<id>/tasks.yml` | `work-model.json` | `analyze` |
| analyze | `fsgg-sdd analyze --work <id>` | — | `readiness/<id>/analysis.json` | implement, then `evidence` |
| evidence | `fsgg-sdd evidence --work <id>` | `work/<id>/evidence.yml` | `work-model.json` | `verify` |
| verify | `fsgg-sdd verify --work <id>` | — | `readiness/<id>/verify.json` | `ship` |
| ship | `fsgg-sdd ship --work <id>` | — | `readiness/<id>/ship.json` | protected-boundary handoff (Governance-owned, optional) |

Notes:

- `analyze`, `verify`, and `ship` do not author a `work/<id>/` source; they
  aggregate the authored sources into a generated readiness view and refresh the
  work model.
- `analyze` reports `analysis.json` as implementation-ready and points you to
  record implementation evidence next; `evidence` then authors `evidence.yml`.
- `ship` aggregates SDD-owned merge-boundary readiness into `ship.json` and
  points ship-ready work to the **Governance-owned protected-boundary handoff**.
  That handoff is optional and lives outside SDD — SDD never evaluates or enforces
  it.

## Cross-cutting generators

Two commands are **not** lifecycle stages. They bring generated views back to
currency and emit no lifecycle successor (`nextLifecycleCommand = None`):

- `fsgg-sdd agents --work <id>` derives per-target Claude/Codex command and skill
  guidance from `readiness/<id>/work-model.json` into
  `readiness/<id>/agent-commands/<target>/` (a `guidance.json` manifest plus
  `commands.md` and `skills.md`), marked generated with source digests.
- `fsgg-sdd refresh --work <id>` regenerates the work model and agent guidance,
  renders the human-readable `readiness/<id>/summary.md` projection, and reports
  the currency of `analysis.json` / `verify.json` / `ship.json`.

Run `agents` and `refresh` whenever authored sources change, so the generated
views reflect the current lifecycle state. A generated view that exists on disk
but has not been refreshed against its current sources is stale — presence is not
currency.

## Result

After the run, the SDD-owned readiness artifacts under `readiness/<id>/` are:

- `work-model.json` — the normalized work model the generators consume;
- `analysis.json` — analysis readiness;
- `verify.json` — verification readiness;
- `ship.json` — merge-boundary readiness;
- `summary.md` — the generated human-readable readiness projection;
- `agent-commands/<target>/` — per-target agent command and skill guidance.

Each generated view records its sources, source digests, schema version, and
generator identity, and is marked generated.

As an **optional** next step, ship-ready work points to the Governance-owned
protected-boundary handoff. Governance owns routing, effective-evidence
freshness, profiles, gates, audit, and release enforcement. SDD reports
readiness; it does not evaluate or enforce any of those. See
[Adopting Governance](adopting-governance.md) for how that optional layer is
added after `init`.
