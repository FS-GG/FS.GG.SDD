---
title: Adopting Governance
category: SDD
categoryindex: 6
index: 15
description: Add the optional Governance compatibility layer after fsgg-sdd init without changing SDD command usability.
---

# Adopting Governance

Governance is an **optional, additive** layer. FS.GG.SDD is fully usable without
it (see the [Quickstart](quickstart.md)). This note documents how Governance
owners add the Governance files after `fsgg-sdd init`, and the boundary that keeps
SDD command behavior unchanged whether those files are present, absent, or
incomplete.

The references to Governance here are advisory compatibility facts. SDD does not
evaluate or enforce any Governance behavior.

## After `fsgg-sdd init`

Once the SDD skeleton exists, Governance owners may add the Governance files
under `.fsgg/`:

- `.fsgg/policy.yml`
- `.fsgg/capabilities.yml`
- `.fsgg/tooling.yml`

Adding these files is additive. It does not change the SDD lifecycle, the
authored sources under `work/<id>/`, or the generated readiness views under
`readiness/<id>/`.

## Usability guarantee

Every SDD lifecycle command stays usable regardless of the Governance files'
state:

- **Absent** — the lifecycle runs end to end, exactly as in the Quickstart.
- **Present** — the commands behave identically; SDD does not read them to gate,
  route, or alter its output.
- **Incomplete or malformed** — the commands still succeed; SDD never parses the
  Governance files for enforcement, so partial content cannot block a command.

In all three cases the SDD commands report the Governance files as optional
compatibility facts (state `notEvaluated`) and emit no routing, profile,
freshness, gate, audit, or protected-branch fields.

## Boundary

- **SDD reports readiness.** It aggregates lifecycle, verification, evidence, and
  generated-view state into the readiness views and points ship-ready work to the
  Governance-owned protected-boundary handoff.
- **Governance owns enforcement.** Routing, effective-evidence freshness,
  profiles, gates, audit, and release decisions belong to FS.GG.Governance.

SDD never evaluates or enforces any of those concerns. The protected-boundary
handoff that ship-ready work points to is the seam between the two: SDD produces
the readiness; Governance, if adopted, decides what to do with it.
