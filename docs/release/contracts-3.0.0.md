---
title: FS.GG.Contracts 3.0.0 — break declaration for the three-state `consumers`
category: SDD
categoryindex: 6
index: 26
description: The declared binary break that forces FS.GG.Contracts to 3.0.0 — why `ContractEntry.Consumers` had to be retyped, why no additive spelling exists, and the consumer adaptation (there is none).
---

# FS.GG.Contracts 3.0.0

`FS.GG.Contracts` moves `2.1.0` → **`3.0.0`**. This note is the **declaration** of the
break. It is declared, not suppressed: there is no `CompatibilitySuppressions.xml`, and
there will not be one.

The bump follows the
[FS.GG.Contracts version-bump checklist](contracts-version-bump-checklist.md) — source,
feed, and the `.github` registry advance as one coordinated change.

> **The version line.** As with [2.0.0](contracts-2.0.0.md), `FS.GG.Contracts` is not on
> the `FS.GG.SDD.*` product line: it carries its own `<Version>` and its own SemVer, so
> the [versioning policy](versioning-policy.md)'s `docs/release/migrations/<version>.md`
> obligation does not reach this bump, and this note deliberately does not live there.

## The breaking change

`Fsgg.Registry.ContractEntry.Consumers` is **retyped**:

```fsharp
// 2.1.0
Consumers: string list

// 3.0.0
Consumers: ConsumerDeclaration

type ConsumerDeclaration =
    | ConsumersUnspecified
    | ConsumersDeclared of consumers: string list
    | ConsumersMalformed of raw: string
```

Per this repo's own change-class table
([checklist](contracts-version-bump-checklist.md)) — *"remove/rename/**retype** a public
member; change a signature → breaking → major, migration note required"* — this is a
major, and the note you are reading is that requirement discharged.

### There is no additive spelling of this change

Worth stating explicitly, because the obvious escape hatch does not work. Keeping
`Consumers: string list` and adding a parallel `ConsumersDeclaration: ConsumerDeclaration`
field is **also** a break: an F# record generates a positional primary constructor, so
*every* new field changes its arity and deletes the old constructor. That is the first row
of the same table, and it is the one that
[shipped 2.0.0](contracts-2.0.0.md). Both spellings are majors; one of them is also two
sources of truth for one fact. So the direct retype is the honest option, not the
expensive one.

## Why a major was spent on this

The two-state `string list` **could not distinguish an absent `consumers:` from an
explicitly empty one**. The YAML edge collapsed them —
`Internal.scalarList` ends in `Option.defaultValue []`, so a missing key and a present
`[]` both arrived as `[]` — and `validateDocument` therefore had to refuse the pair
(`"Contract 'x' is missing a non-empty 'consumers'."`).

That made a real, published package **unregisterable**. `FS.GG.NewSddWorkspace` is a
`dotnet new` template humans install: no repo restores it, and `.github`'s own fixture
builds it from source rather than from the feed. `consumers: []` is its only true value,
and the schema could not carry it — so the org's package inventory read "off by two"
while [ADR-0039 §5](https://github.com/FS-GG/.github/blob/main/docs/adr/0039-nuget-org-is-the-read-path.md)
required both `.github` packages to be registered. The only moves left without a schema
change were to **invent a consuming repo** (a lie the registry then *enforces* —
`fsgg-surface-impact` reads `consumers` to route consumer-impact issues, so a fictional
edge mails a real issue to a repo that does not consume the contract) or to **drop the
row** (which is how the inventory went off by two in the first place). See
[FS.GG.SDD#508](https://github.com/FS-GG/FS.GG.SDD/issues/508).

### Why the empty case is safe, and what makes it so

`consumers` is a **fail-open** field: `fsgg-surface-impact` files **zero** consumer-impact
issues for a breaking change and prints `(none declared)` without complaint. For a
genuinely consumerless package that is correct. For a row that merely *forgot* to declare
consumers it is silent misrouting — and under the old two-state model those two rows were
**identical by construction**.

So the loosening is deliberately partial: **`ConsumersDeclared []` validates;
`ConsumersUnspecified` is still refused.** The three-state read is what makes "nothing
consumes this" an assertion somebody made rather than a question nobody answered — it is
the reason the empty case can be allowed at all, not a decoration on top of allowing it.

`ConsumersMalformed` carries the third state for the same reason: a present-but-unparseable
declaration (`consumers: sdd`, `consumers:` with no value) must not collapse into either
neighbour. Reading it as *unspecified* tells the author they forgot a line that is right
there; reading it as *declared empty* would — now that empty is legal — pass a typo off as
a deliberate "nothing consumes this".

### The model is `MirrorDeclaration`'s; the change class is not

The three-state shape is the
[`MirrorDeclaration`](https://github.com/FS-GG/FS.GG.SDD/blob/main/src/FS.GG.Contracts/Registry.fsi)
precedent (feature 104, `.github#658`) applied to the same absent-vs-empty question, in the
same file, decided the same way. What it does **not** inherit is that feature's bump: #426
**added** types, which is additive → minor, where this one **mutates** a shipped record.

Same shape, different change class — worth being explicit about, because *"we did this
before as a minor"* is the exact reasoning that shipped
[2.0.1 understated](contracts-2.1.0.md).

## Who is actually affected

**Measured, not assumed** (both declared consumers of `fsgg-contracts`, 2026-07-17):

| repo | references `FS.GG.Contracts` | uses `Fsgg.Registry` | affected |
|---|---|---|---|
| **FS.GG.Governance** | yes (`Fsgg.Schemas`) | **no** | no |
| **FS.GG.Templates** | no | **no** | no |

Governance's own `ContractEntry` is an **unrelated domain type** in its
`specs/007-routing-severity-modes/contracts/Route.fsi`, not `Fsgg.Registry.ContractEntry`.
Neither consumer touches the retyped surface.

So — exactly as with [2.0.0](contracts-2.0.0.md) — **for a consumer already on 2.1.0 this
is a version-number change and no source edit.**

That does not make it a minor. The surface broke; the number says so. A version that
understates its API is the defect [2.1.0](contracts-2.1.0.md) exists to correct, and
"nobody happens to use it" is not a change class.

## Consumer adaptation

**For Governance and Templates: none.** Advance the pin; there is nothing to edit.

**For any future consumer constructing or reading a `ContractEntry`:**

```fsharp
// Constructing — before
{ Id = "alpha"; …; Consumers = [ "templates" ]; … }

// Constructing — after
{ Id = "alpha"; …; Consumers = ConsumersDeclared [ "templates" ]; … }

// Reading — before
if entry.Consumers.IsEmpty then …
for c in entry.Consumers do …

// Reading — after: the three states are the point; do not flatten them back
match entry.Consumers with
| ConsumersUnspecified -> // the question was never answered — NOT "no consumers"
| ConsumersMalformed raw -> // present but unparseable — report it, don't guess
| ConsumersDeclared consumers -> for c in consumers do …
```

> **Do not reintroduce the collapse in the consumer.** A
> `match … with ConsumersDeclared cs -> cs | _ -> []` shim compiles, restores the old
> ergonomics, and re-creates the exact bug this major was spent to remove — one level out.
> If you only need the list, say so about the state you are in; do not default the other
> two to empty.

## The gate that saw it, and the one that could not

This break is a clean demonstration of the two detectors and their blind spots
([checklist](contracts-version-bump-checklist.md), *"The two detectors, and the class each
one is blind to"*):

| detector | verdict on this change |
|---|---|
| **reflection `PublicSurface.baseline`** | **+10 lines, all additions.** It captures member *names*, not their *types* — `ContractEntry.Consumers` still reads as one member before and after — so it sees the new DU and is **blind to the retype**. It passes, and it is not wrong; it is not looking at this. |
| **`surface --check`** (`.fsi` text diff) | **`breaking (major)`** on `src/FS.GG.Contracts/Registry.fsi`, named on sight. |

Both are green here because both were run and the committed baselines re-pinned. The point
is that only one of them **could** have caught it — which is why
[FS.GG.SDD#475](https://github.com/FS-GG/FS.GG.SDD/issues/475) committed the `.fsi`
baselines to this repo in the first place.

> **One honest wrinkle.** With the source already bumped, `surface --check` reports
> `surfaceVersionSuggested: 4.0.0` — it computes "a major up from the version I can see",
> and the version it can see is already 3.0.0. It is advisory by design (ADR-0025 §2: SDD
> cannot see the previously *published* version, so it states an implication, not an
> accusation). The classification — `breaking` / `major` — is the load-bearing half and is
> correct. Read the suggestion as "this needs a major", not as a target.

## Sequencing

`Fsgg.Registry` lives here and the registry document lives in `.github`, so this lands as
**two ordered PRs across two repos** — no PR spans both
([`.github#689`](https://github.com/FS-GG/.github/issues/689) / ADR-0037):

1. **This repo (publish-before-flip, FR-007):** land the retype, publish `FS.GG.Contracts`
   3.0.0 and a CLI carrying it to the org feed.
2. **`.github`:** advance the `fsgg-contracts` row + the validator pin, bump
   `registry-schema.version` + `schemaVersion` (the schema comment marks `consumers`
   **(required)**; "required, may be empty" is schema growth under `.github#686`), and land
   the `new-sdd-workspace` row with `consumers: []`.

**The validator half rejects nothing the current document does.** Today absent-or-empty is
refused; after this, absent is refused and empty is accepted — strictly *more* accepting.
So ADR-0037's *"the new rule may reject nothing the current document does"* is satisfied
without a `schemaVersion` gate on the validator itself. Verified against `.github` HEAD:
every `consumers:` in `registry/dependencies.yml` is a non-empty inline list, so the
`Malformed` and blank arms fire on nothing that exists.
