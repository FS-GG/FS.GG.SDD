---
title: Adopting Governance
category: SDD
categoryindex: 6
index: 15
description: Adopt, author, or decline the optional Governance compatibility layer after fsgg-sdd init without changing SDD command usability.
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

There are two ways to get them, and neither is required.

### Adopt the org reference gate set (the seeded route)

`fsgg-sdd init` seeds one file, `.fsgg/governance-resolution.proj`, that pins the
published `FS.GG.Governance.ReferenceGateSet` package at an explicit version. It
does nothing on its own. Run the verb the package defines to resolve the org
profile into this repository's `.fsgg/`:

```bash
cd .fsgg
dotnet restore governance-resolution.proj
dotnet msbuild governance-resolution.proj -t:FsggResolveReferenceGateSet
```

That writes `governance.yml`, `capabilities.yml`, `policy.yml`, `tooling.yml` and
the controlled-import contract files. Add
`-p:FsggReferenceGateSetOverwrite=false` to refuse overwriting files you have
edited locally. To move to a different profile version, edit the `Version` in the
seeded project and re-run — the pin is explicit, so which profile a product
adopted is always answerable from its own tree.

`FsggResolveReferenceGateSet` is defined by FS.GG.Governance, not by FS.GG.SDD.
SDD seeds the pinned reference and points the destination at `.fsgg/`; it authors
none of the resolved content. See
[decision 0005](decisions/0005-generated-product-governance-resolution-route.md).

**Resolving is distribution, not enforcement.** The org's inherited gate floor is
embedded in the Governance runtime and is read from no file. Never resolving, or
editing or deleting what you resolved, changes what a product *declares*; it
cannot change what a product *inherits*.

### Author your own, or decline

Writing the three files by hand is equally supported — the seeded project is a
convenience, not a requirement.

Declining Governance entirely is also a supported end state, not an incomplete
setup. Delete `.fsgg/governance-resolution.proj` and it stays deleted:
`fsgg-sdd doctor` does not report it missing and `fsgg-sdd upgrade` does not
re-seed it. Every guarantee in the next section applies unchanged.

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
