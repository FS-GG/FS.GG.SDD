---
title: Installation
category: SDD
categoryindex: 6
index: 19
description: Install the fsgg-sdd CLI as a .NET global tool in a clean environment and run the lifecycle from init through ship with no Governance runtime.
---

# Installation

This guide installs the `fsgg-sdd` CLI in a **clean environment** with no prior
FS.GG repository knowledge and **no Governance runtime**. SDD installs, runs, and
ships work items entirely on its own. (FR-011 / SC-007)

## Prerequisites

- The .NET SDK (the CLI ships as a .NET global tool).

You do **not** need an FS.GG checkout, the FS.GG.Governance runtime, or any gate
configuration.

## Install the CLI

```bash
dotnet tool install --global FS.GG.SDD.Cli
```

The registry source the package is published to is per release-ops and **out of
scope** for this document. If your environment requires an explicit source, add
`--add-source <feed>` as directed by your release-ops instructions.

Confirm the install and the single reconciled version:

```bash
fsgg-sdd --version
```

This prints `0.2.0` — the one version shared by `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` (see the
[versioning policy](versioning-policy.md)).

## Run the lifecycle, init through ship

From an empty directory, take one work item from `init` through `ship`. `<id>` is
your work item id (for example `001-my-first-feature`).

```bash
fsgg-sdd init --root .

fsgg-sdd charter   --work <id> --title "<title>"
fsgg-sdd specify   --work <id> --input "<intent>"
fsgg-sdd clarify   --work <id>
fsgg-sdd checklist --work <id>
fsgg-sdd plan      --work <id>
fsgg-sdd tasks     --work <id>
fsgg-sdd analyze   --work <id>
# implement, then record evidence:
fsgg-sdd evidence  --work <id>
fsgg-sdd verify    --work <id>
fsgg-sdd ship      --work <id>
```

`ship` aggregates SDD-owned merge-boundary readiness into
`readiness/<id>/ship.json` and points ship-ready work to the **Governance-owned
protected-boundary handoff**. That handoff is optional and lives outside SDD —
SDD never evaluates or enforces it.

For the full stage-by-stage walkthrough (authored sources, generated views, and
next actions), see the [Quickstart](../quickstart.md).

## What is out of scope

This document covers obtaining and running the CLI. It does **not** cover, and
SDD does not own:

- the public registry, account setup, or feed configuration;
- package signing, provenance, or trusted publishing;
- any Governance gate runtime or enforcement.

These are Governance- or release-ops-owned concerns.
