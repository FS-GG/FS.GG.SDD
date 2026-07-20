---
title: FS.GG.Contracts 5.0.0 — the break that ends the record-shape ABI tax
category: SDD
categoryindex: 6
index: 25
description: The declared binary break that reshapes Fsgg.Registry.ContractEntry from a positional F# record into a non-positional class, so future additive fields stop forcing a SemVer major.
---

# FS.GG.Contracts 5.0.0

`FS.GG.Contracts` moves `4.0.0` → **`5.0.0`**. This note is the **declaration** of
the break. It is declared, not suppressed: there is no
`CompatibilitySuppressions.xml`, and there will not be one — the same discipline the
[2.0.0 declaration](contracts-2.0.0.md) set.

The bump itself follows the
[FS.GG.Contracts version-bump checklist](contracts-version-bump-checklist.md) —
source, feed, and the `.github` registry advance as one coordinated change.

> **This is the last major that `Fsgg.Registry.ContractEntry` will force.** The
> versioning history reads `2.0.0`, `3.0.0`, `4.0.0` — three majors in a week
> ([ContractVersionTests](../../tests/FS.GG.Contracts.Tests/ContractVersionTests.fs)),
> every one of them the same public record either gaining a field or retyping one.
> `5.0.0` spends one more break to make the next N of them **minors**.

## The problem this ends

`FS.GG.SDD#610`. `Fsgg.Registry.ContractEntry` was a public F# **record**:

```fsharp
type ContractEntry =
    { Id: string
      Version: string
      Owner: string
      Surface: string
      Consumers: ConsumerDeclaration
      WireContract: WireContractDeclaration
      PackageVersion: string option
      Range: string option }
```

An F# record generates a **positional primary constructor**. Adding a field — at the
end, in the middle, anywhere — changes that constructor's arity and **deletes the old
one**, which ApiCompat reports as `CP0002`. There is **no** additive way to add a
field to an F# record, so *every* growth of this contract's typed surface was a forced
SemVer major:

| bump | change to `ContractEntry` | why major |
|---|---|---|
| `2.0.0` | (`ProviderDescriptor` gained `IdentifierParameter`) | new record field → ctor arity 11→12 |
| `3.0.0` | `Consumers` retyped `string list` → `ConsumerDeclaration` | ctor signature changed |
| `4.0.0` | gained `WireContract: WireContractDeclaration` | new record field |

Each was semantically **additive or a strict improvement** — 4.0.0's field is
optional and rejects nothing — yet each forced every consumer to file an adopt PR and
the hub to file a registry flip. That is the "ABI tax" #610 was opened to kill.

## The breaking change

`ContractEntry` is now a **non-positional class**: a single parameterless constructor
whose arity never moves, plus one settable typed property per field.

```fsharp
[<Sealed>]
type ContractEntry =
    new: unit -> ContractEntry
    member Id: string with get, set
    member Version: string with get, set
    member Owner: string with get, set
    member Surface: string with get, set
    member Consumers: ConsumerDeclaration with get, set
    member WireContract: WireContractDeclaration with get, set
    member PackageVersion: string option with get, set
    member Range: string option with get, set
```

The record's positional `.ctor(string, string, …)` and its get-only properties are
gone, so ApiCompat reports the reshape as a `CP0002` break against the `4.0.0`
baseline. **This is a binary break, and it is declared as one.** It buys a permanent
change of class for future growth:

> **From `5.0.0`, adding a field to `ContractEntry` is a new settable property — a
> binary-additive change, hence a MINOR.** No forced adopt round, no registry flip.
> The typed-union discipline the prior three majors bought is fully preserved: a new
> field is still its own typed union property (as `Consumers`/`WireContract` are).

### Why a class, and not `[<CLIMutable>]`

`[<CLIMutable>]` was the instinctive fix and it does **not** work: a `[<CLIMutable>]`
record still emits the full positional constructor, so adding a field still changes
its arity and still trips `CP0002`. Only a genuinely non-positional shape — a class
with a parameterless constructor and settable properties — has an arity that does not
move when the surface grows. (A stable-core-plus-extension-map was the other candidate
#610 named; it was declined because a stringly-typed map is the exact opposite of the
"make illegal states unrepresentable" typed-union design the three majors above spent
themselves establishing.)

### What the change costs

- **Construction** moves from the record literal `{ Id = …; Version = …; … }` to the
  object-initializer `ContractEntry(Id = …, Version = …, …)`.
- **Record copy-update** (`{ entry with Range = Some "1.x" }`) is gone; construct a
  fresh entry (the properties are settable) or clone-and-set.
- **Structural comparison** (`IComparable`) is gone. **Structural equality is kept** —
  `ContractEntry` overrides `Equals`/`GetHashCode` over its eight members, so value
  equality and any record that contains a `ContractEntry list` still compare by value.

## Who is actually affected

Measured against the declared `Fsgg.Registry` consumers (unchanged from 3.0.0/4.0.0):
**neither FS.GG.Governance nor FS.GG.Templates references `Fsgg.Registry`** — the only
in-tree consumer of `ContractEntry` is SDD's own YAML load edge
(`FS.GG.SDD.Artifacts/LifecycleArtifacts/RegistryDocument.fs`), migrated in this same
change set. So for a consumer already on `4.0.0` this is a version-number change and no
source edit. That does not make it a minor — the record surface was replaced — but it
makes `5.0.0` the cheapest possible time to spend the last forced major on this type.

`ProviderDescriptor` is **still a record** and still carries the tax; reshaping it the
same way is a separate follow-up, out of scope here so this change stays one story.

## Landing it — the coordinated release

Per the [version-bump checklist](contracts-version-bump-checklist.md), a
`FS.GG.Contracts` major is a single coordinated change across source, feed, and the
`.github` registry, and the required `api-compatibility-gate` compares the built
assembly against the **newest feed baseline**. So this PR's merge is gated on `5.0.0`
being published to the org feed (which makes `5.0.0` the new baseline the gate compares
against). The steps, in order:

1. **Source** — bumped here: `FS.GG.Contracts.fsproj` `<Version>` and
   `Fsgg.ContractVersion.value` to `5.0.0` (kept in lockstep, enforced by the
   fsproj-vs-constant test).
2. **Feed** — dispatch the publish workflow at `-f version=5.0.0` so the org feed
   serves it; the `api-compatibility-gate` then compares `5.0.0`-vs-`5.0.0` and passes.
3. **`.github` registry** — advance `fsgg-contracts.version`, then (only after the feed
   confirms `5.0.0`) `fsgg-contracts.package-version`, via the cross-repo protocol.

Consumers on a floating pin adopt `5.0.0` by moving construction/copy-update sites, if
any — but the measured blast radius above is that there are none outside this repo.
